using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Markup;

namespace PCHardwareMonitor;

internal sealed partial class ProgramHistoryWindow : Window
{
    private sealed class RemovalRow
    {
        internal ProgramLeftoverReport Report = null!;
        public string Name => Report.Baseline.Name;
        public string Version => Report.Baseline.Version;
        public string Date => SearchFailed || Report.SearchPending ? "—" : DateText(Report.CheckedUtc);
        internal bool SearchFailed;
        public string Source => Report.Baseline.NativeResult?.State == InstalledProgramRemovalState.RegistrationRemoved
            ? SettingsService.L("Удаление записи", "Registration removal") : Report.Baseline.ObservationOnly ? SettingsService.L("Наблюдение", "Observation") :
            SettingsService.L("Штатное удаление", "Native uninstall");
        public string Result { get; set; } = "";
    }
    private readonly ListView _removals = MakeList(("Name", "Программа", "Application", 280),
        ("Version", "Версия", "Version", 100), ("Date", "Последний поиск", "Last search", 170),
        ("Result", "Результат", "Result", 180), ("Source", "Источник", "Source", 160));
    private readonly ListView _changes = MakeList(("Name", "Программа", "Application", 250),
        ("Type", "Изменение", "Change", 180), ("Version", "Версия", "Version", 100),
        ("Date", "Обнаружено", "Detected", 150), ("Continuity", "Наблюдение", "Observation", 180));
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
    private readonly Button _selected, _close;
    private bool _busy;

