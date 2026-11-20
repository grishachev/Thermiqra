using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PCHardwareMonitor;

internal static class ProgramCleanupRecoveryService
{
    internal static int RetentionDays => SettingsService.Current.ProgramRegistryBackupDays switch
    { 0 => 0, 7 => 7, 90 => 90, _ => 30 };

    internal static Task<List<ProgramCleanupBackup>> LoadAsync(bool expire = true) => ProgramLeftoverCleanupService.ExclusiveAsync(() =>
    {
        var backups = ProgramCleanupBackupStore.ReadAll();
        int days = RetentionDays;
        if (expire && days > 0)
            foreach (ProgramCleanupBackup backup in backups)
            {
                if (backup.CreatedUtc > DateTime.UtcNow.AddDays(-days)) continue;
                bool changed = false;
                for (int i = 0; i < backup.Entries.Count; i++)
                {
                    var entry = backup.Entries[i];
                    if (entry.State == "Restored" && (entry.BackupPath.Length == 0 || IsAbsent(entry.BackupPath)))
                    {
                        backup.Entries[i] = entry with { Registry = null, BackupPath = "", State = "Expired" };
                        changed = true; continue;
                    }
                    // Unconfirmed operations and old file backups are never
                    // silently discarded by the registry retention policy.
                    if (entry.Registry != null && entry.State is "Removed" or "Restored")
                    { backup.Entries[i] = entry with { Registry = null, State = "Expired" }; changed = true; }
                }
                if (changed) ProgramCleanupBackupStore.Save(backup);
                if (backup.Entries.All(e => e.Registry == null && e.BackupPath.Length == 0))
                    ProgramCleanupBackupStore.DeleteManifest(backup);
            }
        return ProgramCleanupBackupStore.ReadAll();
    });

    internal static Task<ProgramCleanupBackup> DiscardAsync(ProgramCleanupBackup backup) => ProgramLeftoverCleanupService.ExclusiveAsync(() =>
    {
        // Reread the journal rather than trusting a stale view.
        backup = ProgramCleanupBackupStore.ReadAll().FirstOrDefault(b => b.Id == backup.Id)
            ?? throw new IOException(SettingsService.L("Копия уже отсутствует.", "Backup is already absent."));
        for (int i = 0; i < backup.Entries.Count; i++)
        {
            var entry = backup.Entries[i];
            try
            {
                if (entry.BackupPath.Length > 0 && !IsAbsent(entry.BackupPath))
                {
                    string root = ProgramLeftoverCleanupPolicy.Root(entry.Probe);
                    if (entry.BackupPath != ProgramLeftoverCleanupPolicy.BackupPath(root, backup.Id))
                        ProgramLeftoverCleanupPolicy.Deny();
                    ProgramLeftoverCleanupPolicy.CheckInventory(entry.BackupPath, ProgramLeftoverCleanupService.LiveInventory(null));
                    ProgramLeftoverCleanupPolicy.CheckDirectory(entry.BackupPath);
                    ProgramRecycleBin.SendAsync(entry.BackupPath).GetAwaiter().GetResult();
                }
                backup.Entries[i] = entry with { Registry = null, BackupPath = "", State = "Discarded", Detail = "" };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                System.Security.SecurityException or ArgumentException or InvalidOperationException or
                System.Runtime.InteropServices.COMException)
            { backup.Entries[i] = entry with { Detail = ex.Message }; }
            ProgramCleanupBackupStore.Save(backup);
        }
        if (backup.Entries.All(e => e.State == "Discarded")) ProgramCleanupBackupStore.DeleteManifest(backup);
        return backup;
    });

    private static bool IsAbsent(string path)
    {
        try { File.GetAttributes(path); return false; }
        catch (FileNotFoundException) { return true; }
        catch (DirectoryNotFoundException) { return true; }
    }

}
