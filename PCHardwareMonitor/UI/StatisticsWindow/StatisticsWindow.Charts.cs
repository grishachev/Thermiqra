using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PCHardwareMonitor;

public partial class StatisticsWindow
{
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
                ? SettingsService.L(
                    $"RAM — {totalRam.Value:F1} ГБ",
                    $"RAM — {totalRam.Value:F1} GB")
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
                    SettingsService.L(
                        $"Накопитель — {storage.DeviceName}",
                        $"Storage — {storage.DeviceName}")));
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
                        SettingsService.L(
                            "Температура",
                            "Temperature"),
                        "°C"),
                    new ChartMetricOption(
                        StatisticsChartMetric.CpuLoad,
                        SettingsService.L(
                            "Загрузка",
                            "Load"),
                        "%")
                },

            ChartSourceKind.Ram =>
                new[]
                {
                    new ChartMetricOption(
                        StatisticsChartMetric.RamLoad,
                        SettingsService.L(
                            "Загрузка",
                            "Load"),
                        "%")
                },

            ChartSourceKind.Gpu =>
                new[]
                {
                    new ChartMetricOption(
                        StatisticsChartMetric.GpuTemperature,
                        SettingsService.L(
                            "Температура",
                            "Temperature"),
                        "°C"),
                    new ChartMetricOption(
                        StatisticsChartMetric.GpuLoad,
                        SettingsService.L(
                            "Загрузка",
                            "Load"),
                        "%"),
                    new ChartMetricOption(
                        StatisticsChartMetric.GpuHotSpotTemperature,
                        "Hot Spot",
                        "°C"),
                    new ChartMetricOption(
                        StatisticsChartMetric.GpuMemoryTemperature,
                        SettingsService.L(
                            "Температура VRAM",
                            "VRAM temperature"),
                        "°C")
                },

            ChartSourceKind.Storage =>
                new[]
                {
                    new ChartMetricOption(
                        StatisticsChartMetric.StorageTemperature,
                        SettingsService.L(
                            "Температура",
                            "Temperature"),
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
                SettingsService.L(
                    $"Не удалось построить график: {ex.Message}",
                    $"Failed to build chart: {ex.Message}");
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
                SettingsService.L(
                    "За выбранный период данных для графика пока нет.",
                    "No chart data is available for the selected period yet.");
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
                    localTime.ToString(SettingsService.IsRussian ? "dd.MM.yyyy HH:mm:ss" : "yyyy-MM-dd HH:mm:ss"),
                StatisticsPeriod.Last7Days =>
                    localTime.ToString(SettingsService.IsRussian ? "dd.MM.yyyy HH:mm" : "yyyy-MM-dd HH:mm"),
                StatisticsPeriod.Last30Days =>
                    localTime.ToString(SettingsService.IsRussian ? "dd.MM.yyyy HH:mm" : "yyyy-MM-dd HH:mm"),
                _ =>
                    localTime.ToString(SettingsService.IsRussian ? "dd.MM.yyyy HH:mm" : "yyyy-MM-dd HH:mm")
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
                localTime.ToString(SettingsService.IsRussian ? "dd.MM" : "MM-dd"),
            StatisticsPeriod.Last30Days =>
                localTime.ToString(SettingsService.IsRussian ? "dd.MM" : "MM-dd"),
            _ =>
                localTime.ToString(SettingsService.IsRussian ? "dd.MM" : "MM-dd")
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

}
