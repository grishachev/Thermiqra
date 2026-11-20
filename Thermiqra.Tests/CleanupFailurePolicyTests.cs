using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

public class CleanupFailurePolicyTests
{
    [Theory]
    [InlineData("Recycled", true, true, "Recycled")]
    [InlineData("Removed", true, true, "Removed")]
    [InlineData("Prepared", true, true, "Failed")]
    [InlineData("Prepared", false, true, "AlreadyAbsent")]
    [InlineData("Prepared", false, false, "Failed")]
    public void DisappearanceAfterMutationIsNotEvidenceThatDeletionWasUnnecessary(string state, bool prepared, bool absent, string expected)
    {
        var probe = new ProgramLeftoverProbe("Directory", @"C:\Example", ProgramLeftoverGroup.UserData, "", "");
        var outcome = ProgramCleanupFailurePolicy.Record(new(probe, "", null, state), prepared, absent, "Journal write failed");
        Assert.Equal(expected, outcome.State);
        if (prepared) Assert.Equal("Journal write failed", outcome.Detail);
    }
}
