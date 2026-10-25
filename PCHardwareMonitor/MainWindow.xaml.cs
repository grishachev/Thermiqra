using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
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

    private bool _isHardwareChangeCheckRunning;

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

                checkUpdatesAction:
                    CheckForUpdatesManually,

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

        _ = CheckHardwareChangesAsync();
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

        window.Show();
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
            new(
                _statistics)
            {
                Owner = this
            };

        window.Show();
    }

    private void Timer_Tick(
        object? sender,
        EventArgs e)
    {
        UpdateMonitor();
    }

    private void MainWindow_StateChanged(
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
}
