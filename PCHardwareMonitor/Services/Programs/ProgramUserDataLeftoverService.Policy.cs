using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PCHardwareMonitor;

internal static partial class ProgramUserDataLeftoverService
{
    private static bool Conflicts(string folder, string identity, IReadOnlyList<InstalledProgramInfo> inventory)
    {
        string leaf = Key(Path.GetFileName(folder));
        return inventory.Any(app => !app.Identity.Equals(identity, StringComparison.OrdinalIgnoreCase) &&
            (Names(app.Name, app.InstallLocation).Contains(leaf) ||
             ProgramDataFileIdentity.Locations().Any(root => ProgramPackageDataLayout.MatchesNamespace(folder, root, app.Identity, app.Name)) ||
             (!string.IsNullOrWhiteSpace(app.InstallLocation) && ProgramLeftoverPathPolicy.Inside(
                 ProgramLeftoverPathPolicy.Normalize(app.InstallLocation), folder))));
    }

    private static void ValidatePath(ProgramLeftoverProbe probe, ProgramRemovalBaseline baseline)
    {
        string path = ProgramLeftoverPathPolicy.Normalize(probe.Path);
        string root = ProgramLeftoverPathPolicy.Normalize(probe.EvidenceRoot);
        if (probe.Kind != "UserData" || path.Length == 0 || !Roots().Contains(root, StringComparer.OrdinalIgnoreCase) ||
            !ProgramLeftoverPathPolicy.Inside(path, root) || path.Equals(root, StringComparison.OrdinalIgnoreCase)) ProgramLeftoverCleanupPolicy.Deny();
        string[] parts = Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar);
        var names = Names(baseline.Name, baseline.InstallRoot);
        names.UnionWith(ProgramGameDataLocations.Aliases(baseline).Select(Key));
        if (probe.EvidenceTarget == ProgramDataFileIdentity.Evidence)
        {
            if (!ProgramDataFileIdentity.Scoped(path, root)) ProgramLeftoverCleanupPolicy.Deny();
        }
        else if (!RegisteredDataLayout(probe, baseline) && (parts.Length is < 1 or > 2 || !names.Contains(Key(parts[^1])) ||
            (parts.Length == 2 && Key(parts[0]) != Key(baseline.Publisher)))) ProgramLeftoverCleanupPolicy.Deny();
        ProgramLeftoverCleanupPolicy.CheckRoot(path);
        ProgramLeftoverCleanupPolicy.CheckAncestors(path);
        string ownData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Thermiqra");
        if (ProgramLeftoverPathPolicy.Inside(path, ownData) || ProgramLeftoverPathPolicy.Inside(ownData, path)) ProgramLeftoverCleanupPolicy.Deny();
    }

    private static void CheckTree(string root)
    {
        Stopwatch budget = Stopwatch.StartNew();
        Stack<string> pending = new(); pending.Push(root); int count = 0;
        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) ProgramLeftoverCleanupPolicy.Deny();
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                if (++count > 10000 || budget.Elapsed > TimeSpan.FromSeconds(5)) ProgramLeftoverCleanupPolicy.Deny();
                FileAttributes attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) ProgramLeftoverCleanupPolicy.Deny();
                if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
            }
        }
    }

    internal static void Validate(ProgramLeftoverProbe probe, ProgramRemovalBaseline baseline, IReadOnlyList<InstalledProgramInfo> inventory)
    {
        ValidatePath(probe, baseline);
        ProgramSharedComponentGuard.Check(probe.Path);
        if (baseline.ObservationOnly || probe.Group != ProgramLeftoverGroup.UserData ||
            Conflicts(probe.Path, baseline.Identity, inventory)) ProgramLeftoverCleanupPolicy.Deny();
        if (probe.EvidenceTarget == ProgramDataFileIdentity.Evidence || RegisteredDataLayout(probe, baseline))
        {
            ProgramLeftoverCleanupPolicy.CheckInventory(probe.Path, inventory);
            if (inventory.Any(app => Key(ProductName(app.Name)) == Key(ProductName(baseline.Name)))) ProgramLeftoverCleanupPolicy.Deny();
        }
        var marker = probe.EvidenceTarget switch
        {
            ChromiumProfileEvidence => FindProfile(probe, baseline),
            RecordedDataEvidence => FindRecordedData(probe, baseline),
            UnrealProfileEvidence => FindGameProfile(probe, baseline),
            ProgramDataFileIdentity.Evidence => (ProgramDataFileIdentity.Evidence, ProgramDataFileIdentity.Fingerprint(probe, baseline)),
            ProgramProductDataLayout.Evidence => ProgramProductDataLayout.Matches(probe, baseline)
                ? (ProgramProductDataLayout.Evidence, ProfileFingerprint(probe.Path)) : ((string Path, string Hash)?)null,
            ProgramPackageDataLayout.Evidence => ProgramPackageDataLayout.Matches(probe, baseline)
                ? (ProgramPackageDataLayout.Evidence, ProfileFingerprint(probe.Path)) : ((string Path, string Hash)?)null,
            _ => FindMarker(probe.Path, baseline)
        };
        if (marker == null || !marker.Value.Path.Equals(probe.EvidenceTarget, StringComparison.OrdinalIgnoreCase) ||
            marker.Value.Hash != probe.EvidenceFingerprint) ProgramLeftoverCleanupPolicy.Deny();
        CheckTree(probe.Path);
    }
}
