using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCHardwareMonitor;

public partial class MainWindow : Window
{
    private readonly HardwareMonitorService _monitor;

    private readonly DispatcherTimer _timer;

    private readonly DispatcherTimer _updateCheckTimer;

    private readonly TrayService _tray;

    private readonly NotificationService _notifications;

    private readonly AlertService _alerts;

    private readonly StatisticsService? _statistics;

    private readonly RingGauge _cpuGauge;

    private CpuDetailsWindow? _cpuDetailsWindow;

    private readonly List<RingGauge>
        _gpuGauges =
            new();

    private HardwareSnapshot?
        _lastSnapshot;

    private bool _isExiting;

    private bool _servicesDisposed;

    private bool _livePulseBright = true;

    private bool _isUpdateCheckRunning;

    private bool _isUpdateInstallRunning;

    private UpdateInfo? _pendingUpdate;

    private string? _notifiedUpdateVersion;

    private static readonly string SettingsDirectory =
        System.IO.Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "Thermiqra");

    private static readonly string WindowSettingsPath =
        System.IO.Path.Combine(
            SettingsDirectory,
            "window.json");

    public MainWindow()
    {
        InitializeComponent();

        SettingsService.ApplyLanguageToWindow(
            this);

        RestoreWindowPlacement();

        _monitor =
            new HardwareMonitorService();

        try
        {
            _statistics =
                new StatisticsService();
        }
        catch (Exception ex)
        {
            _statistics = null;

            System.Diagnostics.Debug.WriteLine(
                $"Не удалось запустить статистику: {ex}");
        }

        _tray =
            new TrayService(
                openAction:
                    ShowMainWindow,

                settingsAction:
                    () =>
                        OpenSettings(false),

                exitAction:
                    ExitApplication);

        _notifications =
            new NotificationService();

        _alerts =
            new AlertService(
                _notifications,
                ShowMainWindow);

        _cpuGauge =
            new RingGauge(
                "CPU",
                TemperatureType.Cpu);

        MainGaugesPanel
            .Children
            .Add(
                _cpuGauge);

        _cpuGauge.CpuThreadsRequested +=
            CpuGauge_CpuThreadsRequested;

        SettingsService.ApplyLanguageToElement(
            _cpuGauge);

        _timer =
            new DispatcherTimer
            {
                Interval =
                    TimeSpan.FromSeconds(1)
            };

        _timer.Tick +=
            Timer_Tick;

        _updateCheckTimer =
            new DispatcherTimer
            {
                Interval =
                    TimeSpan.FromHours(12)
            };

        _updateCheckTimer.Tick +=
            UpdateCheckTimer_Tick;

        SizeChanged +=
            MainWindow_SizeChanged;

        StateChanged +=
            MainWindow_StateChanged;

        Closing +=
            MainWindow_Closing;
    }

    public void StartMonitoring()
    {
        UpdateMonitor();

        _timer.Start();

        _updateCheckTimer.Start();

        _ = CheckForUpdatesAsync();
    }

    public void ShowMainWindow()
    {
        if (!IsVisible)
            Show();

        if (WindowState ==
            WindowState.Minimized)
        {
            WindowState =
                WindowState.Normal;
        }

        Activate();

        Topmost = true;
        Topmost = false;

        Focus();

        if (_lastSnapshot != null)
        {
            Dispatcher.BeginInvoke(
                () =>
                {
                    UpdateDrives(
                        _lastSnapshot);

                    UpdateStorageTemperatures(
                        _lastSnapshot);
                });
        }

        TryShowPendingUpdate();
    }

    public void RefreshLanguage()
    {
        SettingsService.ApplyLanguageToWindow(
            this);

        _tray.RefreshLanguage();

        _cpuDetailsWindow?
            .RefreshLanguage();

        if (_lastSnapshot != null)
        {
            UpdateMemory(
                _lastSnapshot);

            UpdateDrives(
                _lastSnapshot);

            UpdateStorageTemperatures(
                _lastSnapshot);
        }
    }


    public void ResetAlertStatesAfterSettingsChange()
    {
        _alerts.ResetStates();
    }


    public void OpenSettings(
        bool firstRun)
    {
        ShowMainWindow();

        SettingsWindow window =
            new(firstRun)
            {
                Owner = this
            };

        window.ShowDialog();

        TryShowPendingUpdate();

        if (window.Saved &&
            _lastSnapshot != null)
        {
            UpdateDrives(
                _lastSnapshot);

            UpdateStorageTemperatures(
                _lastSnapshot);
        }
    }

    private void SettingsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        OpenSettings(false);
    }

    private void StatisticsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_statistics == null)
        {
            MessageBox.Show(
                this,
                SettingsService.L(
                    "Сервис статистики сейчас недоступен.",
                    "Statistics service is currently unavailable."),
                SettingsService.L(
                    "Thermiqra — Статистика",
                    "Thermiqra — Statistics"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        StatisticsWindow window =
            new(
                _statistics,
                _lastSnapshot)
            {
                Owner = this
            };

        window.ShowDialog();
    }

    private void CpuGauge_CpuThreadsRequested(
        object? sender,
        EventArgs e)
    {
        if (_cpuDetailsWindow != null)
        {
            if (!_cpuDetailsWindow.IsVisible)
            {
                _cpuDetailsWindow.Show();
            }

            if (_cpuDetailsWindow.WindowState ==
                WindowState.Minimized)
            {
                _cpuDetailsWindow.WindowState =
                    WindowState.Normal;
            }

            _cpuDetailsWindow.Activate();

            _cpuDetailsWindow.Topmost =
                true;

            _cpuDetailsWindow.Topmost =
                false;

            return;
        }

        CpuDetailsWindow window =
            new()
            {
                Owner =
                    this
            };

        _cpuDetailsWindow =
            window;

        window.Closed +=
            (_, _) =>
            {
                if (ReferenceEquals(
                        _cpuDetailsWindow,
                        window))
                {
                    _cpuDetailsWindow =
                        null;
                }
            };

        if (_lastSnapshot != null)
        {
            window.UpdateSnapshot(
                _lastSnapshot);
        }

        window.Show();
        window.Activate();
    }

    private void SystemInfoButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SystemInfoWindow window =
            new()
            {
                Owner = this
            };

        window.ShowDialog();
    }

    private void Timer_Tick(
        object? sender,
        EventArgs e)
    {
        UpdateMonitor();
    }

    private async void UpdateCheckTimer_Tick(
        object? sender,
        EventArgs e)
    {
        await CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_isUpdateCheckRunning ||
            _isExiting)
        {
            return;
        }

        _isUpdateCheckRunning = true;

        try
        {
            UpdateInfo? update =
                await UpdateService
                    .CheckForUpdateAsync();

            if (update == null)
                return;

            if (string.Equals(
                    _notifiedUpdateVersion,
                    update.VersionText,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _notifiedUpdateVersion =
                update.VersionText;

            _pendingUpdate =
                update;

            if (IsVisible &&
                IsActive &&
                WindowState != WindowState.Minimized)
            {
                TryShowPendingUpdate();
            }
            else
            {
                _notifications.Show(
                    NotificationType.Info,
                    SettingsService.L(
                        "Доступно обновление Thermiqra",
                        "Thermiqra update available"),
                    SettingsService.L(
                        $"Доступна версия {update.VersionText}. Нажмите, чтобы открыть Thermiqra и установить обновление.",
                        $"Version {update.VersionText} is available. Click to open Thermiqra and install the update."),
                    ShowMainWindow);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Не удалось проверить обновления Thermiqra: {ex}");
        }
        finally
        {
            _isUpdateCheckRunning = false;
        }
    }

    private async void TryShowPendingUpdate()
    {
        if (_pendingUpdate == null ||
            !IsVisible ||
            !IsActive ||
            WindowState == WindowState.Minimized ||
            _isUpdateInstallRunning)
        {
            return;
        }

        UpdateInfo update =
            _pendingUpdate;

        _pendingUpdate = null;

        if (!update.HasInstaller)
        {
            MessageBoxResult fallbackResult =
                MessageBox.Show(
                    this,
                    SettingsService.L(
                        $"Доступна Thermiqra {update.VersionText}, но автоматический установщик этого релиза не найден.\n\nОткрыть страницу релиза GitHub?",
                        $"Thermiqra {update.VersionText} is available, but the automatic installer for this release was not found.\n\nOpen the GitHub release page?"),
                    SettingsService.L(
                        "Thermiqra — Обновление",
                        "Thermiqra — Update"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (fallbackResult ==
                MessageBoxResult.Yes)
            {
                OpenReleasePage(
                    update);
            }

            return;
        }

        if (!ShowUpdatePrompt(
                update))
        {
            return;
        }

        await InstallUpdateAsync(
            update);
    }

    private bool ShowUpdatePrompt(
        UpdateInfo update)
    {
        string description =
            SettingsService.L(
                $"Доступна новая версия Thermiqra {update.VersionText}.",
                $"A new version of Thermiqra {update.VersionText} is available.");

        Window dialog =
            new()
            {
                Owner = this,
                Title =
                    SettingsService.L(
                        "Thermiqra — Обновление",
                        "Thermiqra — Update"),
                Width = 560,
                SizeToContent =
                    SizeToContent.Height,
                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner,
                ResizeMode =
                    ResizeMode.NoResize,
                ShowInTaskbar = false
            };

        dialog.SetResourceReference(
            Window.BackgroundProperty,
            "WindowBackgroundBrush");

        Grid root =
            new()
            {
                Margin =
                    new Thickness(
                        22)
            };

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        TextBlock title =
            new()
            {
                Text =
                    SettingsService.L(
                        $"Доступна Thermiqra {update.VersionText}",
                        $"Thermiqra {update.VersionText} is available"),
                FontSize = 20,
                FontWeight =
                    FontWeights.Bold,
                TextWrapping =
                    TextWrapping.Wrap
            };

        title.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        Grid.SetRow(
            title,
            0);

        root.Children.Add(
            title);

        TextBlock version =
            new()
            {
                Text =
                    SettingsService.L(
                        $"Текущая версия: {update.CurrentVersionText}",
                        $"Current version: {update.CurrentVersionText}"),
                Margin =
                    new Thickness(
                        0,
                        7,
                        0,
                        0),
                FontSize = 12
            };

        version.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        Grid.SetRow(
            version,
            1);

        root.Children.Add(
            version);

        TextBlock details =
            new()
            {
                Text =
                    description +
                    Environment.NewLine +
                    Environment.NewLine +
                    SettingsService.L(
                        "Нажмите «Обновить» — Thermiqra автоматически скачает установщик, проверит его целостность и установит новую версию. После установки Thermiqra запустится снова. Настройки и статистика будут сохранены.",
                        "Click “Update” — Thermiqra will automatically download the installer, verify its integrity, and install the new version. Thermiqra will start again after installation. Your settings and statistics will be preserved."),
                Margin =
                    new Thickness(
                        0,
                        18,
                        0,
                        20),
                TextWrapping =
                    TextWrapping.Wrap,
                LineHeight = 19
            };

        details.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        Grid.SetRow(
            details,
            2);

        root.Children.Add(
            details);

        StackPanel buttons =
            new()
            {
                Orientation =
                    Orientation.Horizontal,
                HorizontalAlignment =
                    HorizontalAlignment.Right
            };

        Button laterButton =
            new()
            {
                Content =
                    SettingsService.L(
                        "Позже",
                        "Later"),
                MinWidth = 100,
                Padding =
                    new Thickness(
                        16,
                        8,
                        16,
                        8),
                Margin =
                    new Thickness(
                        0,
                        0,
                        10,
                        0),
                IsCancel = true
            };

        laterButton.SetResourceReference(
            Control.BackgroundProperty,
            "InputBackgroundBrush");

        laterButton.SetResourceReference(
            Control.ForegroundProperty,
            "PrimaryTextBrush");

        laterButton.SetResourceReference(
            Control.BorderBrushProperty,
            "BorderBrush");

        Button updateButton =
            new()
            {
                Content =
                    SettingsService.L(
                        "Обновить",
                        "Update"),
                MinWidth = 110,
                Padding =
                    new Thickness(
                        16,
                        8,
                        16,
                        8),
                IsDefault = true
            };

        updateButton.SetResourceReference(
            Control.BackgroundProperty,
            "InputBackgroundBrush");

        updateButton.SetResourceReference(
            Control.ForegroundProperty,
            "PrimaryTextBrush");

        updateButton.SetResourceReference(
            Control.BorderBrushProperty,
            "AccentBrush");

        laterButton.Click +=
            (_, _) =>
            {
                dialog.DialogResult =
                    false;
            };

        updateButton.Click +=
            (_, _) =>
            {
                dialog.DialogResult =
                    true;
            };

        buttons.Children.Add(
            laterButton);

        buttons.Children.Add(
            updateButton);

        Grid.SetRow(
            buttons,
            3);

        root.Children.Add(
            buttons);

        dialog.Content =
            root;

        return dialog.ShowDialog() ==
               true;
    }

    private async Task InstallUpdateAsync(
        UpdateInfo update)
    {
        if (_isUpdateInstallRunning)
            return;

        _isUpdateInstallRunning =
            true;

        Window? progressWindow =
            null;

        bool installerStarted =
            false;

        try
        {
            progressWindow =
                CreateUpdateProgressWindow(
                    out ProgressBar progressBar,
                    out TextBlock statusText);

            progressWindow.Show();
            progressWindow.Activate();

            IsEnabled =
                false;

            Progress<int> progress =
                new(
                    percent =>
                    {
                        progressBar.Value =
                            percent;

                        statusText.Text =
                            SettingsService.L(
                                $"Скачивание обновления… {percent}%",
                                $"Downloading update… {percent}%");
                    });

            string installerPath =
                await UpdateService
                    .DownloadInstallerAsync(
                        update,
                        progress);

            statusText.Text =
                SettingsService.L(
                    "Запуск установки…",
                    "Starting installation…");

            progressBar.Value =
                100;

            await Task.Delay(
                250);

            UpdateService.StartInstaller(
                installerPath);

            installerStarted =
                true;

            progressWindow.Close();
            progressWindow = null;

            IsEnabled =
                true;

            ExitApplication();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Не удалось установить обновление Thermiqra: {ex}");

            if (progressWindow != null)
            {
                progressWindow.Close();
                progressWindow = null;
            }

            IsEnabled =
                true;

            MessageBox.Show(
                this,
                SettingsService.L(
                    $"Не удалось автоматически установить обновление.\n\n{ex.Message}",
                    $"The update could not be installed automatically.\n\n{ex.Message}"),
                SettingsService.L(
                    "Thermiqra — Обновление",
                    "Thermiqra — Update"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            if (!installerStarted &&
                IsVisible)
            {
                IsEnabled =
                    true;
            }

            if (progressWindow != null)
            {
                progressWindow.Close();
            }

            _isUpdateInstallRunning =
                false;
        }
    }

    private Window CreateUpdateProgressWindow(
        out ProgressBar progressBar,
        out TextBlock statusText)
    {
        Window window =
            new()
            {
                Owner = this,
                Title =
                    SettingsService.L(
                        "Thermiqra — Обновление",
                        "Thermiqra — Update"),
                Width = 460,
                Height = 155,
                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner,
                ResizeMode =
                    ResizeMode.NoResize,
                ShowInTaskbar = false
            };

        window.SetResourceReference(
            Window.BackgroundProperty,
            "WindowBackgroundBrush");

        StackPanel panel =
            new()
            {
                Margin =
                    new Thickness(
                        22)
            };

        statusText =
            new TextBlock
            {
                Text =
                    SettingsService.L(
                        "Подготовка к скачиванию…",
                        "Preparing download…"),
                FontSize = 14,
                FontWeight =
                    FontWeights.SemiBold,
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        14)
            };

        statusText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        progressBar =
            new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Height = 18
            };

        progressBar.SetResourceReference(
            Control.BackgroundProperty,
            "InputBackgroundBrush");

        progressBar.SetResourceReference(
            Control.ForegroundProperty,
            "AccentBrush");

        panel.Children.Add(
            statusText);

        panel.Children.Add(
            progressBar);

        window.Content =
            panel;

        return window;
    }

    private void OpenReleasePage(
        UpdateInfo update)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName =
                        update.ReleaseUrl,

                    UseShellExecute =
                        true
                });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Не удалось открыть страницу релиза: {ex}");

            MessageBox.Show(
                this,
                SettingsService.L(
                    "Не удалось открыть страницу релиза в браузере.",
                    "Failed to open the release page in the browser."),
                SettingsService.L(
                    "Thermiqra — Обновление",
                    "Thermiqra — Update"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }    private void MainWindow_StateChanged(
        object? sender,
        EventArgs e)
    {
        if (WindowState !=
            WindowState.Minimized)
        {
            return;
        }

        Hide();

        WindowState =
            WindowState.Normal;
    }

    private void MainWindow_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        if (_lastSnapshot == null)
            return;

        UpdateDrives(
            _lastSnapshot);

        UpdateStorageTemperatures(
            _lastSnapshot);
    }

    private void MainWindow_Closing(
        object? sender,
        CancelEventArgs e)
    {
        SaveWindowPlacement();

        if (_isExiting)
            return;

        if (SettingsService
                .Current
                .MinimizeToTrayOnClose)
        {
            e.Cancel =
                true;

            Hide();

            return;
        }

        _isExiting =
            true;

        DisposeServices();

        Application.Current.Shutdown();
    }

    public void ExitApplication()
    {
        if (_isExiting)
            return;

        _isExiting =
            true;

        SaveWindowPlacement();

        DisposeServices();

        Close();

        Application.Current.Shutdown();
    }

    private void DisposeServices()
    {
        if (_servicesDisposed)
            return;

        _servicesDisposed =
            true;

        _timer.Stop();

        _updateCheckTimer.Stop();

        _notifications.Dispose();

        _tray.Dispose();

        _monitor.Dispose();
    }

    // ============================================================
    // МОНИТОРИНГ
    // ============================================================

    private void UpdateMonitor()
    {
        try
        {
            HardwareSnapshot snapshot =
                _monitor.GetSnapshot();

            _lastSnapshot =
                snapshot;

            _cpuDetailsWindow?
                .UpdateSnapshot(
                    snapshot);

            try
            {
                _statistics?.SaveSnapshot(
                    snapshot);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Не удалось сохранить статистику: {ex}");
            }

            UpdateCpu(snapshot);

            UpdateGpus(snapshot);

            UpdateMemory(snapshot);

            UpdateDrives(snapshot);

            UpdateStorageTemperatures(
                snapshot);

            _alerts.Evaluate(
                snapshot);

            string statusText =
                SettingsService.L(
                    "Обновлено: ",
                    "Updated: ") +
                $"{DateTime.Now:HH:mm:ss}";

            StatusText.Text =
                statusText;

            SteamStatusText.Text =
                statusText;

            FrostStatusText.Text =
                statusText;

            MilitaryStatusText.Text =
                statusText;

            _livePulseBright =
                !_livePulseBright;

            double pulseOpacity =
                _livePulseBright
                    ? 1.0
                    : 0.35;

            LivePulseDot.Opacity =
                pulseOpacity;

            SteamLivePulseDot.Opacity =
                pulseOpacity;

            FrostLivePulseDot.Opacity =
                pulseOpacity;

            MilitaryLivePulseDot.Opacity =
                pulseOpacity;
        }
        catch (Exception ex)
        {
            string errorText =
                SettingsService.L(
                    $"Ошибка: {ex.Message}",
                    $"Error: {ex.Message}");

            StatusText.Text =
                errorText;

            SteamStatusText.Text =
                errorText;

            FrostStatusText.Text =
                errorText;

            MilitaryStatusText.Text =
                errorText;
        }
    }

    // ============================================================
    // CPU
    // ============================================================

    private void UpdateCpu(
        HardwareSnapshot snapshot)
    {
        _cpuGauge.Update(
            snapshot.Cpu.Temperature,
            snapshot.Cpu.Load,
            snapshot.Cpu.Name,
            "");
    }

    // ============================================================
    // GPU
    // ============================================================

    private void UpdateGpus(
        HardwareSnapshot snapshot)
    {
        EnsureGpuGauges(
            snapshot.Gpus.Count);

        for (int i = 0;
             i < snapshot.Gpus.Count;
             i++)
        {
            GpuInfo gpu =
                snapshot.Gpus[i];

            _gpuGauges[i]
                .Update(
                    gpu.Temperature,
                    gpu.Load,
                    gpu.Name,
                    BuildGpuDetails(gpu));
        }
    }

    private void EnsureGpuGauges(
        int requiredCount)
    {
        while (_gpuGauges.Count <
               requiredCount)
        {
            RingGauge gauge =
                new(
                    "GPU",
                    TemperatureType.Gpu);

            _gpuGauges.Add(
                gauge);

            MainGaugesPanel
                .Children
                .Add(
                    gauge);

            SettingsService.ApplyLanguageToElement(
                gauge);
        }

        while (_gpuGauges.Count >
               requiredCount)
        {
            RingGauge gauge =
                _gpuGauges[
                    _gpuGauges.Count - 1];

            MainGaugesPanel
                .Children
                .Remove(
                    gauge);

            _gpuGauges.RemoveAt(
                _gpuGauges.Count - 1);
        }
    }

    private static string BuildGpuDetails(
        GpuInfo gpu)
    {
        List<string> details =
            new();

        if (gpu.HotSpotTemperature.HasValue)
        {
            details.Add(
                $"Hot Spot " +
                $"{gpu.HotSpotTemperature.Value:F0}°");
        }

        if (gpu.MemoryTemperature.HasValue)
        {
            details.Add(
                $"VRAM " +
                $"{gpu.MemoryTemperature.Value:F0}°");
        }

        return string.Join(
            "   •   ",
            details);
    }

    // ============================================================
    // RAM
    // ============================================================

    private void UpdateMemory(
        HardwareSnapshot snapshot)
    {
        float? used =
            snapshot.Memory.UsedGb;

        float? total =
            snapshot.Memory.TotalGb;

        string memoryText;

        if (used.HasValue &&
            total.HasValue)
        {
            memoryText =
                SettingsService.L(
                    $"{used.Value:F1} ГБ ",
                    $"{used.Value:F1} GB ") +
                SettingsService.L(
                    $"из {total.Value:F1} ГБ",
                    $"of {total.Value:F1} GB");
        }
        else
        {
            memoryText =
                SettingsService.L(
                    "Нет данных",
                    "No data");
        }

        RamText.Text =
            memoryText;

        SteamRamText.Text =
            memoryText;

        FrostRamText.Text =
            memoryText;

        MilitaryRamText.Text =
            memoryText;

        double ramLoad =
            ClampPercent(
                snapshot.Memory.Load);

        RamBar.Value =
            ramLoad;

        SteamRamBar.Value =
            ramLoad;

        FrostRamBar.Value =
            ramLoad;

        Brush ramBrush;

        if (ramLoad >= 90)
        {
            ramBrush =
                System.Windows.Media.Brushes.Red;
        }
        else if (ramLoad >= 85)
        {
            ramBrush =
                System.Windows.Media.Brushes.Orange;
        }
        else if (ramLoad >= 70)
        {
            ramBrush =
                System.Windows.Media.Brushes.Gold;
        }
        else
        {
            ramBrush =
                UiBrushes.Theme(
                    "AccentBrush");
        }

        RamBar.Foreground =
            ramBrush;

        SteamRamBar.Foreground =
            ramBrush;

        FrostRamBar.Foreground =
            ramBrush;

        FrostRamFill.Background =
            ramBrush;

        FrostRamFill.Height =
            78.0 * ramLoad / 100.0;

        Border[] militaryRamSegments =
        {
            MilitaryRamSegment0,
            MilitaryRamSegment1,
            MilitaryRamSegment2,
            MilitaryRamSegment3,
            MilitaryRamSegment4,
            MilitaryRamSegment5,
            MilitaryRamSegment6,
            MilitaryRamSegment7,
            MilitaryRamSegment8,
            MilitaryRamSegment9
        };

        int activeMilitarySegments =
            ramLoad <= 0
                ? 0
                : Math.Clamp(
                    (int)Math.Ceiling(
                        ramLoad / 10.0),
                    0,
                    10);

        for (int i = 0;
             i < militaryRamSegments.Length;
             i++)
        {
            if (i < activeMilitarySegments)
            {
                militaryRamSegments[i].Background =
                    ramBrush;
            }
            else
            {
                militaryRamSegments[i]
                    .SetResourceReference(
                        Border.BackgroundProperty,
                        "MilitaryGridBrush");
            }
        }

        RamPercentText.Text =
            SettingsService.L(
                "Использовано: ",
                "Used: ") +
            $"{FormatPercent(snapshot.Memory.Load)}";

        SteamRamPercentText.Text =
            $"{ramLoad:F0} %";

        FrostRamPercentText.Text =
            $"{ramLoad:F0} %";

        MilitaryRamPercentText.Text =
            $"{ramLoad:F0} %";
    }

    // ============================================================
    // ДИСКИ
    // ============================================================

    private void UpdateDrives(
        HardwareSnapshot snapshot)
    {
        PrepareResponsiveGrid(
            DrivesPanel,
            snapshot.Drives.Count,
            out int columns);

        for (int i = 0;
             i < snapshot.Drives.Count;
             i++)
        {
            LogicalDriveInfo drive =
                snapshot.Drives[i];

            int row =
                i / columns;

            int column =
                i % columns;

            UIElement card =
                CreateDriveCard(
                    drive,
                    column,
                    columns);

            Grid.SetRow(
                card,
                row);

            Grid.SetColumn(
                card,
                column);

            DrivesPanel.Children.Add(
                card);
        }
    }

    private UIElement CreateDriveCard(
    LogicalDriveInfo drive,
    int column,
    int columns)
    {
        string skinName =
            SettingsService
                .Current
                .SkinName;

        if (skinName == "SteamPunk")
        {
            return CreateSteamDriveCard(
                drive,
                column,
                columns);
        }

        if (skinName == "FrostCore")
        {
            return CreateFrostDriveCard(
                drive,
                column,
                columns);
        }

        if (skinName == "MilitaryOps")
        {
            return CreateMilitaryDriveCard(
                drive,
                column,
                columns);
        }

        Border border =
            CreateGridCardBorder(
                column,
                columns);

        string label =
            string.IsNullOrWhiteSpace(
                drive.VolumeLabel)

                ? drive.Name

                : $"{drive.Name} {drive.VolumeLabel}";

        double totalGb =
            BytesToGb(
                drive.TotalBytes);

        double freeGb =
            BytesToGb(
                drive.FreeBytes);

        double usedPercent =
            0;

        if (drive.TotalBytes > 0)
        {
            usedPercent =
                100.0 -
                (
                    drive.FreeBytes /
                    (double)drive.TotalBytes *
                    100.0
                );
        }

        Brush statusBrush =
            UiBrushes.DiskUsage(
                usedPercent);


        Grid root =
            new();

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    new GridLength(3)
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });


        Border topAccent =
            new()
            {
                Width = 72,
                Height = 3,

                HorizontalAlignment =
                    HorizontalAlignment.Left,

                Margin =
                    new Thickness(
                        14, 0, 0, 0)
            };

        topAccent.SetResourceReference(
            Border.BackgroundProperty,
            "AccentBrush");


        Grid.SetRow(
            topAccent,
            0);

        root.Children.Add(
            topAccent);


        Grid body =
            new()
            {
                Margin =
                    new Thickness(
                        15, 12, 15, 13)
            };

        body.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(46)
            });

        body.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        body.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });


        // ========================================================
        // ИКОНКА НАКОПИТЕЛЯ
        // ========================================================

        Grid driveIcon =
            new()
            {
                Width = 34,
                Height = 38,

                VerticalAlignment =
                    VerticalAlignment.Center,

                HorizontalAlignment =
                    HorizontalAlignment.Left
            };


        Border iconOutline =
            new()
            {
                Width = 30,
                Height = 34,

                BorderThickness =
                    new Thickness(1),

                CornerRadius =
                    new CornerRadius(3),

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        iconOutline.SetResourceReference(
            Border.BorderBrushProperty,
            "AccentBrush");


        Border iconSlot =
            new()
            {
                Width = 16,
                Height = 2,

                VerticalAlignment =
                    VerticalAlignment.Top,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                Margin =
                    new Thickness(
                        0, 8, 0, 0)
            };

        iconSlot.SetResourceReference(
            Border.BackgroundProperty,
            "AccentBrush");


        TextBlock iconText =
            new()
            {
                Text =
                    drive.Name
                        .TrimEnd('\\'),

                FontSize = 8,

                FontWeight =
                    FontWeights.Bold,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Bottom,

                Margin =
                    new Thickness(
                        0, 0, 0, 6)
            };

        iconText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");


        driveIcon.Children.Add(
            iconOutline);

        driveIcon.Children.Add(
            iconSlot);

        driveIcon.Children.Add(
            iconText);


        // ========================================================
        // ОСНОВНАЯ ИНФОРМАЦИЯ
        // ========================================================

        Grid information =
            new()
            {
                Margin =
                    new Thickness(
                        3, 0, 12, 0)
            };

        information.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        information.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        information.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        information.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });


        TextBlock moduleLabel =
            new()
            {
                Text = "STORAGE VOLUME",

                FontSize = 8,

                FontWeight =
                    FontWeights.SemiBold,

                Margin =
                    new Thickness(
                        0, 0, 0, 2)
            };

        moduleLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");


        TextBlock name =
            new()
            {
                Text = label,

                FontSize = 16,

                FontWeight =
                    FontWeights.Bold,

                TextTrimming =
                    TextTrimming.CharacterEllipsis,

                Margin =
                    new Thickness(
                        0, 0, 0, 7)
            };

        name.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");


        TextBlock free =
            new()
            {
                Text =
                    SettingsService.L(
                    "Свободно: ",
                    "Free: ") +
                    $"{FormatSize(freeGb)} " +
                    SettingsService.L(
                    $"из {FormatSize(totalGb)}",
                    $"of {FormatSize(totalGb)}"),

                FontSize = 12,

                Margin =
                    new Thickness(
                        0, 0, 0, 7)
            };

        free.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");


        ProgressBar bar =
            new()
            {
                Minimum = 0,
                Maximum = 100,

                Value =
                    Math.Clamp(
                        usedPercent,
                        0,
                        100),

                Height = 7,

                Foreground =
                    statusBrush
            };


        Grid.SetRow(
            moduleLabel,
            0);

        Grid.SetRow(
            name,
            1);

        Grid.SetRow(
            free,
            2);

        Grid.SetRow(
            bar,
            3);


        information.Children.Add(
            moduleLabel);

        information.Children.Add(
            name);

        information.Children.Add(
            free);

        information.Children.Add(
            bar);


        // ========================================================
        // ПРОЦЕНТ ЗАПОЛНЕНИЯ
        // ========================================================

        StackPanel usagePanel =
            new()
            {
                VerticalAlignment =
                    VerticalAlignment.Center,

                HorizontalAlignment =
                    HorizontalAlignment.Right
            };


        TextBlock usageLabel =
            new()
            {
                Text = SettingsService.L(
                "ЗАНЯТО",
                "USED"),

                FontSize = 8,

                FontWeight =
                    FontWeights.SemiBold,

                HorizontalAlignment =
                    HorizontalAlignment.Right
            };

        usageLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");


        TextBlock usageValue =
            new()
            {
                Text =
                    $"{usedPercent:F0} %",

                FontSize = 18,

                FontWeight =
                    FontWeights.Bold,

                Foreground =
                    statusBrush,

                HorizontalAlignment =
                    HorizontalAlignment.Right,

                Margin =
                    new Thickness(
                        0, 3, 0, 0)
            };


        Border statusLine =
            new()
            {
                Width = 30,
                Height = 2,

                Background =
                    statusBrush,

                HorizontalAlignment =
                    HorizontalAlignment.Right,

                Margin =
                    new Thickness(
                        0, 8, 0, 0)
            };


        usagePanel.Children.Add(
            usageLabel);

        usagePanel.Children.Add(
            usageValue);

        usagePanel.Children.Add(
            statusLine);


        Grid.SetColumn(
            driveIcon,
            0);

        Grid.SetColumn(
            information,
            1);

        Grid.SetColumn(
            usagePanel,
            2);


        body.Children.Add(
            driveIcon);

        body.Children.Add(
            information);

        body.Children.Add(
            usagePanel);


        Grid.SetRow(
            body,
            1);

        root.Children.Add(
            body);


        border.Child =
            root;

        return border;
    }

    private UIElement CreateSteamDriveCard(
        LogicalDriveInfo drive,
        int column,
        int columns)
    {
        string label =
            string.IsNullOrWhiteSpace(
                drive.VolumeLabel)

                ? drive.Name

                : $"{drive.Name} {drive.VolumeLabel}";

        double totalGb =
            BytesToGb(
                drive.TotalBytes);

        double freeGb =
            BytesToGb(
                drive.FreeBytes);

        double usedPercent =
            0;

        if (drive.TotalBytes > 0)
        {
            usedPercent =
                100.0 -
                (
                    drive.FreeBytes /
                    (double)drive.TotalBytes *
                    100.0
                );
        }

        Brush statusBrush =
            UiBrushes.DiskUsage(
                usedPercent);

        Border outer =
            CreateSteamGridCardBorder(
                column,
                columns);

        Grid root = new()
        {
            Height = 116
        };

        // Центральный корпус — не карточка, а горизонтальный механический модуль.
        Border body = new()
        {
            Margin =
                new Thickness(
                    38, 8, 38, 8),

            Padding =
                new Thickness(
                    54, 10, 66, 10),

            BorderThickness =
                new Thickness(2),

            CornerRadius =
                new CornerRadius(24)
        };

        body.SetResourceReference(
            Border.BackgroundProperty,
            "SteamPanelBrush");

        body.SetResourceReference(
            Border.BorderBrushProperty,
            "SteamMetalBrush");

        StackPanel information = new()
        {
            VerticalAlignment =
                VerticalAlignment.Center
        };

        TextBlock name = new()
        {
            Text = label,
            FontSize = 15,
            FontWeight =
                FontWeights.Bold,
            TextTrimming =
                TextTrimming.CharacterEllipsis
        };

        name.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        TextBlock free = new()
        {
            Text =
                SettingsService.L(
                    "Свободно: ",
                    "Free: ") +
                $"{FormatSize(freeGb)} " +
                SettingsService.L(
                    $"из {FormatSize(totalGb)}",
                    $"of {FormatSize(totalGb)}"),

            FontSize = 10,

            Margin =
                new Thickness(
                    0, 3, 0, 7)
        };

        free.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        Grid fillRow = new();

        fillRow.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        fillRow.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        ProgressBar fillBar = new()
        {
            Minimum = 0,
            Maximum = 100,
            Value =
                Math.Clamp(
                    usedPercent,
                    0,
                    100),
            Height = 22,
            Foreground =
                statusBrush,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        if (TryFindResource(
                "SteamPipeProgressStyle")
            is Style pipeStyle)
        {
            fillBar.Style =
                pipeStyle;
        }

        TextBlock fillText = new()
        {
            Text =
                SettingsService.L(
                $"Занято {usedPercent:F0} %",
                $"Used {usedPercent:F0} %"),
            Foreground =
                statusBrush,
            FontSize = 10,
            FontWeight =
                FontWeights.Bold,
            VerticalAlignment =
                VerticalAlignment.Center,
            Margin =
                new Thickness(
                    10, 0, 0, 0)
        };

        Grid.SetColumn(
            fillBar,
            0);

        Grid.SetColumn(
            fillText,
            1);

        fillRow.Children.Add(
            fillBar);

        fillRow.Children.Add(
            fillText);

        information.Children.Add(
            name);

        information.Children.Add(
            free);

        information.Children.Add(
            fillRow);

        body.Child =
            information;

        root.Children.Add(
            body);

        // Левый барабан с буквой диска.
        Grid drum = new()
        {
            Width = 82,
            Height = 82,
            HorizontalAlignment =
                HorizontalAlignment.Left,
            VerticalAlignment =
                VerticalAlignment.Center,
            Margin =
                new Thickness(
                    5, 0, 0, 0)
        };

        System.Windows.Shapes.Ellipse drumOuter =
            new()
            {
                StrokeThickness = 4
            };

        drumOuter.SetResourceReference(
            System.Windows.Shapes.Shape.FillProperty,
            "SteamGlassBrush");

        drumOuter.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "SteamMetalBrush");

        System.Windows.Shapes.Ellipse drumMiddle =
            new()
            {
                Width = 60,
                Height = 60,
                StrokeThickness = 2
            };

        drumMiddle.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "BorderBrush");

        Border drumVertical =
            new()
            {
                Width = 3,
                Height = 46,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        drumVertical.SetResourceReference(
            Border.BackgroundProperty,
            "SteamMetalBrush");

        Border drumHorizontal =
            new()
            {
                Width = 46,
                Height = 3,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        drumHorizontal.SetResourceReference(
            Border.BackgroundProperty,
            "SteamMetalBrush");

        System.Windows.Shapes.Ellipse drumHub =
            new()
            {
                Width = 35,
                Height = 35,
                StrokeThickness = 2
            };

        drumHub.SetResourceReference(
            System.Windows.Shapes.Shape.FillProperty,
            "SteamDarkMetalBrush");

        drumHub.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "AccentBrush");

        TextBlock driveLetter =
            new()
            {
                Text =
                    drive.Name
                        .TrimEnd('\\'),

                FontSize = 11,

                FontWeight =
                    FontWeights.Bold,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        driveLetter.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        drum.Children.Add(
            drumOuter);

        drum.Children.Add(
            drumMiddle);

        drum.Children.Add(
            drumVertical);

        drum.Children.Add(
            drumHorizontal);

        drum.Children.Add(
            drumHub);

        drum.Children.Add(
            driveLetter);

        root.Children.Add(
            drum);

        // Правый круглый счётчик.
        Grid meter = new()
        {
            Width = 74,
            Height = 74,
            HorizontalAlignment =
                HorizontalAlignment.Right,
            VerticalAlignment =
                VerticalAlignment.Center,
            Margin =
                new Thickness(
                    0, 0, 7, 0)
        };

        System.Windows.Shapes.Ellipse meterOuter =
            new()
            {
                StrokeThickness = 4
            };

        meterOuter.SetResourceReference(
            System.Windows.Shapes.Shape.FillProperty,
            "SteamGlassBrush");

        meterOuter.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "SteamMetalBrush");

        System.Windows.Shapes.Ellipse meterInner =
            new()
            {
                Width = 58,
                Height = 58,
                StrokeThickness = 1
            };

        meterInner.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "BorderBrush");

        StackPanel meterText = new()
        {
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        TextBlock meterLabel =
            new()
            {
                Text = SettingsService.L(
                "ЗАНЯТО",
                "USED"),
                FontSize = 7,
                FontWeight =
                    FontWeights.Bold,
                HorizontalAlignment =
                    HorizontalAlignment.Center
            };

        meterLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        TextBlock usage =
            new()
            {
                Text =
                    $"{usedPercent:F0}%",

                FontSize = 18,
                FontWeight =
                    FontWeights.Bold,
                Foreground =
                    statusBrush,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                Margin =
                    new Thickness(
                        0, 1, 0, 0)
            };

        meterText.Children.Add(
            meterLabel);

        meterText.Children.Add(
            usage);

        meter.Children.Add(
            meterOuter);

        meter.Children.Add(
            meterInner);

        meter.Children.Add(
            meterText);

        root.Children.Add(
            meter);

        outer.Child =
            root;

        return outer;
    }


    private UIElement CreateFrostDriveCard(
        LogicalDriveInfo drive,
        int column,
        int columns)
    {
        string label =
            string.IsNullOrWhiteSpace(
                drive.VolumeLabel)
                ? drive.Name
                : $"{drive.Name} {drive.VolumeLabel}";

        double totalGb =
            BytesToGb(
                drive.TotalBytes);

        double freeGb =
            BytesToGb(
                drive.FreeBytes);

        double usedPercent = 0;

        if (drive.TotalBytes > 0)
        {
            usedPercent =
                100.0 -
                (
                    drive.FreeBytes /
                    (double)drive.TotalBytes *
                    100.0
                );
        }

        Brush statusBrush =
            UiBrushes.DiskUsage(
                usedPercent);

        Border outer =
            CreateFrostGridCardBorder(
                column,
                columns);

        Border body = new()
        {
            Height = 116,
            Padding = new Thickness(12),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(18)
        };

        body.SetResourceReference(
            Border.BackgroundProperty,
            "FrostPanelBrush");

        body.SetResourceReference(
            Border.BorderBrushProperty,
            "FrostSteelBrush");

        Grid layout = new();

        layout.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(86)
            });

        layout.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        layout.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(94)
            });

        // Герметичная криокассета.
        Grid capsule = new()
        {
            Width = 68,
            Height = 84,
            HorizontalAlignment =
                HorizontalAlignment.Left,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        Border capsuleOuter = new()
        {
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(22)
        };

        capsuleOuter.SetResourceReference(
            Border.BackgroundProperty,
            "FrostGlassBrush");

        capsuleOuter.SetResourceReference(
            Border.BorderBrushProperty,
            "FrostSteelBrush");

        Border capsuleInner = new()
        {
            Margin = new Thickness(9),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(15)
        };

        capsuleInner.SetResourceReference(
            Border.BorderBrushProperty,
            "FrostIceBrush");

        TextBlock frostMark = new()
        {
            Text = "❄",
            FontSize = 20,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Top,
            Margin = new Thickness(0, 11, 0, 0),
            Opacity = 0.72
        };

        frostMark.SetResourceReference(
            TextBlock.ForegroundProperty,
            "FrostIceBrush");

        Border divider = new()
        {
            Width = 32,
            Height = 1,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        divider.SetResourceReference(
            Border.BackgroundProperty,
            "FrostSteelBrush");

        TextBlock driveLetter = new()
        {
            Text = drive.Name.TrimEnd('\\'),
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 12)
        };

        driveLetter.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        capsule.Children.Add(capsuleOuter);
        capsule.Children.Add(capsuleInner);
        capsule.Children.Add(frostMark);
        capsule.Children.Add(divider);
        capsule.Children.Add(driveLetter);

        StackPanel information = new()
        {
            VerticalAlignment =
                VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0)
        };

        TextBlock caption = new()
        {
            Text = SettingsService.L(
                "КРИОМОДУЛЬ ХРАНЕНИЯ",
                "CRYO STORAGE MODULE"),
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        caption.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        TextBlock name = new()
        {
            Text = label,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            TextTrimming =
                TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 2, 0, 3)
        };

        name.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        TextBlock free = new()
        {
            Text =
                SettingsService.L(
                    "Свободно: ",
                    "Free: ") +
                $"{FormatSize(freeGb)} " +
                SettingsService.L(
                    $"из {FormatSize(totalGb)}",
                    $"of {FormatSize(totalGb)}"),
            FontSize = 10,
            Margin = new Thickness(0, 0, 0, 7)
        };

        free.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        ProgressBar fillBar = new()
        {
            Minimum = 0,
            Maximum = 100,
            Value = Math.Clamp(
                usedPercent,
                0,
                100),
            Height = 16,
            Foreground = statusBrush
        };

        if (TryFindResource(
                "FrostCapsuleProgressStyle")
            is Style frostProgressStyle)
        {
            fillBar.Style =
                frostProgressStyle;
        }

        information.Children.Add(caption);
        information.Children.Add(name);
        information.Children.Add(free);
        information.Children.Add(fillBar);

        Border meter = new()
        {
            Padding = new Thickness(10),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(15),
            VerticalAlignment =
                VerticalAlignment.Center
        };

        meter.SetResourceReference(
            Border.BackgroundProperty,
            "FrostGlassBrush");

        meter.SetResourceReference(
            Border.BorderBrushProperty,
            "FrostSteelBrush");

        StackPanel meterStack = new();

        TextBlock meterLabel = new()
        {
            Text = SettingsService.L(
                "ЗАНЯТО",
                "USED"),
            FontSize = 7,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center
        };

        meterLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        TextBlock usage = new()
        {
            Text = $"{usedPercent:F0}%",
            FontSize = 21,
            FontWeight = FontWeights.Bold,
            Foreground = statusBrush,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 2)
        };

        TextBlock levelLabel = new()
        {
            Text = SettingsService.L(
                "КРИОУРОВЕНЬ",
                "CRYO LEVEL"),
            FontSize = 7,
            HorizontalAlignment =
                HorizontalAlignment.Center
        };

        levelLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        meterStack.Children.Add(meterLabel);
        meterStack.Children.Add(usage);
        meterStack.Children.Add(levelLabel);
        meter.Child = meterStack;

        Grid.SetColumn(capsule, 0);
        Grid.SetColumn(information, 1);
        Grid.SetColumn(meter, 2);

        layout.Children.Add(capsule);
        layout.Children.Add(information);
        layout.Children.Add(meter);
        body.Child = layout;
        outer.Child = body;

        return outer;
    }


    private UIElement CreateMilitaryDriveCard(
        LogicalDriveInfo drive,
        int column,
        int columns)
    {
        string label =
            string.IsNullOrWhiteSpace(
                drive.VolumeLabel)
                ? drive.Name
                : $"{drive.Name} {drive.VolumeLabel}";

        double totalGb =
            BytesToGb(
                drive.TotalBytes);

        double freeGb =
            BytesToGb(
                drive.FreeBytes);

        double usedPercent = 0;

        if (drive.TotalBytes > 0)
        {
            usedPercent =
                100.0 -
                (
                    drive.FreeBytes /
                    (double)drive.TotalBytes *
                    100.0
                );
        }

        Brush statusBrush =
            UiBrushes.DiskUsage(
                usedPercent);

        Border outer =
            CreateMilitaryGridCardBorder(
                column,
                columns);

        Grid root = new()
        {
            Height = 108
        };

        Border body = new()
        {
            Padding = new Thickness(12, 10, 12, 10),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(1)
        };

        body.SetResourceReference(
            Border.BackgroundProperty,
            "MilitaryPanelBrush");

        body.SetResourceReference(
            Border.BorderBrushProperty,
            "MilitaryKhakiBrush");

        Grid layout = new();

        layout.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(82)
            });

        layout.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        layout.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(96)
            });

        // Компас / маркировка контейнера.
        Grid compass = new()
        {
            Width = 62,
            Height = 62,
            HorizontalAlignment =
                HorizontalAlignment.Left,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        System.Windows.Shapes.Ellipse compassOuter =
            new()
            {
                StrokeThickness = 2
            };

        compassOuter.SetResourceReference(
            System.Windows.Shapes.Shape.FillProperty,
            "MilitaryPlateBrush");

        compassOuter.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "MilitaryKhakiBrush");

        Border compassVertical = new()
        {
            Width = 1,
            Height = 44,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        compassVertical.SetResourceReference(
            Border.BackgroundProperty,
            "MilitaryGridBrush");

        Border compassHorizontal = new()
        {
            Width = 44,
            Height = 1,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        compassHorizontal.SetResourceReference(
            Border.BackgroundProperty,
            "MilitaryGridBrush");

        TextBlock north = new()
        {
            Text = "N",
            FontSize = 8,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 0, 0)
        };

        north.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        Border centerPlate = new()
        {
            Width = 30,
            Height = 23,
            BorderThickness = new Thickness(1),
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        centerPlate.SetResourceReference(
            Border.BackgroundProperty,
            "MilitaryPanelBrush");

        centerPlate.SetResourceReference(
            Border.BorderBrushProperty,
            "MilitaryKhakiBrush");

        TextBlock driveLetter = new()
        {
            Text = drive.Name.TrimEnd('\\'),
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        driveLetter.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        centerPlate.Child = driveLetter;
        compass.Children.Add(compassOuter);
        compass.Children.Add(compassVertical);
        compass.Children.Add(compassHorizontal);
        compass.Children.Add(north);
        compass.Children.Add(centerPlate);

        Grid information = new()
        {
            Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment =
                VerticalAlignment.Center
        };

        information.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });
        information.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });
        information.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });
        information.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        TextBlock caption = new()
        {
            Text = SettingsService.L(
                "КОНТЕЙНЕР ДАННЫХ / СЕКТОР",
                "DATA CONTAINER / SECTOR"),
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        caption.SetResourceReference(
            TextBlock.ForegroundProperty,
            "MilitaryKhakiBrush");

        TextBlock name = new()
        {
            Text = label,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            TextTrimming =
                TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 2, 0, 2)
        };

        name.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        TextBlock free = new()
        {
            Text =
                SettingsService.L(
                    "Свободно: ",
                    "Free: ") +
                $"{FormatSize(freeGb)} " +
                SettingsService.L(
                    $"из {FormatSize(totalGb)}",
                    $"of {FormatSize(totalGb)}"),
            FontSize = 9,
            Margin = new Thickness(0, 0, 0, 6)
        };

        free.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        Grid segments = new()
        {
            Height = 14
        };

        int activeSegments =
            usedPercent <= 0
                ? 0
                : Math.Clamp(
                    (int)Math.Ceiling(
                        usedPercent / 10.0),
                    0,
                    10);

        for (int i = 0;
             i < 10;
             i++)
        {
            segments.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = new GridLength(
                        1,
                        GridUnitType.Star)
                });

            Border segment = new()
            {
                Margin = new Thickness(
                    0,
                    0,
                    i == 9 ? 0 : 3,
                    0),
                BorderThickness = new Thickness(1),
                Background =
                    i < activeSegments
                        ? statusBrush
                        : UiBrushes.Theme(
                            "MilitaryGridBrush")
            };

            segment.SetResourceReference(
                Border.BorderBrushProperty,
                "MilitaryKhakiBrush");

            Grid.SetColumn(
                segment,
                i);

            segments.Children.Add(
                segment);
        }

        Grid.SetRow(caption, 0);
        Grid.SetRow(name, 1);
        Grid.SetRow(free, 2);
        Grid.SetRow(segments, 3);

        information.Children.Add(caption);
        information.Children.Add(name);
        information.Children.Add(free);
        information.Children.Add(segments);

        Border meter = new()
        {
            Padding = new Thickness(9),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(1),
            VerticalAlignment =
                VerticalAlignment.Center
        };

        meter.SetResourceReference(
            Border.BackgroundProperty,
            "MilitaryPlateBrush");

        meter.SetResourceReference(
            Border.BorderBrushProperty,
            "MilitaryKhakiBrush");

        StackPanel meterStack = new();

        TextBlock meterLabel = new()
        {
            Text = SettingsService.L(
                "ЗАНЯТО",
                "USED"),
            FontSize = 7,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center
        };

        meterLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "MilitaryKhakiBrush");

        TextBlock usage = new()
        {
            Text = $"{usedPercent:F0}%",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = statusBrush,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 2)
        };

        TextBlock sector = new()
        {
            Text = SettingsService.L(
                "СЕКТОР STG",
                "STG SECTOR"),
            FontSize = 7,
            HorizontalAlignment =
                HorizontalAlignment.Center
        };

        sector.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        meterStack.Children.Add(meterLabel);
        meterStack.Children.Add(usage);
        meterStack.Children.Add(sector);
        meter.Child = meterStack;

        Grid.SetColumn(compass, 0);
        Grid.SetColumn(information, 1);
        Grid.SetColumn(meter, 2);

        layout.Children.Add(compass);
        layout.Children.Add(information);
        layout.Children.Add(meter);
        body.Child = layout;
        root.Children.Add(body);

        Border topLeft = new()
        {
            Width = 24,
            Height = 3,
            HorizontalAlignment =
                HorizontalAlignment.Left,
            VerticalAlignment =
                VerticalAlignment.Top,
            Margin = new Thickness(7)
        };

        topLeft.SetResourceReference(
            Border.BackgroundProperty,
            "MilitaryKhakiBrush");

        Border bottomRight = new()
        {
            Width = 24,
            Height = 3,
            HorizontalAlignment =
                HorizontalAlignment.Right,
            VerticalAlignment =
                VerticalAlignment.Bottom,
            Margin = new Thickness(7)
        };

        bottomRight.SetResourceReference(
            Border.BackgroundProperty,
            "MilitaryKhakiBrush");

        root.Children.Add(topLeft);
        root.Children.Add(bottomRight);
        outer.Child = root;

        return outer;
    }


    // ============================================================
    // ТЕМПЕРАТУРА НАКОПИТЕЛЕЙ
    // ============================================================

    private void UpdateStorageTemperatures(
    HardwareSnapshot snapshot)
    {
        PrepareResponsiveGrid(
            StorageTemperaturePanel,
            snapshot.StorageDevices.Count,
            out int columns);

        for (int i = 0;
             i < snapshot.StorageDevices.Count;
             i++)
        {
            StorageDeviceInfo storage =
                snapshot.StorageDevices[i];

            int row =
                i / columns;

            int column =
                i % columns;

            string skinName =
                SettingsService
                    .Current
                    .SkinName;

            if (skinName == "SteamPunk")
            {
                Border steamCard =
                    CreateSteamStorageCard(
                        storage,
                        column,
                        columns);

                Grid.SetRow(
                    steamCard,
                    row);

                Grid.SetColumn(
                    steamCard,
                    column);

                StorageTemperaturePanel
                    .Children
                    .Add(
                        steamCard);

                continue;
            }

            if (skinName == "FrostCore")
            {
                Border frostCard =
                    CreateFrostStorageCard(
                        storage,
                        column,
                        columns);

                Grid.SetRow(
                    frostCard,
                    row);

                Grid.SetColumn(
                    frostCard,
                    column);

                StorageTemperaturePanel
                    .Children
                    .Add(
                        frostCard);

                continue;
            }

            if (skinName == "MilitaryOps")
            {
                Border militaryCard =
                    CreateMilitaryStorageCard(
                        storage,
                        column,
                        columns);

                Grid.SetRow(
                    militaryCard,
                    row);

                Grid.SetColumn(
                    militaryCard,
                    column);

                StorageTemperaturePanel
                    .Children
                    .Add(
                        militaryCard);

                continue;
            }

            Border border =
                CreateGridCardBorder(
                    column,
                    columns);


            Brush statusBrush =
                UiBrushes.StorageTemperature(
                    storage.Temperature);


            double rawTemperature =
                storage.Temperature ?? 0;

            double normalized =
                Math.Clamp(
                    (rawTemperature - 20.0) /
                    50.0,
                    0,
                    1);


            Grid root =
                new();

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        new GridLength(3)
                });

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Auto
                });


            Border topAccent =
                new()
                {
                    Width = 72,
                    Height = 3,

                    HorizontalAlignment =
                        HorizontalAlignment.Left,

                    Margin =
                        new Thickness(
                            14, 0, 0, 0)
                };

            topAccent.SetResourceReference(
                Border.BackgroundProperty,
                "AccentBrush");


            Grid.SetRow(
                topAccent,
                0);

            root.Children.Add(
                topAccent);


            Grid body =
                new()
                {
                    Margin =
                        new Thickness(
                            15, 12, 15, 13)
                };

            body.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(44)
                });

            body.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(
                            1,
                            GridUnitType.Star)
                });

            body.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        GridLength.Auto
                });


            // ========================================================
            // ТЕРМОМЕТР
            // ========================================================

            Grid thermometer =
                new()
                {
                    Width = 27,
                    Height = 42,

                    HorizontalAlignment =
                        HorizontalAlignment.Left,

                    VerticalAlignment =
                        VerticalAlignment.Center
                };


            Border tubeOutline =
                new()
                {
                    Width = 9,
                    Height = 27,

                    CornerRadius =
                        new CornerRadius(5),

                    BorderBrush =
                        statusBrush,

                    BorderThickness =
                        new Thickness(1.5),

                    Background =
                        Brushes.Transparent,

                    HorizontalAlignment =
                        HorizontalAlignment.Center,

                    VerticalAlignment =
                        VerticalAlignment.Top,

                    Margin =
                        new Thickness(
                            0, 0, 0, 10)
                };


            double fillHeight =
                5 +
                (17 * normalized);


            Border tubeFill =
                new()
                {
                    Width = 4,
                    Height =
                        fillHeight,

                    CornerRadius =
                        new CornerRadius(2),

                    Background =
                        statusBrush,

                    HorizontalAlignment =
                        HorizontalAlignment.Center,

                    VerticalAlignment =
                        VerticalAlignment.Bottom,

                    Margin =
                        new Thickness(
                            0, 0, 0, 13)
                };


            System.Windows.Shapes.Ellipse bulb =
                new()
                {
                    Width = 15,
                    Height = 15,

                    Fill =
                        statusBrush,

                    Stroke =
                        statusBrush,

                    StrokeThickness = 1,

                    HorizontalAlignment =
                        HorizontalAlignment.Center,

                    VerticalAlignment =
                        VerticalAlignment.Bottom
                };


            thermometer.Children.Add(
                tubeOutline);

            thermometer.Children.Add(
                tubeFill);

            thermometer.Children.Add(
                bulb);


            // ========================================================
            // ИНФОРМАЦИЯ
            // ========================================================

            Grid information =
                new()
                {
                    Margin =
                        new Thickness(
                            3, 0, 12, 0)
                };

            information.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Auto
                });

            information.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Auto
                });

            information.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Auto
                });


            TextBlock sensorLabel =
                new()
                {
                    Text = "THERMAL SENSOR",

                    FontSize = 8,

                    FontWeight =
                        FontWeights.SemiBold,

                    Margin =
                        new Thickness(
                            0, 0, 0, 3)
                };

            sensorLabel.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");


            TextBlock name =
                new()
                {
                    Text =
                        storage.Name,

                    FontSize = 12,

                    FontWeight =
                        FontWeights.SemiBold,

                    TextWrapping =
                        TextWrapping.Wrap,

                    TextTrimming =
                        TextTrimming.CharacterEllipsis,

                    Margin =
                        new Thickness(
                            0, 0, 0, 8)
                };

            name.SetResourceReference(
                TextBlock.ForegroundProperty,
                "PrimaryTextBrush");


            ProgressBar thermalBar =
                new()
                {
                    Minimum = 0,
                    Maximum = 100,

                    Value =
                        normalized * 100,

                    Height = 5,

                    Foreground =
                        statusBrush
                };


            Grid.SetRow(
                sensorLabel,
                0);

            Grid.SetRow(
                name,
                1);

            Grid.SetRow(
                thermalBar,
                2);


            information.Children.Add(
                sensorLabel);

            information.Children.Add(
                name);

            information.Children.Add(
                thermalBar);


            // ========================================================
            // ТЕМПЕРАТУРА / СТАТУС
            // ========================================================

            StackPanel valuePanel =
                new()
                {
                    HorizontalAlignment =
                        HorizontalAlignment.Right,

                    VerticalAlignment =
                        VerticalAlignment.Center
                };


            TextBlock temperature =
                new()
                {
                    Text =
                        FormatTemperature(
                            storage.Temperature),

                    Foreground =
                        statusBrush,

                    FontSize = 19,

                    FontWeight =
                        FontWeights.Bold,

                    HorizontalAlignment =
                        HorizontalAlignment.Right
                };


            StackPanel sensorStatus =
                new()
                {
                    Orientation =
                        Orientation.Horizontal,

                    HorizontalAlignment =
                        HorizontalAlignment.Right,

                    Margin =
                        new Thickness(
                            0, 5, 0, 0)
                };


            System.Windows.Shapes.Ellipse statusDot =
                new()
                {
                    Width = 5,
                    Height = 5,

                    Fill =
                        storage.Temperature.HasValue
                            ? statusBrush
                            : Brushes.Gray,

                    VerticalAlignment =
                        VerticalAlignment.Center,

                    Margin =
                        new Thickness(
                            0, 0, 5, 0)
                };


            TextBlock statusText =
                new()
                {
                    Text =
                        storage.Temperature.HasValue
                            ? "SENSOR ACTIVE"
                            : "NO DATA",

                    FontSize = 8,

                    FontWeight =
                        FontWeights.SemiBold
                };

            statusText.SetResourceReference(
                TextBlock.ForegroundProperty,
                storage.Temperature.HasValue
                    ? "SecondaryTextBrush"
                    : "SecondaryTextBrush");


            sensorStatus.Children.Add(
                statusDot);

            sensorStatus.Children.Add(
                statusText);


            valuePanel.Children.Add(
                temperature);

            valuePanel.Children.Add(
                sensorStatus);


            Grid.SetColumn(
                thermometer,
                0);

            Grid.SetColumn(
                information,
                1);

            Grid.SetColumn(
                valuePanel,
                2);


            body.Children.Add(
                thermometer);

            body.Children.Add(
                information);

            body.Children.Add(
                valuePanel);


            Grid.SetRow(
                body,
                1);

            root.Children.Add(
                body);


            border.Child =
                root;


            Grid.SetRow(
                border,
                row);

            Grid.SetColumn(
                border,
                column);


            StorageTemperaturePanel
                .Children
                .Add(
                    border);
        }
    }

    private Border CreateSteamStorageCard(
        StorageDeviceInfo storage,
        int column,
        int columns)
    {
        Brush statusBrush =
            UiBrushes.StorageTemperature(
                storage.Temperature);

        double rawTemperature =
            storage.Temperature ?? 0;

        double normalized =
            Math.Clamp(
                (rawTemperature - 20.0) /
                50.0,
                0,
                1);

        Border outer =
            CreateSteamGridCardBorder(
                column,
                columns);

        Grid root = new()
        {
            Height = 112
        };

        // Центральная часть выглядит как секция котловой магистрали.
        Border body =
            new()
            {
                Margin =
                    new Thickness(
                        34, 8, 38, 8),

                Padding =
                    new Thickness(
                        53, 11, 67, 11),

                BorderThickness =
                    new Thickness(2),

                CornerRadius =
                    new CornerRadius(22)
            };

        body.SetResourceReference(
            Border.BackgroundProperty,
            "SteamPanelBrush");

        body.SetResourceReference(
            Border.BorderBrushProperty,
            "SteamMetalBrush");

        StackPanel information =
            new()
            {
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        TextBlock caption =
            new()
            {
                Text =
                    SettingsService.L(
                    "ТЕМПЕРАТУРА НАКОПИТЕЛЯ",
                    "STORAGE TEMPERATURE"),

                FontSize = 8,

                FontWeight =
                    FontWeights.Bold
            };

        caption.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        TextBlock name =
            new()
            {
                Text =
                    storage.Name,

                FontSize = 12,

                FontWeight =
                    FontWeights.SemiBold,

                TextWrapping =
                    TextWrapping.Wrap,

                TextTrimming =
                    TextTrimming.CharacterEllipsis,

                Margin =
                    new Thickness(
                        0, 3, 0, 7)
            };

        name.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        StackPanel statusRow =
            new()
            {
                Orientation =
                    Orientation.Horizontal
            };

        System.Windows.Shapes.Ellipse lamp =
            new()
            {
                Width = 6,
                Height = 6,

                Fill =
                    storage.Temperature.HasValue
                        ? statusBrush
                        : Brushes.Gray,

                Margin =
                    new Thickness(
                        0, 0, 6, 0),

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        TextBlock status =
            new()
            {
                Text =
                    storage.Temperature.HasValue
                        ? SettingsService.L(
                        "Датчик активен",
                        "Sensor active")
                        : SettingsService.L(
                    "Нет данных",
                    "No data"),

                FontSize = 9,

                FontWeight =
                    FontWeights.SemiBold
            };

        status.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        statusRow.Children.Add(
            lamp);

        statusRow.Children.Add(
            status);

        information.Children.Add(
            caption);

        information.Children.Add(
            name);

        information.Children.Add(
            statusRow);

        body.Child =
            information;

        root.Children.Add(
            body);

        // Слева — настоящий вертикальный термометр с колбой.
        Grid thermometerHousing =
            new()
            {
                Width = 72,
                Height = 88,
                HorizontalAlignment =
                    HorizontalAlignment.Left,
                VerticalAlignment =
                    VerticalAlignment.Center,
                Margin =
                    new Thickness(
                        3, 0, 0, 0)
            };

        System.Windows.Shapes.Ellipse plate =
            new()
            {
                StrokeThickness = 4
            };

        plate.SetResourceReference(
            System.Windows.Shapes.Shape.FillProperty,
            "SteamGlassBrush");

        plate.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "SteamMetalBrush");

        Grid thermometer =
            new()
            {
                Width = 28,
                Height = 63,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        Border tube =
            new()
            {
                Width = 11,
                Height = 43,
                CornerRadius =
                    new CornerRadius(6),
                BorderBrush =
                    statusBrush,
                BorderThickness =
                    new Thickness(2),
                VerticalAlignment =
                    VerticalAlignment.Top,
                HorizontalAlignment =
                    HorizontalAlignment.Center
            };

        Border fill =
            new()
            {
                Width = 5,
                Height =
                    7 +
                    (28 * normalized),
                CornerRadius =
                    new CornerRadius(3),
                Background =
                    statusBrush,
                VerticalAlignment =
                    VerticalAlignment.Bottom,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                Margin =
                    new Thickness(
                        0, 0, 0, 16)
            };

        System.Windows.Shapes.Ellipse bulb =
            new()
            {
                Width = 18,
                Height = 18,
                Fill =
                    statusBrush,
                Stroke =
                    statusBrush,
                StrokeThickness = 1,
                VerticalAlignment =
                    VerticalAlignment.Bottom,
                HorizontalAlignment =
                    HorizontalAlignment.Center
            };

        thermometer.Children.Add(
            tube);

        thermometer.Children.Add(
            fill);

        thermometer.Children.Add(
            bulb);

        thermometerHousing.Children.Add(
            plate);

        thermometerHousing.Children.Add(
            thermometer);

        root.Children.Add(
            thermometerHousing);

        // Справа — встроенный круглый цифровой термометр.
        Grid meter =
            new()
            {
                Width = 78,
                Height = 78,
                HorizontalAlignment =
                    HorizontalAlignment.Right,
                VerticalAlignment =
                    VerticalAlignment.Center,
                Margin =
                    new Thickness(
                        0, 0, 5, 0)
            };

        System.Windows.Shapes.Ellipse meterOuter =
            new()
            {
                StrokeThickness = 4
            };

        meterOuter.SetResourceReference(
            System.Windows.Shapes.Shape.FillProperty,
            "SteamGlassBrush");

        meterOuter.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "SteamMetalBrush");

        System.Windows.Shapes.Ellipse meterInner =
            new()
            {
                Width = 60,
                Height = 60,
                StrokeThickness = 1
            };

        meterInner.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "BorderBrush");

        StackPanel temperatureStack =
            new()
            {
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        TextBlock tempLabel =
            new()
            {
                Text = SettingsService.L(
                "ТЕМП.",
                "TEMP."),
                FontSize = 7,
                FontWeight =
                    FontWeights.Bold,
                HorizontalAlignment =
                    HorizontalAlignment.Center
            };

        tempLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        TextBlock temperature =
            new()
            {
                Text =
                    FormatTemperature(
                        storage.Temperature),

                Foreground =
                    statusBrush,

                FontSize = 16,
                FontWeight =
                    FontWeights.Bold,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                Margin =
                    new Thickness(
                        0, 1, 0, 0)
            };

        temperatureStack.Children.Add(
            tempLabel);

        temperatureStack.Children.Add(
            temperature);

        meter.Children.Add(
            meterOuter);

        meter.Children.Add(
            meterInner);

        meter.Children.Add(
            temperatureStack);

        root.Children.Add(
            meter);

        outer.Child =
            root;

        return outer;
    }


    private Border CreateFrostStorageCard(
        StorageDeviceInfo storage,
        int column,
        int columns)
    {
        Brush statusBrush =
            UiBrushes.StorageTemperature(
                storage.Temperature);

        double rawTemperature =
            storage.Temperature ?? 0;

        double normalized =
            Math.Clamp(
                (rawTemperature - 20.0) /
                50.0,
                0,
                1);

        Border outer =
            CreateFrostGridCardBorder(
                column,
                columns);

        Border body = new()
        {
            Height = 112,
            Padding = new Thickness(12),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(18)
        };

        body.SetResourceReference(
            Border.BackgroundProperty,
            "FrostPanelBrush");

        body.SetResourceReference(
            Border.BorderBrushProperty,
            "FrostSteelBrush");

        Grid layout = new();

        layout.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(84)
            });

        layout.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        layout.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(98)
            });

        Grid cryoProbe = new()
        {
            Width = 62,
            Height = 82,
            HorizontalAlignment =
                HorizontalAlignment.Left,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        Border probeOuter = new()
        {
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(21)
        };

        probeOuter.SetResourceReference(
            Border.BackgroundProperty,
            "FrostGlassBrush");

        probeOuter.SetResourceReference(
            Border.BorderBrushProperty,
            "FrostSteelBrush");

        Border probeInner = new()
        {
            Width = 28,
            Height = 58,
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(1),
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        probeInner.SetResourceReference(
            Border.BorderBrushProperty,
            "FrostIceBrush");

        Border fill = new()
        {
            Width = 20,
            Height = 6 + 44 * normalized,
            CornerRadius = new CornerRadius(10),
            Background = statusBrush,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 13),
            Opacity = 0.9
        };

        TextBlock snow = new()
        {
            Text = "❄",
            FontSize = 13,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Top,
            Margin = new Thickness(0, 8, 0, 0),
            Opacity = 0.65
        };

        snow.SetResourceReference(
            TextBlock.ForegroundProperty,
            "FrostIceBrush");

        cryoProbe.Children.Add(probeOuter);
        cryoProbe.Children.Add(probeInner);
        cryoProbe.Children.Add(fill);
        cryoProbe.Children.Add(snow);

        StackPanel information = new()
        {
            VerticalAlignment =
                VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0)
        };

        TextBlock caption = new()
        {
            Text = SettingsService.L(
                "КРИОТЕРМОДАТЧИК НАКОПИТЕЛЯ",
                "STORAGE CRYO SENSOR"),
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        caption.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        TextBlock name = new()
        {
            Text = storage.Name,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming =
                TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 3, 0, 7)
        };

        name.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        StackPanel statusRow = new()
        {
            Orientation = Orientation.Horizontal
        };

        System.Windows.Shapes.Ellipse lamp =
            new()
            {
                Width = 6,
                Height = 6,
                Fill = storage.Temperature.HasValue
                    ? statusBrush
                    : Brushes.Gray,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        TextBlock status = new()
        {
            Text = storage.Temperature.HasValue
                ? SettingsService.L(
                    "Контур датчика активен",
                    "Sensor circuit active")
                : SettingsService.L(
                    "Нет данных",
                    "No data"),
            FontSize = 9,
            FontWeight = FontWeights.SemiBold
        };

        status.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        statusRow.Children.Add(lamp);
        statusRow.Children.Add(status);

        information.Children.Add(caption);
        information.Children.Add(name);
        information.Children.Add(statusRow);

        Border meter = new()
        {
            Padding = new Thickness(9),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(15),
            VerticalAlignment =
                VerticalAlignment.Center
        };

        meter.SetResourceReference(
            Border.BackgroundProperty,
            "FrostGlassBrush");

        meter.SetResourceReference(
            Border.BorderBrushProperty,
            "FrostSteelBrush");

        StackPanel temperatureStack = new();

        TextBlock tempLabel = new()
        {
            Text = SettingsService.L(
                "ТЕМП.",
                "TEMP."),
            FontSize = 7,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center
        };

        tempLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        TextBlock temperature = new()
        {
            Text = FormatTemperature(
                storage.Temperature),
            Foreground = statusBrush,
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 2)
        };

        TextBlock state = new()
        {
            Text = SettingsService.L(
                "КРИОКОНТРОЛЬ",
                "CRYO CONTROL"),
            FontSize = 7,
            HorizontalAlignment =
                HorizontalAlignment.Center
        };

        state.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        temperatureStack.Children.Add(tempLabel);
        temperatureStack.Children.Add(temperature);
        temperatureStack.Children.Add(state);
        meter.Child = temperatureStack;

        Grid.SetColumn(cryoProbe, 0);
        Grid.SetColumn(information, 1);
        Grid.SetColumn(meter, 2);

        layout.Children.Add(cryoProbe);
        layout.Children.Add(information);
        layout.Children.Add(meter);
        body.Child = layout;
        outer.Child = body;

        return outer;
    }


    private Border CreateMilitaryStorageCard(
        StorageDeviceInfo storage,
        int column,
        int columns)
    {
        Brush statusBrush =
            UiBrushes.StorageTemperature(
                storage.Temperature);

        double rawTemperature =
            storage.Temperature ?? 0;

        double normalized =
            Math.Clamp(
                (rawTemperature - 20.0) /
                50.0,
                0,
                1);

        Border outer =
            CreateMilitaryGridCardBorder(
                column,
                columns);

        Grid root = new()
        {
            Height = 104
        };

        Border body = new()
        {
            Padding = new Thickness(12, 10, 12, 10),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(1)
        };

        body.SetResourceReference(
            Border.BackgroundProperty,
            "MilitaryPanelBrush");

        body.SetResourceReference(
            Border.BorderBrushProperty,
            "MilitaryKhakiBrush");

        Grid layout = new();

        layout.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(72)
            });

        layout.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        layout.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(102)
            });

        // Прицельная сетка датчика.
        Grid sensor = new()
        {
            Width = 56,
            Height = 56,
            HorizontalAlignment =
                HorizontalAlignment.Left,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        System.Windows.Shapes.Ellipse sensorOuter =
            new()
            {
                StrokeThickness = 2
            };

        sensorOuter.SetResourceReference(
            System.Windows.Shapes.Shape.FillProperty,
            "MilitaryPlateBrush");

        sensorOuter.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "MilitaryKhakiBrush");

        System.Windows.Shapes.Ellipse sensorInner =
            new()
            {
                Width = 34,
                Height = 34,
                StrokeThickness = 1
            };

        sensorInner.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "MilitaryGridBrush");

        Border sensorVertical = new()
        {
            Width = 1,
            Height = 46,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        sensorVertical.SetResourceReference(
            Border.BackgroundProperty,
            "MilitaryGridBrush");

        Border sensorHorizontal = new()
        {
            Width = 46,
            Height = 1,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        sensorHorizontal.SetResourceReference(
            Border.BackgroundProperty,
            "MilitaryGridBrush");

        System.Windows.Shapes.Ellipse sensorCenter =
            new()
            {
                Width = 9,
                Height = 9,
                Fill = statusBrush,
                StrokeThickness = 1
            };

        sensorCenter.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "MilitaryKhakiBrush");

        sensor.Children.Add(sensorOuter);
        sensor.Children.Add(sensorInner);
        sensor.Children.Add(sensorVertical);
        sensor.Children.Add(sensorHorizontal);
        sensor.Children.Add(sensorCenter);

        Grid information = new()
        {
            Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment =
                VerticalAlignment.Center
        };

        information.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });
        information.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });
        information.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        TextBlock caption = new()
        {
            Text = SettingsService.L(
                "ПОЛЕВОЙ ТЕРМОДАТЧИК",
                "FIELD THERMAL SENSOR"),
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        caption.SetResourceReference(
            TextBlock.ForegroundProperty,
            "MilitaryKhakiBrush");

        TextBlock name = new()
        {
            Text = storage.Name,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming =
                TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 3, 0, 7)
        };

        name.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        Grid threatBar = new()
        {
            Height = 13
        };

        int activeSegments =
            normalized <= 0
                ? 0
                : Math.Clamp(
                    (int)Math.Ceiling(
                        normalized * 5),
                    0,
                    5);

        for (int i = 0;
             i < 5;
             i++)
        {
            threatBar.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = new GridLength(
                        1,
                        GridUnitType.Star)
                });

            Border segment = new()
            {
                Margin = new Thickness(
                    0,
                    0,
                    i == 4 ? 0 : 3,
                    0),
                BorderThickness = new Thickness(1),
                Background =
                    i < activeSegments
                        ? statusBrush
                        : UiBrushes.Theme(
                            "MilitaryGridBrush")
            };

            segment.SetResourceReference(
                Border.BorderBrushProperty,
                "MilitaryKhakiBrush");

            Grid.SetColumn(
                segment,
                i);

            threatBar.Children.Add(
                segment);
        }

        Grid.SetRow(caption, 0);
        Grid.SetRow(name, 1);
        Grid.SetRow(threatBar, 2);

        information.Children.Add(caption);
        information.Children.Add(name);
        information.Children.Add(threatBar);

        Border meter = new()
        {
            Padding = new Thickness(9),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(1),
            VerticalAlignment =
                VerticalAlignment.Center
        };

        meter.SetResourceReference(
            Border.BackgroundProperty,
            "MilitaryPlateBrush");

        meter.SetResourceReference(
            Border.BorderBrushProperty,
            "MilitaryKhakiBrush");

        StackPanel meterStack = new();

        TextBlock meterLabel = new()
        {
            Text = storage.Temperature.HasValue
                ? SettingsService.L(
                    "ТЕМПЕРАТУРА",
                    "TEMPERATURE")
                : SettingsService.L(
                    "НЕТ ДАННЫХ",
                    "NO DATA"),
            FontSize = 7,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center
        };

        meterLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "MilitaryKhakiBrush");

        TextBlock temperature = new()
        {
            Text = FormatTemperature(
                storage.Temperature),
            Foreground = statusBrush,
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 2)
        };

        TextBlock sector = new()
        {
            Text = SettingsService.L(
                "СЕКТОР THM",
                "THM SECTOR"),
            FontSize = 7,
            HorizontalAlignment =
                HorizontalAlignment.Center
        };

        sector.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        meterStack.Children.Add(meterLabel);
        meterStack.Children.Add(temperature);
        meterStack.Children.Add(sector);
        meter.Child = meterStack;

        Grid.SetColumn(sensor, 0);
        Grid.SetColumn(information, 1);
        Grid.SetColumn(meter, 2);

        layout.Children.Add(sensor);
        layout.Children.Add(information);
        layout.Children.Add(meter);
        body.Child = layout;
        root.Children.Add(body);
        outer.Child = root;

        return outer;
    }


    private static Border CreateSteamGridCardBorder(
        int column,
        int columns)
    {
        double rightMargin =
            column < columns - 1
                ? 12
                : 0;

        return new Border
        {
            Padding =
                new Thickness(0),

            BorderThickness =
                new Thickness(0),

            Background =
                Brushes.Transparent,

            Margin =
                new Thickness(
                    0,
                    0,
                    rightMargin,
                    12),

            HorizontalAlignment =
                HorizontalAlignment.Stretch
        };
    }


    private static Border CreateFrostGridCardBorder(
        int column,
        int columns)
    {
        double rightMargin =
            column < columns - 1
                ? 12
                : 0;

        return new Border
        {
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Margin = new Thickness(
                0,
                0,
                rightMargin,
                12),
            HorizontalAlignment =
                HorizontalAlignment.Stretch
        };
    }


    private static Border CreateMilitaryGridCardBorder(
        int column,
        int columns)
    {
        double rightMargin =
            column < columns - 1
                ? 12
                : 0;

        return new Border
        {
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Margin = new Thickness(
                0,
                0,
                rightMargin,
                12),
            HorizontalAlignment =
                HorizontalAlignment.Stretch
        };
    }


    private static void AddSteamCardRivet(
        Grid root,
        HorizontalAlignment horizontal,
        VerticalAlignment vertical)
    {
        System.Windows.Shapes.Ellipse rivet =
            new()
            {
                Width = 7,
                Height = 7,
                Margin =
                    new Thickness(7),
                HorizontalAlignment =
                    horizontal,
                VerticalAlignment =
                    vertical,
                StrokeThickness = 1,
                IsHitTestVisible = false
            };

        rivet.SetResourceReference(
            System.Windows.Shapes.Shape.FillProperty,
            "SteamRivetBrush");

        rivet.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "SteamDarkMetalBrush");

        root.Children.Add(
            rivet);
    }

    // ============================================================
    // АДАПТИВНАЯ СЕТКА
    // ============================================================

    private void PrepareResponsiveGrid(
        Grid grid,
        int itemCount,
        out int columns)
    {
        grid.Children.Clear();

        grid.ColumnDefinitions.Clear();

        grid.RowDefinitions.Clear();

        if (itemCount <= 0)
        {
            columns = 1;
            return;
        }

        const double minimumCardWidth =
            250;

        const double gap =
            12;

        double availableWidth =
            grid.ActualWidth;

        if (availableWidth <= 0)
        {
            availableWidth =
                Math.Max(
                    300,
                    Width - 48);
        }

        columns =
            (int)Math.Floor(
                (availableWidth + gap) /
                (minimumCardWidth + gap));

        columns =
            Math.Clamp(
                columns,
                1,
                itemCount);

        int rows =
            (int)Math.Ceiling(
                itemCount /
                (double)columns);

        for (int i = 0;
             i < columns;
             i++)
        {
            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(
                            1,
                            GridUnitType.Star)
                });
        }

        for (int i = 0;
             i < rows;
             i++)
        {
            grid.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Auto
                });
        }
    }

    private static Border CreateGridCardBorder(
    int column,
    int columns)
    {
        double rightMargin =
            column < columns - 1
                ? 12
                : 0;

        Border border =
            new()
            {
                CornerRadius =
                    new CornerRadius(6),

                Padding =
                    new Thickness(0),

                BorderThickness =
                    new Thickness(1),

                Margin =
                    new Thickness(
                        0,
                        0,
                        rightMargin,
                        12),

                HorizontalAlignment =
                    HorizontalAlignment.Stretch
            };

        border.SetResourceReference(
            Border.BackgroundProperty,
            "CardBackgroundBrush");

        border.SetResourceReference(
            Border.BorderBrushProperty,
            "BorderBrush");

        return border;
    }

    // ============================================================
    // РАЗМЕР / ПОЛОЖЕНИЕ ОКНА
    // ============================================================

    private void RestoreWindowPlacement()
    {
        try
        {
            if (!System.IO.File.Exists(
                    WindowSettingsPath))
            {
                SetDefaultWindowPlacement();
                return;
            }

            string json =
                System.IO.File.ReadAllText(
                    WindowSettingsPath);

            WindowSettings? settings =
                JsonSerializer.Deserialize<WindowSettings>(
                    json);

            if (settings == null ||
                settings.Width < MinWidth ||
                settings.Height < MinHeight)
            {
                SetDefaultWindowPlacement();
                return;
            }

            Width =
                settings.Width;

            Height =
                settings.Height;

            Left =
                settings.Left;

            Top =
                settings.Top;

            if (!IsWindowVisibleEnough())
            {
                CenterWindowOnPrimaryScreen();
            }

            WindowState =
                settings.IsMaximized
                    ? WindowState.Maximized
                    : WindowState.Normal;
        }
        catch
        {
            SetDefaultWindowPlacement();
        }
    }

    private void SetDefaultWindowPlacement()
    {
        Rect workArea =
            SystemParameters.WorkArea;

        Width =
            Math.Max(
                MinWidth,
                Math.Min(
                    1400,
                    workArea.Width * 0.88));

        Height =
            Math.Max(
                MinHeight,
                Math.Min(
                    820,
                    workArea.Height * 0.88));

        Left =
            workArea.Left +
            (workArea.Width - Width) / 2;

        Top =
            workArea.Top +
            (workArea.Height - Height) / 2;

        WindowState =
            WindowState.Normal;
    }

    private void CenterWindowOnPrimaryScreen()
    {
        Rect workArea =
            SystemParameters.WorkArea;

        Width =
            Math.Min(
                Width,
                workArea.Width * 0.95);

        Height =
            Math.Min(
                Height,
                workArea.Height * 0.95);

        Left =
            workArea.Left +
            (workArea.Width - Width) / 2;

        Top =
            workArea.Top +
            (workArea.Height - Height) / 2;
    }

    private bool IsWindowVisibleEnough()
    {
        Rect windowRect =
            new(
                Left,
                Top,
                Width,
                Height);

        Rect virtualScreen =
            new(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);

        Rect intersection =
            Rect.Intersect(
                windowRect,
                virtualScreen);

        return
            !intersection.IsEmpty &&
            intersection.Width >= 100 &&
            intersection.Height >= 100;
    }

    private void SaveWindowPlacement()
    {
        try
        {
            Rect bounds =
                WindowState ==
                WindowState.Normal

                    ? new Rect(
                        Left,
                        Top,
                        Width,
                        Height)

                    : RestoreBounds;

            WindowSettings settings =
                new()
                {
                    Left =
                        bounds.Left,

                    Top =
                        bounds.Top,

                    Width =
                        bounds.Width,

                    Height =
                        bounds.Height,

                    IsMaximized =
                        WindowState ==
                        WindowState.Maximized
                };

            System.IO.Directory
                .CreateDirectory(
                    SettingsDirectory);

            string json =
                JsonSerializer.Serialize(
                    settings,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

            System.IO.File.WriteAllText(
                WindowSettingsPath,
                json);
        }
        catch
        {
        }
    }

    // ============================================================
    // ФОРМАТИРОВАНИЕ
    // ============================================================

    private static string FormatTemperature(
        float? value)
    {
        return value.HasValue
            ? $"{value.Value:F1} °C"
            : SettingsService.L(
                    "Нет данных",
                    "No data");
    }

    private static string FormatPercent(
        float? value)
    {
        return value.HasValue
            ? $"{value.Value:F1} %"
            : SettingsService.L(
                    "Нет данных",
                    "No data");
    }

    private static double ClampPercent(
        float? value)
    {
        return value.HasValue
            ? Math.Clamp(
                value.Value,
                0,
                100)
            : 0;
    }

    private static double BytesToGb(
        long bytes)
    {
        return
            bytes /
            1024d /
            1024d /
            1024d;
    }

    private static string FormatSize(
        double gigabytes)
    {
        if (gigabytes >= 1024)
        {
            return
                SettingsService.L(
                $"{gigabytes / 1024:F2} ТБ",
                $"{gigabytes / 1024:F2} TB");
        }

        return
            SettingsService.L(
            $"{gigabytes:F0} ГБ",
            $"{gigabytes:F0} GB");
    }
}


public sealed class WindowSettings
{
    public double Left { get; set; }

    public double Top { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    public bool IsMaximized { get; set; }
}