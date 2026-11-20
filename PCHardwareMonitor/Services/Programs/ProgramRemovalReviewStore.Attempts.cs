using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PCHardwareMonitor;

internal static partial class ProgramRemovalReviewStore
{
    // Include an attempted uninstall even when an earlier version stopped
    // after a vendor error and never wrote an .after report.
    private static IEnumerable<ProgramLeftoverReport> ReadPendingAttempts()
    {
        List<LegacyResult>? legacy = null;
        foreach (FileInfo file in new DirectoryInfo(Folder).EnumerateFiles("*.before.json")
            .OrderByDescending(f => f.LastWriteTimeUtc).Take(5000))
        {
            ProgramRemovalBaseline? baseline;
            try
            {
                if (file.Length > 4 * 1024 * 1024 || (file.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                baseline = JsonSerializer.Deserialize<ProgramRemovalBaseline>(File.ReadAllText(file.FullName));
                if (baseline == null || !Guid.TryParseExact(baseline.Id, "N", out _) || file.Name != baseline.Id + ".before.json" ||
                    HasReport(baseline.Id) || baseline.ObservationOnly || baseline.Identity == null || baseline.Name == null ||
                    baseline.Probes == null || baseline.Limitations == null || baseline.Probes.Count > 5000 ||
                    baseline.Probes.Any(p => p?.Path == null || p.Kind == null || p.Hive == null || p.View == null ||
                        p.EvidenceRoot == null || p.EvidenceTarget == null || p.EvidenceFingerprint == null)) continue;
                var result = baseline.NativeResult ?? (legacy ??= ReadLegacyResults()).Where(a => a.Identity.Equals(baseline.Identity, StringComparison.OrdinalIgnoreCase) &&
                    a.Finished >= baseline.CapturedUtc && a.Finished - baseline.CapturedUtc < TimeSpan.FromHours(6))
                    .OrderBy(a => a.Finished).Select(a => a.Result).FirstOrDefault();
                if (result == null) continue; // Confirmation alone is not an uninstall attempt.
                baseline = baseline with { NativeResult = result };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or System.Security.SecurityException)
            { continue; }
            yield return new(baseline, baseline.CapturedUtc, new(), new() {
                new("Результат удаления сохранён; поиск остатков ещё не выполнялся. Нажмите «Найти остатки».",
                    "Uninstall outcome was saved; leftovers have not been checked yet. Choose Find leftovers.") }, 0, SearchPending: true);
        }
    }

    private sealed record LegacyResult(string Identity, DateTime Finished, InstalledProgramRemovalResult Result);
    private static List<LegacyResult> ReadLegacyResults()
    {
        List<LegacyResult> results = new();
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Thermiqra", "UninstallLogs");
        if (!Directory.Exists(directory) || InstalledProgramLocationService.HasReparseAncestor(directory)) return results;
        foreach (FileInfo file in new DirectoryInfo(directory).EnumerateFiles("*.json").OrderByDescending(f => f.LastWriteTimeUtc).Take(3000))
            try
            {
                if (file.Length > 1024 * 1024 || (file.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                using JsonDocument json = JsonDocument.Parse(File.ReadAllText(file.FullName));
                var root = json.RootElement;
                if (!root.TryGetProperty("Completed", out var complete) || complete.ValueKind != JsonValueKind.True ||
                    !root.TryGetProperty("FinishedUtc", out var time) || !time.TryGetDateTime(out var finished) ||
                    !root.TryGetProperty("Plan", out var plan) || !plan.TryGetProperty("Identity", out var identity) ||
                    !root.TryGetProperty("Result", out var result) || result.ValueKind != JsonValueKind.Object) continue;
                var value = result.Deserialize<InstalledProgramRemovalResult>();
                string? id = identity.GetString();
                if (value != null && id != null && Enum.IsDefined(value.State)) results.Add(new(id, finished, value));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException or System.Security.SecurityException) { }
        return results;
    }
}
