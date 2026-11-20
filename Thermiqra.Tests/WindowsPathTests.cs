using Xunit;
namespace PCHardwareMonitor.Tests;
public sealed class WindowsPathTests
{
    [Theory][InlineData(@"C:relative")][InlineData(@"\\server\share\file")][InlineData(@"\\?\C:\App")][InlineData(@"C:\App\file:stream")]
    [InlineData(@"C:\App\NUL.txt")][InlineData(@"C:\App\COM1")][InlineData(@"C:\App\folder.")][InlineData(@"C:\App\folder \file")]
    public void AmbiguousAndDevicePathsAreRejected(string path) => Assert.Empty(WindowsPathRules.Normalize(path));
    [Theory][InlineData("\"C:\\App")][InlineData("C:\\App\"")][InlineData("\"\"C:\\App\"\"")]
    public void BrokenQuotingIsRejected(string path) => Assert.Empty(WindowsPathRules.Normalize(path));
    [Fact] public void DriveRootStaysAbsolute() => Assert.Equal(@"C:\", WindowsPathRules.Normalize(@"C:\"));
    [Fact] public void PrefixIsNotContainment() => Assert.False(WindowsPathRules.Inside(@"C:\Apps\ReaderExtra", @"C:\Apps\Reader"));
    [Fact] public void CaseDoesNotChangeScope() => Assert.True(WindowsPathRules.Inside(@"c:\apps\reader\data", @"C:\APPS\READER"));
    [Fact] public void ParentTraversalLeavesScope() => Assert.False(WindowsPathRules.Inside(WindowsPathRules.Normalize(@"C:\Apps\Reader\..\Acrobat\data"), @"C:\Apps\Reader"));
}
