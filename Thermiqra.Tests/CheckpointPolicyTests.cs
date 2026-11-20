using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

public class CheckpointPolicyTests
{
    [Theory] [InlineData("Created", 5, true)] [InlineData("Created", 31, false)]
    [InlineData("Created", -1, false)] [InlineData("Unconfirmed", 5, false)] [InlineData("NotRequested", 5, false)]
    public void OnlyRecentVerifiedCheckpointIsReusable(string state, int age, bool reusable)
    {
        var now = new DateTime(2026, 11, 16, 1, 0, 0, DateTimeKind.Utc);
        Assert.Equal(reusable, ProgramCleanupCheckpointPolicy.CanReuse(new(state, now.AddMinutes(-age), "test", 1), now));
    }

    [Fact] public void MissingSequenceCannotBeReused() => Assert.False(
        ProgramCleanupCheckpointPolicy.CanReuse(new("Created", DateTime.UtcNow, "test"), DateTime.UtcNow));
}
