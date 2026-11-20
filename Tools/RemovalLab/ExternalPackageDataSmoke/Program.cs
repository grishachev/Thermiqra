using System;
using System.IO;
using System.Linq;
using PCHardwareMonitor;

// Explicit opt-in smoke test. Only a unique synthetic AppData profile and its
// own journal are changed. No software is installed/uninstalled. Windows moves
// the test product profile to Recycle Bin, just as the real cleanup engine does.
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 1 || args[0] != "--run")
        {
            Console.WriteLine("Synthetic MSIX external-data cleanup smoke test. Pass --run to create and recycle a test-owned profile.");
            return 2;
        }
        string data = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string vendor = "ThermiqraCleanupProbe" + Guid.NewGuid().ToString("N");
        string owned = Path.Combine(data, vendor), product = Path.Combine(owned, "Removal Probe"), sibling = Path.Combine(owned, "Other Product");
        if (Directory.Exists(owned)) throw new IOException("Test area already exists");
        ProgramCleanupBackup? journal = null;
        try
        {
            Directory.CreateDirectory(Path.Combine(product, "cache"));
            File.WriteAllText(Path.Combine(product, "cache", "probe.dat"), "synthetic test payload");
            Directory.CreateDirectory(sibling); File.WriteAllText(Path.Combine(sibling, "keep.dat"), "sibling product");
            var baseline = new ProgramRemovalBaseline(Guid.NewGuid().ToString("N"), DateTime.UtcNow,
                "msix:" + vendor + ".RemovalProbe_labpublisher", "Removal Probe 1.0.0", "1.0.0", new(), new(),
                NativeResult: new(InstalledProgramRemovalState.Removed, 0, "", true));
            var report = ProgramLeftoverDiscoveryService.Inspect(baseline);
            var selected = report.Found.Single(p => p.Path == product);
            journal = ProgramLeftoverCleanupService.CleanAsync(report, new[] { selected }).GetAwaiter().GetResult();
            foreach (var entry in journal.Entries) Console.WriteLine($"ENGINE: {entry.State} | {entry.Detail}");
            bool removed = journal.Entries.Count == 1 && journal.Entries[0].State == "Recycled" && !Directory.Exists(product);
            var verified = ProgramLeftoverDiscoveryService.Inspect(report.Baseline);
            bool preserved = File.ReadAllText(Path.Combine(sibling, "keep.dat")) == "sibling product";
            bool auditPresent = ProgramCleanupBackupStore.ReadAll().Any(j => j.Id == journal.Id);
            Console.WriteLine($"RECHECK_FOUND={verified.Found.Count}; LIMITATIONS={verified.Limitations.Count}; SIBLING_PRESERVED={preserved}; JOURNAL_READABLE={auditPresent}");
            return removed && verified.Found.Count == 0 && verified.Limitations.Count == 0 && preserved && auditPresent ? 0 : 1;
        }
        finally
        {
            if (journal != null) ProgramCleanupBackupStore.DeleteManifest(journal);
            if (Path.GetDirectoryName(Path.GetFullPath(owned)) != Path.GetFullPath(data) || Path.GetFileName(owned) != vendor)
                throw new IOException("Unexpected test teardown path");
            if (Directory.Exists(owned)) Directory.Delete(owned, true);
        }
    }
}
