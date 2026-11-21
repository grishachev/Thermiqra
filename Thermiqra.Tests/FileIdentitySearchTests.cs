using System.Diagnostics;
using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

public class FileIdentitySearchTests
{
    private static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static (string Product, string Publisher) Identity
    {
        get
        {
            var v = FileVersionInfo.GetVersionInfo(typeof(SettingsService).Assembly.Location);
            return (v.ProductName!, v.CompanyName!);
        }
    }
    private static string NewRoot()
    {
        string root = Path.Combine(Local, "ThermiqraAssociationTest" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
    private static void RemoveOwnRoot(string root)
    {
        Assert.Equal(Local, Path.GetDirectoryName(root));
        Assert.StartsWith("ThermiqraAssociationTest", Path.GetFileName(root));
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
    private static void Seed(string folder) => File.Copy(typeof(SettingsService).Assembly.Location, Path.Combine(folder, "application.exe"));

    [Fact]
    public void SlowEnumerationStillFindsAssociationInsteadOfDeclaringTimeout()
    {
        string root = NewRoot();
        try
        {
            Seed(root);
            IEnumerable<string> Enumerate(string path)
            {
                if (path.Equals(Local, StringComparison.OrdinalIgnoreCase))
                {
                    Thread.Sleep(2100); // A real delay beyond the published 1.1.0 deadline.
                    return new[] { root };
                }
                return Array.Empty<string>();
            }
            var probes = new List<ProgramLeftoverProbe>(); var limits = new List<ProgramReviewLimitation>();
            var identity = Identity;
            ProgramDataFileIdentity.Discover(identity.Product, identity.Publisher, probes, limits, Enumerate);
            Assert.Contains(probes, p => p.Path == root && p.Group == ProgramLeftoverGroup.NeedsReview);
            Assert.Empty(limits);
        }
        finally { RemoveOwnRoot(root); }
    }

    [Fact]
    public void AssociationBeyondOldFolderAndExecutableCutoffsIsFound()
    {
        string root = NewRoot();
        try
        {
            for (int i = 0; i < 1502; i++) Directory.CreateDirectory(Path.Combine(root, i.ToString("D4")));
            string last = Path.Combine(root, "1501");
            for (int i = 0; i < 8; i++) File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), Path.Combine(last, i + ".exe"));
            Seed(last);
            Assert.Equal(9, Directory.EnumerateFiles(last, "*.exe").Count());
            Assert.EndsWith("application.exe", Directory.EnumerateFiles(last, "*.exe").Last());
            IEnumerable<string> Enumerate(string path) => path.Equals(Local, StringComparison.OrdinalIgnoreCase)
                ? new[] { root } : path == root ? Directory.EnumerateDirectories(root) : Array.Empty<string>();
            var probes = new List<ProgramLeftoverProbe>(); var limits = new List<ProgramReviewLimitation>();
            var identity = Identity;
            ProgramDataFileIdentity.Discover(identity.Product, identity.Publisher, probes, limits, Enumerate);
            Assert.Contains(probes, p => p.Path == last && p.Group == ProgramLeftoverGroup.NeedsReview);
            Assert.Empty(limits);
            Assert.False(ProgramDataFileIdentity.Scoped(Path.Combine(last, "third-level"), Local));
            Assert.False(ProgramDataFileIdentity.Scoped(Path.Combine(Local, "Microsoft"), Local));
        }
        finally { RemoveOwnRoot(root); }
    }

    [Fact]
    public void GenuineReadFailureRemainsVisible()
    {
        IEnumerable<string> Enumerate(string path) => path.Equals(Local, StringComparison.OrdinalIgnoreCase)
            ? throw new UnauthorizedAccessException("Controlled test denial") : Array.Empty<string>();
        var probes = new List<ProgramLeftoverProbe>(); var limits = new List<ProgramReviewLimitation>();
        var identity = Identity;
        ProgramDataFileIdentity.Discover(identity.Product, identity.Publisher, probes, limits, Enumerate);
        Assert.Empty(probes);
        Assert.Contains(limits, l => l.ReasonEn == "Some file identity metadata was inaccessible" && l.Detail == Local);
    }

    [Fact]
    public void FreshSearchDropsOnlyObsoleteSearchDeadlineFromSavedHistory()
    {
        var obsolete = new ProgramReviewLimitation("Старый предел", "File identity association search was bounded by time or folder count");
        var denied = new ProgramReviewLimitation("Нет доступа", "Some file identity metadata was inaccessible", "test-root");
        var snapshot = new ProgramReviewLimitation("Ограниченный снимок", "Footprint was incomplete");
        var current = ProgramDataFileIdentity.RefreshSearchLimitations(new[] { obsolete, denied, snapshot });
        Assert.DoesNotContain(obsolete, current);
        Assert.Contains(denied, current); Assert.Contains(snapshot, current);
    }
}