    internal ProgramHistoryWindow(Window owner)
    {
        Owner = owner; Title = SettingsService.L("Thermiqra — История программ", "Thermiqra — Program history");
        Width = 1140; Height = 740; MinWidth = 900; MinHeight = 580; FontSize = 14; FontFamily = owner.FontFamily;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        ContentRendered += (_, _) => WindowPlacementService.ConstrainToWorkArea(this);
        SetResourceReference(BackgroundProperty, "WindowBackgroundBrush"); SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
        if (owner.TryFindResource(typeof(Button)) is Style buttons) Resources[typeof(Button)] = buttons;
        if (owner.TryFindResource(typeof(System.Windows.Controls.Primitives.ScrollBar)) is Style scrollbars)
            Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] = scrollbars;
        Grid root = new() { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        FrameworkElement note = ProgramWindowChrome.Header(SettingsService.L("История программ", "Program history"),
            SettingsService.L("Выберите программу и нажмите «Найти остатки» или дважды щёлкните по строке. Будет выполнен новый поиск. Поиск ничего не удаляет.",
                "Select an application and choose Find leftovers or double-click its row to run a fresh search. Searching does not delete anything."));
        note.Margin = new Thickness(0, 0, 0, 12); root.Children.Add(note);
        TabControl tabs = _tabs; tabs.SetResourceReference(Control.BackgroundProperty, "CardBackgroundBrush");
        tabs.SetResourceReference(Control.ForegroundProperty, "PrimaryTextBrush");
        Style tabStyle = new(typeof(TabItem));
        tabStyle.Setters.Add(new Setter(Control.TemplateProperty, (ControlTemplate)XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="{x:Type TabItem}">
              <Border x:Name="TabBorder" Background="{TemplateBinding Background}" BorderBrush="{DynamicResource BorderBrush}" BorderThickness="1"
                      Padding="{TemplateBinding Padding}" CornerRadius="4,4,0,0">
                <ContentPresenter x:Name="TabHeader" ContentSource="Header" RecognizesAccessKey="True" TextElement.Foreground="{TemplateBinding Foreground}"/>
              </Border>
              <ControlTemplate.Triggers>
                <Trigger Property="IsSelected" Value="True">
                  <Setter TargetName="TabBorder" Property="Background" Value="{DynamicResource AccentBrush}"/>
                  <Setter TargetName="TabHeader" Property="TextElement.Foreground" Value="{DynamicResource WindowBackgroundBrush}"/>
                </Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
            """)));
        foreach (var item in new[] { (SettingsService.L("Удалённые программы", "Removed applications"), _removals),
            (SettingsService.L("Изменения программ", "Application changes"), _changes) })
        {
            TabItem tab = new() { Header = item.Item1, Content = item.Item2, Padding = new Thickness(12, 7, 12, 7), Style = tabStyle };
            tab.SetResourceReference(Control.BackgroundProperty, "CardBackgroundBrush");
            tab.SetResourceReference(Control.ForegroundProperty, "PrimaryTextBrush"); tabs.Items.Add(tab);
        }
        var filters = CreateFilters(); Grid.SetRow(filters, 1); root.Children.Add(filters);
        var tableFrame = ProgramWindowChrome.Frame(tabs, 9); Grid.SetRow(tableFrame, 2); root.Children.Add(tableFrame);
        _status.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush"); Grid.SetRow(_status, 3); root.Children.Add(_status);
        WrapPanel actions = new() { HorizontalAlignment = HorizontalAlignment.Right };
        _selected = Button("Найти остатки", "Find leftovers"); _close = Button("Закрыть", "Close");
        _selected.ToolTip = SettingsService.L("Повторно найти остатки выбранной программы и открыть результат. Ничего не удаляет.",
            "Search again for the selected application's leftovers and open the result. Does not delete anything.");
        InitializeResultInteraction();
        foreach (Button button in new[] { _selected, _close }) actions.Children.Add(button);
        var footer = ProgramWindowChrome.Footer(actions); Grid.SetRow(footer, 4); root.Children.Add(footer); Content = root;
        _selected.Click += async (_, _) => await SearchSelectedAsync();
        _close.Click += (_, _) => Close(); _close.IsCancel = true;
        Closing += (_, args) => { if (_busy) args.Cancel = true; };
        _removals.SelectionChanged += (_, _) => UpdateButtons(); tabs.SelectionChanged += (_, e) => { if (e.Source == tabs) { ConfigureResultFilter(); ApplyFilters(); } };
        Loaded += async (_, _) => await RunAsync(ReloadAsync); InitializeAutoRefresh(); UpdateButtons();
    }

    private static string DateText(DateTime utc) => utc.ToLocalTime().ToString(SettingsService.IsRussian ? "dd.MM.yyyy HH:mm" : "yyyy-MM-dd HH:mm");
    private static string Found(ProgramLeftoverReport report) => report.Found.Count > 0
        ? SettingsService.L($"Найдено объектов: {report.Found.Count}", $"Objects found: {report.Found.Count}")
        : report.Limitations.Count > 0 ? SettingsService.L("Проверка с ограничениями", "Inspection has limitations")
        : SettingsService.L("Остатки не найдены", "No leftovers found");
    private static Button Button(string ru, string en) => new() { Content = SettingsService.L(ru, en), FontSize = 14, MinHeight = 38, Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(6, 0, 0, 0) };
    private void UpdateButtons()
    {
        _selected.IsEnabled = !_busy && _removals.IsVisible && _removals.SelectedItem != null;
        _close.IsEnabled = !_busy;
        _selected.Visibility = _tabs.SelectedIndex == 1 ? Visibility.Collapsed : Visibility.Visible;
        _search.IsEnabled = _period.IsEnabled = _resultFilter.IsEnabled = _tabs.IsEnabled = !_busy;
    }
    private async Task RunAsync(Func<Task> action)
    {
        if (_busy) return; _busy = true; UpdateButtons();
        try { await action(); }
        catch (Exception ex) { InstalledProgramActionDialog.Show(this, Title, ex.Message, "OK", false); }
        finally { _busy = false; UpdateButtons(); }
    }
    private static ListView MakeList(params (string Field, string Ru, string En, int Width)[] columns)
    {
        ListView list = new(); GridView view = new();
        foreach (var c in columns) view.Columns.Add(new GridViewColumn { Header = SettingsService.L(c.Ru, c.En), Width = c.Width, DisplayMemberBinding = new Binding(c.Field) });
        list.View = view;
        ProgramListStyles.Apply(list); return list;
    }
}
