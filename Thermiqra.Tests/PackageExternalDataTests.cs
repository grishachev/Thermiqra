using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

public sealed class PackageExternalDataTests
{
    private sealed class Area : IDisposable
    {
        internal string Root { get; } = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        internal string Vendor { get; } = "Thermiqra MSIX Lab " + Guid.NewGuid().ToString("N");
        internal string VendorPath => Path.Combine(Root, Vendor);
        internal string ProductPath => Path.Combine(VendorPath, "Mesh Studio");
        internal string SiblingPath => Path.Combine(VendorPath, "Image Studio");
        internal string Identity => "msix:" + ProgramProductIdentity.Token(Vendor) + ".MeshStudio_labpublisher";
        internal ProgramRemovalBaseline Baseline => new(Guid.NewGuid().ToString("N"), DateTime.UtcNow,
            Identity, "Mesh Studio 2.0.0", "2.0.0", [], [], Publisher: "—",
            InstallRoot: @"C:\Program Files\WindowsApps\Synthetic.Package_2.0.0.0_x64__labpublisher",
            NativeResult: new(InstalledProgramRemovalState.Removed, 0, "", true));
        internal Area()
        {
            if (Directory.Exists(VendorPath)) throw new IOException("Test area already exists");
            Directory.CreateDirectory(Path.Combine(ProductPath, "1.0", "cache"));
            File.WriteAllBytes(Path.Combine(ProductPath, "1.0", "cache", "compat.dat"), new byte[] { 1, 2, 3 });
            Directory.CreateDirectory(SiblingPath); File.WriteAllText(Path.Combine(SiblingPath, "keep.txt"), "other product");
        }
        public void Dispose()
        {
            string target = Path.GetFullPath(VendorPath);
            if (Path.GetDirectoryName(target) != Path.GetFullPath(Root) || Path.GetFileName(target) != Vendor ||
                !Vendor.StartsWith("Thermiqra MSIX Lab ", StringComparison.Ordinal)) throw new IOException("Unexpected cleanup path");
            if (Directory.Exists(target)) Directory.Delete(target, true);
        }
    }

    [Theory]
    [InlineData("msix:BlenderFoundation.Blender_ppwjx1n5r4v9t", "Blender 5.3.0.0", "Blender Foundation", "Blender")]
    [InlineData("msix:ExampleCompany.MeshStudio_publisher", "Mesh Studio 2.0.0", "Example Company", "Mesh Studio")]
    [InlineData("msix:Northstar.PhotoEditor_publisher", "Photo Editor 4.2.0", "Northstar", "PhotoEditor")]
    public void RegisteredPackageNamespaceWorksAcrossProductsWithoutAnApplicationList(string identity, string title, string vendor, string product)
    {
        string root = @"C:\Users\Example\AppData\Roaming";
        Assert.True(ProgramPackageDataLayout.MatchesNamespace(Path.Combine(root, vendor, product), root, identity, title));
        Assert.False(ProgramPackageDataLayout.MatchesNamespace(Path.Combine(root, vendor), root, identity, title));
        Assert.False(ProgramPackageDataLayout.MatchesNamespace(Path.Combine(root, vendor, "Different Product"), root, identity, title));
        Assert.False(ProgramPackageDataLayout.MatchesNamespace(Path.Combine(root, vendor, product, "child"), root, identity, title));
    }

    [Theory]
    [InlineData("msix:NoFamilyDelimiter")][InlineData("msix:../Vendor.Product_hash")]
    [InlineData("msix:Vendor..Product_hash")][InlineData("registry:HKCU:64:Example")]
    public void InvalidOrUnrelatedIdentityCannotAuthorizeVendorNamespace(string identity)
    {
        Assert.False(ProgramPackageDataLayout.MatchesNamespace(@"C:\Data\Vendor\Product", @"C:\Data", identity, "Product"));
    }

    [Fact]
    public void DiscoveryAddsActualProductScopeWithoutClaimingVendorOrSibling()
    {
        using var area = new Area();
        var probes = new List<ProgramLeftoverProbe>(); var limits = new List<ProgramReviewLimitation>();
        ProgramPackageDataLayout.Capture(area.Identity, area.Baseline.Name, probes, limits, new[] { area.Root });
        Assert.Empty(limits);
        var probe = Assert.Single(probes);
        Assert.Equal(area.ProductPath, probe.Path); Assert.Equal(ProgramPackageDataLayout.Evidence, probe.EvidenceTarget);
        Assert.NotEqual(area.VendorPath, probe.Path); Assert.NotEqual(area.SiblingPath, probe.Path);
    }

