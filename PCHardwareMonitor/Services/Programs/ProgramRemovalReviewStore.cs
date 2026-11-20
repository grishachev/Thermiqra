using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace PCHardwareMonitor;

internal static partial class ProgramRemovalReviewStore
{
    private static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Thermiqra", "RemovalReviews");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, IgnoreReadOnlyProperties = true };
    internal static long Revision
    {
        get
        {
            try { return Directory.Exists(Folder) ? Directory.GetLastWriteTimeUtc(Folder).Ticks : 0; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return 0; }
        }
    }

    internal static void SaveBaseline(ProgramRemovalBaseline baseline) =>
        Write(Path.Combine(Folder, baseline.Id + ".before.json"), baseline);

    internal static void SaveReport(ProgramLeftoverReport report)
    {
        Write(Path.Combine(Folder, report.Baseline.Id + ".after.json"), report);
        Write(Path.Combine(Folder, "latest.json"), report);
    }

    internal static ProgramLeftoverReport? ReadLatest() => ReadReport(Path.Combine(Folder, "latest.json"));

    internal static bool HasReport(string id) => Guid.TryParseExact(id, "N", out _) && File.Exists(Path.Combine(Folder, id + ".after.json"));

    internal static List<ProgramLeftoverReport> ReadHistory()
    {
        try
        {
            if (!Directory.Exists(Folder) || InstalledProgramLocationService.HasReparseAncestor(Folder)) return new();
            return new DirectoryInfo(Folder).EnumerateFiles("*.after.json")
                .OrderByDescending(f => f.LastWriteTimeUtc).Take(5000)
                .Select(f => ReadReport(f.FullName)).OfType<ProgramLeftoverReport>()
                .Concat(ReadPendingAttempts())
                .GroupBy(r => r.Baseline.Identity, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(r => r.Baseline.CapturedUtc).ThenBy(r => r.Baseline.ObservationOnly).First())
                .OrderByDescending(r => r.Baseline.CapturedUtc).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return new(); }
    }

    private static ProgramLeftoverReport? ReadReport(string path)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 4 * 1024 * 1024 ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return null;
            var report = JsonSerializer.Deserialize<ProgramLeftoverReport>(File.ReadAllText(path));
            return report?.Baseline != null && Guid.TryParseExact(report.Baseline.Id, "N", out _) &&
                report.Baseline.Identity != null && report.Baseline.Probes != null &&
                report.Baseline.Probes.Count <= 5000 && !report.Baseline.Probes.Any(p => p == null || p.Path == null ||
                    p.Kind == null || p.Hive == null || p.View == null || p.EvidenceRoot == null ||
                    p.EvidenceTarget == null || p.EvidenceFingerprint == null) &&
                report.Found != null && report.Limitations != null &&
                report.Found.Count <= 5000 && !report.Found.Any(p => p == null || p.Path == null ||
                    p.ReasonRu == null || p.ReasonEn == null || p.Kind == null || p.Hive == null || p.View == null || p.EvidenceRoot == null ||
                    p.EvidenceTarget == null || p.EvidenceFingerprint == null) &&
                !report.Limitations.Any(l => l == null || l.ReasonRu == null || l.ReasonEn == null || l.Detail == null)
                ? report : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or
            ArgumentException or System.Security.SecurityException) { return null; }
    }

    private static void Write<T>(string path, T value)
    {
        ProgramLeftoverCleanupPolicy.CheckAncestors(Folder);
        Directory.CreateDirectory(Folder);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, Options));
            File.Move(temporary, path, true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
