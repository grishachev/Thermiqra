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

    private StackPanel? _historyAnalyticsPanel;

    private StackPanel? _eventsListPanel;

    private TextBlock? _eventCountsText;

    private bool _extendedSectionsBuilt;

    public StatisticsWindow(
        StatisticsService statistics,
        HardwareSnapshot? currentSnapshot = null)
    {
        InitializeComponent();

        SettingsService.ApplyLanguageToWindow(
            this);

        ExportCsvButton.Content =
            SettingsService.L(
                "Экспорт CSV",
                "Export CSV");

        SummaryReportButton.Content =
            SettingsService.L(
                "Сводный отчёт",
                "Summary report");

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
                ? SettingsService.L(
                    "Модель процессора: нет данных",
                    "Processor model: no data")
                : cpuName;

        float? totalRam =
            _currentSnapshot?.Memory.TotalGb;

        RamHardwareInfoText.Text =
            totalRam.HasValue
                ? SettingsService.L(
                    $"Установлено: {totalRam.Value:F1} ГБ",
                    $"Installed: {totalRam.Value:F1} GB")
                : SettingsService.L(
                    "Общий объём памяти: нет данных",
                    "Total memory: no data");
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
                    ? SettingsService.L(
                        "История загружена. Точки сохраняются примерно раз в минуту.",
                        "History loaded. Samples are saved approximately once per minute.")
                    : SettingsService.L(
                        "За выбранный период сохранённых точек пока нет.",
                        "No saved samples are available for the selected period yet.");
        }
        catch (Exception ex)
        {
            StatusText.Text =
                SettingsService.L(
                    $"Не удалось прочитать статистику: {ex.Message}",
                    $"Failed to read statistics: {ex.Message}");
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

}
