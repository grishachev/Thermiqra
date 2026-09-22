using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PCHardwareMonitor;

public partial class StatisticsWindow : Window
{
    private readonly StatisticsService _statistics;

    private readonly HardwareSnapshot? _currentSnapshot;

    private StatisticsPeriod _currentPeriod =
        StatisticsPeriod.Today;

    private StatisticsSummary? _currentSummary;

    private StatisticsChartSeries? _currentChartSeries;

    private ChartMetricOption? _currentChartMetric;

    private bool _updatingChartSelectors;

    private Grid? _recordsPanel;

    private StackPanel? _eventsListPanel;

    private TextBlock? _eventCountsText;

    private bool _extendedSectionsBuilt;

    public StatisticsWindow(
        StatisticsService statistics,
        HardwareSnapshot? currentSnapshot = null)
    {
        InitializeComponent();

        _statistics =
            statistics ??
            throw new ArgumentNullException(
                nameof(statistics));

        _currentSnapshot =
            currentSnapshot;

        Loaded +=
            StatisticsWindow_Loaded;
    }

    private void StatisticsWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        BuildExtendedSections();

        UpdateHardwareIdentity();

        LoadPeriod(
            StatisticsPeriod.Today);
    }

    private void UpdateHardwareIdentity()
    {
        string cpuName =
            _currentSnapshot?.Cpu.Name ?? "";

        CpuHardwareNameText.Text =
            string.IsNullOrWhiteSpace(
                cpuName)
                ? "Модель процессора: нет данных"
                : cpuName;

        float? totalRam =
            _currentSnapshot?.Memory.TotalGb;

        RamHardwareInfoText.Text =
            totalRam.HasValue
                ? $"Установлено: {totalRam.Value:F1} ГБ"
                : "Общий объём памяти: нет данных";
    }


    private void TodayButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        LoadPeriod(
            StatisticsPeriod.Today);
    }

    private void Last24HoursButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        LoadPeriod(
            StatisticsPeriod.Last24Hours);
    }

    private void Last7DaysButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        LoadPeriod(
            StatisticsPeriod.Last7Days);
    }

    private void Last30DaysButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        LoadPeriod(
            StatisticsPeriod.Last30Days);
    }

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }


    private void ChartSourceComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_updatingChartSelectors)
            return;

        RebuildChartMetrics();
        RefreshChart();
    }

    private void ChartMetricComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_updatingChartSelectors)
            return;

        _currentChartMetric =
            ChartMetricComboBox.SelectedItem
                as ChartMetricOption;

        RefreshChart();
    }

    private void ChartCanvas_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        DrawChart();
    }

    private void ChartSelector_PreviewMouseWheel(
        object sender,
        MouseWheelEventArgs e)
    {
        if (sender is ComboBox comboBox &&
            comboBox.IsDropDownOpen)
        {
            return;
        }

        StatisticsScrollViewer.ScrollToVerticalOffset(
            StatisticsScrollViewer.VerticalOffset - e.Delta);

        e.Handled = true;
    }

    private void LoadPeriod(
        StatisticsPeriod period)
    {
        _currentPeriod = period;

        try
        {
            StatisticsSummary summary =
                _statistics.GetSummary(
                    period);

            _currentSummary =
                summary;

            UpdatePeriodButtons();
            UpdateSummary(summary);
            RebuildChartSources(summary);
            RefreshExtendedSections();

            StatusText.Text =
                summary.SampleCount > 0
                    ? "История загружена. Точки сохраняются примерно раз в минуту."
                    : "За выбранный период сохранённых точек пока нет.";
        }
        catch (Exception ex)
        {
            StatusText.Text =
                $"Не удалось прочитать статистику: {ex.Message}";
        }
    }

    private void UpdatePeriodButtons()
    {
        Button[] buttons =
        {
            TodayButton,
            Last24HoursButton,
            Last7DaysButton,
            Last30DaysButton
        };

        foreach (Button button in buttons)
        {
            button.ClearValue(
                Button.BackgroundProperty);

            button.SetResourceReference(
                Button.BackgroundProperty,
                "SettingsInputBrush");

            button.ClearValue(
                Button.BorderBrushProperty);

            button.SetResourceReference(
                Button.BorderBrushProperty,
                "SettingsCardBorderBrush");

            button.ClearValue(
                Button.ForegroundProperty);

            button.SetResourceReference(
                Button.ForegroundProperty,
                "PrimaryTextBrush");
        }

        Button selectedButton =
            _currentPeriod switch
            {
                StatisticsPeriod.Today =>
                    TodayButton,

                StatisticsPeriod.Last24Hours =>
                    Last24HoursButton,

                StatisticsPeriod.Last7Days =>
                    Last7DaysButton,

                StatisticsPeriod.Last30Days =>
                    Last30DaysButton,

                _ =>
                    TodayButton
            };

        selectedButton.SetResourceReference(
            Button.BorderBrushProperty,
            "AccentBrush");

        selectedButton.SetResourceReference(
            Button.ForegroundProperty,
            "AccentBrush");

        selectedButton.SetResourceReference(
            Button.BackgroundProperty,
            "SettingsCardBrush");
    }

    private void RebuildChartSources(
        StatisticsSummary summary)
    {
        string? previousKey =
            (ChartSourceComboBox.SelectedItem
                as ChartSourceOption)
            ?.Key;

        _updatingChartSelectors = true;

        ChartSourceComboBox.Items.Clear();

        string cpuDisplayName =
            string.IsNullOrWhiteSpace(
                _currentSnapshot?.Cpu.Name)
                ? "CPU"
                : $"CPU — {_currentSnapshot!.Cpu.Name}";

        float? totalRam =
            _currentSnapshot?.Memory.TotalGb;

        string ramDisplayName =
            totalRam.HasValue
                ? $"RAM — {totalRam.Value:F1} ГБ"
                : "RAM";

        ChartSourceComboBox.Items.Add(
            new ChartSourceOption(
                "cpu",
                ChartSourceKind.Cpu,
                "",
                cpuDisplayName));

        ChartSourceComboBox.Items.Add(
            new ChartSourceOption(
                "ram",
                ChartSourceKind.Ram,
                "",
                ramDisplayName));

        foreach (GpuStatisticsSummary gpu in summary.Gpus)
        {
            ChartSourceComboBox.Items.Add(
                new ChartSourceOption(
                    $"gpu:{gpu.DeviceId}",
                    ChartSourceKind.Gpu,
                    gpu.DeviceId,
                    $"GPU — {gpu.DeviceName}"));
        }

        foreach (StorageStatisticsSummary storage in
                 summary.StorageDevices)
        {
            ChartSourceComboBox.Items.Add(
                new ChartSourceOption(
                    $"storage:{storage.DeviceId}",
                    ChartSourceKind.Storage,
                    storage.DeviceId,
                    $"Накопитель — {storage.DeviceName}"));
        }

        ChartSourceOption? selected =
            ChartSourceComboBox.Items
                .OfType<ChartSourceOption>()
                .FirstOrDefault(
                    item =>
                        item.Key == previousKey);

        ChartSourceComboBox.SelectedItem =
            selected ??
            ChartSourceComboBox.Items
                .OfType<ChartSourceOption>()
                .FirstOrDefault();

        _updatingChartSelectors = false;

        RebuildChartMetrics();
        RefreshChart();
    }

    private void RebuildChartMetrics()
    {
        ChartSourceOption? source =
            ChartSourceComboBox.SelectedItem
                as ChartSourceOption;

        if (source == null)
            return;

        StatisticsChartMetric? previousMetric =
            (ChartMetricComboBox.SelectedItem
                as ChartMetricOption)
            ?.Metric;

        _updatingChartSelectors = true;

        ChartMetricComboBox.Items.Clear();

        foreach (ChartMetricOption option in
                 GetMetricsForSource(source.Kind))
        {
            ChartMetricComboBox.Items.Add(option);
        }

        ChartMetricOption? selected =
            ChartMetricComboBox.Items
                .OfType<ChartMetricOption>()
                .FirstOrDefault(
                    item =>
                        item.Metric == previousMetric);

        ChartMetricComboBox.SelectedItem =
            selected ??
            ChartMetricComboBox.Items
                .OfType<ChartMetricOption>()
                .FirstOrDefault();

        _currentChartMetric =
            ChartMetricComboBox.SelectedItem
                as ChartMetricOption;

        _updatingChartSelectors = false;
    }

    private static IReadOnlyList<ChartMetricOption> GetMetricsForSource(
        ChartSourceKind kind)
    {
        return kind switch
        {
            ChartSourceKind.Cpu =>
                new[]
                {
                    new ChartMetricOption(
                        StatisticsChartMetric.CpuTemperature,
                        "Температура",
                        "°C"),
                    new ChartMetricOption(
                        StatisticsChartMetric.CpuLoad,
                        "Загрузка",
                        "%")
                },

            ChartSourceKind.Ram =>
                new[]
                {
                    new ChartMetricOption(
                        StatisticsChartMetric.RamLoad,
                        "Загрузка",
                        "%")
                },

            ChartSourceKind.Gpu =>
                new[]
                {
                    new ChartMetricOption(
                        StatisticsChartMetric.GpuTemperature,
                        "Температура",
                        "°C"),
                    new ChartMetricOption(
                        StatisticsChartMetric.GpuLoad,
                        "Загрузка",
                        "%"),
                    new ChartMetricOption(
                        StatisticsChartMetric.GpuHotSpotTemperature,
                        "Hot Spot",
                        "°C"),
                    new ChartMetricOption(
                        StatisticsChartMetric.GpuMemoryTemperature,
                        "Температура VRAM",
                        "°C")
                },

            ChartSourceKind.Storage =>
                new[]
                {
                    new ChartMetricOption(
                        StatisticsChartMetric.StorageTemperature,
                        "Температура",
                        "°C")
                },

            _ =>
                Array.Empty<ChartMetricOption>()
        };
    }

    private void RefreshChart()
    {
        ChartSourceOption? source =
            ChartSourceComboBox.SelectedItem
                as ChartSourceOption;

        ChartMetricOption? metric =
            ChartMetricComboBox.SelectedItem
                as ChartMetricOption;

        if (source == null ||
            metric == null)
        {
            _currentChartSeries = null;
            DrawChart();
            return;
        }

        _currentChartMetric = metric;

        try
        {
            _currentChartSeries =
                _statistics.GetChartSeries(
                    _currentPeriod,
                    metric.Metric,
                    source.DeviceId,
                    520);

            ChartTitleText.Text =
                $"{source.DisplayName} / {metric.DisplayName}";

            MetricStatistics? statistics =
                GetSelectedMetricStatistics(
                    source,
                    metric);

            ChartAverageText.Text =
                FormatChartValue(
                    statistics?.Average,
                    metric.Unit);

            ChartMaximumText.Text =
                FormatChartValue(
                    statistics?.Maximum,
                    metric.Unit);

            ChartMaximumTimeText.Text =
                FormatMaximumTime(
                    statistics?.MaximumAtUtc);

            DrawChart();
        }
        catch (Exception ex)
        {
            _currentChartSeries = null;
            ChartEmptyText.Text =
                $"Не удалось построить график: {ex.Message}";
            ChartEmptyText.Visibility =
                Visibility.Visible;
            ChartCanvas.Children.Clear();
        }
    }

    private MetricStatistics? GetSelectedMetricStatistics(
        ChartSourceOption source,
        ChartMetricOption metric)
    {
        if (_currentSummary == null)
            return null;

        if (source.Kind == ChartSourceKind.Cpu)
        {
            return metric.Metric switch
            {
                StatisticsChartMetric.CpuTemperature =>
                    _currentSummary.CpuTemperature,
                StatisticsChartMetric.CpuLoad =>
                    _currentSummary.CpuLoad,
                _ => null
            };
        }

        if (source.Kind == ChartSourceKind.Ram)
        {
            return _currentSummary.RamLoad;
        }

        if (source.Kind == ChartSourceKind.Gpu)
        {
            GpuStatisticsSummary? gpu =
                _currentSummary.Gpus
                    .FirstOrDefault(
                        item =>
                            item.DeviceId == source.DeviceId);

            if (gpu == null)
                return null;

            return metric.Metric switch
            {
                StatisticsChartMetric.GpuTemperature =>
                    gpu.Temperature,
                StatisticsChartMetric.GpuLoad =>
                    gpu.Load,
                StatisticsChartMetric.GpuHotSpotTemperature =>
                    gpu.HotSpotTemperature,
                StatisticsChartMetric.GpuMemoryTemperature =>
                    gpu.MemoryTemperature,
                _ => null
            };
        }

        if (source.Kind == ChartSourceKind.Storage)
        {
            return _currentSummary.StorageDevices
                .FirstOrDefault(
                    item =>
                        item.DeviceId == source.DeviceId)
                ?.Temperature;
        }

        return null;
    }

    private void DrawChart()
    {
        ChartCanvas.Children.Clear();

        StatisticsChartSeries? series =
            _currentChartSeries;

        ChartMetricOption? metric =
            _currentChartMetric;

        if (series == null ||
            metric == null ||
            series.Points.Count == 0 ||
            ChartCanvas.ActualWidth < 120 ||
            ChartCanvas.ActualHeight < 100)
        {
            ChartEmptyText.Text =
                "За выбранный период данных для графика пока нет.";
            ChartEmptyText.Visibility =
                Visibility.Visible;
            return;
        }

        ChartEmptyText.Visibility =
            Visibility.Collapsed;

        double width =
            ChartCanvas.ActualWidth;

        double height =
            ChartCanvas.ActualHeight;

        const double left = 52;
        const double right = 16;
        const double top = 16;
        const double bottom = 32;

        double plotWidth =
            Math.Max(
                10,
                width - left - right);

        double plotHeight =
            Math.Max(
                10,
                height - top - bottom);

        double minValue =
            series.Points.Min(
                point => point.Value);

        double maxValue =
            series.Points.Max(
                point => point.Value);

        bool percentMetric =
            metric.Unit == "%";

        double yMin;
        double yMax;

        if (percentMetric)
        {
            yMin = 0;
            yMax = 100;
        }
        else
        {
            double span =
                maxValue - minValue;

            if (span < 10)
                span = 10;

            double padding =
                Math.Max(
                    3,
                    span * 0.12);

            yMin =
                Math.Floor(
                    (minValue - padding) / 5.0) * 5.0;

            yMax =
                Math.Ceiling(
                    (maxValue + padding) / 5.0) * 5.0;

            if (yMax - yMin < 10)
                yMax = yMin + 10;
        }

        for (int i = 0;
             i <= 4;
             i++)
        {
            double ratio =
                i / 4.0;

            double y =
                top +
                plotHeight * ratio;

            Line gridLine =
                new()
                {
                    X1 = left,
                    X2 = left + plotWidth,
                    Y1 = y,
                    Y2 = y,
                    StrokeThickness = 1,
                    Opacity = 0.5,
                    IsHitTestVisible = false
                };

            gridLine.SetResourceReference(
                Shape.StrokeProperty,
                "BorderBrush");

            ChartCanvas.Children.Add(
                gridLine);

            double axisValue =
                yMax -
                (yMax - yMin) * ratio;

            TextBlock label =
                CreateChartAxisLabel(
                    percentMetric
                        ? $"{axisValue:F0}%"
                        : $"{axisValue:F0}°");

            Canvas.SetLeft(
                label,
                2);

            Canvas.SetTop(
                label,
                y - 8);

            ChartCanvas.Children.Add(
                label);
        }

        DateTimeOffset rangeStart =
            series.StartUtc;

        DateTimeOffset rangeEnd =
            series.EndUtc;

        double totalSeconds =
            Math.Max(
                1,
                (rangeEnd - rangeStart)
                    .TotalSeconds);

        for (int i = 0;
             i <= 4;
             i++)
        {
            double ratio =
                i / 4.0;

            double x =
                left +
                plotWidth * ratio;

            Line gridLine =
                new()
                {
                    X1 = x,
                    X2 = x,
                    Y1 = top,
                    Y2 = top + plotHeight,
                    StrokeThickness = 1,
                    Opacity = 0.28,
                    IsHitTestVisible = false
                };

            gridLine.SetResourceReference(
                Shape.StrokeProperty,
                "BorderBrush");

            ChartCanvas.Children.Add(
                gridLine);

            DateTimeOffset labelTime =
                rangeStart.AddSeconds(
                    totalSeconds * ratio)
                    .ToLocalTime();

            TextBlock label =
                CreateChartAxisLabel(
                    FormatChartTimeLabel(
                        labelTime));

            label.TextAlignment =
                TextAlignment.Center;

            Canvas.SetLeft(
                label,
                x - 28);

            Canvas.SetTop(
                label,
                top + plotHeight + 8);

            ChartCanvas.Children.Add(
                label);
        }

        Polyline line =
            new()
            {
                StrokeThickness = 2.2,
                StrokeLineJoin =
                    PenLineJoin.Round,
                IsHitTestVisible = false
            };

        line.SetResourceReference(
            Shape.StrokeProperty,
            "AccentBrush");

        List<(StatisticsChartPoint Data, Point Plot)> plottedPoints =
            new();

        foreach (StatisticsChartPoint point in
                 series.Points)
        {
            double xRatio =
                Math.Clamp(
                    (point.TimestampUtc - rangeStart)
                        .TotalSeconds /
                    totalSeconds,
                    0,
                    1);

            double yRatio =
                Math.Clamp(
                    (point.Value - yMin) /
                    Math.Max(
                        0.0001,
                        yMax - yMin),
                    0,
                    1);

            Point chartPoint =
                new(
                    left + plotWidth * xRatio,
                    top +
                    plotHeight *
                    (1 - yRatio));

            line.Points.Add(
                chartPoint);

            plottedPoints.Add(
                (point, chartPoint));
        }

        ChartCanvas.Children.Add(
            line);

        bool showPermanentMarkers =
            plottedPoints.Count <= 60;

        foreach ((StatisticsChartPoint data, Point plot) in
                 plottedPoints)
        {
            if (showPermanentMarkers)
            {
                Ellipse marker =
                    new()
                    {
                        Width = 5,
                        Height = 5,
                        IsHitTestVisible = false
                    };

                marker.SetResourceReference(
                    Shape.FillProperty,
                    "AccentBrush");

                Canvas.SetLeft(
                    marker,
                    plot.X - 2.5);

                Canvas.SetTop(
                    marker,
                    plot.Y - 2.5);

                ChartCanvas.Children.Add(
                    marker);
            }

            Ellipse hitTarget =
                new()
                {
                    Width = 14,
                    Height = 14,
                    Fill = Brushes.Transparent,
                    StrokeThickness = 2,
                    Opacity = 0.01,
                    Cursor = Cursors.Hand,
                    ToolTip =
                        CreateChartPointToolTip(
                            data,
                            metric)
                };

            hitTarget.SetResourceReference(
                Shape.StrokeProperty,
                "AccentBrush");

            hitTarget.MouseEnter +=
                (_, _) =>
                    hitTarget.Opacity = 1.0;

            hitTarget.MouseLeave +=
                (_, _) =>
                    hitTarget.Opacity = 0.01;

            Canvas.SetLeft(
                hitTarget,
                plot.X - 7);

            Canvas.SetTop(
                hitTarget,
                plot.Y - 7);

            ChartCanvas.Children.Add(
                hitTarget);
        }
    }

    private ToolTip CreateChartPointToolTip(
        StatisticsChartPoint point,
        ChartMetricOption metric)
    {
        DateTimeOffset localTime =
            point.TimestampUtc.ToLocalTime();

        string timeText =
            _currentPeriod switch
            {
                StatisticsPeriod.Today =>
                    localTime.ToString("HH:mm:ss"),
                StatisticsPeriod.Last24Hours =>
                    localTime.ToString("dd.MM.yyyy HH:mm:ss"),
                StatisticsPeriod.Last7Days =>
                    localTime.ToString("dd.MM.yyyy HH:mm"),
                StatisticsPeriod.Last30Days =>
                    localTime.ToString("dd.MM.yyyy HH:mm"),
                _ =>
                    localTime.ToString("dd.MM.yyyy HH:mm")
            };

        Border content =
            new()
            {
                Padding =
                    new Thickness(
                        10, 7, 10, 7),
                BorderThickness =
                    new Thickness(1)
            };

        content.SetResourceReference(
            Border.BackgroundProperty,
            "SettingsCardBrush");

        content.SetResourceReference(
            Border.BorderBrushProperty,
            "AccentBrush");

        object? radius =
            TryFindResource(
                "MiniCardCornerRadius");

        if (radius is CornerRadius cornerRadius)
        {
            content.CornerRadius =
                cornerRadius;
        }

        StackPanel stack =
            new();

        TextBlock time =
            new()
            {
                Text = timeText,
                FontSize = 10
            };

        time.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        TextBlock value =
            new()
            {
                Text =
                    FormatChartValue(
                        point.Value,
                        metric.Unit),
                FontSize = 14,
                FontWeight =
                    FontWeights.Bold,
                Margin =
                    new Thickness(
                        0, 3, 0, 0)
            };

        value.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        stack.Children.Add(time);
        stack.Children.Add(value);
        content.Child = stack;

        return new ToolTip
        {
            Content = content,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Placement = PlacementMode.Mouse,
            HasDropShadow = true
        };
    }

    private TextBlock CreateChartAxisLabel(
        string text)
    {
        TextBlock label =
            new()
            {
                Text = text,
                FontSize = 9,
                Width = 56,
                IsHitTestVisible = false
            };

        label.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        return label;
    }

    private string FormatChartTimeLabel(
        DateTimeOffset localTime)
    {
        return _currentPeriod switch
        {
            StatisticsPeriod.Today =>
                localTime.ToString("HH:mm"),
            StatisticsPeriod.Last24Hours =>
                localTime.ToString("HH:mm"),
            StatisticsPeriod.Last7Days =>
                localTime.ToString("dd.MM"),
            StatisticsPeriod.Last30Days =>
                localTime.ToString("dd.MM"),
            _ =>
                localTime.ToString("dd.MM")
        };
    }

    private static string FormatChartValue(
        double? value,
        string unit)
    {
        if (!value.HasValue)
            return "—";

        return unit == "%"
            ? $"{value.Value:F1} %"
            : $"{value.Value:F1} °C";
    }

    private void UpdateSummary(
        StatisticsSummary summary)
    {
        SampleCountText.Text =
            summary.SampleCount.ToString();

        DateTimeOffset localStart =
            summary.StartUtc.ToLocalTime();

        DateTimeOffset localEnd =
            summary.EndUtc.ToLocalTime();

        PeriodRangeText.Text =
            $"{localStart:dd.MM.yyyy HH:mm} — " +
            $"{localEnd:dd.MM.yyyy HH:mm}";

        CpuMaxTemperatureText.Text =
            FormatTemperature(
                summary.CpuTemperature.Maximum);

        CpuMaxTemperatureTimeText.Text =
            FormatMaximumTime(
                summary.CpuTemperature.MaximumAtUtc);

        CpuAverageTemperatureText.Text =
            FormatTemperature(
                summary.CpuTemperature.Average);

        CpuDetailMaxTemperatureText.Text =
            FormatTemperature(
                summary.CpuTemperature.Maximum);

        CpuAverageLoadText.Text =
            FormatPercent(
                summary.CpuLoad.Average);

        CpuMaxLoadText.Text =
            FormatPercent(
                summary.CpuLoad.Maximum);

        RamMaxLoadText.Text =
            FormatPercent(
                summary.RamLoad.Maximum);

        RamMaxLoadTimeText.Text =
            FormatMaximumTime(
                summary.RamLoad.MaximumAtUtc);

        RamAverageLoadText.Text =
            FormatPercent(
                summary.RamLoad.Average);

        RamDetailMaxLoadText.Text =
            FormatPercent(
                summary.RamLoad.Maximum);

        UpdateGpuSummary(summary);
        UpdateStorageSummary(summary);
        RebuildGpuDetails(summary);
        RebuildStorageDetails(summary);
    }

    private void UpdateGpuSummary(
        StatisticsSummary summary)
    {
        GpuStatisticsSummary? hottestGpu =
            summary.Gpus
                .Where(
                    gpu =>
                        gpu.Temperature.Maximum.HasValue)
                .OrderByDescending(
                    gpu =>
                        gpu.Temperature.Maximum)
                .FirstOrDefault();

        if (hottestGpu == null)
        {
            GpuMaxTemperatureText.Text = "—";
            GpuMaxTemperatureDeviceText.Text = "Нет данных";
            return;
        }

        GpuMaxTemperatureText.Text =
            FormatTemperature(
                hottestGpu.Temperature.Maximum);

        GpuMaxTemperatureDeviceText.Text =
            $"{hottestGpu.DeviceName} • " +
            $"{FormatMaximumTime(hottestGpu.Temperature.MaximumAtUtc)}";
    }

    private void UpdateStorageSummary(
        StatisticsSummary summary)
    {
        StorageStatisticsSummary? hottestStorage =
            summary.StorageDevices
                .Where(
                    storage =>
                        storage.Temperature.Maximum.HasValue)
                .OrderByDescending(
                    storage =>
                        storage.Temperature.Maximum)
                .FirstOrDefault();

        if (hottestStorage == null)
        {
            StorageMaxTemperatureText.Text = "—";
            StorageMaxTemperatureDeviceText.Text = "Нет данных";
            return;
        }

        StorageMaxTemperatureText.Text =
            FormatTemperature(
                hottestStorage.Temperature.Maximum);

        StorageMaxTemperatureDeviceText.Text =
            $"{hottestStorage.DeviceName} • " +
            $"{FormatMaximumTime(hottestStorage.Temperature.MaximumAtUtc)}";
    }

    private void RebuildGpuDetails(
        StatisticsSummary summary)
    {
        PrepareDetailsGrid(
            GpuDetailsPanel,
            summary.Gpus.Count);

        if (summary.Gpus.Count == 0)
        {
            Border emptyCard =
                CreateEmptyMessage(
                    "За выбранный период данных GPU нет.");

            Grid.SetColumnSpan(
                emptyCard,
                3);

            GpuDetailsPanel.Children.Add(
                emptyCard);

            return;
        }

        for (int i = 0;
             i < summary.Gpus.Count;
             i++)
        {
            GpuStatisticsSummary gpu =
                summary.Gpus[i];

            Border card =
                CreateCard();

            StackPanel root =
                new();

            root.Children.Add(
                CreateDeviceTitle(
                    gpu.DeviceName));

            Grid values =
                CreateValuesGrid();

            AddMetric(
                values,
                0,
                0,
                "Температура / средняя",
                FormatTemperature(
                    gpu.Temperature.Average));

            AddMetric(
                values,
                0,
                1,
                "Температура / максимум",
                FormatTemperature(
                    gpu.Temperature.Maximum));

            AddMetric(
                values,
                1,
                0,
                "Загрузка / средняя",
                FormatPercent(
                    gpu.Load.Average));

            AddMetric(
                values,
                1,
                1,
                "Загрузка / максимум",
                FormatPercent(
                    gpu.Load.Maximum));

            AddMetric(
                values,
                2,
                0,
                "Hot Spot / максимум",
                FormatTemperature(
                    gpu.HotSpotTemperature.Maximum));

            AddMetric(
                values,
                2,
                1,
                "VRAM / максимум",
                FormatTemperature(
                    gpu.MemoryTemperature.Maximum));

            root.Children.Add(values);

            card.Child = root;

            AddDetailsCard(
                GpuDetailsPanel,
                card,
                i);
        }
    }

    private void RebuildStorageDetails(
        StatisticsSummary summary)
    {
        PrepareDetailsGrid(
            StorageDetailsPanel,
            summary.StorageDevices.Count);

        if (summary.StorageDevices.Count == 0)
        {
            Border emptyCard =
                CreateEmptyMessage(
                    "За выбранный период данных накопителей нет.");

            Grid.SetColumnSpan(
                emptyCard,
                3);

            StorageDetailsPanel.Children.Add(
                emptyCard);

            return;
        }

        for (int i = 0;
             i < summary.StorageDevices.Count;
             i++)
        {
            StorageStatisticsSummary storage =
                summary.StorageDevices[i];

            Border card =
                CreateCard();

            Grid grid =
                new();

            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(
                            1,
                            GridUnitType.Star)
                });

            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        GridLength.Auto
                });

            StackPanel information =
                new();

            information.Children.Add(
                CreateDeviceTitle(
                    storage.DeviceName));

            TextBlock maximumTime =
                new()
                {
                    Text =
                        $"Максимум зафиксирован: " +
                        $"{FormatMaximumTime(storage.Temperature.MaximumAtUtc)}",
                    FontSize = 10,
                    Margin =
                        new Thickness(
                            0, 5, 12, 0),
                    TextWrapping =
                        TextWrapping.Wrap
                };

            maximumTime.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            information.Children.Add(
                maximumTime);

            StackPanel values =
                new()
                {
                    Orientation =
                        Orientation.Horizontal,
                    VerticalAlignment =
                        VerticalAlignment.Center
                };

            values.Children.Add(
                CreateCompactMetric(
                    "СРЕДНЯЯ",
                    FormatTemperature(
                        storage.Temperature.Average)));

            values.Children.Add(
                CreateCompactMetric(
                    "МАКСИМУМ",
                    FormatTemperature(
                        storage.Temperature.Maximum),
                    new Thickness(
                        20, 0, 0, 0)));

            Grid.SetColumn(
                information,
                0);

            Grid.SetColumn(
                values,
                1);

            grid.Children.Add(
                information);

            grid.Children.Add(
                values);

            card.Child = grid;

            AddDetailsCard(
                StorageDetailsPanel,
                card,
                i);
        }
    }

    private static void PrepareDetailsGrid(
        Grid grid,
        int itemCount)
    {
        grid.Children.Clear();
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(12)
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        int rowCount =
            Math.Max(
                1,
                (int)Math.Ceiling(
                    itemCount / 2.0));

        for (int i = 0;
             i < rowCount;
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

    private static void AddDetailsCard(
        Grid grid,
        Border card,
        int index)
    {
        int row =
            index / 2;

        int column =
            index % 2 == 0
                ? 0
                : 2;

        card.Margin =
            new Thickness(
                0, 0, 0, 10);

        Grid.SetRow(
            card,
            row);

        Grid.SetColumn(
            card,
            column);

        grid.Children.Add(
            card);
    }

    private Border CreateCard()
    {
        Border card =
            new()
            {
                Padding =
                    new Thickness(18),
                BorderThickness =
                    new Thickness(1),
                HorizontalAlignment =
                    HorizontalAlignment.Stretch
            };

        card.SetResourceReference(
            Border.BackgroundProperty,
            "SettingsCardBrush");

        card.SetResourceReference(
            Border.BorderBrushProperty,
            "SettingsCardBorderBrush");

        object? radius =
            TryFindResource(
                "PanelCornerRadius");

        if (radius is CornerRadius cornerRadius)
        {
            card.CornerRadius =
                cornerRadius;
        }

        return card;
    }

    private TextBlock CreateDeviceTitle(
        string text)
    {
        TextBlock title =
            new()
            {
                Text = text,
                FontSize = 14,
                FontWeight =
                    FontWeights.Bold,
                TextTrimming =
                    TextTrimming.CharacterEllipsis,
                Margin =
                    new Thickness(
                        0, 0, 0, 12)
            };

        title.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        return title;
    }

    private static Grid CreateValuesGrid()
    {
        Grid grid =
            new();

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        for (int i = 0; i < 3; i++)
        {
            grid.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Auto
                });
        }

        return grid;
    }

    private void AddMetric(
        Grid grid,
        int row,
        int column,
        string label,
        string value)
    {
        StackPanel panel =
            new()
            {
                Margin =
                    new Thickness(
                        column == 0 ? 0 : 10,
                        row == 0 ? 0 : 12,
                        column == 0 ? 10 : 0,
                        0)
            };

        TextBlock labelText =
            new()
            {
                Text = label,
                FontSize = 10,
                FontWeight =
                    FontWeights.SemiBold
            };

        labelText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        TextBlock valueText =
            new()
            {
                Text = value,
                FontSize = 15,
                FontWeight =
                    FontWeights.SemiBold,
                Margin =
                    new Thickness(
                        0, 4, 0, 0)
            };

        valueText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        panel.Children.Add(
            labelText);

        panel.Children.Add(
            valueText);

        Grid.SetRow(
            panel,
            row);

        Grid.SetColumn(
            panel,
            column);

        grid.Children.Add(
            panel);
    }

    private StackPanel CreateCompactMetric(
        string label,
        string value,
        Thickness? margin = null)
    {
        StackPanel panel =
            new()
            {
                Margin =
                    margin ??
                    new Thickness(0)
            };

        TextBlock labelText =
            new()
            {
                Text = label,
                FontSize = 9,
                FontWeight =
                    FontWeights.SemiBold,
                HorizontalAlignment =
                    HorizontalAlignment.Right
            };

        labelText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        TextBlock valueText =
            new()
            {
                Text = value,
                FontSize = 16,
                FontWeight =
                    FontWeights.Bold,
                HorizontalAlignment =
                    HorizontalAlignment.Right,
                Margin =
                    new Thickness(
                        0, 3, 0, 0)
            };

        valueText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        panel.Children.Add(
            labelText);

        panel.Children.Add(
            valueText);

        return panel;
    }

    private Border CreateEmptyMessage(
        string text)
    {
        Border card =
            CreateCard();

        TextBlock message =
            new()
            {
                Text = text,
                FontSize = 11
            };

        message.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        card.Child = message;

        return card;
    }

    private void BuildExtendedSections()
    {
        if (_extendedSectionsBuilt)
            return;

        if (StatisticsScrollViewer.Content
            is not StackPanel root)
        {
            return;
        }

        _extendedSectionsBuilt = true;

        StorageDetailsPanel.Margin =
            new Thickness(
                0, 0, 0, 20);

        TextBlock recordsTitle =
            CreateDynamicSectionTitle(
                "РЕКОРДЫ ЗА ВСЁ ВРЕМЯ");

        root.Children.Add(
            recordsTitle);

        _recordsPanel =
            new Grid
            {
                Margin =
                    new Thickness(
                        0, 0, 0, 20)
            };

        root.Children.Add(
            _recordsPanel);

        TextBlock eventsTitle =
            CreateDynamicSectionTitle(
                "СОБЫТИЯ WARNING / CRITICAL");

        root.Children.Add(
            eventsTitle);

        Border eventsCard =
            CreateCard();

        eventsCard.Margin =
            new Thickness(
                0, 0, 0, 14);

        StackPanel eventsRoot =
            new();

        _eventCountsText =
            new TextBlock
            {
                Text =
                    "WARNING: 0   •   CRITICAL: 0",
                FontSize = 13,
                FontWeight =
                    FontWeights.Bold,
                Margin =
                    new Thickness(
                        0, 0, 0, 12)
            };

        _eventCountsText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        TextBlock eventsCaption =
            new()
            {
                Text =
                    "Последние события за выбранный период",
                FontSize = 10,
                Margin =
                    new Thickness(
                        0, 0, 0, 8)
            };

        eventsCaption.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        _eventsListPanel =
            new StackPanel();

        eventsRoot.Children.Add(
            _eventCountsText);

        eventsRoot.Children.Add(
            eventsCaption);

        eventsRoot.Children.Add(
            _eventsListPanel);

        eventsCard.Child =
            eventsRoot;

        root.Children.Add(
            eventsCard);

        Border clearCard =
            CreateCard();

        clearCard.Margin =
            new Thickness(
                0, 0, 0, 8);

        Grid clearGrid =
            new();

        clearGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        clearGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        StackPanel clearText =
            new();

        TextBlock clearTitle =
            new()
            {
                Text =
                    "ОЧИСТКА СТАТИСТИКИ",
                FontSize = 12,
                FontWeight =
                    FontWeights.Bold
            };

        clearTitle.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        TextBlock clearDescription =
            new()
            {
                Text =
                    "Удаляет историю датчиков, журнал событий и рекорды. Настройки Thermiqra не затрагиваются.",
                FontSize = 10,
                TextWrapping =
                    TextWrapping.Wrap,
                Margin =
                    new Thickness(
                        0, 4, 18, 0)
            };

        clearDescription.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        clearText.Children.Add(
            clearTitle);

        clearText.Children.Add(
            clearDescription);

        Button clearButton =
            new()
            {
                Content =
                    "Очистить статистику",
                MinWidth = 150,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        clearButton.Click +=
            ClearStatisticsButton_Click;

        Grid.SetColumn(
            clearText,
            0);

        Grid.SetColumn(
            clearButton,
            1);

        clearGrid.Children.Add(
            clearText);

        clearGrid.Children.Add(
            clearButton);

        clearCard.Child =
            clearGrid;

        root.Children.Add(
            clearCard);
    }

    private TextBlock CreateDynamicSectionTitle(
        string text)
    {
        TextBlock title =
            new()
            {
                Text = text,
                FontSize = 15,
                FontWeight =
                    FontWeights.Bold,
                Margin =
                    new Thickness(
                        0, 0, 0, 10)
            };

        title.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        object? font =
            TryFindResource(
                "DisplayFontFamily");

        if (font is FontFamily fontFamily)
        {
            title.FontFamily =
                fontFamily;
        }

        return title;
    }

    private void RefreshExtendedSections()
    {
        if (!_extendedSectionsBuilt)
            return;

        RebuildAllTimeRecords();
        RebuildAlertEvents();
    }

    private void RebuildAllTimeRecords()
    {
        if (_recordsPanel == null)
            return;

        List<AllTimeRecord> records =
            _statistics.GetAllTimeRecords();

        PrepareDetailsGrid(
            _recordsPanel,
            records.Count);

        if (records.Count == 0)
        {
            Border emptyCard =
                CreateEmptyMessage(
                    "Рекордов пока нет.");

            Grid.SetColumnSpan(
                emptyCard,
                3);

            _recordsPanel.Children.Add(
                emptyCard);

            return;
        }

        for (int i = 0;
             i < records.Count;
             i++)
        {
            AllTimeRecord record =
                records[i];

            Border card =
                CreateCard();

            Grid content =
                new();

            content.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(
                            1,
                            GridUnitType.Star)
                });

            content.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        GridLength.Auto
                });

            StackPanel information =
                new();

            information.Children.Add(
                CreateDeviceTitle(
                    GetRecordDisplayName(
                        record)));

            TextBlock time =
                new()
                {
                    Text =
                        $"Зафиксирован: " +
                        $"{record.TimestampUtc.ToLocalTime():dd.MM.yyyy HH:mm}",
                    FontSize = 10
                };

            time.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            information.Children.Add(
                time);

            TextBlock value =
                new()
                {
                    Text =
                        FormatRecordValue(
                            record),
                    FontSize = 20,
                    FontWeight =
                        FontWeights.Bold,
                    VerticalAlignment =
                        VerticalAlignment.Center,
                    Margin =
                        new Thickness(
                            18, 0, 0, 0)
                };

            value.SetResourceReference(
                TextBlock.ForegroundProperty,
                "AccentBrush");

            Grid.SetColumn(
                information,
                0);

            Grid.SetColumn(
                value,
                1);

            content.Children.Add(
                information);

            content.Children.Add(
                value);

            card.Child =
                content;

            AddDetailsCard(
                _recordsPanel,
                card,
                i);
        }
    }

    private void RebuildAlertEvents()
    {
        if (_eventsListPanel == null ||
            _eventCountsText == null)
        {
            return;
        }

        AlertEventCounts counts =
            _statistics.GetAlertEventCounts(
                _currentPeriod);

        _eventCountsText.Text =
            $"WARNING: {counts.WarningCount}   •   " +
            $"CRITICAL: {counts.CriticalCount}";

        _eventsListPanel.Children.Clear();

        List<StatisticsAlertEvent> events =
            _statistics.GetAlertEvents(
                _currentPeriod,
                30);

        if (events.Count == 0)
        {
            TextBlock empty =
                new()
                {
                    Text =
                        "За выбранный период событий не зафиксировано.",
                    FontSize = 11
                };

            empty.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            _eventsListPanel.Children.Add(
                empty);

            return;
        }

        foreach (StatisticsAlertEvent alertEvent in
                 events)
        {
            Border row =
                new()
                {
                    Padding =
                        new Thickness(
                            0, 8, 0, 8),
                    BorderThickness =
                        new Thickness(
                            0, 0, 0, 1)
                };

            row.SetResourceReference(
                Border.BorderBrushProperty,
                "BorderBrush");

            Grid grid =
                new();

            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(145)
                });

            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(92)
                });

            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(
                            1,
                            GridUnitType.Star)
                });

            TextBlock time =
                new()
                {
                    Text =
                        alertEvent.TimestampUtc
                            .ToLocalTime()
                            .ToString(
                                "dd.MM.yyyy HH:mm"),
                    FontSize = 10,
                    VerticalAlignment =
                        VerticalAlignment.Center
                };

            time.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            TextBlock level =
                new()
                {
                    Text =
                        alertEvent.Level ==
                        StatisticsAlertLevel.Critical
                            ? "CRITICAL"
                            : "WARNING",
                    FontSize = 10,
                    FontWeight =
                        FontWeights.Bold,
                    VerticalAlignment =
                        VerticalAlignment.Center,
                    Foreground =
                        alertEvent.Level ==
                        StatisticsAlertLevel.Critical
                            ? UiBrushes.Load(95)
                            : UiBrushes.Load(80)
                };

            TextBlock description =
                new()
                {
                    Text =
                        $"{alertEvent.DeviceName} • " +
                        $"{alertEvent.Subject}: " +
                        $"{alertEvent.Value:F0} {alertEvent.Unit}",
                    FontSize = 11,
                    TextWrapping =
                        TextWrapping.Wrap,
                    VerticalAlignment =
                        VerticalAlignment.Center
                };

            description.SetResourceReference(
                TextBlock.ForegroundProperty,
                "PrimaryTextBrush");

            Grid.SetColumn(
                time,
                0);

            Grid.SetColumn(
                level,
                1);

            Grid.SetColumn(
                description,
                2);

            grid.Children.Add(
                time);

            grid.Children.Add(
                level);

            grid.Children.Add(
                description);

            row.Child =
                grid;

            _eventsListPanel.Children.Add(
                row);
        }
    }

    private void ClearStatisticsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        MessageBoxResult result =
            MessageBox.Show(
                this,
                "Удалить всю накопленную статистику, журнал событий и рекорды?\n\nНастройки Thermiqra останутся без изменений.",
                "Thermiqra — Очистка статистики",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

        if (result !=
            MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _statistics.ClearAll();

            LoadPeriod(
                _currentPeriod);

            StatusText.Text =
                "Статистика очищена. Новые точки начнут накапливаться автоматически.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Не удалось очистить статистику: {ex.Message}",
                "Thermiqra — Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static string GetRecordDisplayName(
        AllTimeRecord record)
    {
        return record.Category switch
        {
            "CPU" =>
                "CPU / МАКС. ТЕМПЕРАТУРА",
            "GPU" =>
                $"GPU / {record.DeviceName}",
            "RAM" =>
                "RAM / МАКС. ЗАГРУЗКА",
            "STORAGE" =>
                $"НАКОПИТЕЛЬ / {record.DeviceName}",
            _ =>
                record.DeviceName
        };
    }

    private static string FormatRecordValue(
        AllTimeRecord record)
    {
        return record.Unit == "%"
            ? $"{record.Value:F1} %"
            : $"{record.Value:F1} °C";
    }

    private enum ChartSourceKind
    {
        Cpu,
        Ram,
        Gpu,
        Storage
    }

    private sealed class ChartSourceOption
    {
        public ChartSourceOption(
            string key,
            ChartSourceKind kind,
            string deviceId,
            string displayName)
        {
            Key = key;
            Kind = kind;
            DeviceId = deviceId;
            DisplayName = displayName;
        }

        public string Key { get; }

        public ChartSourceKind Kind { get; }

        public string DeviceId { get; }

        public string DisplayName { get; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    private sealed class ChartMetricOption
    {
        public ChartMetricOption(
            StatisticsChartMetric metric,
            string displayName,
            string unit)
        {
            Metric = metric;
            DisplayName = displayName;
            Unit = unit;
        }

        public StatisticsChartMetric Metric { get; }

        public string DisplayName { get; }

        public string Unit { get; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    private static string FormatTemperature(
        double? value)
    {
        return value.HasValue
            ? $"{value.Value:F1} °C"
            : "—";
    }

    private static string FormatPercent(
        double? value)
    {
        return value.HasValue
            ? $"{value.Value:F1} %"
            : "—";
    }

    private static string FormatMaximumTime(
        DateTimeOffset? valueUtc)
    {
        return valueUtc.HasValue
            ? valueUtc.Value
                .ToLocalTime()
                .ToString(
                    "dd.MM.yyyy HH:mm")
            : "Нет данных";
    }
}
