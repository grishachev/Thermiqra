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
    private void RebuildHistoryAnalytics()
    {
        if (_historyAnalyticsPanel == null)
            return;

        _historyAnalyticsPanel.Children.Clear();

        HistoryAnalyticsResult analytics =
            _statistics.GetHistoryAnalytics(
                _currentPeriod);

        Grid overview =
            new();

        overview.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        overview.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(12)
            });

        overview.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        Border comparisonCard =
            CreateCard();

        comparisonCard.Margin =
            new Thickness(
                0, 0, 0, 10);

        StackPanel comparisonRoot =
            new();

        comparisonRoot.Children.Add(
            CreateAnalyticsTitle(
                SettingsService.L(
                    "СРАВНЕНИЕ С ПРЕДЫДУЩИМ ПЕРИОДОМ",
                    "COMPARE WITH PREVIOUS PERIOD")));

        TextBlock comparisonRange =
            CreateAnalyticsCaption(
                SettingsService.L(
                    $"Сейчас: {FormatAnalyticsRange(analytics.CurrentStartUtc, analytics.CurrentEndUtc)}\n" +
                    $"До этого: {FormatAnalyticsRange(analytics.PreviousStartUtc, analytics.PreviousEndUtc)}",
                    $"Current: {FormatAnalyticsRange(analytics.CurrentStartUtc, analytics.CurrentEndUtc)}\n" +
                    $"Previous: {FormatAnalyticsRange(analytics.PreviousStartUtc, analytics.PreviousEndUtc)}"));

        comparisonRoot.Children.Add(
            comparisonRange);

        comparisonRoot.Children.Add(
            CreateAnalyticsLine(
                BuildTrendLine(
                    SettingsService.L(
                        "CPU / средняя температура",
                        "CPU / average temperature"),
                    analytics.PreviousCpuTemperature.Average,
                    analytics.CurrentCpuTemperature.Average,
                    "°C",
                    3.0,
                    false)));

        comparisonRoot.Children.Add(
            CreateAnalyticsLine(
                BuildTrendLine(
                    SettingsService.L(
                        "CPU / средняя загрузка",
                        "CPU / average load"),
                    analytics.PreviousCpuLoad.Average,
                    analytics.CurrentCpuLoad.Average,
                    "%",
                    5.0,
                    true)));

        comparisonRoot.Children.Add(
            CreateAnalyticsLine(
                BuildTrendLine(
                    SettingsService.L(
                        "RAM / средняя загрузка",
                        "RAM / average load"),
                    analytics.PreviousRamLoad.Average,
                    analytics.CurrentRamLoad.Average,
                    "%",
                    5.0,
                    true)));

        comparisonRoot.Children.Add(
            CreateAnalyticsLine(
                SettingsService.L(
                    $"События: WARNING {analytics.PreviousAlerts.WarningCount} → {analytics.CurrentAlerts.WarningCount}, " +
                    $"CRITICAL {analytics.PreviousAlerts.CriticalCount} → {analytics.CurrentAlerts.CriticalCount}",
                    $"Events: WARNING {analytics.PreviousAlerts.WarningCount} → {analytics.CurrentAlerts.WarningCount}, " +
                    $"CRITICAL {analytics.PreviousAlerts.CriticalCount} → {analytics.CurrentAlerts.CriticalCount}")));

        comparisonRoot.Children.Add(
            CreateAnalyticsCaption(
                SettingsService.L(
                    $"Сохранённых точек: {analytics.PreviousSampleCount:N0} → {analytics.CurrentSampleCount:N0}",
                    $"Saved samples: {analytics.PreviousSampleCount:N0} → {analytics.CurrentSampleCount:N0}")));

        comparisonCard.Child =
            comparisonRoot;

        Grid.SetColumn(
            comparisonCard,
            0);

        overview.Children.Add(
            comparisonCard);

        Border weeklyCard =
            CreateCard();

        weeklyCard.Margin =
            new Thickness(
                0, 0, 0, 10);

        StackPanel weeklyRoot =
            new();

        weeklyRoot.Children.Add(
            CreateAnalyticsTitle(
                SettingsService.L(
                    "СВОДКА ЗА ПОСЛЕДНИЕ 7 ДНЕЙ",
                    "LAST 7 DAYS SUMMARY")));

        weeklyRoot.Children.Add(
            CreateAnalyticsCaption(
                FormatAnalyticsRange(
                    analytics.Last7Days.StartUtc,
                    analytics.Last7Days.EndUtc)));

        weeklyRoot.Children.Add(
            CreateAnalyticsLine(
                SettingsService.L(
                    $"Дней с сохранёнными данными: {analytics.Last7Days.DaysWithSamples}",
                    $"Calendar days with saved data: {analytics.Last7Days.DaysWithSamples}")));

        weeklyRoot.Children.Add(
            CreateAnalyticsLine(
                SettingsService.L(
                    $"Точек мониторинга: {analytics.Last7Days.SampleCount:N0}",
                    $"Monitoring samples: {analytics.Last7Days.SampleCount:N0}")));

        weeklyRoot.Children.Add(
            CreateAnalyticsLine(
                SettingsService.L(
                    $"CPU температура: средняя {FormatAnalyticsValue(analytics.Last7Days.CpuTemperature.Average, "°C")}, " +
                    $"максимум {FormatAnalyticsValue(analytics.Last7Days.CpuTemperature.Maximum, "°C")}",
                    $"CPU temperature: average {FormatAnalyticsValue(analytics.Last7Days.CpuTemperature.Average, "°C")}, " +
                    $"maximum {FormatAnalyticsValue(analytics.Last7Days.CpuTemperature.Maximum, "°C")}")));

        weeklyRoot.Children.Add(
            CreateAnalyticsLine(
                SettingsService.L(
                    $"CPU загрузка: средняя {FormatAnalyticsValue(analytics.Last7Days.CpuLoad.Average, "%")}, " +
                    $"максимум {FormatAnalyticsValue(analytics.Last7Days.CpuLoad.Maximum, "%")}",
                    $"CPU load: average {FormatAnalyticsValue(analytics.Last7Days.CpuLoad.Average, "%")}, " +
                    $"maximum {FormatAnalyticsValue(analytics.Last7Days.CpuLoad.Maximum, "%")}")));

        weeklyRoot.Children.Add(
            CreateAnalyticsLine(
                SettingsService.L(
                    $"RAM загрузка: средняя {FormatAnalyticsValue(analytics.Last7Days.RamLoad.Average, "%")}, " +
                    $"максимум {FormatAnalyticsValue(analytics.Last7Days.RamLoad.Maximum, "%")}",
                    $"RAM load: average {FormatAnalyticsValue(analytics.Last7Days.RamLoad.Average, "%")}, " +
                    $"maximum {FormatAnalyticsValue(analytics.Last7Days.RamLoad.Maximum, "%")}")));

        weeklyRoot.Children.Add(
            CreateAnalyticsLine(
                $"WARNING: {analytics.Last7Days.Alerts.WarningCount}   •   " +
                $"CRITICAL: {analytics.Last7Days.Alerts.CriticalCount}"));

        weeklyCard.Child =
            weeklyRoot;

        Grid.SetColumn(
            weeklyCard,
            2);

        overview.Children.Add(
            weeklyCard);

        _historyAnalyticsPanel.Children.Add(
            overview);

        Border recurringCard =
            CreateCard();

        recurringCard.Margin =
            new Thickness(
                0, 0, 0, 2);

        StackPanel recurringRoot =
            new();

        recurringRoot.Children.Add(
            CreateAnalyticsTitle(
                SettingsService.L(
                    "ПОВТОРЯЮЩИЕСЯ WARNING / CRITICAL",
                    "REPEATING WARNING / CRITICAL")));

        recurringRoot.Children.Add(
            CreateAnalyticsCaption(
                SettingsService.L(
                    "Показываются одинаковые события, которые повторились два раза или чаще за выбранный период.",
                    "Shows identical events that occurred two or more times during the selected period.")));

        if (analytics.RecurringAlerts.Count == 0)
        {
            recurringRoot.Children.Add(
                CreateAnalyticsLine(
                    SettingsService.L(
                        "Повторяющихся событий за выбранный период не найдено.",
                        "No repeating events were found for the selected period.")));
        }
        else
        {
            foreach (RecurringAlertSummary recurring in
                     analytics.RecurringAlerts)
            {
                string level =
                    recurring.Level ==
                    StatisticsAlertLevel.Critical
                        ? "CRITICAL"
                        : "WARNING";

                string countText =
                    SettingsService.L(
                        $"{recurring.Count} раз",
                        $"{recurring.Count} time(s)");

                string subject =
                    SettingsService.TranslateKnown(
                        recurring.Subject);

                string line =
                    $"{level} • {recurring.DeviceName} • {subject} — " +
                    $"{countText}, " +
                    SettingsService.L(
                        $"максимум {FormatAnalyticsValue(recurring.MaximumValue, recurring.Unit)}, " +
                        $"последнее {FormatLocalDateTime(recurring.LastAtUtc)}",
                        $"maximum {FormatAnalyticsValue(recurring.MaximumValue, recurring.Unit)}, " +
                        $"latest {FormatLocalDateTime(recurring.LastAtUtc)}");

                recurringRoot.Children.Add(
                    CreateAnalyticsLine(
                        line));
            }
        }

        recurringCard.Child =
            recurringRoot;

        _historyAnalyticsPanel.Children.Add(
            recurringCard);
    }


    private TextBlock CreateAnalyticsTitle(
        string text)
    {
        TextBlock title =
            new()
            {
                Text =
                    text,

                FontSize =
                    12,

                FontWeight =
                    FontWeights.Bold,

                Margin =
                    new Thickness(
                        0, 0, 0, 8)
            };

        title.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        return title;
    }


    private TextBlock CreateAnalyticsCaption(
        string text)
    {
        TextBlock caption =
            new()
            {
                Text =
                    text,

                FontSize =
                    10,

                TextWrapping =
                    TextWrapping.Wrap,

                Margin =
                    new Thickness(
                        0, 0, 0, 10)
            };

        caption.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        return caption;
    }


    private TextBlock CreateAnalyticsLine(
        string text)
    {
        TextBlock line =
            new()
            {
                Text =
                    text,

                FontSize =
                    11,

                TextWrapping =
                    TextWrapping.Wrap,

                Margin =
                    new Thickness(
                        0, 4, 0, 4)
            };

        line.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        return line;
    }


    private static string BuildTrendLine(
        string label,
        double? previous,
        double? current,
        string unit,
        double meaningfulDelta,
        bool percentagePoints)
    {
        if (!previous.HasValue ||
            !current.HasValue)
        {
            return SettingsService.L(
                $"{label}: недостаточно данных для сравнения",
                $"{label}: not enough data to compare");
        }

        double delta =
            current.Value -
            previous.Value;

        string direction;

        if (Math.Abs(delta) <
            meaningfulDelta)
        {
            direction =
                SettingsService.L(
                    "изменение небольшое",
                    "small change");
        }
        else if (delta > 0)
        {
            direction =
                SettingsService.L(
                    "выше",
                    "higher");
        }
        else
        {
            direction =
                SettingsService.L(
                    "ниже",
                    "lower");
        }

        string deltaUnit =
            percentagePoints
                ? SettingsService.L(
                    "п.п.",
                    "pp")
                : unit;

        string deltaSign =
            delta > 0
                ? "+"
                : "";

        return
            $"{label}: " +
            $"{FormatAnalyticsValue(previous, unit)} → " +
            $"{FormatAnalyticsValue(current, unit)} " +
            $"({deltaSign}{FormatAnalyticsNumber(delta)} {deltaUnit}) • " +
            $"{direction}";
    }


    private static string FormatAnalyticsValue(
        double? value,
        string unit)
    {
        if (!value.HasValue)
            return "—";

        return
            $"{FormatAnalyticsNumber(value.Value)} {unit}";
    }


    private static string FormatAnalyticsValue(
        double value,
        string unit)
    {
        return
            $"{FormatAnalyticsNumber(value)} {unit}";
    }


    private static string FormatAnalyticsNumber(
        double value)
    {
        CultureInfo culture =
            SettingsService.IsRussian
                ? CultureInfo.GetCultureInfo(
                    "ru-RU")
                : CultureInfo.InvariantCulture;

        return value.ToString(
            "0.#",
            culture);
    }


    private static string FormatAnalyticsRange(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc)
    {
        DateTimeOffset start =
            startUtc.ToLocalTime();

        DateTimeOffset end =
            endUtc.ToLocalTime();

        string format =
            SettingsService.IsRussian
                ? "dd.MM HH:mm"
                : "MM-dd HH:mm";

        return
            $"{start.ToString(format)} — " +
            $"{end.ToString(format)}";
    }


}
