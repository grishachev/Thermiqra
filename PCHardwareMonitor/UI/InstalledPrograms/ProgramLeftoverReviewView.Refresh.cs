using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace PCHardwareMonitor;

internal sealed partial class ProgramLeftoverReviewView
{
    private readonly DispatcherTimer _existenceTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool _existenceRefresh, _reviewClosed;

    private void InitializeExistenceRefresh()
    {
        Loaded += (_, _) => _existenceTimer.Start();
        _owner.Closed += (_, _) => { _reviewClosed = true; _existenceTimer.Stop(); };
        _existenceTimer.Tick += async (_, _) =>
        {
            if (_cleanupBusy || _surface.HasDecision || _existenceRefresh || !IsVisible || !_owner.IsEnabled || _reviewClosed) return;
            _existenceRefresh = true;
            try
            {
                var snapshot = _report.Found.Where(p => p.Kind is "Directory" or "Shortcut" or "UserData" or "RecordedFile").ToList();
                bool disappeared = await Task.Run(() => snapshot.Any(ProgramLeftoverCleanupPolicy.AlreadyAbsent));
                if (disappeared && !_cleanupBusy && !_reviewClosed && !_surface.HasDecision && IsVisible && _owner.IsEnabled)
                {
                    SetBusy(true);
                    try { await RefreshReportAsync(); }
                    finally { SetBusy(false); }
                }
            }
            catch (Exception ex)
            {
                if (!_reviewClosed) _counts.Text = SettingsService.L("Не удалось обновить наличие объектов: ",
                    "Could not refresh item presence: ") + ex.Message;
            }
            finally { _existenceRefresh = false; }
        };
    }
}
