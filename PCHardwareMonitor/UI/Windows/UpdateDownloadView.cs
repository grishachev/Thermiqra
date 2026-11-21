using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace PCHardwareMonitor;

internal sealed class UpdateDownloadView
{
    internal Window Window { get; }
    internal Button CancelButton { get; }
    internal TextBlock Status { get; } = new() { FontSize = 14, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    internal TextBlock Amount { get; } = new() { FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 6), TextWrapping = TextWrapping.Wrap };
    internal TextBlock Speed { get; } = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    internal ProgressBar Bar { get; } = new() { Height = 16, Minimum = 0, Maximum = 100, Margin = new Thickness(0, 15, 0, 0) };

    internal UpdateDownloadView(Window? owner)
    {
        Window = new Window
        {
            Owner = owner, Title = SettingsService.L("Thermiqra — Обновление", "Thermiqra — Update"),
            Width = 480, Height = 276, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
            WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner
        };
        Window.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(
            new Uri("/Thermiqra;component/UI/Appearance/ProgramInputStyles.xaml", UriKind.Relative)));
        Window.SetResourceReference(System.Windows.Window.BackgroundProperty, "WindowBackgroundBrush");
        var root = new Grid { Margin = new Thickness(22) };
        for (int i = 0; i < 6; i++) root.RowDefinitions.Add(new RowDefinition { Height = i == 4 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
        Status.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryTextBrush");
        Amount.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryTextBrush");
        Speed.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
        Bar.SetResourceReference(Control.BackgroundProperty, "InputBackgroundBrush");
        Bar.SetResourceReference(Control.ForegroundProperty, "AccentBrush");
        Bar.SetResourceReference(FrameworkElement.StyleProperty, "ThermiqraTransferProgressStyle");
        UIElement[] content = { Status, Bar, Amount, Speed };
        for (int i = 0; i < content.Length; i++) { Grid.SetRow(content[i], i); root.Children.Add(content[i]); }
        CancelButton = new Button { Content = SettingsService.L("Отмена", "Cancel"), MinWidth = 100, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        CancelButton.SetResourceReference(FrameworkElement.StyleProperty, "ThermiqraButtonStyle");
        Grid.SetRow(CancelButton, 5); root.Children.Add(CancelButton);
        Window.Content = root;
        Update(new(UpdateDownloadStage.Preparing, 0, null, 0, TimeSpan.Zero));
    }

    internal void Update(UpdateDownloadProgress value)
    {
        bool waiting = value.IdleFor.TotalSeconds >= 5;
        int idle = (int)Math.Min(int.MaxValue, value.IdleFor.TotalSeconds);
        Status.Text = value.Stage switch
        {
            UpdateDownloadStage.Preparing => waiting ? SettingsService.L($"Ожидание ответа сервера… {idle} с", $"Waiting for the server… {idle} s") : SettingsService.L("Подготовка к скачиванию…", "Preparing download…"),
            UpdateDownloadStage.Downloading => waiting ? SettingsService.L($"Ожидание данных от сервера… {idle} с", $"Waiting for data from the server… {idle} s") : SettingsService.L("Скачивание обновления…", "Downloading update…"),
            UpdateDownloadStage.Verifying => SettingsService.L("Проверка файла и подписи…", "Verifying file and signature…"),
            _ => SettingsService.L("Файл проверен. Подготовка установки…", "File verified. Preparing installation…")
        };
        var culture = CultureInfo.GetCultureInfo(SettingsService.IsRussian ? "ru-RU" : "en-US");
        string Mb(long bytes) => (bytes / 1048576d).ToString("N1", culture);
        Amount.Text = value.TotalBytes is > 0
            ? SettingsService.L($"{Mb(value.ReceivedBytes)} из {Mb(value.TotalBytes.Value)} МБ", $"{Mb(value.ReceivedBytes)} of {Mb(value.TotalBytes.Value)} MB")
            : SettingsService.L($"Скачано: {Mb(value.ReceivedBytes)} МБ", $"Downloaded: {Mb(value.ReceivedBytes)} MB");
        if (value.Percent is int percent) Amount.Text += $" · {percent}%";
        double speed = double.IsFinite(value.BytesPerSecond) ? Math.Max(0, value.BytesPerSecond) : 0;
        string rate = speed >= 1048576 ? (speed / 1048576).ToString("N1", culture) + SettingsService.L(" МБ/с", " MB/s")
            : speed >= 1024 ? (speed / 1024).ToString("N1", culture) + SettingsService.L(" КБ/с", " KB/s")
            : speed.ToString("N0", culture) + SettingsService.L(" Б/с", " B/s");
        Speed.Text = value.Stage is UpdateDownloadStage.Preparing or UpdateDownloadStage.Downloading
            ? SettingsService.L($"Скорость: {rate}", $"Speed: {rate}")
            : SettingsService.L("Загрузка завершена. Выполняется проверка и подготовка.", "Download finished. Verification and preparation are in progress.");
        Bar.IsIndeterminate = value.Stage is UpdateDownloadStage.Preparing or UpdateDownloadStage.Verifying || value.Percent == null;
        Bar.Value = value.Percent ?? 0;
    }
    internal void StartingInstallation()
    {
        Status.Text = SettingsService.L("Запуск установки…", "Starting installation…");
        Speed.Text = SettingsService.L("Файл проверен. Настройки и статистика сохранятся.", "File verified. Your settings and statistics will be preserved.");
        Bar.IsIndeterminate = false; Bar.Value = 100; CancelButton.IsEnabled = false;
    }
}
