using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

public class DriveCapacityTests
{
    [Theory] [InlineData(1000, 200, 800)] [InlineData(1000, 0, 1000)] [InlineData(1000, 1000, 0)]
    public void UsedIsWindowsTotalMinusFree(long total, long free, long used) => Assert.Equal(used, DriveCapacityValues.Create(total, free).Used);

    [Theory] [InlineData(null, null)] [InlineData(0L, 0L)] [InlineData(1000L, -1L)] [InlineData(1000L, 1001L)]
    public void InvalidOrUnavailableNumbersStayUnknown(long? total, long? free)
    {
        var values = DriveCapacityValues.Create(total, free);
        Assert.Null(values.Total); Assert.Null(values.Free); Assert.Null(values.Used);
    }
}
