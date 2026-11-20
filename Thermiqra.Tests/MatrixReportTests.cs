using System.Text.Json;
using Thermiqra.RemovalLab;
using Xunit;

namespace Thermiqra.Tests;

public class MatrixReportTests
{
    [Fact] public void WaitingForRebootIsNotACompletedRunAndCanResumeWithoutLosingChecks()
    {
        var original = new MatrixReport { Status = "AwaitingReboot" };
        var scenario = original.Cases.Single(c => c.Id == "MsiReboot");
        scenario.Status = "AwaitingReboot"; scenario.BaselineId = Guid.NewGuid().ToString("N");
        scenario.Checks.Add(new("Native returned reboot required", true));
        var restored = JsonSerializer.Deserialize<MatrixReport>(JsonSerializer.Serialize(original))!;
        Assert.Null(restored.FinishedUtc);
        Assert.Equal("AwaitingReboot", restored.Status);
        var pending = restored.Cases.Single(c => c.Id == "MsiReboot");
        Assert.Equal(scenario.BaselineId, pending.BaselineId);
        Assert.Single(pending.Checks);
        Assert.All(restored.Cases.Where(c => c != pending), c => Assert.Equal("NotRun", c.Status));
    }

    [Fact] public void BlockedEnvironmentCannotBeReportedAsPassed()
    {
        var report = new MatrixReport { Status = "Blocked" };
        Assert.All(report.Cases, scenario => Assert.Equal("NotRun", scenario.Status));
        Assert.DoesNotContain(report.Cases, scenario => scenario.Status == "Passed");
    }
}
