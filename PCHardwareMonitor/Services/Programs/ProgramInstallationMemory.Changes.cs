using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PCHardwareMonitor;

internal static partial class ProgramInstallationMemory
{
    private static List<ProgramFootprintChange> Compare(ProgramFootprint? before, ProgramFootprint after)
    {
        List<ProgramFootprintChange> result = new();
        if (before == null) return result; // Initial inventory is not an observed installation.
        string FileKey(ProgramFootprintFile file) => Path.Combine(file.Root, file.RelativePath);
        string RegistryKey(ProgramFootprintRegistry key) => key.Hive + "|" + key.View + "|" + key.Path;
        var oldFiles = before.Files.GroupBy(FileKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var newFiles = after.Files.GroupBy(FileKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var oldKeys = before.Registry.GroupBy(RegistryKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var newKeys = after.Registry.GroupBy(RegistryKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        void Add(string kind, string path)
        { if (result.Count < 1000) result.Add(new(after.CapturedUtc, kind, path)); }
        foreach (var pair in newFiles)
            if (!oldFiles.TryGetValue(pair.Key, out var old)) Add("FileFirstObserved", pair.Key);
            else if (old.Directory != pair.Value.Directory || old.Length != pair.Value.Length ||
                old.LastWriteUtcTicks != pair.Value.LastWriteUtcTicks) Add("FileMetadataChanged", pair.Key);
        foreach (var pair in newKeys)
            if (!oldKeys.TryGetValue(pair.Key, out var old)) Add("RegistryFirstObserved", pair.Key);
            else if (old.Complete && pair.Value.Complete && old.Fingerprint != pair.Value.Fingerprint)
                Add("RegistryValuesChanged", pair.Key);
        // Incomplete reads cannot prove that a previously seen object went
        // away. Even a complete diff records absence, not who removed it.
        if (before.Complete && after.Complete)
        {
            foreach (string path in oldFiles.Keys.Except(newFiles.Keys, StringComparer.OrdinalIgnoreCase))
                if (after.Roots?.Contains(oldFiles[path].Root, StringComparer.OrdinalIgnoreCase) == true)
                    Add("FileNotObserved", path);
            foreach (string path in oldKeys.Keys.Except(newKeys.Keys, StringComparer.OrdinalIgnoreCase))
                if (after.RegistryRoots?.Any(root => path.Equals(root, StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase)) == true)
                    Add("RegistryNotObserved", path);
        }
        return result;
    }
}
