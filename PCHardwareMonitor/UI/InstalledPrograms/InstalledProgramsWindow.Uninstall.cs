using System;
using System.Threading.Tasks;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PCHardwareMonitor;

public partial class MainWindow
{
    private sealed partial class InstalledProgramsWindow
    {
        private Button? _uninstallButton;
        private bool _uninstallBusy;

        private Button CreateUninstallButton()
        {
            _uninstallButton = new Button
            {
                Content = SettingsService.L("Удалить программу", "Uninstall application"),
                IsEnabled = false, Margin = new Thickness(8, 0, 8, 12),
                Padding = new Thickness(10, 9, 10, 9), MinHeight = 38
            };
            _uninstallButton.Click += async (_, _) => await UninstallSelectedAsync();
            Closing += (_, e) =>
            {
                if (!_uninstallBusy) return;
                e.Cancel = true;
                _statusText.Text = SettingsService.L(
                    "Дождитесь завершения текущей операции.",
                    "Wait for the current operation to finish.");
            };
            return _uninstallButton;
        }

        private void UpdateUninstallButton()
        {
            if (_uninstallButton == null) return;
            _uninstallButton.IsEnabled = !_uninstallBusy && _programsTable.SelectedItem is InstalledProgramInfo app &&
                !app.IsAuxiliary && !app.CachedInventoryUtc.HasValue &&
                !app.Name.Equals("Thermiqra", StringComparison.OrdinalIgnoreCase);
        }

        private async Task UninstallSelectedAsync()
        {
            if (_uninstallBusy || _programsTable.SelectedItem is not InstalledProgramInfo app) return;
            _uninstallBusy = true;
            if (_reviewButton != null) _reviewButton.IsEnabled = false;
            if (_recoveryButton != null) _recoveryButton.IsEnabled = false;
            _loadCancellation?.Cancel();
            _refreshButton.IsEnabled = false;
            _programsTable.IsEnabled = false;
            UpdateUninstallButton();
            bool attempted = false;
            try
            {
                ProgramUninstallWindow operation = new(this, app, _programs.ToList());
                operation.ShowDialog();
                attempted = operation.Attempted;
                if (operation.Report is { } report) _lastRemovalReport = report;

            }
            catch (Exception ex)
            {
                InstalledProgramActionDialog.Show(this, SettingsService.L("Thermiqra — Удаление программы", "Thermiqra — Uninstall application"),
                    ex.Message, "OK", false);
            }
            finally
            {
                _uninstallBusy = false;
                if (_reviewButton != null) _reviewButton.IsEnabled = true;
                _programsTable.IsEnabled = true;
                _refreshButton.IsEnabled = true;
                if (IsVisible)
                {
                    // Refresh Windows and the persistent snapshot after success,
                    // cancellation, or failure. Never hide a row optimistically.
                    await RefreshAsync();
                    if (attempted) ShowSelectedDetails();
                }
                UpdateUninstallButton();
                await UpdateRecoveryButtonAsync();
            }
        }
    }
}
