using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PCHardwareMonitor;

public partial class StorageAnalyzerWindow
{
    private void SetCapacityHeader(long? total, long? free)
    {
        foreach (var summary in new[] { CyberCapacity, SteamCapacity, FrostCapacity, MilitaryCapacity }) summary.Update(total, free);
    }

    private async Task RefreshCapacityHeaderAsync(CancellationTokenSource analysis)
    {
        var previous = _currentResult;
        var capacity = await Task.Run(() =>
        {
            try
            {
                DriveInfo drive = new(_driveName);
                return (Total: (long?)drive.TotalSize, Free: (long?)drive.AvailableFreeSpace);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
            { return (Total: (long?)null, Free: (long?)null); }
        });
        if (ReferenceEquals(_analysisCancellation, analysis) && !analysis.IsCancellationRequested && ReferenceEquals(previous, _currentResult))
            SetCapacityHeader(capacity.Total, capacity.Free);
    }
}
