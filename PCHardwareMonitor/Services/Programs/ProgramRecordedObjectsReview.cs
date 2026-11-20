using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace PCHardwareMonitor;

// Verify remembered objects after removal. Existence is an audit result;
// it does not authorize deletion or attribute changes to an installer.
internal static class ProgramRecordedObjectsReview
{
    internal static ProgramRecordedObjectsResult? Inspect(ProgramRemovalBaseline baseline)
    {
        var footprint = ProgramRemovalReviewStore.ReadObjects(baseline.Id);
        if (footprint == null)
        {
            if (baseline.Memory != null) throw new IOException(SettingsService.L(
                "Снимок объектов перед удалением не найден.", "The pre-uninstall object inventory was not found."));
            return null;
        }
        int present = 0, absent = 0, keysPresent = 0, keysAbsent = 0;
        int totalFiles = footprint.Files.Count(f => !f.Directory);
        Stopwatch budget = Stopwatch.StartNew();
        foreach (var group in footprint.Files.Where(f => !f.Directory).GroupBy(f => f.Root, StringComparer.OrdinalIgnoreCase))
        {
            if (budget.Elapsed > TimeSpan.FromSeconds(1)) break;
            try
            {
                ProgramLeftoverCleanupPolicy.CheckAncestors(group.Key);
                try { File.GetAttributes(group.Key); }
                catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
                { absent += group.Count(); continue; }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
            { continue; }
            foreach (var file in group)
            {
                if (budget.Elapsed > TimeSpan.FromSeconds(1)) break;
                try
                {
                    string path = Path.GetFullPath(Path.Combine(file.Root, file.RelativePath));
                    if (!ProgramLeftoverPathPolicy.Inside(path, file.Root)) continue;
                    ProgramLeftoverCleanupPolicy.CheckAncestors(path);
                    File.GetAttributes(path); present++;
                }
                catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { absent++; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException) { }
            }
        }
        foreach (var key in footprint.Registry)
        {
            if (budget.Elapsed > TimeSpan.FromSeconds(1)) break;
            try
            {
                if (key.Hive is not ("HKCU" or "HKLM") || key.View is not ("32" or "64")) continue;
                using var root = RegistryKey.OpenBaseKey(key.Hive == "HKLM" ? RegistryHive.LocalMachine : RegistryHive.CurrentUser,
                    key.View == "64" ? RegistryView.Registry64 : RegistryView.Registry32);
                using var current = root.OpenSubKey(key.Path, writable: false);
                if (current == null) keysAbsent++; else keysPresent++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException) { }
        }
        return new(present, absent, totalFiles - present - absent, keysPresent, keysAbsent,
            footprint.Registry.Count - keysPresent - keysAbsent);
    }
}
