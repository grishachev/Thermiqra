using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PCHardwareMonitor;

internal sealed partial class ProgramHistoryWindow
{
    private readonly Dictionary<string, string> _searchErrors = new(StringComparer.OrdinalIgnoreCase);

    private void InitializeResultInteraction()
    {
        // No table-wide tooltip: empty space is not an action target. Row names
        // are already visible and do not need competing hover popups.
        ToolTipService.SetIsEnabled(_removals, false);
        ToolTipService.SetIsEnabled(_changes, false);
        _removals.AddHandler(Mouse.PreviewMouseDownEvent,
            new MouseButtonEventHandler(async (_, args) =>
            {
                if (_busy || args.ChangedButton != MouseButton.Left ||
                    args.OriginalSource is not DependencyObject source ||
                    ItemsControl.ContainerFromElement(_removals, source) is not ListViewItem item ||
                    item.Content is not RemovalRow row) return;

                // Resolve the actual clicked row, not a previous selection or
                // IsMouseOver sampled after another handler has run.
                _removals.SelectedItem = row;
                item.Focus();
                if (args.ClickCount == 2)
                {
                    args.Handled = true;
                    await SearchSelectedAsync();
                }
            }), handledEventsToo: true);
        _removals.PreviewKeyDown += async (_, args) =>
        {
            if (args.Key != Key.Enter || _removals.SelectedItem is not RemovalRow) return;
            args.Handled = true; await SearchSelectedAsync();
        };
    }

    private Task SearchSelectedAsync() => RunAsync(async () =>
    {
        if (_removals.SelectedItem is not RemovalRow row) return;
        string id = row.Report.Baseline.Id;
        _status.Text = SettingsService.L("Поиск остатков: ", "Finding leftovers: ") + row.Name;
        var window = new ProgramLeftoverReviewWindow(this, row.Report.Baseline);
        window.ShowDialog();
        if (window.SearchError.Length > 0 || window.Report == null)
        {
            _searchErrors[id] = window.SearchError;
            row.SearchFailed = true;
            row.Result = SettingsService.L("Поиск не завершён", "Search incomplete");
            _removals.Items.Refresh();
            _status.Text = SettingsService.L("Поиск не завершён: ", "Search incomplete: ") + window.SearchError;
            return;
        }
        var report = window.Report;
        _searchErrors.Remove(id);
        row.SearchFailed = false; row.Report = report; row.Result = ProgramRemovalOutcome.CanClean(report.Baseline.NativeResult)
            ? Found(report) : SettingsService.L("Удаление не подтверждено", "Uninstall unconfirmed");
        _removals.Items.Refresh();
        await ReloadAsync();
    });
}
