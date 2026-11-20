using System;
using System.Linq;

namespace PCHardwareMonitor;

internal static class ProgramRegistryRestorePolicy
{
    internal static bool IsUnchangedSubset(ProgramRegistrySnapshot saved, ProgramRegistrySnapshot remaining)
    {
        if (!ProgramSettingsRegistryCleanup.Valid(saved) || !ProgramSettingsRegistryCleanup.Valid(remaining)) return false;
        foreach (var node in remaining.Tree!)
        {
            var original = saved.Tree!.FirstOrDefault(n => n.Path.Equals(node.Path, StringComparison.OrdinalIgnoreCase));
            if (original == null || original.Security != node.Security || !original.Values.SequenceEqual(node.Values)) return false;
        }
        return true;
    }
}
