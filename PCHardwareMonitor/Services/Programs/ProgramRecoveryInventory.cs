using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PCHardwareMonitor;

internal sealed record ProgramRecoveryContents(List<ProgramCleanupEntry> Available, List<string> Issues)
{
    internal bool Visible => Available.Count > 0 || Issues.Count > 0;
    internal bool CanRestore => Available.Count > 0;
    internal string Summary => SettingsService.L(
        $"Разделов реестра: {Available.Count(e => e.Registry != null)}; каталогов: {Available.Count(e => e.Probe.Kind == "Directory")}",
        $"Registry keys: {Available.Count(e => e.Registry != null)}; folders: {Available.Count(e => e.Probe.Kind == "Directory")}") +
        (Issues.Count > 0 ? SettingsService.L("; часть копий недоступна", "; some backups unavailable") : "");
    internal string Details => string.Join("\n", Available.Select(e => e.Probe.Kind == "Registry"
        ? e.Probe.Hive + "\\" + e.Probe.Path : e.Probe.Path).Concat(Issues));
}

internal static class ProgramRecoveryInventory
{
    internal static ProgramRecoveryContents Inspect(ProgramCleanupBackup backup)
    {
        List<ProgramCleanupEntry> available = new(); List<string> issues = new();
        foreach (var entry in backup.Entries)
        {
            if (entry.State is "Restored" or "Expired" or "Discarded" or "Recycled") continue;
            try
            {
                if (entry.Registry != null)
                {
                    byte[] security = Convert.FromBase64String(entry.Registry.Security);
                    if (entry.Probe.Kind != "Registry" || security.Length == 0 || security.Length > 65536 ||
                        (entry.Registry.Tree != null ? !ProgramSettingsRegistryCleanup.Valid(entry.Registry)
                            : entry.Registry.Values.Count is < 1 or > 2))
                        throw new IOException(SettingsService.L("Содержимое копии не прошло проверку.", "Backup contents did not pass validation."));
                    available.Add(entry);
                }
                else if (entry.BackupPath.Length > 0)
                {
                    string root = ProgramLeftoverCleanupPolicy.Root(entry.Probe);
                    if (entry.Probe.Kind != "Directory" || root.Length == 0 ||
                        entry.BackupPath != ProgramLeftoverCleanupPolicy.BackupPath(root, backup.Id))
                        ProgramLeftoverCleanupPolicy.Deny();
                    // GetAttributes preserves the distinction between missing
                    // content and access failure. Never scan through links.
                    FileAttributes attributes = File.GetAttributes(entry.BackupPath);
                    ProgramLeftoverCleanupPolicy.CheckAncestors(entry.BackupPath);
                    if ((attributes & FileAttributes.Directory) == 0) ProgramLeftoverCleanupPolicy.Deny();
                    available.Add(entry);
                }
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                System.Security.SecurityException or FormatException)
            { issues.Add(entry.Probe.Path + ": " + ex.Message); }
        }
        return new(available, issues);
    }

    internal static string Size(ProgramCleanupBackup backup, ProgramRecoveryContents contents)
    {
        if (!contents.CanRestore || contents.Issues.Count > 0) return SettingsService.L("Не подсчитан", "Not measured");
        try
        {
            long bytes = ProgramCleanupBackupStore.ManifestBytes(backup); int count = 0;
            foreach (var entry in contents.Available.Where(e => e.Probe.Kind == "Directory"))
            {
                Stack<string> pending = new(); pending.Push(entry.BackupPath);
                while (pending.Count > 0)
                {
                    string directory = pending.Pop(); ProgramLeftoverCleanupPolicy.CheckAncestors(directory);
                    foreach (string path in Directory.EnumerateFileSystemEntries(directory))
                    {
                        FileAttributes attributes = File.GetAttributes(path);
                        if (++count > 5000 || (attributes & FileAttributes.ReparsePoint) != 0)
                            return SettingsService.L("Не подсчитан", "Not measured");
                        if ((attributes & FileAttributes.Directory) != 0) pending.Push(path);
                        else bytes += new FileInfo(path).Length;
                    }
                }
            }
            string[] units = SettingsService.L("Б,КБ,МБ,ГБ,ТБ", "B,KB,MB,GB,TB").Split(',');
            int unit = 0; double value = bytes;
            while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
            return value.ToString(unit == 0 ? "N0" : "N1") + " " + units[unit];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { return SettingsService.L("Не подсчитан", "Not measured"); }
    }
}
