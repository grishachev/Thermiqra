using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

public class RegistryNamespaceTests
{
    private static ProgramRemovalBaseline Baseline => new("test", DateTime.UtcNow,
        "registry:HKCU:64:AcmeReader", "Acme Reader 4.2", "4.2", new(), new(),
        Publisher: "Acme", InstallRoot: @"B:\Apps\AcmeReader");

    [Theory]
    [InlineData("SOFTWARE\\Acme\\AcmeReader", "HKCU", true)]
    [InlineData("SOFTWARE\\Acme\\AcmeReader", "HKLM", true)]
    [InlineData("SOFTWARE\\Acme\\Acrobat", "HKCU", false)]
    [InlineData("SOFTWARE\\Acme", "HKCU", false)]
    [InlineData("SOFTWARE\\OtherVendor\\AcmeReader", "HKLM", false)]
    [InlineData("SOFTWARE\\Acme\\AcmeReaderAddon", "HKCU", false)]
    public void AnotherProductOrVendorParentIsNotTheApplication(string path, string hive, bool matches)
    {
        var probe = new ProgramLeftoverProbe("Registry", path, ProgramLeftoverGroup.UserData, "", "", hive, "64");
        Assert.Equal(matches, ProgramSettingsRegistryCleanup.HasApplicationNamespace(probe, Baseline));
    }

    [Theory]
    [InlineData("SOFTWARE\\Microsoft\\AcmeReader")]
    [InlineData("SOFTWARE\\Classes\\AcmeReader")]
    [InlineData("SYSTEM\\CurrentControlSet\\Services")]
    [InlineData("SOFTWARE")]
    [InlineData("SOFTWARE\\Acme\\AcmeReader\\Nested")]
    public void ProtectedBranchesFailBeforeAnyRegistryRead(string path)
    {
        var probe = new ProgramLeftoverProbe("Registry", path, ProgramLeftoverGroup.UserData, "", "", "HKCU", "64");
        Assert.Throws<IOException>(() => ProgramSettingsRegistryCleanup.HasApplicationNamespace(probe, Baseline));
    }
}