    [Fact]
    public void OldPackageReportWithoutExternalProbesFindsAndValidatesDataFromEarlierVersions()
    {
        using var area = new Area(); var baseline = area.Baseline;
        var report = ProgramLeftoverDiscoveryService.Inspect(baseline);
        var probe = Assert.Single(report.Found, p => p.Path == area.ProductPath);
        Assert.Equal(ProgramLeftoverGroup.UserData, probe.Group);
        Assert.Empty(report.Limitations);
        Assert.Equal(ProgramPackageDataLayout.Evidence, probe.EvidenceTarget);
        Assert.True(ProgramRemovalDecisions.Evaluate(probe, report.Baseline, new(true)).Allowed);
        Directory.SetLastWriteTimeUtc(Path.Combine(area.ProductPath, "1.0", "cache"), DateTime.UtcNow.AddMinutes(1));
        ProgramUserDataLeftoverService.Validate(probe, report.Baseline, Array.Empty<InstalledProgramInfo>());
        // A different application with the same package product namespace is a conflict,
        // even if its display name was changed and no install path points into AppData.
        var sameProduct = new InstalledProgramInfo("Different display title", "Vendor", "3", "MSIX", "—", "", "",
            Identity: area.Identity.Replace("labpublisher", "otherpublisher"));
        Assert.Throws<IOException>(() => ProgramUserDataLeftoverService.Validate(probe, report.Baseline, new[] { sameProduct }));
        var sibling = new InstalledProgramInfo("Image Studio", "Vendor", "1", "MSIX", "—", "", "",
            Identity: area.Identity.Replace("MeshStudio", "ImageStudio"));
        ProgramUserDataLeftoverService.Validate(probe, report.Baseline, new[] { sibling });
        Assert.Equal("other product", File.ReadAllText(Path.Combine(area.SiblingPath, "keep.txt")));
        var parent = probe with { Path = area.VendorPath };
        Assert.False(ProgramRemovalDecisions.Evaluate(parent, report.Baseline, new(true)).Allowed);
        Assert.Throws<IOException>(() => ProgramUserDataLeftoverService.Validate(parent, report.Baseline, Array.Empty<InstalledProgramInfo>()));
        File.WriteAllText(Path.Combine(area.ProductPath, "changed-after-review.dat"), "new content");
        Assert.Throws<IOException>(() => ProgramUserDataLeftoverService.Validate(probe, report.Baseline, Array.Empty<InstalledProgramInfo>()));
    }

    [Fact]
    public void ObservationOnlyPackageDoesNotGainDeletionAuthority()
    {
        using var area = new Area();
        var report = ProgramLeftoverDiscoveryService.Inspect(area.Baseline with { ObservationOnly = true });
        var probe = Assert.Single(report.Found, p => p.Path == area.ProductPath);
        Assert.Equal(ProgramLeftoverGroup.NeedsReview, probe.Group);
        Assert.False(ProgramRemovalDecisions.Evaluate(probe, report.Baseline, new(true)).Allowed);
    }

    [Fact]
    public void RemovingLastProductLeavesNoApplicationResidueInRecheckAndPreservesSibling()
    {
        using var area = new Area();
        var report = ProgramLeftoverDiscoveryService.Inspect(area.Baseline);
        Assert.Contains(report.Found, p => p.Path == area.ProductPath);
        // Deletion is restricted to this test-owned fixture. No installed package,
        // user files, Windows registration or Recycle Bin is modified by tests.
        Assert.Equal(area.VendorPath, Directory.GetParent(area.ProductPath)!.FullName);
        Directory.Delete(area.ProductPath, true);
        var verified = ProgramLeftoverDiscoveryService.Inspect(report.Baseline);
        Assert.Empty(verified.Found); Assert.Empty(verified.Limitations);
        Assert.Equal("other product", File.ReadAllText(Path.Combine(area.SiblingPath, "keep.txt")));
    }

    [Theory]
    [InlineData("Directory", "WindowsApps")][InlineData("UserData", "Packages")]
    [InlineData("UserData", "Root")][InlineData("Registry", "Registry")]
    public void PackageSystemAreasStayBlocked(string kind, string target)
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var baseline = new ProgramRemovalBaseline("test", DateTime.UtcNow, "msix:Vendor.Product_publisher", "Product", "1", [], []);
        string path = target switch { "WindowsApps" => @"C:\Program Files\WindowsApps\Vendor.Product", "Packages" => Path.Combine(root, "Packages", "Vendor.Product_publisher"), "Root" => root, _ => @"SOFTWARE\Vendor\Product" };
        var probe = new ProgramLeftoverProbe(kind, path, ProgramLeftoverGroup.UserData, "", "", EvidenceRoot: root,
            EvidenceTarget: ProgramPackageDataLayout.Evidence, EvidenceFingerprint: "fingerprint");
        Assert.False(ProgramRemovalDecisions.Evaluate(probe, baseline, new(true)).Allowed);
    }
}
