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


}
