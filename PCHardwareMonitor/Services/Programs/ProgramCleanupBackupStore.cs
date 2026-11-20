using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PCHardwareMonitor;

internal sealed record ProgramRegistryText(string Name, string Text, int Kind);
internal sealed record ProgramRegistryNode(string Path, List<ProgramRegistryText> Values, string Security);
internal sealed record ProgramRegistrySnapshot(List<ProgramRegistryText> Values, string Security,
    List<ProgramRegistryNode>? Tree = null);
internal sealed record ProgramCleanupEntry(ProgramLeftoverProbe Probe, string BackupPath,
    ProgramRegistrySnapshot? Registry, string State = "Prepared", string Detail = "");
internal sealed record ProgramCleanupBackup(string Id, string ReviewId, string Name, DateTime CreatedUtc,
    List<ProgramCleanupEntry> Entries, string Identity = "");

internal static class ProgramCleanupBackupStore
{
    private static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Thermiqra", "CleanupBackups");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    internal static void Save(ProgramCleanupBackup backup)
    {
        if (!Guid.TryParseExact(backup.Id, "N", out _)) throw new IOException(SettingsService.L("Некорректный идентификатор резервной копии.", "Invalid backup identity."));
        ProgramLeftoverCleanupPolicy.CheckAncestors(Folder);
        Directory.CreateDirectory(Folder);
        if (InstalledProgramLocationService.HasReparseAncestor(Folder)) throw new IOException(SettingsService.L("Каталог резервных копий является ссылкой.", "Backup directory is a link."));
        string path = Path.Combine(Folder, backup.Id + ".json");
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, backup, Options);
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    internal static List<ProgramCleanupBackup> ReadAll()
    {
        List<ProgramCleanupBackup> result = new();
        try
        {
            if (!Directory.Exists(Folder) || InstalledProgramLocationService.HasReparseAncestor(Folder)) return result;
            foreach (FileInfo file in new DirectoryInfo(Folder).EnumerateFiles("*.json")
                .OrderByDescending(f => f.LastWriteTimeUtc).Take(5000))
            {
                if (file.Length > 2 * 1024 * 1024 || (file.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                ProgramCleanupBackup? backup;
                try { backup = JsonSerializer.Deserialize<ProgramCleanupBackup>(File.ReadAllText(file.FullName)); }
                catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException) { DiagnosticLog.Failure("Recovery.Read", ex); continue; }
                if (backup == null || !Guid.TryParseExact(backup.Id, "N", out _) ||
                    backup.CreatedUtc.Year < 2000 || backup.CreatedUtc > DateTime.UtcNow.AddDays(1) ||
                    !Guid.TryParseExact(backup.ReviewId, "N", out _) || backup.Name == null || backup.Identity == null ||
                    !(backup.Identity.StartsWith("registry:", StringComparison.Ordinal) || backup.Identity.StartsWith("msix:", StringComparison.Ordinal)) ||
                    backup.Id + ".json" != file.Name || backup.Entries == null ||
                    backup.Entries.Count > 200 || (backup.Identity.StartsWith("msix:", StringComparison.Ordinal) && backup.Entries.Any(e => e?.Probe?.Kind != "UserData")) ||
                    backup.Entries.Any(e => e?.Probe?.Path == null || e.BackupPath == null ||
                        e.State == null || e.Detail == null || e.Probe.EvidenceRoot == null ||
                        e.Probe.EvidenceTarget == null || e.Probe.EvidenceFingerprint == null ||
                        (e.Registry != null && (e.Registry.Security == null || e.Registry.Values == null ||
                            e.Registry.Values.Any(v => v?.Name == null || v.Text == null) ||
                            (e.Registry.Tree != null && !ProgramSettingsRegistryCleanup.Valid(e.Registry)))))) continue;
                result.Add(backup);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { DiagnosticLog.Failure("Recovery.Inventory", ex); }
        return result.OrderByDescending(b => b.CreatedUtc).ToList();
    }

    internal static bool HasRecoverable(ProgramCleanupBackup backup) => backup.Entries.Any(e =>
        e.State is not ("Restored" or "Expired" or "Discarded" or "Recycled") && (e.BackupPath.Length > 0 || e.Registry != null));

    internal static long ManifestBytes(ProgramCleanupBackup backup) => new FileInfo(Path.Combine(Folder, backup.Id + ".json")).Length;

    internal static void DeleteManifest(ProgramCleanupBackup backup)
    {
        if (!Guid.TryParseExact(backup.Id, "N", out _)) throw new IOException(SettingsService.L("Некорректный идентификатор резервной копии.", "Invalid backup identity."));
        ProgramLeftoverCleanupPolicy.CheckAncestors(Folder);
        File.Delete(Path.Combine(Folder, backup.Id + ".json"));
    }
}
