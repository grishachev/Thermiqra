using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

// Real NTFS operations, exclusively in a unique test-owned temporary directory.
// No Windows registration, Recycle Bin, installed application or global ACL changes.
public class IsolatedFileIntegrationTests
{
    private sealed class Area : IDisposable
    {
        private readonly string _expected = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Thermiqra-integration-" + Guid.NewGuid().ToString("N")));
        internal string Root { get; }
        internal Area() { Root = _expected; Directory.CreateDirectory(Root); }
        public void Dispose()
        {
            if (Path.GetFullPath(Root) != _expected || Directory.GetParent(Root)!.FullName != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar))
                throw new IOException("Unexpected test teardown path");
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }

    [Fact] public void LockedFileCannotBeMovedAndOriginalSurvives()
    {
        using Area area = new(); string file = Path.Combine(area.Root, "payload.dat"), backup = Path.Combine(area.Root, "backup.dat");
        File.WriteAllText(file, "original payload"); string expected = Hash(file);
        using (var held = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.Throws<IOException>(() => ProgramMutationBarrier.Execute(
                () => ProgramOperationJournal.Write(Path.Combine(area.Root, "intent.json"), new { Path = file }),
                () => { }, () => File.Move(file, backup)));
        Assert.Equal(expected, Hash(file)); Assert.False(File.Exists(backup));
    }

    [Fact] public void FailedDurableJournalLeavesPayloadUntouched()
    {
        using Area area = new(); string file = Path.Combine(area.Root, "payload.dat"), journal = Path.Combine(area.Root, "intent.json");
        File.WriteAllText(file, "original"); File.WriteAllText(journal, "previous intent");
        using (var held = new FileStream(journal, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var error = Record.Exception(() => ProgramMutationBarrier.Execute(() => ProgramOperationJournal.Write(journal, new { New = true }),
                () => { }, () => File.Delete(file)));
            Assert.True(error is IOException or UnauthorizedAccessException);
        }
        Assert.Equal("original", File.ReadAllText(file)); Assert.Equal("previous intent", File.ReadAllText(journal));
    }

    [Fact] public void ActualAccessDenialDoesNotMutatePayload()
    {
        using Area area = new(); var file = new FileInfo(Path.Combine(area.Root, "payload.dat")); File.WriteAllText(file.FullName, "original");
        var original = file.GetAccessControl(); var denied = file.GetAccessControl();
        denied.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ReadData, AccessControlType.Deny));
        try
        {
            file.SetAccessControl(denied);
            Assert.Throws<UnauthorizedAccessException>(() => ProgramMutationBarrier.Execute(
                () => ProgramOperationJournal.Write(Path.Combine(area.Root, "intent.json"), new { File = file.FullName }),
                () => { using var input = File.OpenRead(file.FullName); }, () => File.Delete(file.FullName)));
        }
        finally
        {
            var restore = new FileSecurity();
            restore.SetSecurityDescriptorBinaryForm(original.GetSecurityDescriptorBinaryForm(), AccessControlSections.Access);
            file.SetAccessControl(restore);
        }
        Assert.Equal("original", File.ReadAllText(file.FullName));
    }

    [Fact] public void OtherRegisteredApplicationInsideFolderBlocksTheRealInventoryGuard()
    {
        using Area area = new();
        var peer = new InstalledProgramInfo("Peer", "Vendor", "1", "Win32", "—", Path.Combine(area.Root, "SharedEngine"), "", Identity: "synthetic");
        Assert.Throws<IOException>(() => ProgramLeftoverCleanupPolicy.CheckInventory(area.Root, new[] { peer }));
    }
}
