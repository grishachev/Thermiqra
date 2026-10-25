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
                    SettingsService.L(
                    "Рекордов пока нет.",
                    "No records yet."));

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
                        SettingsService.L(
                        "Зафиксирован: ",
                        "Recorded: ") +
                        FormatLocalDateTime(
                            record.TimestampUtc),
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
                        SettingsService.L(
                        "За выбранный период событий не зафиксировано.",
                        "No events were recorded for the selected period."),
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
                        FormatLocalDateTime(
                            alertEvent.TimestampUtc),
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
                        $"{SettingsService.TranslateKnown(alertEvent.Subject)}: " +
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
                SettingsService.L(
                    "Удалить всю накопленную статистику, журнал событий и рекорды?\n\nНастройки Thermiqra останутся без изменений.",
                    "Delete all accumulated statistics, the event log, and records?\n\nThermiqra settings will remain unchanged."),
                SettingsService.L(
                    "Thermiqra — Очистка статистики",
                    "Thermiqra — Clear Statistics"),
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
                SettingsService.L(
                    "Статистика очищена. Новые точки начнут накапливаться автоматически.",
                    "Statistics cleared. New samples will start accumulating automatically.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                SettingsService.L(
                    $"Не удалось очистить статистику: {ex.Message}",
                    $"Failed to clear statistics: {ex.Message}"),
                SettingsService.L(
                    "Thermiqra — Ошибка",
                    "Thermiqra — Error"),
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
                SettingsService.L(
                    "CPU / МАКС. ТЕМПЕРАТУРА",
                    "CPU / MAX TEMPERATURE"),
            "GPU" =>
                $"GPU / {record.DeviceName}",
            "RAM" =>
                SettingsService.L(
                    "RAM / МАКС. ЗАГРУЗКА",
                    "RAM / MAX LOAD"),
            "STORAGE" =>
                SettingsService.L(
                    $"НАКОПИТЕЛЬ / {record.DeviceName}",
                    $"STORAGE / {record.DeviceName}"),
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

}
