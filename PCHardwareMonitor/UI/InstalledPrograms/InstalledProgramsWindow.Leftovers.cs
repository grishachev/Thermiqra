using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PCHardwareMonitor;

public partial class MainWindow
{
    private sealed partial class InstalledProgramsWindow
    {
        private ProgramLeftoverReport? _lastRemovalReport;
        private Button? _reviewButton;
        private Button? _recoveryButton;

        private Button CreateRecoveryButton()
        {
            Button button = new() { Content = SettingsService.L("Восстановление", "Recovery"),
                Margin = new Thickness(8, 0, 8, 12), Padding = new Thickness(8), MinHeight = 38, IsEnabled = false };
            _recoveryButton = button;
            Loaded += async (_, _) => await UpdateRecoveryButtonAsync();
            button.Click += async (_, _) =>
            {
                if (_uninstallBusy) return;
                _uninstallBusy = true;
                button.IsEnabled = false;
                _loadCancellation?.Cancel();
                _refreshButton.IsEnabled = false;
                _programsTable.IsEnabled = false;
                UpdateUninstallButton();
                try { await ProgramCleanupPresentation.ShowRecoveryAsync(this); }
                finally
                {
                    _uninstallBusy = false;
                    _refreshButton.IsEnabled = true;
                    _programsTable.IsEnabled = true;
                    UpdateUninstallButton();
                    await UpdateRecoveryButtonAsync();
                }
            };
            return button;
        }

        private async Task UpdateRecoveryButtonAsync()
        {
            try { await ProgramCleanupRecoveryService.LoadAsync(); }
            catch (Exception ex) { _statusText.Text = SettingsService.L("Не удалось проверить срок хранения копий: ", "Could not check backup retention: ") + ex.Message; }
            if (_recoveryButton != null && IsVisible) _recoveryButton.IsEnabled = !_uninstallBusy;
        }

        private Button CreateReviewButton()
        {
            _reviewButton = new Button
            {
                Content = SettingsService.L("История и остатки", "History and leftovers"),
                Margin = new Thickness(8, 0, 8, 12), Padding = new Thickness(8), MinHeight = 38,
                IsEnabled = true
            };
            _reviewButton.Click += async (_, _) =>
            {
                if (!_uninstallBusy)
                {
                    _uninstallBusy = true;
                    _reviewButton.IsEnabled = false;
                    UpdateUninstallButton();
                    try
                    {
                        if (IsVisible) new ProgramHistoryWindow(this).ShowDialog();
                    }
                    catch (Exception ex)
                    { InstalledProgramActionDialog.Show(this, SettingsService.L("Анализ остатков", "Leftover review"), ex.Message, "OK", false); }
                    finally
                    {
                        _uninstallBusy = false;
                        _reviewButton.IsEnabled = true;
                        UpdateUninstallButton();
                        await UpdateRecoveryButtonAsync();
                    }
                }
            };
            Loaded += async (_, _) =>
            {
                var report = await Task.Run(ProgramRemovalReviewStore.ReadLatest);
                if (!IsVisible || _lastRemovalReport != null) return;
                _lastRemovalReport = report;
                _reviewButton.IsEnabled = !_uninstallBusy;
            };
            return _reviewButton;
        }

    }
}
