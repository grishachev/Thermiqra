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
            SettingsService.IsRussian
                ? $"{localStart:dd.MM.yyyy HH:mm} — " +
                  $"{localEnd:dd.MM.yyyy HH:mm}"
                : $"{localStart:yyyy-MM-dd HH:mm} — " +
                  $"{localEnd:yyyy-MM-dd HH:mm}";

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
            GpuMaxTemperatureDeviceText.Text = SettingsService.L(
                "Нет данных",
                "No data");
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
            StorageMaxTemperatureDeviceText.Text = SettingsService.L(
                "Нет данных",
                "No data");
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
                    SettingsService.L(
                    "За выбранный период данных GPU нет.",
                    "No GPU data is available for the selected period."));

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
                SettingsService.L(
                    "Температура / средняя",
                    "Temperature / average"),
                FormatTemperature(
                    gpu.Temperature.Average));

            AddMetric(
                values,
                0,
                1,
                SettingsService.L(
                    "Температура / максимум",
                    "Temperature / maximum"),
                FormatTemperature(
                    gpu.Temperature.Maximum));

            AddMetric(
                values,
                1,
                0,
                SettingsService.L(
                    "Загрузка / средняя",
                    "Load / average"),
                FormatPercent(
                    gpu.Load.Average));

            AddMetric(
                values,
                1,
                1,
                SettingsService.L(
                    "Загрузка / максимум",
                    "Load / maximum"),
                FormatPercent(
                    gpu.Load.Maximum));

            AddMetric(
                values,
                2,
                0,
                SettingsService.L(
                    "Hot Spot / максимум",
                    "Hot Spot / maximum"),
                FormatTemperature(
                    gpu.HotSpotTemperature.Maximum));

            AddMetric(
                values,
                2,
                1,
                SettingsService.L(
                    "VRAM / максимум",
                    "VRAM / maximum"),
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
                    SettingsService.L(
                    "За выбранный период данных накопителей нет.",
                    "No storage data is available for the selected period."));

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
                        SettingsService.L(
                        "Максимум зафиксирован: ",
                        "Maximum recorded: ") +
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
                    SettingsService.L(
                    "СРЕДНЯЯ",
                    "AVERAGE"),
                    FormatTemperature(
                        storage.Temperature.Average)));

            values.Children.Add(
                CreateCompactMetric(
                    SettingsService.L(
                    "МАКСИМУМ",
                    "MAXIMUM"),
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

    private static string FormatLocalDateTime(
        DateTimeOffset valueUtc)
    {
        DateTimeOffset local =
            valueUtc.ToLocalTime();

        return local.ToString(
            SettingsService.IsRussian
                ? "dd.MM.yyyy HH:mm"
                : "yyyy-MM-dd HH:mm");
    }


    private static string FormatMaximumTime(
        DateTimeOffset? valueUtc)
    {
        return valueUtc.HasValue
            ? FormatLocalDateTime(
                valueUtc.Value)
            : SettingsService.L(
                "Нет данных",
                "No data");
    }
}
