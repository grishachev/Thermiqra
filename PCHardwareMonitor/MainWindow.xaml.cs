using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCHardwareMonitor;

public partial class MainWindow : Window
{
    private readonly HardwareMonitorService _monitor;

    private readonly DispatcherTimer _timer;

    private readonly TrayService _tray;

    private readonly AlertService _alerts;

    private readonly RingGauge _cpuGauge;

    private readonly List<RingGauge>
        _gpuGauges =
            new();

    private HardwareSnapshot?
        _lastSnapshot;

    private bool _isExiting;

    private bool _servicesDisposed;

    private bool _livePulseBright = true;

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

        RestoreWindowPlacement();

        _monitor =
            new HardwareMonitorService();

        _tray =
            new TrayService(
                openAction:
                    ShowMainWindow,

                settingsAction:
                    () =>
                        OpenSettings(false),

                exitAction:
                    ExitApplication);

        _alerts =
            new AlertService(
                _tray);

        _cpuGauge =
            new RingGauge(
                "CPU",
                TemperatureType.Cpu);

        MainGaugesPanel
            .Children
            .Add(
                _cpuGauge);

        _timer =
            new DispatcherTimer
            {
                Interval =
                    TimeSpan.FromSeconds(1)
            };

        _timer.Tick +=
            Timer_Tick;

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
    }

    private void SettingsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        OpenSettings(false);
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

    private void ExitApplication()
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

            UpdateCpu(snapshot);

            UpdateGpus(snapshot);

            UpdateMemory(snapshot);

            UpdateDrives(snapshot);

            UpdateStorageTemperatures(
                snapshot);

            _alerts.Evaluate(
                snapshot);

            StatusText.Text =
                $"Обновлено: " +
                $"{DateTime.Now:HH:mm:ss}";

            _livePulseBright =
                !_livePulseBright;

            LivePulseDot.Opacity =
                _livePulseBright
                    ? 1.0
                    : 0.35;
        }
        catch (Exception ex)
        {
            StatusText.Text =
                $"Ошибка: {ex.Message}";
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

        if (used.HasValue &&
            total.HasValue)
        {
            RamText.Text =
                $"{used.Value:F1} ГБ " +
                $"из {total.Value:F1} ГБ";
        }
        else
        {
            RamText.Text =
                "Нет данных";
        }

        double ramLoad =
            ClampPercent(
                snapshot.Memory.Load);

        RamBar.Value =
            ramLoad;

        if (ramLoad >= 90)
        {
            RamBar.Foreground =
                System.Windows.Media.Brushes.Red;
        }
        else if (ramLoad >= 85)
        {
            RamBar.Foreground =
                System.Windows.Media.Brushes.Orange;
        }
        else if (ramLoad >= 70)
        {
            RamBar.Foreground =
                System.Windows.Media.Brushes.Gold;
        }
        else
        {
            RamBar.Foreground =
                UiBrushes.Theme(
                    "AccentBrush");
        }

        RamPercentText.Text =
            $"Использовано: " +
            $"{FormatPercent(snapshot.Memory.Load)}";
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


        System.Windows.Shapes.Path frame =
            new()
            {
                Data =
                    Geometry.Parse(
                        "M 8,0 L 100,0 L 100,84 L 94,100 L 0,100 L 0,8 Z"),

                Stretch =
                    Stretch.Fill,

                StrokeThickness = 1,

                IsHitTestVisible =
                    false
            };

        frame.SetResourceReference(
            System.Windows.Shapes.Shape.FillProperty,
            "CardBackgroundBrush");

        frame.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "BorderBrush");

        Grid.SetRowSpan(
            frame,
            2);

        root.Children.Add(
            frame);


        Border topAccent =
            new()
            {
                Width = 62,
                Height = 3,

                HorizontalAlignment =
                    HorizontalAlignment.Left,

                Margin =
                    new Thickness(
                        26, 0, 0, 0)
            };

        topAccent.SetResourceReference(
            Border.BackgroundProperty,
            "AccentBrush");


        Grid.SetRow(
            topAccent,
            0);

        root.Children.Add(
            topAccent);


        Border bottomAccent =
            new()
            {
                Width = 30,
                Height = 2,

                HorizontalAlignment =
                    HorizontalAlignment.Right,

                VerticalAlignment =
                    VerticalAlignment.Bottom,

                Margin =
                    new Thickness(
                        0, 0, 28, 0),

                Opacity = 0.68
            };

        bottomAccent.SetResourceReference(
            Border.BackgroundProperty,
            "AccentBrush");

        Grid.SetRowSpan(
            bottomAccent,
            2);

        root.Children.Add(
            bottomAccent);


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

                FontSize = 9,

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
                    $"Свободно: " +
                    $"{FormatSize(freeGb)} " +
                    $"из {FormatSize(totalGb)}",

                FontSize = 11,

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
                Text = "ЗАНЯТО",

                FontSize = 9,

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


            System.Windows.Shapes.Path frame =
                new()
                {
                    Data =
                        Geometry.Parse(
                            "M 8,0 L 100,0 L 100,84 L 94,100 L 0,100 L 0,8 Z"),

                    Stretch =
                        Stretch.Fill,

                    StrokeThickness = 1,

                    IsHitTestVisible =
                        false
                };

            frame.SetResourceReference(
                System.Windows.Shapes.Shape.FillProperty,
                "CardBackgroundBrush");

            frame.SetResourceReference(
                System.Windows.Shapes.Shape.StrokeProperty,
                "BorderBrush");

            Grid.SetRowSpan(
                frame,
                2);

            root.Children.Add(
                frame);


            Border topAccent =
                new()
                {
                    Width = 62,
                    Height = 3,

                    HorizontalAlignment =
                        HorizontalAlignment.Left,

                    Margin =
                        new Thickness(
                            26, 0, 0, 0)
                };

            topAccent.SetResourceReference(
                Border.BackgroundProperty,
                "AccentBrush");


            Grid.SetRow(
                topAccent,
                0);

            root.Children.Add(
                topAccent);


            Border bottomAccent =
                new()
                {
                    Width = 30,
                    Height = 2,

                    HorizontalAlignment =
                        HorizontalAlignment.Right,

                    VerticalAlignment =
                        VerticalAlignment.Bottom,

                    Margin =
                        new Thickness(
                            0, 0, 28, 0),

                    Opacity = 0.68
                };

            bottomAccent.SetResourceReference(
                Border.BackgroundProperty,
                "AccentBrush");

            Grid.SetRowSpan(
                bottomAccent,
                2);

            root.Children.Add(
                bottomAccent);


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

                    FontSize = 9,

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

                    FontSize = 9,

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
                    new CornerRadius(0),

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
            : "Нет данных";
    }

    private static string FormatPercent(
        float? value)
    {
        return value.HasValue
            ? $"{value.Value:F1} %"
            : "Нет данных";
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
                $"{gigabytes / 1024:F2} ТБ";
        }

        return
            $"{gigabytes:F0} ГБ";
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