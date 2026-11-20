using System;
using System.IO;
using System.Linq;

namespace PCHardwareMonitor;

internal static partial class ProgramUserDataLeftoverService
{
    private const string RecordedDataEvidence = "observed-application-data";

    private static (string Path, string Hash)? FindRecordedData(ProgramLeftoverProbe probe, ProgramRemovalBaseline baseline)
    {
        // The configured product namespace AND a real pre-uninstall observation
        // are required. Merely listing an expected (possibly absent) root is not
        // an observation, and arbitrary files outside this folder never qualify.
        string product = Key(ProductName(baseline.Name));
        string leaf = Key(Path.GetFileName(probe.Path));
        if (!Specific(product) || product.Length < 4 || !Names(baseline.Name, baseline.InstallRoot).Contains(leaf)) return null;
        var snapshot = ProgramRemovalReviewStore.ReadObjects(baseline.Id);
        if (snapshot == null || !snapshot.Files.Any(f => f.Root.Equals(probe.Path, StringComparison.OrdinalIgnoreCase))) return null;
        return (RecordedDataEvidence, ProfileFingerprint(probe.Path));
    }
}
