using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PCHardwareMonitor;

internal sealed partial class ProgramHistoryWindow
{
    private readonly TabControl _tabs = new();
    private readonly TextBox _search = new() { MinWidth = 220, FontSize = 14, Padding = new Thickness(8), Margin = new Thickness(0, 0, 12, 8) };
    private readonly ComboBox _period = new() { MinWidth = 160, FontSize = 14, Margin = new Thickness(0, 0, 12, 8) };
    private readonly ComboBox _resultFilter = new() { MinWidth = 190, FontSize = 14, Margin = new Thickness(0, 0, 12, 8) };
    private readonly TextBlock _count = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
    private HistoryData? _historyData;
    private bool _configuring;

    private WrapPanel CreateFilters()
    {
        WrapPanel panel = new() { Margin = new Thickness(0, 0, 0, 8) };
        StackPanel SearchField(string title, Control control)
        {
            StackPanel field = new();
            TextBlock label = new() { Text = title, Margin = new Thickness(0, 0, 0, 4), FontSize = 14 };
            label.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
            field.Children.Add(label); field.Children.Add(control); return field;
        }
        _search.SetResourceReference(FrameworkElement.StyleProperty, "ProgramSearchBoxStyle");
        _search.SetResourceReference(Control.BackgroundProperty, "CardBackgroundBrush");
        _search.SetResourceReference(Control.ForegroundProperty, "PrimaryTextBrush");
        _search.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
        _search.ToolTip = SettingsService.L("Поиск по названию или версии", "Search by name or version");
        foreach (int days in new[] { 30, 90, 365, 0 })
            _period.Items.Add(new ComboBoxItem { Tag = days, Content = days == 0 ? SettingsService.L("За всё время", "All time") : SettingsService.L($"За {days} дней", $"Last {days} days") });
        _period.SelectedIndex = 0;
        _period.ToolTip = SettingsService.L("Удалённые программы — по дате последнего поиска остатков. Изменения — по дате обнаружения.",
            "Removed applications: date of the last leftover search. Changes: date detected.");
        ProgramRecoveryStyles.Apply(_period); ProgramRecoveryStyles.Apply(_resultFilter);
        ConfigureResultFilter();
        panel.Children.Add(SearchField(SettingsService.L("Название или версия", "Name or version"), _search));
        panel.Children.Add(SearchField(SettingsService.L("Период", "Period"), _period));
        panel.Children.Add(SearchField(SettingsService.L("Показать", "Show"), _resultFilter));
        _count.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush"); panel.Children.Add(_count);
        _search.TextChanged += (_, _) => ApplyFilters();
        _period.SelectionChanged += (_, _) => ApplyFilters();
        _resultFilter.SelectionChanged += (_, _) => { if (!_configuring) ApplyFilters(); };
        return panel;
    }

    private void ConfigureResultFilter()
    {
        _configuring = true;
        _resultFilter.Items.Clear();
        var options = _tabs.SelectedIndex == 1
            ? new[] { ("", "Все изменения", "All changes"), ("Added", "Появились", "Appeared"), ("Removed", "Исчезли", "Disappeared"), ("Changed", "Изменились сведения", "Details changed") }
            : new[] { ("", "Все результаты", "All results"), ("Found", "Есть найденные объекты", "Objects found"), ("Empty", "Объекты не найдены", "No objects found") };
        foreach (var (key, ru, en) in options) _resultFilter.Items.Add(new ComboBoxItem { Tag = key, Content = SettingsService.L(ru, en) });
        _resultFilter.SelectedIndex = 0; _configuring = false;
    }

    private void ApplyFilters()
    {
        if (_historyData == null || _configuring) return;
        string? selectedRemoval = (_removals.SelectedItem as RemovalRow)?.Report.Baseline.Identity;
        string? selectedChange = (_changes.SelectedItem as ChangeRow)?.Key;
        string query = _search.Text.Trim();
        int days = (_period.SelectedItem as ComboBoxItem)?.Tag is int value ? value : 0;
        DateTime since = days == 0 ? DateTime.MinValue : DateTime.Now.Date.AddDays(1 - days).ToUniversalTime();
        string filter = (_resultFilter.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        bool Matches(string name, string version) => query.Length == 0 || name.Contains(query, StringComparison.CurrentCultureIgnoreCase) || version.Contains(query, StringComparison.CurrentCultureIgnoreCase);
        bool changesTab = _tabs.SelectedIndex == 1;
        var removals = _historyData.Removals.Where(r => r.Report.CheckedUtc >= since && Matches(r.Name, r.Version) &&
            (changesTab || filter == "" || (!r.SearchFailed && !r.Report.SearchPending && ProgramRemovalOutcome.CanClean(r.Report.Baseline.NativeResult) &&
                (filter == "Found" ? r.Report.Found.Count > 0 : (r.Report.Found.Count == 0 && r.Report.Limitations.Count == 0))))).ToList();
        var changes = _historyData.Changes.Where(c => c.DetectedUtc >= since && Matches(c.Name, c.Version) &&
            (!changesTab || filter == "" || c.Kind == filter)).ToList();
        _removals.ItemsSource = removals; _changes.ItemsSource = changes;
        _removals.SelectedItem = removals.FirstOrDefault(r => r.Report.Baseline.Identity == selectedRemoval);
        _changes.SelectedItem = changes.FirstOrDefault(c => c.Key == selectedChange);
        int shown = changesTab ? changes.Count : removals.Count;
        int total = changesTab ? _historyData.Changes.Count : _historyData.Removals.Count;
        _count.Text = SettingsService.L($"Показано: {shown} из {total}", $"Showing: {shown} of {total}");
        UpdateButtons();
    }
}
