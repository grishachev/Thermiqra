using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace PCHardwareMonitor;

internal sealed class ProgramRecoveryWindow : Window
{
    private sealed record Row(ProgramCleanupBackup Backup, ProgramRecoveryContents Contents, string Name, string Date, string Size)
    {
        public string Status => Contents.Summary;
        public string Details => Contents.Details;
    }
    private readonly ListView _list = new();
    private readonly TextBlock _empty;
    private readonly ComboBox _days = new() { MinWidth = 150 };
    private readonly Button _restore, _delete, _bin, _protection, _apply, _close;
    private readonly string? _reviewId;
    private bool _busy;

    internal ProgramRecoveryWindow(Window owner, string? reviewId)
    {
        Owner = owner; _reviewId = reviewId;
        Title = SettingsService.L("Thermiqra — Восстановление", "Thermiqra — Recovery");
        Width = 1020; Height = 660; MinWidth = 800; MinHeight = 520; FontSize = 14;
        FontFamily = owner.FontFamily; ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ContentRendered += (_, _) => WindowPlacementService.ConstrainToWorkArea(this);
        SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
        SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
        if (owner.TryFindResource(typeof(Button)) is Style buttons) Resources[typeof(Button)] = buttons;
        if (owner.TryFindResource(typeof(System.Windows.Controls.Primitives.ScrollBar)) is Style scrollbars)
            Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] = scrollbars;
        Grid root = new() { Margin = new Thickness(18) };
        foreach (GridLength height in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto })
            root.RowDefinitions.Add(new RowDefinition { Height = height });
        root.Children.Add(ProgramWindowChrome.Header(SettingsService.L("Восстановление", "Recovery"), SettingsService.L(
            "Файлы — в Корзине; копии реестра — в списке ниже. Точки восстановления Windows доступны через «Защита системы».",
            "Files are in Recycle Bin; registry backups are listed below. Windows restore points are available through System Protection.")));
        WrapPanel policy = new() { Margin = new Thickness(0, 12, 0, 12), VerticalAlignment = VerticalAlignment.Center };
        policy.Children.Add(Label(SettingsService.L("Хранить копии реестра: ", "Keep registry backups: ")));
        foreach (int days in new[] { 7, 30, 90, 0 })
            _days.Items.Add(new ComboBoxItem { Tag = days, Content = days == 0 ? SettingsService.L("Без ограничения", "No expiry") : SettingsService.L(days + " дней", days + " days") });
        _days.SelectedItem = _days.Items.Cast<ComboBoxItem>().First(i => (int)i.Tag == ProgramCleanupRecoveryService.RetentionDays);
        _days.SetResourceReference(Control.ForegroundProperty, "PrimaryTextBrush");
        _days.SetResourceReference(Control.BackgroundProperty, "CardBackgroundBrush");
        ProgramRecoveryStyles.Apply(_days);
        policy.Children.Add(_days);
        _apply = Button(SettingsService.L("Применить", "Apply")); policy.Children.Add(_apply);
        Grid.SetRow(policy, 1); root.Children.Add(policy);
        GridView view = new();
        foreach (var column in new[] { ("Name", SettingsService.L("Программа", "Application"), 260d),
            ("Date", SettingsService.L("Дата", "Date"), 145d), ("Size", SettingsService.L("Размер копии", "Backup size"), 110d),
            ("Status", SettingsService.L("Содержимое", "Contents"), 285d) })
            view.Columns.Add(new GridViewColumn { Header = column.Item2, Width = column.Item3, DisplayMemberBinding = new Binding(column.Item1) });
        _list.View = view;
        ProgramListStyles.Apply(_list, "Details");
        var tableFrame = ProgramWindowChrome.Frame(_list, 9); Grid.SetRow(tableFrame, 2); root.Children.Add(tableFrame);
        _list.Tag = tableFrame;
        _empty = Label(SettingsService.L("Сохранённых копий для восстановления нет. Удалённые файлы доступны через «Открыть Корзину», пока Корзина не очищена.",
            "No saved backups are available to restore. Deleted files are available through Open Recycle Bin until the bin is emptied."));
        _empty.Margin = new Thickness(16); _empty.Visibility = Visibility.Collapsed;
        _empty.VerticalAlignment = VerticalAlignment.Center; _empty.HorizontalAlignment = HorizontalAlignment.Center; _empty.MaxWidth = 620; _empty.FontSize = 16; _empty.TextAlignment = TextAlignment.Center; Grid.SetRow(_empty, 2); root.Children.Add(_empty);
        StackPanel footer = new();
        footer.Children.Add(Label(SettingsService.L(
            "Истёкшие подтверждённые копии реестра удаляются при открытии окна программ. Содержимое Корзины и старые резервные папки этим сроком не удаляются.",
            "Expired confirmed registry backups are removed when the programs window opens. This policy does not delete Recycle Bin contents or legacy backup folders.")));
        WrapPanel actions = new() { HorizontalAlignment = HorizontalAlignment.Right };
        _restore = Button(SettingsService.L("Восстановить", "Restore"));
        _delete = Button(SettingsService.L("Удалить копию", "Delete backup"));
        _bin = Button(SettingsService.L("Открыть Корзину", "Open Recycle Bin"));
        _protection = Button(SettingsService.L("Защита системы", "System Protection"));
        _close = Button(SettingsService.L("Закрыть", "Close")); _close.IsCancel = true;
        foreach (Button button in new[] { _restore, _delete, _bin, _protection, _close }) actions.Children.Add(button);
        footer.Children.Add(ProgramWindowChrome.Footer(actions)); Grid.SetRow(footer, 3); root.Children.Add(footer); Content = root;
        _close.Click += (_, _) => Close();
        _protection.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(System.IO.Path.Combine(Environment.SystemDirectory,
                "SystemPropertiesProtection.exe")) { UseShellExecute = true }); }
            catch (Exception ex) { Error(ex); }
        };
        _bin.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("shell:RecycleBinFolder") { UseShellExecute = true }); }
            catch (Exception ex) { Error(ex); }
        };
        _list.SelectionChanged += (_, _) => UpdateButtons();
        _restore.Click += async (_, _) => await RunAsync(RestoreSelectedAsync);
        _delete.Click += async (_, _) => await RunAsync(DeleteSelectedAsync);
        _apply.Click += async (_, _) => await RunAsync(ApplyPolicyAsync);
        Loaded += async (_, _) => await RunAsync(ReloadAsync);
        Closing += (_, args) => { if (_busy) args.Cancel = true; };
        UpdateButtons();
    }

    private static Button Button(string text) => new() { Content = text, FontSize = 14, MinHeight = 38, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(8, 8, 0, 0) };
    private static TextBlock Label(string text)
    {
        TextBlock label = new() { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 14, LineHeight = 21, Margin = new Thickness(0, 4, 0, 4), VerticalAlignment = VerticalAlignment.Center };
        label.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush"); return label;
    }
    private void Error(Exception ex) => InstalledProgramActionDialog.Show(this, Title, ex.Message, "OK", false);
    private void UpdateButtons()
    {
        Row? row = _list.SelectedItem as Row;
        _restore.IsEnabled = !_busy && row != null && row.Contents.CanRestore;
        _delete.IsEnabled = !_busy && row != null;
        _delete.Content = SettingsService.L("Удалить копию", "Delete backup");
    }
    private async Task RunAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true; _list.IsEnabled = _days.IsEnabled = _close.IsEnabled = _apply.IsEnabled = _bin.IsEnabled = _protection.IsEnabled = false; UpdateButtons();
        try { await action(); }
        catch (Exception ex) { Error(ex); }
        finally { _busy = false; _list.IsEnabled = _days.IsEnabled = _close.IsEnabled = _apply.IsEnabled = _bin.IsEnabled = _protection.IsEnabled = true; UpdateButtons(); }
    }
    private async Task ReloadAsync()
    {
        var backups = await ProgramCleanupRecoveryService.LoadAsync();
        var rows = await Task.Run(() => backups.Where(b => _reviewId == null || b.ReviewId == _reviewId)
            .Select(b => (Backup: b, Contents: ProgramRecoveryInventory.Inspect(b)))
            .Where(item => item.Contents.Visible)
            .Select(item => new Row(item.Backup, item.Contents, item.Backup.Name,
                item.Backup.CreatedUtc.ToLocalTime().ToString(SettingsService.IsRussian ? "dd.MM.yyyy HH:mm" : "yyyy-MM-dd HH:mm"),
                ProgramRecoveryInventory.Size(item.Backup, item.Contents))).ToList());
        _list.ItemsSource = rows;
        _empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _list.Visibility = rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (_list.Tag is FrameworkElement tableFrame) tableFrame.Visibility = _list.Visibility;
        _restore.Visibility = _delete.Visibility = rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }
    private async Task RestoreSelectedAsync()
    {
        if (_list.SelectedItem is not Row row) return;
        if (!InstalledProgramActionDialog.Show(this, Title, row.Name + "\n\n" + SettingsService.L(
            "Восстановить сохранённые остатки? Существующие объекты не перезаписываются. Файлы, отправленные в Корзину, восстанавливаются из Корзины Windows.",
            "Restore saved leftovers? Existing objects will not be overwritten. Recycled files are restored from Windows Recycle Bin."), SettingsService.L("Восстановить", "Restore"), true)) return;
        try { ProgramCleanupPresentation.ShowResult(this, await ProgramLeftoverCleanupService.RestoreAsync(row.Backup)); }
        finally { await ReloadAsync(); }
    }
    private async Task DeleteSelectedAsync()
    {
        if (_list.SelectedItem is not Row row) return;
        if (!InstalledProgramActionDialog.Show(this, Title, row.Name + "\n\n" + SettingsService.L(
            "Удалить эту копию? Восстановление реестра из неё станет недоступно. Ранее сохранённые папки будут отправлены в Корзину. Уже удалённая программа не изменится.",
            "Delete this backup? Its registry backup will no longer be available to restore. Legacy backup folders will go to Recycle Bin. The uninstalled application will not be changed."), SettingsService.L("Удалить", "Delete"), true)) return;
        try
        {
            var result = await ProgramCleanupRecoveryService.DiscardAsync(row.Backup);
            if (result.Entries.Any(e => e.State != "Discarded")) ProgramCleanupPresentation.ShowResult(this, result);
        }
        finally { await ReloadAsync(); }
    }
    private async Task ApplyPolicyAsync()
    {
        int days = (int)((ComboBoxItem)_days.SelectedItem).Tag;
        if (days > 0 && !InstalledProgramActionDialog.Show(this, Title, SettingsService.L(
            $"Хранить копии реестра {days} дней? Подтверждённые копии старше этого срока будут удалены сейчас. Файлы в Корзине и старые резервные папки это не затрагивает.",
            $"Keep registry backups for {days} days? Confirmed backups older than this period will be removed now. Recycle Bin files and legacy backup folders are unaffected."), SettingsService.L("Применить", "Apply"), true)) return;
        SettingsService.Current.ProgramRegistryBackupDays = days; SettingsService.Save(); await ReloadAsync();
    }
}
