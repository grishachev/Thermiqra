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

    private async void SummaryReportButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SaveFileDialog dialog =
            new()
            {
                Title =
                    SettingsService.L(
                        "Сохранить сводный отчёт Thermiqra",
                        "Save Thermiqra summary report"),

                FileName =
                    $"Thermiqra_Summary_" +
                    $"{GetPeriodFileName(_currentPeriod)}_" +
                    $"{DateTime.Now:yyyy-MM-dd_HH-mm}.html",

                DefaultExt =
                    ".html",

                AddExtension =
                    true,

                Filter =
                    SettingsService.L(
                        "HTML-отчёт (*.html)|*.html|Все файлы (*.*)|*.*",
                        "HTML report (*.html)|*.html|All files (*.*)|*.*")
            };

        bool? result =
            dialog.ShowDialog(
                this);

        if (result != true)
            return;

        StatisticsPeriod period =
            _currentPeriod;

        bool isRussian =
            SettingsService.IsRussian;

        string themeName =
            SettingsService
                .Current
                .ThemeName;

        HardwareSnapshot? currentSnapshot =
            _currentSnapshot;

        SummaryReportButton.IsEnabled =
            false;

        StatusText.Text =
            SettingsService.L(
                "Создание сводного отчёта...",
                "Creating summary report...");

        try
        {
            await Task.Run(
                () =>
                    WriteSummaryReportHtml(
                        dialog.FileName,
                        period,
                        isRussian,
                        themeName,
                        currentSnapshot));

            StatusText.Text =
                SettingsService.L(
                    "Сводный HTML-отчёт сохранён.",
                    "Summary HTML report saved.");

            MessageBoxResult openResult =
                MessageBox.Show(
                    this,
                    SettingsService.L(
                        "Сводный отчёт сохранён.\n\nОткрыть его сейчас?",
                        "The summary report has been saved.\n\nOpen it now?"),
                    "Thermiqra",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information,
                    MessageBoxResult.Yes);

            if (openResult ==
                MessageBoxResult.Yes)
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName =
                            dialog.FileName,

                        UseShellExecute =
                            true
                    });
            }
        }
        catch (Exception ex)
        {
            StatusText.Text =
                SettingsService.L(
                    $"Не удалось создать отчёт: {ex.Message}",
                    $"Failed to create the report: {ex.Message}");

            MessageBox.Show(
                this,
                SettingsService.L(
                    $"Не удалось создать сводный отчёт.\n\n{ex.Message}",
                    $"Failed to create the summary report.\n\n{ex.Message}"),
                "Thermiqra",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            SummaryReportButton.IsEnabled =
                true;
        }
    }


    private void WriteSummaryReportHtml(
        string fileName,
        StatisticsPeriod period,
        bool isRussian,
        string themeName,
        HardwareSnapshot? currentSnapshot)
    {
        StatisticsSummary summary =
            _statistics.GetSummary(
                period);

        AlertEventCounts alertCounts =
            _statistics.GetAlertEventCounts(
                period);

        List<StatisticsAlertEvent> recentEvents =
            _statistics.GetAlertEvents(
                period,
                10);

        GpuStatisticsSummary? hottestGpu =
            summary.Gpus
                .Where(
                    gpu =>
                        gpu.Temperature.Maximum.HasValue)
                .OrderByDescending(
                    gpu =>
                        gpu.Temperature.Maximum)
                .FirstOrDefault();

        StorageStatisticsSummary? hottestStorage =
            summary.StorageDevices
                .Where(
                    storage =>
                        storage.Temperature.Maximum.HasValue)
                .OrderByDescending(
                    storage =>
                        storage.Temperature.Maximum)
                .FirstOrDefault();

        StatisticsChartSeries cpuTemperature =
            _statistics.GetChartSeries(
                period,
                StatisticsChartMetric.CpuTemperature,
                null,
                260);

        StatisticsChartSeries cpuLoad =
            _statistics.GetChartSeries(
                period,
                StatisticsChartMetric.CpuLoad,
                null,
                260);

        StatisticsChartSeries ramLoad =
            _statistics.GetChartSeries(
                period,
                StatisticsChartMetric.RamLoad,
                null,
                260);

        StatisticsChartSeries? gpuTemperature =
            hottestGpu == null
                ? null
                : _statistics.GetChartSeries(
                    period,
                    StatisticsChartMetric.GpuTemperature,
                    hottestGpu.DeviceId,
                    260);

        ReportPalette palette =
            GetReportPalette(
                themeName);

        string html =
            BuildSummaryReportHtml(
                period,
                isRussian,
                currentSnapshot,
                summary,
                alertCounts,
                recentEvents,
                hottestGpu,
                hottestStorage,
                cpuTemperature,
                gpuTemperature,
                cpuLoad,
                ramLoad,
                palette);

        File.WriteAllText(
            fileName,
            html,
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier:
                true));
    }


    private static string BuildSummaryReportHtml(
        StatisticsPeriod period,
        bool isRussian,
        HardwareSnapshot? currentSnapshot,
        StatisticsSummary summary,
        AlertEventCounts alertCounts,
        IReadOnlyList<StatisticsAlertEvent> recentEvents,
        GpuStatisticsSummary? hottestGpu,
        StorageStatisticsSummary? hottestStorage,
        StatisticsChartSeries cpuTemperature,
        StatisticsChartSeries? gpuTemperature,
        StatisticsChartSeries cpuLoad,
        StatisticsChartSeries ramLoad,
        ReportPalette palette)
    {
        StringBuilder html =
            new();

        string periodName =
            GetReportPeriodName(
                period,
                isRussian);

        string periodRange =
            $"{FormatReportDate(summary.StartUtc, isRussian)} — " +
            $"{FormatReportDate(summary.EndUtc, isRussian)}";

        string statusClass;
        string statusTitle;
        string statusText;

        if (summary.SampleCount == 0)
        {
            statusClass = "neutral";

            statusTitle =
                isRussian
                    ? "Пока недостаточно данных"
                    : "Not enough data yet";

            statusText =
                isRussian
                    ? "За выбранный период Thermiqra ещё не накопила сохранённых точек мониторинга."
                    : "Thermiqra has not accumulated saved monitoring samples for the selected period yet.";
        }
        else if (alertCounts.CriticalCount > 0)
        {
            statusClass = "critical";

            statusTitle =
                isRussian
                    ? "Требует внимания"
                    : "Attention required";

            statusText =
                isRussian
                    ? $"За выбранный период зарегистрировано критических событий: {alertCounts.CriticalCount}."
                    : $"Critical events recorded during the selected period: {alertCounts.CriticalCount}.";
        }
        else if (alertCounts.WarningCount > 0)
        {
            statusClass = "warning";

            statusTitle =
                isRussian
                    ? "Есть предупреждения"
                    : "Warnings recorded";

            statusText =
                isRussian
                    ? $"Критических событий нет, предупреждений: {alertCounts.WarningCount}."
                    : $"No critical events were recorded. Warnings: {alertCounts.WarningCount}.";
        }
        else
        {
            statusClass = "ok";

            statusTitle =
                isRussian
                    ? "Без зарегистрированных предупреждений"
                    : "No recorded warnings";

            statusText =
                isRussian
                    ? "За выбранный период Thermiqra не зарегистрировала превышений настроенных Warning/Critical-порогов."
                    : "Thermiqra did not record any configured Warning/Critical threshold events during the selected period.";
        }

        string cpuName =
            string.IsNullOrWhiteSpace(
                currentSnapshot?.Cpu.Name)
                ? "CPU"
                : currentSnapshot!.Cpu.Name;

        string ramDescription =
            currentSnapshot?.Memory.TotalGb is float totalRam
                ? isRussian
                    ? $"{totalRam:F1} ГБ установлено"
                    : $"{totalRam:F1} GB installed"
                : isRussian
                    ? "Объём не определён"
                    : "Capacity unavailable";

        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine(
            $"<html lang=\"{(isRussian ? "ru" : "en")}\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");

        html.AppendLine(
            $"<title>{Html(isRussian ? "Thermiqra — Сводный отчёт" : "Thermiqra — Summary report")}</title>");

        html.AppendLine("<style>");
        html.AppendLine("*{box-sizing:border-box}");
        html.AppendLine(
            $"body{{margin:0;background:{palette.Page};color:{palette.Text};font-family:Segoe UI,Arial,sans-serif;line-height:1.45}}");
        html.AppendLine(".page{max-width:1180px;margin:0 auto;padding:40px 28px 56px}");
        html.AppendLine(
            $".hero{{position:relative;overflow:hidden;background:linear-gradient(135deg,{palette.Card},{palette.CardAlt});border:1px solid {palette.Border};border-radius:24px;padding:34px;box-shadow:0 18px 55px rgba(0,0,0,.28)}}");
        html.AppendLine(
            $".hero:after{{content:'';position:absolute;width:280px;height:280px;border-radius:50%;right:-90px;top:-110px;background:{palette.Accent};opacity:.10;filter:blur(8px)}}");
        html.AppendLine(
            $".brand{{font-size:14px;font-weight:800;letter-spacing:.22em;color:{palette.Accent};text-transform:uppercase}}");
        html.AppendLine("h1{font-size:36px;margin:10px 0 6px;line-height:1.12}");
        html.AppendLine(
            $".sub{{color:{palette.Muted};font-size:15px}}");
        html.AppendLine(
            $".status{{margin-top:24px;border-radius:16px;padding:18px 20px;border:1px solid {palette.Border};background:rgba(255,255,255,.035)}}");
        html.AppendLine(".status-title{font-size:20px;font-weight:800;margin-bottom:4px}");
        html.AppendLine(".status.ok{border-left:5px solid #58D68D}");
        html.AppendLine(".status.warning{border-left:5px solid #F5B041}");
        html.AppendLine(".status.critical{border-left:5px solid #EC7063}");
        html.AppendLine(".status.neutral{border-left:5px solid #95A5A6}");
        html.AppendLine(".grid{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:16px;margin-top:22px}");
        html.AppendLine(
            $".card{{background:{palette.Card};border:1px solid {palette.Border};border-radius:18px;padding:20px;min-height:158px}}");
        html.AppendLine(
            $".label{{font-size:12px;letter-spacing:.10em;text-transform:uppercase;color:{palette.Muted};font-weight:700}}");
        html.AppendLine(".value{font-size:29px;font-weight:800;margin-top:8px;line-height:1.1}");
        html.AppendLine(
            $".detail{{font-size:13px;color:{palette.Muted};margin-top:8px}}");
        html.AppendLine(
            $".section{{margin-top:24px;background:{palette.Card};border:1px solid {palette.Border};border-radius:20px;padding:24px}}");
        html.AppendLine("h2{font-size:21px;margin:0 0 5px}");
        html.AppendLine(
            $".section-note{{color:{palette.Muted};font-size:13px;margin-bottom:18px}}");
        html.AppendLine(
            $".chart{{background:{palette.Chart};border:1px solid {palette.Border};border-radius:16px;padding:12px;overflow:hidden}}");
        html.AppendLine(".chart svg{display:block;width:100%;height:auto}");
        html.AppendLine(
            $".legend{{display:flex;gap:18px;flex-wrap:wrap;color:{palette.Muted};font-size:12px;margin-top:10px}}");
        html.AppendLine(".legend span{display:flex;align-items:center;gap:7px}");
        html.AppendLine(".dot{width:10px;height:10px;border-radius:50%;display:inline-block}");
        html.AppendLine(
            $".insight{{padding:13px 15px;border-radius:12px;background:{palette.Chart};border:1px solid {palette.Border};margin:10px 0}}");
        html.AppendLine("table{width:100%;border-collapse:collapse;margin-top:12px;font-size:13px}");
        html.AppendLine(
            $"th{{text-align:left;color:{palette.Muted};font-size:11px;text-transform:uppercase;letter-spacing:.07em;padding:10px;border-bottom:1px solid {palette.Border}}}");
        html.AppendLine(
            $"td{{padding:11px 10px;border-bottom:1px solid {palette.Border}}}");
        html.AppendLine(
            $".footer{{color:{palette.Muted};text-align:center;font-size:12px;margin-top:24px}}");
        html.AppendLine("@media(max-width:900px){.grid{grid-template-columns:repeat(2,1fr)}h1{font-size:30px}}");
        html.AppendLine("@media(max-width:560px){.page{padding:18px 12px 32px}.grid{grid-template-columns:1fr}.hero{padding:24px}}");
        html.AppendLine("@media print{body{background:#fff;color:#111}.page{max-width:none;padding:0}.hero,.card,.section{box-shadow:none;break-inside:avoid}}");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<main class=\"page\">");

        html.AppendLine("<section class=\"hero\">");
        html.AppendLine("<div class=\"brand\">THERMIQRA</div>");
        html.AppendLine(
            $"<h1>{Html(isRussian ? "Понятная сводка о компьютере" : "Computer health summary")}</h1>");
        html.AppendLine(
            $"<div class=\"sub\">{Html(periodName)} · {Html(periodRange)} · " +
            $"{Html(isRussian ? "создан" : "generated")} {Html(DateTime.Now.ToString(isRussian ? "dd.MM.yyyy HH:mm" : "yyyy-MM-dd HH:mm"))}</div>");
        html.AppendLine(
            $"<div class=\"status {statusClass}\">");
        html.AppendLine(
            $"<div class=\"status-title\">{Html(statusTitle)}</div>");
        html.AppendLine(
            $"<div>{Html(statusText)}</div>");
        html.AppendLine("</div>");

        html.AppendLine("<div class=\"grid\">");

        AppendReportCard(
            html,
            isRussian ? "Процессор" : "Processor",
            FormatReportValue(summary.CpuTemperature.Maximum, "°C", isRussian),
            cpuName,
            BuildAverageMaximumDetail(summary.CpuTemperature, "°C", isRussian));

        AppendReportCard(
            html,
            isRussian ? "Видеокарта" : "Graphics",
            FormatReportValue(hottestGpu?.Temperature.Maximum, "°C", isRussian),
            hottestGpu?.DeviceName ?? (isRussian ? "Нет данных" : "No data"),
            BuildAverageMaximumDetail(hottestGpu?.Temperature, "°C", isRussian));

        AppendReportCard(
            html,
            isRussian ? "Оперативная память" : "Memory",
            FormatReportValue(summary.RamLoad.Maximum, "%", isRussian),
            ramDescription,
            BuildAverageMaximumDetail(summary.RamLoad, "%", isRussian));

        AppendReportCard(
            html,
            isRussian ? "Накопители" : "Storage",
            FormatReportValue(hottestStorage?.Temperature.Maximum, "°C", isRussian),
            hottestStorage?.DeviceName ?? (isRussian ? "Нет данных" : "No data"),
            BuildAverageMaximumDetail(hottestStorage?.Temperature, "°C", isRussian));

        html.AppendLine("</div>");
        html.AppendLine("</section>");

        html.AppendLine("<section class=\"section\">");
        html.AppendLine(
            $"<h2>{Html(isRussian ? "Температуры во времени" : "Temperatures over time")}</h2>");
        html.AppendLine(
            $"<div class=\"section-note\">{Html(isRussian ? "График показывает сохранённые точки Thermiqra за выбранный период." : "The chart shows Thermiqra's saved samples for the selected period.")}</div>");

        List<ReportLine> temperatureLines =
            new()
            {
                new ReportLine(
                    "CPU",
                    palette.Accent,
                    cpuTemperature.Points)
            };

        if (gpuTemperature != null &&
            hottestGpu != null)
        {
            temperatureLines.Add(
                new ReportLine(
                    $"GPU — {hottestGpu.DeviceName}",
                    palette.Accent2,
                    gpuTemperature.Points));
        }

        html.AppendLine(
            BuildSvgLineChart(
                temperatureLines,
                summary.StartUtc,
                summary.EndUtc,
                null,
                null,
                "°C",
                isRussian,
                palette));

        html.AppendLine(
            BuildChartLegend(
                temperatureLines));

        html.AppendLine("</section>");

        html.AppendLine("<section class=\"section\">");
        html.AppendLine(
            $"<h2>{Html(isRussian ? "Загрузка CPU и RAM" : "CPU and RAM load")}</h2>");
        html.AppendLine(
            $"<div class=\"section-note\">{Html(isRussian ? "Проценты показывают, насколько активно использовались процессор и оперативная память." : "Percent values show how actively the processor and memory were used.")}</div>");

        List<ReportLine> loadLines =
            new()
            {
                new ReportLine(
                    "CPU",
                    palette.Accent,
                    cpuLoad.Points),

                new ReportLine(
                    "RAM",
                    palette.Accent2,
                    ramLoad.Points)
            };

        html.AppendLine(
            BuildSvgLineChart(
                loadLines,
                summary.StartUtc,
                summary.EndUtc,
                0,
                100,
                "%",
                isRussian,
                palette));

        html.AppendLine(
            BuildChartLegend(
                loadLines));

        html.AppendLine("</section>");

        html.AppendLine("<section class=\"section\">");
        html.AppendLine(
            $"<h2>{Html(isRussian ? "Что можно понять из отчёта" : "What this report tells you")}</h2>");
        html.AppendLine(
            $"<div class=\"insight\"><strong>{Html(isRussian ? "Сохранённых точек:" : "Saved samples:")}</strong> {summary.SampleCount:N0}</div>");

        if (summary.SampleCount == 0)
        {
            html.AppendLine(
                $"<div class=\"insight\">{Html(isRussian ? "Оставьте Thermiqra работать некоторое время — история сохраняется периодически и постепенно наполнит этот отчёт." : "Leave Thermiqra running for a while. History is saved periodically and will gradually populate this report.")}</div>");
        }
        else if (alertCounts.TotalCount == 0)
        {
            html.AppendLine(
                $"<div class=\"insight\">{Html(isRussian ? "За этот период журнал Thermiqra не содержит Warning/Critical-событий по настроенным порогам." : "For this period, the Thermiqra event log contains no Warning/Critical events for the configured thresholds.")}</div>");
        }
        else
        {
            html.AppendLine(
                $"<div class=\"insight\"><strong>Warning:</strong> {alertCounts.WarningCount} &nbsp;&nbsp; <strong>Critical:</strong> {alertCounts.CriticalCount}</div>");
            html.AppendLine(
                $"<div class=\"insight\">{Html(isRussian ? "Это не означает поломку само по себе. Ниже указаны зарегистрированные события и время, когда они произошли." : "This does not by itself mean that hardware is failing. The recorded events and their times are listed below.")}</div>");
        }

        if (summary.CpuTemperature.MaximumAtUtc.HasValue)
        {
            html.AppendLine(
                $"<div class=\"insight\">{Html(isRussian ? "Максимальная температура CPU:" : "Maximum CPU temperature:")} " +
                $"<strong>{Html(FormatReportValue(summary.CpuTemperature.Maximum, "°C", isRussian))}</strong> · " +
                $"{Html(FormatReportDate(summary.CpuTemperature.MaximumAtUtc.Value, isRussian))}</div>");
        }

        if (hottestGpu?.Temperature.MaximumAtUtc is DateTimeOffset gpuMaxAt)
        {
            html.AppendLine(
                $"<div class=\"insight\">{Html(isRussian ? "Максимальная температура выбранного GPU:" : "Maximum temperature of the highlighted GPU:")} " +
                $"<strong>{Html(FormatReportValue(hottestGpu.Temperature.Maximum, "°C", isRussian))}</strong> · " +
                $"{Html(FormatReportDate(gpuMaxAt, isRussian))}</div>");
        }

        html.AppendLine("</section>");

        html.AppendLine("<section class=\"section\">");
        html.AppendLine(
            $"<h2>{Html(isRussian ? "Последние события периода" : "Latest events in this period")}</h2>");

        if (recentEvents.Count == 0)
        {
            html.AppendLine(
                $"<div class=\"section-note\">{Html(isRussian ? "Warning/Critical-событий за выбранный период нет." : "There are no Warning/Critical events for the selected period.")}</div>");
        }
        else
        {
            html.AppendLine("<table>");
            html.AppendLine("<thead><tr>");
            html.AppendLine($"<th>{Html(isRussian ? "Время" : "Time")}</th>");
            html.AppendLine($"<th>{Html(isRussian ? "Уровень" : "Level")}</th>");
            html.AppendLine($"<th>{Html(isRussian ? "Устройство" : "Device")}</th>");
            html.AppendLine($"<th>{Html(isRussian ? "Показатель" : "Metric")}</th>");
            html.AppendLine($"<th>{Html(isRussian ? "Значение" : "Value")}</th>");
            html.AppendLine("</tr></thead><tbody>");

            foreach (StatisticsAlertEvent alertEvent
                     in recentEvents)
            {
                html.AppendLine("<tr>");
                html.AppendLine($"<td>{Html(FormatReportDate(alertEvent.TimestampUtc, isRussian))}</td>");
                html.AppendLine($"<td>{Html(alertEvent.Level == StatisticsAlertLevel.Critical ? "CRITICAL" : "WARNING")}</td>");
                html.AppendLine($"<td>{Html(alertEvent.DeviceName)}</td>");
                html.AppendLine($"<td>{Html(GetReadableAlertSubject(alertEvent.Subject, isRussian))}</td>");
                html.AppendLine($"<td>{Html(FormatReportValue(alertEvent.Value, alertEvent.Unit, isRussian))}</td>");
                html.AppendLine("</tr>");
            }

            html.AppendLine("</tbody></table>");
        }

        html.AppendLine("</section>");

        html.AppendLine(
            $"<div class=\"footer\">{Html(isRussian ? "Отчёт создан локально Thermiqra. Данные автоматически никуда не отправляются." : "This report was created locally by Thermiqra. The data is not sent anywhere automatically.")}</div>");

        html.AppendLine("</main>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }


    private static void AppendReportCard(
        StringBuilder html,
        string label,
        string value,
        string name,
        string detail)
    {
        html.AppendLine("<div class=\"card\">");
        html.AppendLine($"<div class=\"label\">{Html(label)}</div>");
        html.AppendLine($"<div class=\"value\">{Html(value)}</div>");
        html.AppendLine($"<div class=\"detail\"><strong>{Html(name)}</strong></div>");
        html.AppendLine($"<div class=\"detail\">{Html(detail)}</div>");
        html.AppendLine("</div>");
    }


    private static string BuildAverageMaximumDetail(
        MetricStatistics? metric,
        string unit,
        bool isRussian)
    {
        if (metric == null ||
            (!metric.Average.HasValue &&
             !metric.Maximum.HasValue))
        {
            return isRussian
                ? "За период нет данных"
                : "No data for this period";
        }

        return isRussian
            ? $"Среднее: {FormatReportValue(metric.Average, unit, true)} · Максимум: {FormatReportValue(metric.Maximum, unit, true)}"
            : $"Average: {FormatReportValue(metric.Average, unit, false)} · Maximum: {FormatReportValue(metric.Maximum, unit, false)}";
    }


    private static string BuildSvgLineChart(
        IReadOnlyList<ReportLine> lines,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        double? fixedMinimum,
        double? fixedMaximum,
        string unit,
        bool isRussian,
        ReportPalette palette)
    {
        const double width = 1000;
        const double height = 300;
        const double left = 64;
        const double right = 24;
        const double top = 22;
        const double bottom = 48;

        double plotWidth =
            width - left - right;

        double plotHeight =
            height - top - bottom;

        List<double> allValues =
            lines
                .SelectMany(
                    line =>
                        line.Points)
                .Select(
                    point =>
                        point.Value)
                .Where(
                    value =>
                        !double.IsNaN(value) &&
                        !double.IsInfinity(value))
                .ToList();

        if (allValues.Count == 0)
        {
            return
                $"<div class=\"chart\" style=\"padding:42px;text-align:center;color:{palette.Muted}\">" +
                $"{Html(isRussian ? "За выбранный период данных для графика пока нет." : "No chart data is available for the selected period yet.")}" +
                "</div>";
        }

        double minimum =
            fixedMinimum ??
            allValues.Min();

        double maximum =
            fixedMaximum ??
            allValues.Max();

        if (!fixedMinimum.HasValue ||
            !fixedMaximum.HasValue)
        {
            double range =
                maximum - minimum;

            double padding =
                range > 0
                    ? Math.Max(
                        2,
                        range * 0.12)
                    : Math.Max(
                        2,
                        Math.Abs(maximum) * 0.08);

            if (!fixedMinimum.HasValue)
            {
                minimum =
                    Math.Max(
                        0,
                        minimum - padding);
            }

            if (!fixedMaximum.HasValue)
            {
                maximum +=
                    padding;
            }
        }

        if (maximum <= minimum)
        {
            maximum =
                minimum + 1;
        }

        long startUnix =
            startUtc.ToUnixTimeSeconds();

        long endUnix =
            endUtc.ToUnixTimeSeconds();

        if (endUnix <= startUnix)
        {
            endUnix =
                startUnix + 1;
        }

        StringBuilder svg =
            new();

        svg.AppendLine("<div class=\"chart\">");
        svg.AppendLine(
            $"<svg viewBox=\"0 0 {width:0} {height:0}\" role=\"img\" aria-label=\"chart\">");
        svg.AppendLine(
            $"<rect x=\"0\" y=\"0\" width=\"{width:0}\" height=\"{height:0}\" rx=\"12\" fill=\"{palette.Chart}\"/>");

        for (int i = 0;
             i <= 4;
             i++)
        {
            double ratio =
                i / 4.0;

            double y =
                top +
                plotHeight * ratio;

            double value =
                maximum -
                (maximum - minimum) *
                ratio;

            svg.AppendLine(
                $"<line x1=\"{left.ToString("0.##", CultureInfo.InvariantCulture)}\" " +
                $"y1=\"{y.ToString("0.##", CultureInfo.InvariantCulture)}\" " +
                $"x2=\"{(width - right).ToString("0.##", CultureInfo.InvariantCulture)}\" " +
                $"y2=\"{y.ToString("0.##", CultureInfo.InvariantCulture)}\" " +
                $"stroke=\"{palette.Grid}\" stroke-width=\"1\"/>");

            svg.AppendLine(
                $"<text x=\"{(left - 10).ToString("0.##", CultureInfo.InvariantCulture)}\" " +
                $"y=\"{(y + 4).ToString("0.##", CultureInfo.InvariantCulture)}\" " +
                $"text-anchor=\"end\" fill=\"{palette.Muted}\" font-size=\"11\">" +
                $"{Html($"{value:0.#}{unit}")}</text>");
        }

        foreach (ReportLine line
                 in lines)
        {
            if (line.Points.Count == 0)
                continue;

            StringBuilder points =
                new();

            foreach (StatisticsChartPoint point
                     in line.Points)
            {
                long unix =
                    point.TimestampUtc
                        .ToUnixTimeSeconds();

                double xRatio =
                    (unix - startUnix) /
                    (double)(endUnix - startUnix);

                xRatio =
                    Math.Clamp(
                        xRatio,
                        0,
                        1);

                double yRatio =
                    (point.Value - minimum) /
                    (maximum - minimum);

                yRatio =
                    Math.Clamp(
                        yRatio,
                        0,
                        1);

                double x =
                    left +
                    plotWidth *
                    xRatio;

                double y =
                    top +
                    plotHeight *
                    (1 - yRatio);

                if (points.Length > 0)
                    points.Append(' ');

                points.Append(
                    x.ToString(
                        "0.##",
                        CultureInfo.InvariantCulture));

                points.Append(',');

                points.Append(
                    y.ToString(
                        "0.##",
                        CultureInfo.InvariantCulture));
            }

            svg.AppendLine(
                $"<polyline points=\"{points}\" fill=\"none\" stroke=\"{line.Color}\" stroke-width=\"3\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>");
        }

        svg.AppendLine(
            $"<text x=\"{left.ToString("0.##", CultureInfo.InvariantCulture)}\" y=\"{(height - 15).ToString("0.##", CultureInfo.InvariantCulture)}\" fill=\"{palette.Muted}\" font-size=\"11\">" +
            $"{Html(FormatChartAxisDate(startUtc, isRussian))}</text>");

        svg.AppendLine(
            $"<text x=\"{(width - right).ToString("0.##", CultureInfo.InvariantCulture)}\" y=\"{(height - 15).ToString("0.##", CultureInfo.InvariantCulture)}\" text-anchor=\"end\" fill=\"{palette.Muted}\" font-size=\"11\">" +
            $"{Html(FormatChartAxisDate(endUtc, isRussian))}</text>");

        svg.AppendLine("</svg>");
        svg.AppendLine("</div>");

        return svg.ToString();
    }


    private static string BuildChartLegend(
        IReadOnlyList<ReportLine> lines)
    {
        StringBuilder legend =
            new();

        legend.AppendLine("<div class=\"legend\">");

        foreach (ReportLine line
                 in lines)
        {
            legend.AppendLine(
                $"<span><i class=\"dot\" style=\"background:{line.Color}\"></i>{Html(line.Label)}</span>");
        }

        legend.AppendLine("</div>");

        return legend.ToString();
    }


    private static string GetReadableAlertSubject(
        string subject,
        bool isRussian)
    {
        return subject switch
        {
            "CPU temperature" or "Температура CPU" =>
                isRussian ? "Температура CPU" : "CPU temperature",

            "GPU temperature" or "Температура GPU" =>
                isRussian ? "Температура GPU" : "GPU temperature",

            "Storage temperature" or "Температура накопителя" =>
                isRussian ? "Температура накопителя" : "Storage temperature",

            "Disk usage" or "Заполнение диска" =>
                isRussian ? "Заполнение диска" : "Disk usage",

            _ =>
                subject
        };
    }


    private static string GetReportPeriodName(
        StatisticsPeriod period,
        bool isRussian)
    {
        return period switch
        {
            StatisticsPeriod.Today =>
                isRussian ? "Сегодня" : "Today",

            StatisticsPeriod.Last24Hours =>
                isRussian ? "Последние 24 часа" : "Last 24 hours",

            StatisticsPeriod.Last7Days =>
                isRussian ? "Последние 7 дней" : "Last 7 days",

            StatisticsPeriod.Last30Days =>
                isRussian ? "Последние 30 дней" : "Last 30 days",

            _ =>
                isRussian ? "Выбранный период" : "Selected period"
        };
    }


    private static string FormatReportDate(
        DateTimeOffset valueUtc,
        bool isRussian)
    {
        DateTimeOffset local =
            valueUtc.ToLocalTime();

        return local.ToString(
            isRussian
                ? "dd.MM.yyyy HH:mm"
                : "yyyy-MM-dd HH:mm");
    }


    private static string FormatChartAxisDate(
        DateTimeOffset valueUtc,
        bool isRussian)
    {
        DateTimeOffset local =
            valueUtc.ToLocalTime();

        return local.ToString(
            isRussian
                ? "dd.MM HH:mm"
                : "MM-dd HH:mm");
    }


    private static string FormatReportValue(
        double? value,
        string unit,
        bool isRussian)
    {
        if (!value.HasValue)
            return "—";

        return FormatReportValue(
            value.Value,
            unit,
            isRussian);
    }


    private static string FormatReportValue(
        double value,
        string unit,
        bool isRussian)
    {
        CultureInfo culture =
            isRussian
                ? CultureInfo.GetCultureInfo(
                    "ru-RU")
                : CultureInfo.InvariantCulture;

        return
            $"{value.ToString("0.#", culture)} {unit}";
    }


    private static string Html(
        string? value)
    {
        return WebUtility.HtmlEncode(
            value ??
            string.Empty);
    }


    private static ReportPalette GetReportPalette(
        string themeName)
    {
        return themeName switch
        {
            "SteamPunk" =>
                new ReportPalette(
                    "#17110C",
                    "#251A11",
                    "#302015",
                    "#F39A32",
                    "#DCC08A",
                    "#F7E8D0",
                    "#BDA58A",
                    "#59402A",
                    "#20160F"),

            "FrostCore" =>
                new ReportPalette(
                    "#07131B",
                    "#0D202A",
                    "#102B37",
                    "#76E7FF",
                    "#B8C7FF",
                    "#ECFAFF",
                    "#91ABB5",
                    "#294B59",
                    "#091B24"),

            "MilitaryOps" =>
                new ReportPalette(
                    "#10140D",
                    "#1A2114",
                    "#222B18",
                    "#9BCF4A",
                    "#D7B86E",
                    "#F1F4E8",
                    "#A4AE91",
                    "#3C4A2D",
                    "#141A10"),

            _ =>
                new ReportPalette(
                    "#071117",
                    "#0D1C24",
                    "#102A34",
                    "#2ED9FF",
                    "#8B7CFF",
                    "#EAFBFF",
                    "#89A8B3",
                    "#20424D",
                    "#091820")
        };
    }


    private sealed class ReportPalette
    {
        public ReportPalette(
            string page,
            string card,
            string cardAlt,
            string accent,
            string accent2,
            string text,
            string muted,
            string border,
            string chart)
        {
            Page = page;
            Card = card;
            CardAlt = cardAlt;
            Accent = accent;
            Accent2 = accent2;
            Text = text;
            Muted = muted;
            Border = border;
            Grid = border;
            Chart = chart;
        }

        public string Page { get; }
        public string Card { get; }
        public string CardAlt { get; }
        public string Accent { get; }
        public string Accent2 { get; }
        public string Text { get; }
        public string Muted { get; }
        public string Border { get; }
        public string Grid { get; }
        public string Chart { get; }
    }


    private sealed class ReportLine
    {
        public ReportLine(
            string label,
            string color,
            IReadOnlyList<StatisticsChartPoint> points)
        {
            Label = label;
            Color = color;
            Points = points;
        }

        public string Label { get; }
        public string Color { get; }
        public IReadOnlyList<StatisticsChartPoint> Points { get; }
    }


    private async void ExportCsvButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SaveFileDialog dialog =
            new()
            {
                Title =
                    SettingsService.L(
                        "Экспорт статистики Thermiqra в CSV",
                        "Export Thermiqra statistics to CSV"),

                FileName =
                    $"Thermiqra_Statistics_" +
                    $"{GetPeriodFileName(_currentPeriod)}_" +
                    $"{DateTime.Now:yyyy-MM-dd_HH-mm}.csv",

                DefaultExt =
                    ".csv",

                AddExtension =
                    true,

                Filter =
                    SettingsService.L(
                        "CSV-файл (*.csv)|*.csv|Все файлы (*.*)|*.*",
                        "CSV file (*.csv)|*.csv|All files (*.*)|*.*")
            };

        bool? result =
            dialog.ShowDialog(
                this);

        if (result != true)
            return;

        bool isRussian =
            SettingsService.IsRussian;

        StatisticsPeriod period =
            _currentPeriod;

        ExportCsvButton.IsEnabled =
            false;

        StatusText.Text =
            SettingsService.L(
                "Экспорт статистики...",
                "Exporting statistics...");

        try
        {
            int rowCount =
                await Task.Run(
                    () =>
                        WriteStatisticsCsv(
                            dialog.FileName,
                            period,
                            isRussian));

            StatusText.Text =
                rowCount > 0
                    ? SettingsService.L(
                        $"CSV сохранён. Экспортировано строк: {rowCount:N0}.",
                        $"CSV saved. Exported rows: {rowCount:N0}.")
                    : SettingsService.L(
                        "CSV сохранён, но за выбранный период точек нет.",
                        "CSV saved, but there are no samples for the selected period.");
        }
        catch (Exception ex)
        {
            StatusText.Text =
                SettingsService.L(
                    $"Не удалось экспортировать CSV: {ex.Message}",
                    $"Failed to export CSV: {ex.Message}");

            MessageBox.Show(
                this,
                SettingsService.L(
                    $"Не удалось экспортировать статистику.\n\n{ex.Message}",
                    $"Failed to export statistics.\n\n{ex.Message}"),
                "Thermiqra",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            ExportCsvButton.IsEnabled =
                true;
        }
    }


    private int WriteStatisticsCsv(
        string fileName,
        StatisticsPeriod period,
        bool isRussian)
    {
        CultureInfo valueCulture =
            isRussian
                ? CultureInfo.GetCultureInfo(
                    "ru-RU")
                : CultureInfo.InvariantCulture;

        using StreamWriter writer =
            new(
                fileName,
                append: false,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier:
                    true));

        string[] headers =
            isRussian
                ? new[]
                {
                    "Время",
                    "Категория",
                    "Устройство",
                    "ID устройства",
                    "Показатель",
                    "Значение",
                    "Единица"
                }
                : new[]
                {
                    "Time",
                    "Category",
                    "Device",
                    "Device ID",
                    "Metric",
                    "Value",
                    "Unit"
                };

        writer.WriteLine(
            string.Join(
                ";",
                headers.Select(
                    EscapeCsv)));

        return _statistics.ReadExportRows(
            period,
            row =>
            {
                string[] values =
                {
                    FormatExportDate(
                        row.TimestampUtc,
                        isRussian),

                    GetExportCategory(
                        row.Category,
                        isRussian),

                    row.DeviceName,

                    row.DeviceId,

                    GetExportMetric(
                        row.Metric,
                        isRussian),

                    row.Value.ToString(
                        "0.###",
                        valueCulture),

                    row.Unit
                };

                writer.WriteLine(
                    string.Join(
                        ";",
                        values.Select(
                            EscapeCsv)));
            });
    }


    private static string EscapeCsv(
        string value)
    {
        value ??=
            string.Empty;

        bool needsQuotes =
            value.Contains(';') ||
            value.Contains('"') ||
            value.Contains('\r') ||
            value.Contains('\n');

        if (value.Contains('"'))
        {
            value =
                value.Replace(
                    "\"",
                    "\"\"");
        }

        return needsQuotes
            ? $"\"{value}\""
            : value;
    }


    private static string GetExportCategory(
        string category,
        bool isRussian)
    {
        return category switch
        {
            "CPU" =>
                isRussian
                    ? "Процессор"
                    : "CPU",

            "GPU" =>
                isRussian
                    ? "Видеокарта"
                    : "GPU",

            "RAM" =>
                isRussian
                    ? "Оперативная память"
                    : "RAM",

            "STORAGE" =>
                isRussian
                    ? "Накопитель"
                    : "Storage",

            _ =>
                category
        };
    }


    private static string GetExportMetric(
        string metric,
        bool isRussian)
    {
        return metric switch
        {
            "Temperature" =>
                isRussian
                    ? "Температура"
                    : "Temperature",

            "Load" =>
                isRussian
                    ? "Загрузка"
                    : "Load",

            "HotSpotTemperature" =>
                "Hot Spot",

            "MemoryTemperature" =>
                isRussian
                    ? "Температура памяти GPU"
                    : "GPU memory temperature",

            "MemoryLoad" =>
                isRussian
                    ? "Загрузка памяти GPU"
                    : "GPU memory load",

            _ =>
                metric
        };
    }


    private static string FormatExportDate(
        DateTimeOffset valueUtc,
        bool isRussian)
    {
        DateTimeOffset local =
            valueUtc.ToLocalTime();

        return local.ToString(
            isRussian
                ? "dd.MM.yyyy HH:mm:ss"
                : "yyyy-MM-dd HH:mm:ss");
    }


    private static string GetPeriodFileName(
        StatisticsPeriod period)
    {
        return period switch
        {
            StatisticsPeriod.Today =>
                "Today",

            StatisticsPeriod.Last24Hours =>
                "24h",

            StatisticsPeriod.Last7Days =>
                "7d",

            StatisticsPeriod.Last30Days =>
                "30d",

            _ =>
                "Period"
        };
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

        TextBlock analyticsTitle =
            CreateDynamicSectionTitle(
                SettingsService.L(
                    "АНАЛИТИКА ИСТОРИИ",
                    "HISTORY ANALYTICS"));

        root.Children.Add(
            analyticsTitle);

        _historyAnalyticsPanel =
            new StackPanel
            {
                Margin =
                    new Thickness(
                        0, 0, 0, 20)
            };

        root.Children.Add(
            _historyAnalyticsPanel);

        TextBlock recordsTitle =
            CreateDynamicSectionTitle(
                SettingsService.L(
                "РЕКОРДЫ ЗА ВСЁ ВРЕМЯ",
                "ALL-TIME RECORDS"));

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
                SettingsService.L(
                "СОБЫТИЯ WARNING / CRITICAL",
                "WARNING / CRITICAL EVENTS"));

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
                    SettingsService.L(
                    "Последние события за выбранный период",
                    "Latest events for the selected period"),
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
                    SettingsService.L(
                    "ОЧИСТКА СТАТИСТИКИ",
                    "CLEAR STATISTICS"),
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
                    SettingsService.L(
                    "Удаляет историю датчиков, журнал событий и рекорды. Настройки Thermiqra не затрагиваются.",
                    "Deletes sensor history, the event log, and records. Thermiqra settings are not affected."),
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
                    SettingsService.L(
                    "Очистить статистику",
                    "Clear statistics"),
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

        RebuildHistoryAnalytics();
        RebuildAllTimeRecords();
        RebuildAlertEvents();
    }


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
