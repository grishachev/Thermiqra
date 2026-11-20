using System.Security.AccessControl;
using System.Text.Json;
using Microsoft.Win32;
using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

public class RegistryBackupTests
{
    private static ProgramRegistrySnapshot Snapshot(List<ProgramRegistryText>? values = null)
    {
        var security = new RawSecurityDescriptor("O:BAG:BAD:(A;;KA;;;BA)");
        byte[] bytes = new byte[security.BinaryLength]; security.GetBinaryForm(bytes, 0);
        string acl = Convert.ToBase64String(bytes);
        return new(new(), acl, new() { new("", values ?? new(), acl) });
    }

    [Fact] public void TypedBackupSurvivesSerializationWithoutRegistryAccess()
    {
        List<ProgramRegistryText> values = new()
        {
            new("plain", "Пример", (int)RegistryValueKind.String),
            new("expand", "%LOCALAPPDATA%\\Example", (int)RegistryValueKind.ExpandString),
            new("binary", Convert.ToBase64String(new byte[] { 0, 255, 17 }), (int)RegistryValueKind.Binary),
            new("dword", "-2147483648", (int)RegistryValueKind.DWord),
            new("qword", "-9223372036854775808", (int)RegistryValueKind.QWord),
            new("array", JsonSerializer.Serialize(new[] { "one", "", "два" }), (int)RegistryValueKind.MultiString)
        };
        var original = Snapshot(values);
        var restored = JsonSerializer.Deserialize<ProgramRegistrySnapshot>(JsonSerializer.Serialize(original))!;
        Assert.True(ProgramSettingsRegistryCleanup.Valid(restored));
        Assert.Equal(original.Security, restored.Security);
        Assert.Equal(values, restored.Tree![0].Values);
    }

    [Theory] [InlineData("SymbolicLinkValue", "x", 1)] [InlineData("value", "garbage", 3)]
    [InlineData("value", "2147483648", 4)] [InlineData("value", "null", 7)] [InlineData("value", "[null]", 7)] [InlineData("value", "x", 999)]
    public void MalformedPayloadIsRejected(string name, string value, int kind)
    {
        Assert.False(ProgramSettingsRegistryCleanup.Valid(Snapshot(new() { new(name, value, kind) })));
    }

    [Fact] public void CaseInsensitiveDuplicatesAndMissingParentsAreRejected()
    {
        Assert.False(ProgramSettingsRegistryCleanup.Valid(Snapshot(new() { new("A", "x", 1), new("a", "y", 1) })));
        var snapshot = Snapshot();
        snapshot.Tree!.Add(new("Missing\\Child", new(), snapshot.Security));
        Assert.False(ProgramSettingsRegistryCleanup.Valid(snapshot));
    }

    [Fact] public void RestoreAcceptsOnlyIdenticalSurvivingNodes()
    {
        var saved = Snapshot(new() { new("Mode", "old", 1) });
        saved.Tree!.Add(new("Child", new(), saved.Security));
        var identicalRoot = Snapshot(new() { new("Mode", "old", 1) });
        Assert.True(ProgramRegistryRestorePolicy.IsUnchangedSubset(saved, identicalRoot));
        Assert.False(ProgramRegistryRestorePolicy.IsUnchangedSubset(saved, Snapshot(new() { new("Mode", "new", 1) })));
        var foreign = Snapshot(new() { new("Mode", "old", 1) });
        foreign.Tree!.Add(new("Foreign", new(), foreign.Security));
        Assert.False(ProgramRegistryRestorePolicy.IsUnchangedSubset(saved, foreign));
    }
}
