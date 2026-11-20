using System;
using System.IO;
using Microsoft.Win32;

namespace PCHardwareMonitor;

internal static partial class ProgramSettingsRegistryCleanup
{
    // Machine-wide cleanup is narrower than per-user settings: only a leaf
    // containing paths into the removed, isolated application directory.
    // Arbitrary machine configuration and vendor trees are excluded.
    private static void CheckMachineReferences(ProgramLeftoverProbe p, ProgramRegistrySnapshot saved, bool requireMissing)
    {
        if (p.EvidenceTarget == ProgramOrphanRegistration.Evidence)
        {
            if (saved.Tree?.Count != 1) ProgramLeftoverCleanupPolicy.Deny();
            return;
        }
        if (p.Hive != "HKLM") return;
        ProgramLeftoverCleanupPolicy.CheckRoot(p.EvidenceRoot);
        if (saved.Tree?.Count != 1 || saved.Tree[0].Values.Count is < 1 or > 4) ProgramLeftoverCleanupPolicy.Deny();
        foreach (var value in saved.Tree![0].Values)
        {
            if (value.Kind is not ((int)RegistryValueKind.String or (int)RegistryValueKind.ExpandString)) ProgramLeftoverCleanupPolicy.Deny();
            string path = ProgramLeftoverPathPolicy.Normalize(value.Text);
            if (path.Length == 0 || !ProgramLeftoverPathPolicy.Inside(path, p.EvidenceRoot)) ProgramLeftoverCleanupPolicy.Deny();
            ProgramLeftoverCleanupPolicy.CheckAncestors(path);
            if (!requireMissing) continue;
            try { File.GetAttributes(path); ProgramLeftoverCleanupPolicy.Deny(); }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
}
