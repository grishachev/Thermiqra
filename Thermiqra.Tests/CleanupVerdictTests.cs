using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

public class CleanupVerdictTests
{
    [Theory]
    [InlineData(true, false, 0, 0, 0, false, 0)]
    [InlineData(true, false, 1, 0, 0, false, 1)]
    [InlineData(true, false, 0, 1, 0, false, 2)]
    [InlineData(true, false, 0, 0, 1, false, 3)]
    [InlineData(false, false, 0, 0, 0, false, 4)]
    [InlineData(true, true, 0, 0, 0, false, 4)]
    [InlineData(true, false, 0, 0, 0, true, 4)]
    public void EmptyListAloneCannotMeanSuccessfulCleanup(bool verified, bool pending, int remaining,
        int limitations, int failed, bool saveFailed, int expected) => Assert.Equal((ProgramCleanupVerdict)expected,
        ProgramCleanupVerdicts.Evaluate(verified, pending, remaining, limitations, failed, saveFailed));
}
