using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PCHardwareMonitor;

// Captured associations remain hints, not proof of file/registry ownership.
internal static partial class ProgramObservedRemovalService
{
    internal static string LastError { get; private set; } = "";

    internal static async Task UpdateAsync(ProgramObservationDocument snapshot, CancellationToken token)
    {
        LastError = "";
        CaptureAssociations(snapshot, token);
        foreach (var removed in snapshot.Changes.Where(c => c.Type == "Removed")
            .GroupBy(c => c.Before.Identity, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(c => c.DetectedUtc).First()))
        {
            token.ThrowIfCancellationRequested();
            // Require a second successful absence snapshot. A registry gap
            // during an installer handoff must not instantly create a cleanup.
            if (snapshot.ScannedUtc - removed.DetectedUtc < TimeSpan.FromSeconds(20) ||
                ProgramNativeRemovalScope.Contains(removed.Before.Identity) ||
                snapshot.Apps.Any(a => a.Identity.Equals(removed.Before.Identity, StringComparison.OrdinalIgnoreCase))) continue;
            string id = ProgramObservedAssociationStore.EventId(removed);
            if (ProgramRemovalReviewStore.HasReport(id)) continue;
            try
            {
                var association = ProgramObservedAssociationStore.Read(removed.Before, exact: false);
                if (association != null && ProgramRemovalReviewStore.HasReport(association.Baseline.Id))
                    continue; // The native window already saved this removal.
                if (association != null && (association.Baseline.CapturedUtc > removed.DetectedUtc ||
                    association.App.Version != removed.Before.Version ||
                    !ProgramInstallationMemory.SameProduct(association.App, removed.Before)))
                    association = null;
                ProgramRemovalBaseline baseline;
                if (association != null)
                    baseline = association.Baseline with { Id = id, ObservationOnly = true };
                else
                {
                    var before = removed.Before;
                    var root = ProgramLeftoverPathPolicy.Normalize(before.InstallRoot);
                    List<ProgramLeftoverProbe> probes = new();
                    if (root.Length > 0) probes.Add(new("Directory", root, ProgramLeftoverGroup.NeedsReview,
                        "Расположение сохранено в списке программ; связи объектов до удаления не снимались. Автоматическая очистка исключена.",
                        "Location was saved in inventory; pre-removal object associations were not captured. Automatic cleanup excluded."));
                    baseline = new(id, removed.FromUtc, before.Identity, before.Name, before.Version, probes,
                        new() { new("Снимок связей до удаления отсутствует; возможен только ограниченный повторный поиск",
                            "No pre-removal association snapshot is available; only bounded rediscovery is possible") },
                        before.Publisher, root, true);
                }
                ProgramRemovalReviewStore.SaveBaseline(baseline);
                if (association?.Footprint is { } footprint) ProgramRemovalReviewStore.SaveObjects(baseline.Id, footprint);
                await ProgramLeftoverRecheckService.RecheckAsync(baseline);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                System.Security.SecurityException or InvalidOperationException or System.ComponentModel.Win32Exception)
            { LastError = removed.Before.Name + ": " + ex.Message; }
        }
    }

    private static InstalledProgramInfo ToInfo(ProgramObservedApp app) => new(app.Name, app.Publisher, app.Version,
        app.Identity.StartsWith("msix:", StringComparison.Ordinal) ? "MSIX" : "Win32", "—", app.InstallRoot, "", Identity: app.Identity);
}
