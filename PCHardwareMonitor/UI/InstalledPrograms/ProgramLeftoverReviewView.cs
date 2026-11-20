using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PCHardwareMonitor;

internal sealed partial class ProgramLeftoverReviewView : UserControl
{
    private ProgramLeftoverReport _report;
    private readonly Window _owner;
    private readonly bool _embedded;
    private readonly ProgramOperationSurface _surface = new();
    internal ProgramLeftoverReport Report => _report;
    internal bool IsWorking => _cleanupBusy && !_surface.HasDecision;
    internal void CancelDecision() => _surface.CancelDecision();
    internal event Action<bool>? ActivityChanged;
    internal event Action? CleanupFinished;
    private readonly StackPanel _content = new();
    private readonly StackPanel _emptyState = new() { VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 560, Margin = new Thickness(24) };
    private readonly TextBlock _counts = Text("", 15, "PrimaryTextBrush", true);
    private readonly TextBlock _auditDetails = Text("", 14, "SecondaryTextBrush");

    internal ProgramLeftoverReviewView(Window owner, ProgramLeftoverReport report, bool embedded = false)
    {
        _report = report; _owner = owner; _embedded = embedded;
        FontFamily = owner.FontFamily; FontSize = 14;
        HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch;
        Grid root = new() { Margin = new Thickness(embedded ? 0 : 18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        ContentControl watermark = new() { IsHitTestVisible = false, Focusable = false, ClipToBounds = true,
            HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        watermark.SetResourceReference(ContentControl.ContentTemplateProperty, "SkinWatermarkTemplate");
        Grid.SetRowSpan(watermark, 3); root.Children.Add(watermark);
        root.Children.Add(CreateCompactHeader());
        _emptyState.Children.Add(Text(SettingsService.L("Остатки не найдены", "No leftovers found"), 22, "PrimaryTextBrush", true));
        _emptyState.Children.Add(Text(SettingsService.L("Проверка завершена. В проверенных расположениях ничего не осталось.",
            "Inspection complete. Nothing remains in the checked locations."), 15, "SecondaryTextBrush"));
        foreach (TextBlock text in _emptyState.Children) text.TextAlignment = TextAlignment.Center;
        Grid.SetRow(_emptyState, 1); root.Children.Add(_emptyState);
        RenderItems();
        ScrollViewer scroll = new() { Content = _content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); root.Children.Add(scroll);
        var actions = ProgramWindowChrome.Footer(CreateActions()); Grid.SetRow(actions, 2); root.Children.Add(actions);
        _surface.Content = root; Content = _surface; InitializeExistenceRefresh();
    }

    private UIElement CreateCompactHeader()
    {
        StackPanel header = new() { Margin = new Thickness(0, 0, 0, 10) };
        if (!_embedded) header.Children.Add(ProgramWindowChrome.Header(_report.Baseline.Name, SettingsService.L("Остатки программы", "Application leftovers")));
        if (_report.Baseline.NativeResult is { } native && (native.State != InstalledProgramRemovalState.Removed || native.HelperObservationIncomplete))
            header.Children.Add(Text(ProgramRemovalOutcome.Summary(native), 14, "AccentBrush"));
        bool package = _report.Baseline.Identity.StartsWith("msix:", StringComparison.OrdinalIgnoreCase);
        if (package && _report.Baseline.NativeResult is { State: InstalledProgramRemovalState.Removed,
            RegistrationAbsent: true, HelperObservationIncomplete: false })
            header.Children.Add(Text(SettingsService.L("Приложение удалено для текущего пользователя.",
                "The application was removed for the current user."), 15, "PrimaryTextBrush", true));
        header.Children.Add(_counts);
        header.Children.Add(Text(package ? SettingsService.L(
            "Папками пакета управляет Windows. Данные вне пакета проверяются отдельно; выбранные папки очищаются вместе с настройками. Ручное удаление из WindowsApps запрещено.",
            "Windows manages package folders. Data outside the package is inspected separately; selected folders are cleaned including their settings. Manual deletion from WindowsApps is blocked.") :
            SettingsService.L("Очистка включает выбранные настройки и данные программы.",
            "Cleanup includes the selected application settings and data."), 14, "SecondaryTextBrush"));
        header.Children.Add(ProgramWindowChrome.Details(SettingsService.L("Сведения о проверке", "Inspection details"), _auditDetails));
        return header;
    }

    private void RenderItems()
    {
        _content.Children.Clear(); _selections.Clear();
        bool empty = _report.Found.Count == 0 && _report.Limitations.Count == 0;
        _emptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        _auditDetails.Text = SettingsService.L($"Проверено: {_report.CheckedUtc.ToLocalTime():dd.MM.yyyy HH:mm:ss}. Расположений: {_report.CheckedObjects}.",
            $"Checked: {_report.CheckedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}. Locations: {_report.CheckedObjects}.");
        if (_report.RecordedObjects is { } audit)
            _auditDetails.Text += SettingsService.L($"\nСнимок: файлов осталось {audit.FilesPresent}, отсутствует {audit.FilesAbsent}, не проверено {audit.FilesUnchecked}; разделов реестра осталось {audit.RegistryPresent}, не проверено {audit.RegistryUnchecked}.",
                $"\nRecorded inventory: files present {audit.FilesPresent}, absent {audit.FilesAbsent}, unchecked {audit.FilesUnchecked}; registry keys present {audit.RegistryPresent}, unchecked {audit.RegistryUnchecked}.");
        if (_report.Baseline.Memory is { } memory)
            _auditDetails.Text += SettingsService.L($"\nПеред удалением: файлов {memory.Files}, разделов реестра {memory.RegistryKeys}." +
                (memory.Complete ? "" : " Снимок ограничен."), $"\nBefore uninstall: files {memory.Files}, registry keys {memory.RegistryKeys}." +
                (memory.Complete ? "" : " Snapshot is limited."));
        if (_report.Baseline.RestorePoint is { } point) _auditDetails.Text += "\n" + ProgramRestorePointService.Summary(point);
        foreach (var group in new[] { ProgramLeftoverGroup.Related, ProgramLeftoverGroup.UserData, ProgramLeftoverGroup.NeedsReview, ProgramLeftoverGroup.Protected })
            RenderGroup(group);
        if (_report.Limitations.Count > 0)
        {
            _content.Children.Add(Text(SettingsService.L($"Есть ограничения проверки: {_report.Limitations.Count}.",
                $"Inspection limitations: {_report.Limitations.Count}."), 14, "AccentBrush", true));
            _content.Children.Add(ProgramWindowChrome.Details(SettingsService.L("Показать ограничения", "Show limitations"),
                Text(string.Join("\n", _report.Limitations.Select(l => l.DisplayText)), 14, "SecondaryTextBrush")));
        }
        UpdateActions();
    }

    private static TextBlock Text(string value, double size, string brush, bool bold = false)
    {
        TextBlock text = new() { Text = value, FontSize = Math.Max(14, size), LineHeight = Math.Max(21, size * 1.4), TextWrapping = TextWrapping.Wrap,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Margin = new Thickness(0, 4, 0, 0) };
        text.SetResourceReference(TextBlock.ForegroundProperty, brush); return text;
    }
}
