using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PCHardwareMonitor;

internal sealed partial class ProgramUninstallWindow : Window
{
    private readonly InstalledProgramInfo _app;
    private readonly IReadOnlyList<InstalledProgramInfo> _inventory;
    private readonly ProgramOperationSurface _surface = new();
    private readonly TextBlock _status = new() { FontSize = 15, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
    private readonly ProgressBar _progress = ProgramOperationSurface.BusyBar();
    private readonly CheckBox _restorePoint = new() { IsChecked = true, Margin = new Thickness(0, 16, 0, 0) };
    private readonly Button _start, _close;
    private readonly FrameworkElement _footer;
    private readonly TextBlock[] _steps;
    private InstalledProgramUninstallPlan? _plan;
    private ProgramOrphanRegistrationCandidate? _orphan;
    private ProgramLeftoverReviewView? _review;
    private ProgramLeftoverReport? _report;
    private bool _busy, _closed, _initialized;
    private int _phase;
    internal bool Attempted { get; private set; }
    internal ProgramLeftoverReport? Report => _review?.Report ?? _report;

    internal ProgramUninstallWindow(Window owner, InstalledProgramInfo app, IReadOnlyList<InstalledProgramInfo> inventory)
    {
        Owner = owner; _app = app; _inventory = inventory;
        Title = SettingsService.L("Thermiqra — Удаление программы", "Thermiqra — Uninstall application");
        Width = 980; Height = 820; MinWidth = 760; MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        FontFamily = owner.FontFamily; FontSize = 14;
        SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
        SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
        if (owner.TryFindResource(typeof(Button)) is Style buttons) Resources[typeof(Button)] = buttons;
        if (owner.TryFindResource(typeof(System.Windows.Controls.Primitives.ScrollBar)) is Style scrollbars)
            Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] = scrollbars;
        Grid root = new() { Margin = new Thickness(18) };
        foreach (GridLength height in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto,
            new GridLength(1, GridUnitType.Star), GridLength.Auto }) root.RowDefinitions.Add(new RowDefinition { Height = height });
        root.Children.Add(ProgramWindowChrome.Header(SettingsService.L("Удаление программы", "Uninstall application"), app.Name));
        WrapPanel steps = new() { Margin = new Thickness(0, 12, 0, 0) };
        _steps = new[] { SettingsService.L("1 · Подтверждение", "1 · Confirm"), SettingsService.L("2 · Точка восстановления", "2 · Restore point"),
            SettingsService.L("3 · Удаление", "3 · Uninstall"), SettingsService.L("4 · Остатки", "4 · Leftovers"), SettingsService.L("5 · Результат", "5 · Result") }
            .SelectLabels(steps);
        Grid.SetRow(steps, 1); root.Children.Add(steps);
        _status.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryTextBrush"); Grid.SetRow(_status, 2); root.Children.Add(_status);
        Grid.SetRow(_progress, 3); root.Children.Add(_progress);
        _surface.Margin = new Thickness(0, 8, 0, 0); Grid.SetRow(_surface, 4); root.Children.Add(_surface);
        WrapPanel actions = new() { HorizontalAlignment = HorizontalAlignment.Right };
        _close = MakeButton(SettingsService.L("Отмена", "Cancel")); _start = MakeButton(SettingsService.L("Удалить", "Uninstall"));
        _close.IsCancel = true;
        _close.Click += (_, _) => Close(); _start.Click += async (_, _) =>
        { if (_orphan != null) await RemoveOrphanAsync(); else await StartAsync(); };
        actions.Children.Add(_close); actions.Children.Add(_start);
        _footer = ProgramWindowChrome.Footer(actions); Grid.SetRow(_footer, 5); root.Children.Add(_footer); Content = root;
        Closing += (_, args) =>
        {
            if (_busy || _review?.IsWorking == true) { args.Cancel = true; return; }
            _surface.CancelDecision(); _review?.CancelDecision();
        };
        Closed += (_, _) => _closed = true;
        ContentRendered += (_, _) => WindowPlacementService.ConstrainToWorkArea(this);
        Loaded += async (_, _) => { if (!_initialized) { _initialized = true; await PrepareAsync(); } };
    }

    private static Button MakeButton(string label) => new() { Content = label, FontSize = 14, MinHeight = 38,
        Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(8, 0, 0, 0) };

    private static TextBlock Message(string text, double size = 14)
    {
        TextBlock block = new() { Text = text, FontSize = size, LineHeight = size * 1.5,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12) };
        block.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryTextBrush"); return block;
    }

    private void Stage(int phase, string text, bool working)
    {
        _phase = phase; _status.Text = text; _progress.Visibility = working ? Visibility.Visible : Visibility.Collapsed;
        for (int i = 0; i < _steps.Length; i++)
        {
            _steps[i].FontWeight = i == phase ? FontWeights.Bold : FontWeights.Normal;
            _steps[i].SetResourceReference(TextBlock.ForegroundProperty, i <= phase ? "AccentBrush" : "SecondaryTextBrush");
        }
    }

    private void Working(int phase, string status, string explanation)
    {
        Stage(phase, status, true); _start.Visibility = Visibility.Collapsed; _close.IsEnabled = false;
        _footer.Visibility = Visibility.Visible;
        _surface.Present(Message(explanation, 16));
    }

    private void Finish(string status, string details)
    {
        Stage(4, status, false);
        _surface.Present(new ScrollViewer { Content = Message(details), VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        _footer.Visibility = Visibility.Visible; _start.Visibility = Visibility.Collapsed;
        _close.Content = SettingsService.L("Закрыть", "Close"); _close.IsEnabled = true;
    }
}

internal static class ProgramOperationStepLabels
{
    internal static TextBlock[] SelectLabels(this string[] labels, Panel parent)
    {
        TextBlock[] result = new TextBlock[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            result[i] = new TextBlock { Text = labels[i], FontSize = 13, Margin = new Thickness(0, 0, 18, 6) };
            parent.Children.Add(result[i]);
        }
        return result;
    }
}
