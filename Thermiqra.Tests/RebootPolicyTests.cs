using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

public class RebootPolicyTests
{
    private static readonly DateTime Before = new(2026, 11, 16, 1, 0, 0, DateTimeKind.Utc);
    [Theory] [InlineData(-1, true, false)] [InlineData(10, true, true)] [InlineData(10, false, false)]
    public void OnlyNewBootAndAbsentRegistrationReleaseRebootHold(int minutes, bool absent, bool expected)
    {
        var original = new InstalledProgramRemovalResult(InstalledProgramRemovalState.RebootRequired, 3010, "", absent, AttemptedUtc: Before);
        var refreshed = ProgramRemovalRebootPolicy.Refresh(original, Before.AddMinutes(-1), Before.AddMinutes(minutes));
        Assert.Equal(expected, ProgramRemovalOutcome.CanClean(refreshed));
    }
    [Fact] public void ClockJitterCannotReleaseTheHold()
    {
        var original = new InstalledProgramRemovalResult(InstalledProgramRemovalState.RebootRequired, 3010, "", true, AttemptedUtc: Before);
        Assert.False(ProgramRemovalOutcome.CanClean(ProgramRemovalRebootPolicy.Refresh(original, Before, Before.AddMilliseconds(200))));
    }
    [Fact] public void CancelledRemovalNeverBecomesCleanableAfterReboot()
    {
        var original = new InstalledProgramRemovalResult(InstalledProgramRemovalState.Cancelled, 1602, "", true, AttemptedUtc: Before);
        Assert.Equal(original, ProgramRemovalRebootPolicy.Refresh(original, Before, Before.AddHours(2)));
    }
    [Fact] public void FastRealRestartDoesNotRemainBlockedForThirtySeconds()
    {
        var original = new InstalledProgramRemovalResult(InstalledProgramRemovalState.RebootRequired, 3010, "", true, AttemptedUtc: Before);
        Assert.True(ProgramRemovalOutcome.CanClean(ProgramRemovalRebootPolicy.Refresh(original, Before, Before.AddSeconds(10))));
    }
    [Fact] public void MissingProcessObservationRequiresARealNewBoot()
    {
        var original = new InstalledProgramRemovalResult(InstalledProgramRemovalState.Removed, 0, "", true,
            AttemptedUtc: Before, HelperObservationIncomplete: true);
        Assert.False(ProgramRemovalOutcome.CanClean(original));
        Assert.False(ProgramRemovalOutcome.CanClean(ProgramRemovalRebootPolicy.Refresh(original, Before, Before.AddHours(-1))));
        Assert.True(ProgramRemovalOutcome.CanClean(ProgramRemovalRebootPolicy.Refresh(original, Before, Before.AddMinutes(1))));
    }
}
