using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace PCHardwareMonitor;

internal static partial class ProgramInstallationMemory
{
    private static readonly object Gate = new();

    internal static void RememberObserved(ProgramObservedApp app, ProgramRemovalBaseline current,
        IReadOnlyList<InstalledProgramInfo> inventory, DateTime? epoch, TimeSpan budget, CancellationToken token)
    {
        lock (Gate)
        {
            if (ProgramNativeRemovalScope.Contains(app.Identity)) return;
            Remember(app, current, inventory, epoch, budget, token);
        }
    }

    internal static ProgramRemovalBaseline CaptureForRemoval(InstalledProgramInfo app,
        InstalledProgramUninstallPlan plan, IReadOnlyList<InstalledProgramInfo> inventory)
    {
        var baseline = ProgramDataFileIdentity.Extend(ProgramLeftoverDiscoveryService.Capture(app, plan, inventory));
        var observed = new ProgramObservedApp(app.Identity, app.Name, app.Version, app.Publisher, app.InstallLocation);
        var catalog = ProgramObservationService.Read();
        DateTime? epoch = catalog?.Changes.Where(c => c.Type == "Added" &&
            c.After.Identity.Equals(app.Identity, StringComparison.OrdinalIgnoreCase))
            .Select(c => (DateTime?)c.DetectedUtc).Max();
        var saved = Remember(observed, baseline, inventory, epoch, TimeSpan.FromSeconds(2), default);
        ProgramRemovalReviewStore.SaveObjects(saved.Baseline.Id, saved.Footprint!);
        return saved.Baseline;
    }

    internal static ProgramObservedAssociation Remember(ProgramObservedApp app, ProgramRemovalBaseline current,
        IReadOnlyList<InstalledProgramInfo> inventory, DateTime? installationEpoch,
        TimeSpan budget, CancellationToken token)
    {
        lock (Gate)
        {
            token.ThrowIfCancellationRequested();
            var saved = ProgramObservedAssociationStore.Read(app, exact: false);
            if (saved == null && ProgramObservedAssociationStore.Exists(app.Identity))
                throw new IOException(SettingsService.L(
                    "Сохранённые наблюдения этой программы недоступны. Они не перезаписаны.",
                    "Saved observations for this application are unavailable. They were not overwritten."));
            // Do not carry ownership hints from an earlier installation or a
            // different product that reuses the same uninstall registration.
            if (saved != null && (!SameProduct(saved.App, app) ||
                installationEpoch is DateTime epoch && epoch > saved.Baseline.CapturedUtc)) saved = null;
            int historical = 0;
            var probes = current.Probes.ToList();
            HashSet<string> known = probes.Select(ProbeKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (saved != null)
                foreach (var probe in saved.Baseline.Probes)
                {
                    if (!known.Add(ProbeKey(probe))) continue;
                    if (probes.Count >= 5000)
                    {
                        current.Limitations.Add(new("Часть сохранённых связей не включена: достигнут предел снимка",
                            "Some saved associations were omitted: snapshot limit reached"));
                        break;
                    }
                    probes.Add(RevalidateHistory(probe, app.Identity, inventory)); historical++;
                }
            var baseline = current with { Probes = probes };
            var footprint = ProgramFootprintCapture.Capture(baseline, budget, token);
            baseline = baseline with { Memory = new(footprint.CapturedUtc,
                footprint.Files.Count(f => !f.Directory), footprint.Registry.Count, historical, footprint.Complete,
                saved?.Footprint?.CapturedUtc ?? saved?.Baseline.CapturedUtc) };
            var changes = Compare(saved?.Footprint, footprint);
            var association = new ProgramObservedAssociation(app, baseline, ProgramObservationService.SessionId,
                footprint, (saved?.ObjectChanges ?? new()).Concat(changes).TakeLast(1000).ToList(),
                saved?.FirstObservedUtc ?? saved?.Baseline.CapturedUtc ?? current.CapturedUtc,
                installationEpoch ?? saved?.InstallationEpochUtc);
            token.ThrowIfCancellationRequested();
            ProgramObservedAssociationStore.Save(association);
            return association;
        }
    }

    private static string ProbeKey(ProgramLeftoverProbe probe) =>
        probe.Kind + "|" + probe.Hive + "|" + probe.View + "|" + probe.Path;

    internal static bool SameProduct(ProgramObservedApp old, ProgramObservedApp fresh) =>
        old.Identity.Equals(fresh.Identity, StringComparison.OrdinalIgnoreCase) &&
        ProgramUserDataLeftoverService.ProductName(old.Name).Equals(
            ProgramUserDataLeftoverService.ProductName(fresh.Name), StringComparison.OrdinalIgnoreCase) &&
        (old.Publisher.Length == 0 || fresh.Publisher.Length == 0 ||
         old.Publisher.Equals(fresh.Publisher, StringComparison.OrdinalIgnoreCase));

    private static ProgramLeftoverProbe RevalidateHistory(ProgramLeftoverProbe probe, string identity,
        IReadOnlyList<InstalledProgramInfo> inventory)
    {
        if (probe.Group == ProgramLeftoverGroup.Protected) return probe;
        string root = ProgramLeftoverCleanupPolicy.Root(probe);
        if (root.Length == 0) return probe with { Group = ProgramLeftoverGroup.NeedsReview };
        try
        {
            ProgramLeftoverCleanupPolicy.CheckInventory(root,
                inventory.Where(app => !app.Identity.Equals(identity, StringComparison.OrdinalIgnoreCase)).ToList());
            return probe;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or ArgumentException)
        {
            return probe with { Group = ProgramLeftoverGroup.Protected,
                ReasonRu = "Сохранённое расположение теперь общее, защищено или связано с другой программой; очистка исключена.",
                ReasonEn = "The saved location is now shared, protected or associated with another application; cleanup excluded." };
        }
    }
}
