using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace PCHardwareMonitor;

internal static partial class ProgramObservedRemovalService
{
    private static readonly Dictionary<string, DateTime> CaptureAttempts = new(StringComparer.OrdinalIgnoreCase);

    private static void CaptureAssociations(ProgramObservationDocument snapshot, CancellationToken token)
    {
        var inventory = snapshot.Apps.Select(ToInfo).ToList();
        var changes = snapshot.Changes.Where(c => c.Type is "Added" or "Changed")
            .GroupBy(c => c.After.Identity, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Max(c => c.DetectedUtc), StringComparer.OrdinalIgnoreCase);
        var epochs = snapshot.Changes.Where(c => c.Type == "Added")
            .GroupBy(c => c.After.Identity, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Max(c => c.DetectedUtc), StringComparer.OrdinalIgnoreCase);
        var pending = snapshot.Apps.Select(app => (App: app, Saved: ProgramObservedAssociationStore.Read(app, exact: false)))
            .Where(item => item.Saved == null || item.Saved.Footprint == null || item.Saved.App.Version != item.App.Version ||
                item.Saved.App.Name != item.App.Name || item.Saved.App.Publisher != item.App.Publisher ||
                item.Saved.Baseline.CapturedUtc < changes.GetValueOrDefault(item.App.Identity) ||
                DateTime.UtcNow - item.Saved.Baseline.CapturedUtc >= TimeSpan.FromMinutes(10))
            .OrderByDescending(item => changes.GetValueOrDefault(item.App.Identity) > (item.Saved?.Baseline.CapturedUtc ?? DateTime.MinValue))
            .ThenBy(item => CaptureAttempts.GetValueOrDefault(item.App.Identity))
            .ThenBy(item => item.Saved?.Baseline.CapturedUtc ?? DateTime.MinValue).ToList();
        List<string>? shortcuts = null;
        Stopwatch budget = Stopwatch.StartNew(); int attempted = 0;
        foreach (var item in pending)
        {
            token.ThrowIfCancellationRequested();
            if (attempted++ >= 25 || budget.Elapsed > TimeSpan.FromSeconds(10)) break;
            var app = item.App; CaptureAttempts[app.Identity] = DateTime.UtcNow;
            if (ProgramNativeRemovalScope.Contains(app.Identity)) continue;
            try
            {
                var info = ToInfo(app);
                if (!app.Identity.StartsWith("msix:", StringComparison.Ordinal))
                {
                    using var key = InstalledProgramUninstallPlanReader.OpenRegistration(app.Identity);
                    if (key == null || Convert.ToString(key.GetValue("DisplayName")) != app.Name ||
                        (Convert.ToString(key.GetValue("DisplayVersion")) ?? "") != app.Version) continue;
                    string Value(string name) => Convert.ToString(key.GetValue(name)) ?? "";
                    bool msi = Value("WindowsInstaller") == "1";
                    string code = msi ? app.Identity.Split(':', 4)[3] : "";
                    shortcuts ??= InstalledProgramIconService.EnumerateStartMenuShortcuts();
                    string root = InstalledProgramLocationService.ResolveProgramLocation(app.InstallRoot, Value("DisplayIcon"),
                        app.Name, shortcuts, Value("UninstallString"),
                        msi ? InstalledProgramIconService.ReadMsiProductProperty(code, "InstallLocation") : "", code, token);
                    info = info with { InstallLocation = root, Kind = msi ? "MSI" : "Win32" };
                }
                var baseline = ProgramLeftoverDiscoveryService.Capture(info,
                    new(app.Identity, app.Name, app.Version, info.Kind, "", "", false), inventory);
                ProgramInstallationMemory.RememberObserved(app, baseline, inventory,
                    epochs.TryGetValue(app.Identity, out var epoch) ? epoch : null,
                    TimeSpan.FromMilliseconds(750), token);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                System.Security.SecurityException or System.ComponentModel.Win32Exception)
            { LastError = ex.Message; }
        }
        foreach (string identity in CaptureAttempts.Keys.Except(snapshot.Apps.Select(a => a.Identity), StringComparer.OrdinalIgnoreCase).ToList())
            CaptureAttempts.Remove(identity);
    }
}
