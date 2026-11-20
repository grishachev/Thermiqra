using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

public class NativeOutcomeMatrixTests
{
    [Theory]
    [InlineData("MSI", 1602, true, 1, false)]
    [InlineData("MSI", 1602, false, 1, false)]
    [InlineData("MSI", 3010, false, 2, false)]
    [InlineData("MSI", 1641, false, 2, false)]
    [InlineData("MSI", 1603, true, 3, false)]
    [InlineData("MSI", 1603, false, 3, true)]
    [InlineData("MSI", 1605, false, 3, true)]
    [InlineData("MSI", 0, false, 0, true)]
    [InlineData("MSI", 0, true, 4, false)]
    [InlineData("Win32", 0, false, 0, true)]
    [InlineData("Win32", 0, true, 4, false)]
    [InlineData("Win32", 5, false, 3, true)]
    [InlineData("Win32", 5, true, 3, false)]
    [InlineData("Win32", 1602, false, 3, true)]
    [InlineData("Win32", 3010, false, 2, false)]
    [InlineData("Win32", 1641, false, 2, false)]
    public void NativeCodesAndRegistrationMustBothBeConsidered(string kind, int code, bool present, int expected, bool cleanup)
    {
        var result = InstalledProgramNativeOutcome.Classify(kind, code, present, "native detail");
        Assert.Equal((InstalledProgramRemovalState)expected, result.State);
        Assert.Equal(cleanup, ProgramRemovalOutcome.CanClean(result));
        Assert.Equal("native detail", result.Detail);
    }

    [Fact] public void UnknownPresenceCannotAuthorizeCleanup() => Assert.False(ProgramRemovalOutcome.CanClean(
        InstalledProgramNativeOutcome.Classify("Win32", 0, null, "Provider unavailable")));

    [Fact] public void PendingChildBlocksAnOtherwiseSuccessfulRemoval() => Assert.False(ProgramRemovalOutcome.CanClean(
        InstalledProgramNativeOutcome.Classify("Win32", 0, false, "", new() { new(12345, DateTime.UtcNow) })));
    [Fact] public void WaitFailureDoesNotBecomePermissionWhenRegistrationLaterDisappears()
    {
        var failed = new InstalledProgramRemovalResult(InstalledProgramRemovalState.Failed, null, "Wait failed",
            RegistrationAbsent: true, HelperObservationIncomplete: true);
        Assert.False(ProgramRemovalOutcome.CanClean(failed));
    }
}
