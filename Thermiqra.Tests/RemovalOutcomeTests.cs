using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

public class RemovalOutcomeTests
{
    [Theory]
    [InlineData(0, true, true)] [InlineData(1, true, false)] [InlineData(2, true, false)]
    [InlineData(3, false, false)] [InlineData(3, true, true)] [InlineData(4, false, false)]
    [InlineData(4, true, true)] [InlineData(5, true, true)]
    public void ExitCodeAloneDoesNotAuthorizeCleanup(int state, bool absent, bool permitted) =>
        Assert.Equal(permitted, ProgramRemovalOutcome.CanClean(new((InstalledProgramRemovalState)state, 1, "", absent)));

    [Fact] public void OutstandingHelperBlocksEvenWhenRegistrationDisappeared() => Assert.False(
        ProgramRemovalOutcome.CanClean(new(InstalledProgramRemovalState.Removed, 0, "", true,
            new() { new(12345, DateTime.UtcNow) })));

    [Theory] [InlineData(0, false)] [InlineData(1, true)] [InlineData(-1, true)]
    public void SharedDllReferenceCountIsConservative(int count, bool referenced) =>
        Assert.Equal(referenced, ProgramSharedComponentGuard.IsReferenced(count));

    [Fact] public void MalformedSharedReferenceBlocks() => Assert.True(ProgramSharedComponentGuard.IsReferenced("unknown"));

    [Fact] public void CorruptSavedProcessIdentityCannotAuthorizeCleanup() => Assert.Throws<IOException>(() =>
        ProgramPendingProcesses.Refresh(new(InstalledProgramRemovalState.Removed, 0, "", true,
            new() { new(-1, DateTime.MinValue) })));
}

