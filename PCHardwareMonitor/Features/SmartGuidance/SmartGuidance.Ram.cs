using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCHardwareMonitor;


public partial class SystemInfoWindow
{
    // ============================================================
    // АНАЛИЗ RAM
    // ============================================================

    private void RefreshRamSmartGuidance()
    {
        if (!_smartGuidanceInitialized ||
            _ramGuidanceStatusText == null ||
            _ramGuidanceCurrentText == null ||
            _ramGuidanceHistoryText == null ||
            _ramGuidanceConfigurationText == null ||
            _ramGuidancePlacementWarningText == null ||
            _ramGuidanceMeaningText == null ||
            _ramGuidanceActionText == null)
        {
            return;
        }

        HardwareSnapshot? currentSnapshot =
            (Owner as MainWindow)?
                .GetSmartGuidanceSnapshot();

        RamHistoryEvidence? history =
            ReadRamHistoryEvidence();

        double? currentLoad =
            GetCurrentRamLoad(
                currentSnapshot);

        _ramGuidanceCurrentText.Text =
            BuildCurrentRamText(
                currentSnapshot,
                currentLoad);

        _ramGuidanceHistoryText.Text =
            BuildRamHistoryText(
                history);

        _ramGuidanceConfigurationText.Text =
            BuildRamConfigurationText();

        ApplyRamPlacementGuidance();

        ApplyRamGuidanceConclusion(
            currentLoad,
            history);
    }


    private RamHistoryEvidence? ReadRamHistoryEvidence()
    {
        if (_statistics == null)
            return null;

        try
        {
            StatisticsSummary last24Hours =
                _statistics.GetSummary(
                    StatisticsPeriod.Last24Hours);

            StatisticsSummary last7Days =
                _statistics.GetSummary(
                    StatisticsPeriod.Last7Days);

            StatisticsChartSeries daySeries =
                _statistics.GetChartSeries(
                    StatisticsPeriod.Last24Hours,
                    StatisticsChartMetric.RamLoad,
                    maxPoints: 1200);

            StatisticsChartSeries weekSeries =
                _statistics.GetChartSeries(
                    StatisticsPeriod.Last7Days,
                    StatisticsChartMetric.RamLoad,
                    maxPoints: 1200);

            List<StatisticsChartPoint> dayPoints =
                daySeries.Points;

            List<StatisticsChartPoint> weekPoints =
                weekSeries.Points;

            int dayHighCount =
                dayPoints.Count(
                    point =>
                        point.Value >= 85);

            int weekHighCount =
                weekPoints.Count(
                    point =>
                        point.Value >= 85);

            int weekDaysWithSamples =
                weekPoints
                    .Select(
                        point =>
                            point.TimestampUtc
                                .ToLocalTime()
                                .Date)
                    .Distinct()
                    .Count();

            int weekDaysWithHighLoad =
                weekPoints
                    .Where(
                        point =>
                            point.Value >= 85)
                    .Select(
                        point =>
                            point.TimestampUtc
                                .ToLocalTime()
                                .Date)
                    .Distinct()
                    .Count();

            return new RamHistoryEvidence
            {
                Last24Hours =
                    last24Hours,

                Last7Days =
                    last7Days,

                DaySeriesPointCount =
                    dayPoints.Count,

                DayHighPointCount =
                    dayHighCount,

                WeekSeriesPointCount =
                    weekPoints.Count,

                WeekHighPointCount =
                    weekHighCount,

                WeekDaysWithSamples =
                    weekDaysWithSamples,

                WeekDaysWithHighLoad =
                    weekDaysWithHighLoad
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Не удалось получить данные Smart Guidance для RAM: {ex}");

            return null;
        }
    }


    private void ApplyRamGuidanceConclusion(
        double? currentLoad,
        RamHistoryEvidence? history)
    {
        if (_ramGuidanceStatusText == null ||
            _ramGuidanceMeaningText == null ||
            _ramGuidanceActionText == null)
        {
            return;
        }

        if (!currentLoad.HasValue)
        {
            SetRamGuidanceText(
                SettingsService.L(
                    "Недостаточно текущих данных",
                    "Not enough current data"),

                SettingsService.L(
                    "Thermiqra пока не получила надёжное текущее значение загрузки оперативной памяти (RAM), " +
                    "поэтому не делает вывод о её достаточности.",
                    "Thermiqra has not yet received a reliable current system memory (RAM) load value, " +
                    "so it does not make a conclusion about memory sufficiency."),

                SettingsService.L(
                    "Продолжайте пользоваться компьютером и откройте анализ позже. " +
                    "История будет уточнять вывод по мере накопления данных.",
                    "Continue using the computer and open the analysis again later. " +
                    "The conclusion will become more reliable as history accumulates."));

            return;
        }

        bool enoughDayHistory =
            history != null &&
            history.Last24Hours.SampleCount >= 30 &&
            history.DaySeriesPointCount >= 30;

        bool enoughWeekHistory =
            history != null &&
            history.Last7Days.SampleCount >= 120 &&
            history.WeekSeriesPointCount >= 60 &&
            history.WeekDaysWithSamples >= 2;

        double dayHighRatio =
            history != null &&
            history.DaySeriesPointCount > 0
                ? history.DayHighPointCount /
                  (double)history.DaySeriesPointCount
                : 0;

        double weekHighRatio =
            history != null &&
            history.WeekSeriesPointCount > 0
                ? history.WeekHighPointCount /
                  (double)history.WeekSeriesPointCount
                : 0;

        bool repeatedHighLoad24Hours =
            enoughDayHistory &&
            history!.DayHighPointCount >= 20 &&
            dayHighRatio >= 0.10;

        bool repeatedHighLoad7Days =
            enoughWeekHistory &&
            history!.WeekHighPointCount >= 60 &&
            history.WeekDaysWithHighLoad >= 2 &&
            weekHighRatio >= 0.08;

        bool repeatedHighLoad =
            repeatedHighLoad24Hours ||
            repeatedHighLoad7Days;

        bool enoughHistory =
            enoughDayHistory ||
            enoughWeekHistory;

        bool isolatedHighPeak =
            !repeatedHighLoad &&
            history != null &&
            (
                (history.Last24Hours.RamLoad.Maximum.HasValue &&
                 history.Last24Hours.RamLoad.Maximum.Value >= 90) ||
                (history.Last7Days.RamLoad.Maximum.HasValue &&
                 history.Last7Days.RamLoad.Maximum.Value >= 90)
            );

        double load =
            currentLoad.Value;

        if (repeatedHighLoad)
        {
            string status =
                load >= 90
                    ? SettingsService.L(
                        "Оперативная память (RAM) часто почти заполнена",
                        "System memory (RAM) is often nearly full")
                    : SettingsService.L(
                        "Оперативная память (RAM) часто была почти заполнена",
                        "System memory (RAM) has often been nearly full");

            SetRamGuidanceText(
                status,

                SettingsService.L(
                    "В сохранённой истории оперативная память неоднократно была загружена на 85 % и выше. " +
                    "Это не означает неисправность, но может указывать на нехватку свободной памяти " +
                    "при ваших обычных нагрузках.",
                    "Saved history shows system memory (RAM) repeatedly reaching 85% load or higher. " +
                    "This does not indicate a hardware fault, but it may point to limited free memory " +
                    "under your usual workloads."),

                SettingsService.L(
                    "Сначала проверьте в Диспетчере задач программы, которые занимают больше всего памяти. " +
                    "Если такая картина регулярно повторяется во время обычной работы или игр, " +
                    "увеличение объёма оперативной памяти (RAM) может иметь смысл.",
                    "First check Task Manager for the applications using the most memory. " +
                    "If this pattern regularly repeats during normal work or gaming, " +
                    "increasing system memory (RAM) capacity may be worth considering."));

            return;
        }

        if (load >= 90)
        {
            SetRamGuidanceText(
                SettingsService.L(
                    "Память почти полностью занята сейчас",
                    "Memory is almost fully used right now"),

                enoughHistory
                    ? SettingsService.L(
                        "Текущая загрузка очень высокая, но история пока не показывает устойчивого повторения. " +
                        "Одного такого эпизода недостаточно, чтобы делать вывод о нехватке оперативной памяти (RAM).",
                        "Current load is very high, but history does not yet show a persistent recurring pattern. " +
                        "One episode is not enough to conclude that system memory (RAM) capacity is insufficient.")
                    : SettingsService.L(
                        "Текущая загрузка очень высокая, но накопленной истории пока недостаточно " +
                        "для уверенного вывода о нехватке оперативной памяти (RAM).",
                        "Current load is very high, but there is not enough accumulated history yet " +
                        "to make a reliable conclusion about insufficient system memory (RAM)."),

                SettingsService.L(
                    "Проверьте, какие программы сейчас используют больше всего памяти. " +
                    "Не меняйте конфигурацию только из-за одного пика — сначала посмотрите, повторяется ли ситуация.",
                    "Check which applications are using the most memory right now. " +
                    "Do not change the configuration because of a single peak — first see whether the situation repeats."));

            return;
        }

        if (load >= 85)
        {
            SetRamGuidanceText(
                SettingsService.L(
                    "Оперативная память (RAM) сильно загружена сейчас",
                    "System memory (RAM) is heavily loaded right now"),

                enoughHistory
                    ? SettingsService.L(
                        "Загрузка высокая, но по накопленной истории устойчивого повторения пока не видно. " +
                        "Это ещё не основание считать объём памяти недостаточным.",
                        "Load is high, but accumulated history does not yet show a persistent recurring pattern. " +
                        "This is not enough to conclude that system memory (RAM) capacity is insufficient.")
                    : SettingsService.L(
                        "Загрузка высокая. Истории пока мало, поэтому Thermiqra не делает " +
                        "долгосрочный вывод по одному текущему значению.",
                        "Load is high. There is not enough history yet, so Thermiqra does not make " +
                        "a long-term conclusion from one current value."),

                SettingsService.L(
                    "Если компьютер работает нормально, срочных действий не требуется. " +
                    "При повторении высокой загрузки проверьте самые требовательные программы и историю загрузки оперативной памяти (RAM).",
                    "If the computer is working normally, no urgent action is required. " +
                    "If high load keeps returning, check the most memory-intensive applications and system memory (RAM) history."));

            return;
        }

        if (isolatedHighPeak)
        {
            SetRamGuidanceText(
                SettingsService.L(
                    "Сейчас загрузка оперативной памяти (RAM) нормальная",
                    "System memory (RAM) load is normal right now"),

                SettingsService.L(
                    "В истории был высокий пик, но данных о повторяющейся высокой загрузке недостаточно. " +
                    "Один пик не считается основанием для рекомендации апгрейда.",
                    "History contains a high peak, but there is not enough evidence of recurring high load. " +
                    "A single peak is not a basis for recommending an upgrade."),

                SettingsService.L(
                    "Ничего менять по одному пику не нужно. " +
                    "Наблюдайте за историей при обычной работе и играх.",
                    "There is no need to change anything because of one peak. " +
                    "Watch the history during normal work and gaming."));

            return;
        }

        if (!enoughHistory)
        {
            SetRamGuidanceText(
                load >= 70
                    ? SettingsService.L(
                        "Оперативная память (RAM) заметно загружена, но ситуация спокойная",
                        "System memory (RAM) is noticeably used, but the situation is calm")
                    : SettingsService.L(
                        "Сейчас загрузка оперативной памяти (RAM) нормальная",
                        "System memory (RAM) load is normal right now"),

                SettingsService.L(
                    "Текущее значение не выглядит проблемным, но истории пока недостаточно " +
                    "для долгосрочного вывода.",
                    "The current value does not look problematic, but there is not enough history yet " +
                    "for a long-term conclusion."),

                SettingsService.L(
                    "Срочных действий не требуется. " +
                    "Thermiqra уточнит картину по мере накопления истории.",
                    "No urgent action is required. " +
                    "Thermiqra will refine the picture as more history is collected."));

            return;
        }

        SetRamGuidanceText(
            SettingsService.L(
                "Признаков нехватки оперативной памяти (RAM) не видно",
                "No signs of insufficient system memory (RAM) are visible"),

            SettingsService.L(
                "Текущая загрузка и накопленная история не показывают " +
                "повторяющейся высокой занятости оперативной памяти.",
                "Current load and accumulated history do not show " +
                "recurring high system memory (RAM) usage."),

            SettingsService.L(
                "Ничего менять не требуется. " +
                "Продолжайте использовать компьютер как обычно.",
                "No changes are required. " +
                "Continue using the computer normally."));
    }


    private void SetRamGuidanceText(
        string status,
        string meaning,
        string action)
    {
        if (_ramGuidanceStatusText != null)
        {
            _ramGuidanceStatusText.Text =
                status;
        }

        if (_ramGuidanceMeaningText != null)
        {
            _ramGuidanceMeaningText.Text =
                meaning;
        }

        if (_ramGuidanceActionText != null)
        {
            _ramGuidanceActionText.Text =
                action;
        }
    }


    private static double? GetCurrentRamLoad(
        HardwareSnapshot? snapshot)
    {
        if (snapshot == null)
            return null;

        if (snapshot.Memory.Load.HasValue)
        {
            return Math.Clamp(
                snapshot.Memory.Load.Value,
                0,
                100);
        }

        if (snapshot.Memory.UsedGb.HasValue &&
            snapshot.Memory.AvailableGb.HasValue)
        {
            double total =
                snapshot.Memory.UsedGb.Value +
                snapshot.Memory.AvailableGb.Value;

            if (total > 0)
            {
                return Math.Clamp(
                    snapshot.Memory.UsedGb.Value /
                    total *
                    100.0,
                    0,
                    100);
            }
        }

        return null;
    }


    private static string BuildCurrentRamText(
        HardwareSnapshot? snapshot,
        double? currentLoad)
    {
        if (snapshot == null ||
            !currentLoad.HasValue)
        {
            return SettingsService.L(
                "Загрузка оперативной памяти (RAM) сейчас: нет надёжных данных.",
                "System memory (RAM) usage now: no reliable data.");
        }

        string result =
            SettingsService.L(
                $"Загрузка оперативной памяти (RAM) сейчас: {currentLoad.Value:F0} %",
                $"System memory (RAM) usage now: {currentLoad.Value:F0}%");

        if (snapshot.Memory.UsedGb.HasValue &&
            snapshot.Memory.TotalGb.HasValue)
        {
            result +=
                SettingsService.L(
                    $"   •   занято {snapshot.Memory.UsedGb.Value:F1} ГБ " +
                    $"из {snapshot.Memory.TotalGb.Value:F1} ГБ",
                    $"   •   used {snapshot.Memory.UsedGb.Value:F1} GB " +
                    $"of {snapshot.Memory.TotalGb.Value:F1} GB");
        }
        else if (snapshot.Memory.UsedGb.HasValue)
        {
            result +=
                SettingsService.L(
                    $"   •   занято {snapshot.Memory.UsedGb.Value:F1} ГБ",
                    $"   •   used {snapshot.Memory.UsedGb.Value:F1} GB");
        }

        if (snapshot.Memory.AvailableGb.HasValue)
        {
            result +=
                SettingsService.L(
                    $"   •   доступно {snapshot.Memory.AvailableGb.Value:F1} ГБ",
                    $"   •   available {snapshot.Memory.AvailableGb.Value:F1} GB");
        }

        return result;
    }

    private string BuildRamConfigurationText()
    {
        if (_snapshot == null)
        {
            return SettingsService.L(
                "Оперативная память: данные пока недоступны.",
                "System memory: data is not available yet.");
        }

        MemoryDetails memory =
            _snapshot.Memory;

        return SettingsService.L(
            $"Установлено: {FormatBytes(memory.TotalBytes)}" +
            $"   •   модулей памяти: {memory.Modules.Count}",
            $"Installed: {FormatBytes(memory.TotalBytes)}" +
            $"   •   memory modules: {memory.Modules.Count}");
    }


    private void ApplyRamPlacementGuidance()
    {
        if (_ramGuidancePlacementWarningText == null)
            return;

        string? warning =
            BuildRamPlacementWarning();

        if (string.IsNullOrWhiteSpace(
                warning))
        {
            _ramGuidancePlacementWarningText.Text =
                string.Empty;

            _ramGuidancePlacementWarningText.Visibility =
                Visibility.Collapsed;

            return;
        }

        _ramGuidancePlacementWarningText.Text =
            warning;

        _ramGuidancePlacementWarningText.Visibility =
            Visibility.Visible;
    }


    private string? BuildRamPlacementWarning()
    {
        if (_snapshot == null)
            return null;

        MemoryDetails memory =
            _snapshot.Memory;

        // Срабатываем только в достаточно понятном случае:
        // 4 физических слота, установлено ровно 2 модуля,
        // а названия обоих слотов надёжно указывают на один канал.
        // Если BIOS/WMI отдаёт неоднозначные BANK/DIMM номера,
        // рекомендацию не показываем.
        if (memory.PhysicalSlotCount != 4 ||
            memory.Modules.Count != 2)
        {
            return null;
        }

        string? firstChannel =
            TryGetReliableMemoryChannel(
                memory.Modules[0].Slot);

        string? secondChannel =
            TryGetReliableMemoryChannel(
                memory.Modules[1].Slot);

        if (string.IsNullOrWhiteSpace(
                firstChannel) ||
            string.IsNullOrWhiteSpace(
                secondChannel) ||
            !string.Equals(
                firstChannel,
                secondChannel,
                StringComparison.Ordinal))
        {
            return null;
        }

        return SettingsService.L(
            "СТОИТ ПРОВЕРИТЬ УСТАНОВКУ ПАМЯТИ\n" +
            "Thermiqra видит признаки того, что два модуля оперативной памяти " +
            "могут быть установлены рядом. На платах с четырьмя разъёмами пару " +
            "модулей обычно устанавливают через один свободный разъём. " +
            "Это позволяет памяти работать эффективнее и может увеличить её пропускную способность.\n" +
            "Если модули действительно стоят рядом, при возможности переставьте один из них " +
            "через разъём. Перед перестановкой полностью выключите компьютер и отключите питание.",
            "CHECK THE MEMORY INSTALLATION\n" +
            "Thermiqra sees signs that the two system memory (RAM) modules may be installed next to each other. " +
            "On motherboards with four memory slots, a pair of modules is usually installed " +
            "with one empty slot between them. This can let memory work more efficiently " +
            "and may increase memory bandwidth.\n" +
            "If the modules are physically next to each other, move one module over by one slot " +
            "when possible. Fully shut down the computer and disconnect power before moving memory.");
    }


    private static string? TryGetReliableMemoryChannel(
        string? slot)
    {
        if (string.IsNullOrWhiteSpace(
                slot))
        {
            return null;
        }

        string normalized =
            new(
                slot
                    .Where(
                        char.IsLetterOrDigit)
                    .Select(
                        char.ToUpperInvariant)
                    .ToArray());

        if (normalized.Contains(
                "CHANNELA",
                StringComparison.Ordinal))
        {
            return "A";
        }

        if (normalized.Contains(
                "CHANNELB",
                StringComparison.Ordinal))
        {
            return "B";
        }

        bool looksLikeDimmName =
            normalized.Contains(
                "DIMM",
                StringComparison.Ordinal) ||
            normalized.Contains(
                "DDR",
                StringComparison.Ordinal) ||
            normalized.Length <= 3;

        if (!looksLikeDimmName)
            return null;

        if (normalized.EndsWith(
                "A1",
                StringComparison.Ordinal) ||
            normalized.EndsWith(
                "A2",
                StringComparison.Ordinal))
        {
            return "A";
        }

        if (normalized.EndsWith(
                "B1",
                StringComparison.Ordinal) ||
            normalized.EndsWith(
                "B2",
                StringComparison.Ordinal))
        {
            return "B";
        }

        return null;
    }

    private static string BuildRamHistoryText(
        RamHistoryEvidence? history)
    {
        if (history == null)
        {
            return SettingsService.L(
                "История загрузки памяти сейчас недоступна.",
                "Memory usage history is currently unavailable.");
        }

        bool useDay =
            history.Last24Hours.SampleCount > 0;

        StatisticsSummary summary =
            useDay
                ? history.Last24Hours
                : history.Last7Days;

        if (summary.SampleCount <= 0)
        {
            return SettingsService.L(
                "История загрузки оперативной памяти пока не накоплена.",
                "System memory (RAM) usage history has not been accumulated yet.");
        }

        string periodNameRu =
            useDay
                ? "За последние 24 часа"
                : "За последние 7 дней";

        string periodNameEn =
            useDay
                ? "Over the last 24 hours"
                : "Over the last 7 days";

        string average =
            summary.RamLoad.Average.HasValue
                ? $"{summary.RamLoad.Average.Value:F0} %"
                : "—";

        string maximum =
            summary.RamLoad.Maximum.HasValue
                ? $"{summary.RamLoad.Maximum.Value:F0} %"
                : "—";

        int highPoints =
            useDay
                ? history.DayHighPointCount
                : history.WeekHighPointCount;

        int sampledPoints =
            useDay
                ? history.DaySeriesPointCount
                : history.WeekSeriesPointCount;

        double highRatio =
            sampledPoints > 0
                ? highPoints /
                  (double)sampledPoints
                : 0;

        bool repeatedHighLoad =
            useDay
                ? highPoints >= 20 &&
                  highRatio >= 0.10
                : highPoints >= 60 &&
                  history.WeekDaysWithHighLoad >= 2 &&
                  highRatio >= 0.08;

        string assessmentRu;
        string assessmentEn;

        if (highPoints == 0)
        {
            assessmentRu =
                "Высокой загрузки памяти не наблюдалось.";

            assessmentEn =
                "No high memory usage was observed.";
        }
        else if (repeatedHighLoad)
        {
            assessmentRu =
                "Высокая загрузка памяти повторялась.";

            assessmentEn =
                "High memory usage occurred repeatedly.";
        }
        else
        {
            assessmentRu =
                "Были отдельные высокие пики, но устойчивой высокой загрузки не видно.";

            assessmentEn =
                "There were isolated high peaks, but no sustained high memory usage is visible.";
        }

        return SettingsService.L(
            $"{periodNameRu}: средняя загрузка оперативной памяти — {average}, " +
            $"максимальная — {maximum}. {assessmentRu}",
            $"{periodNameEn}: average system memory (RAM) usage was {average}, " +
            $"maximum was {maximum}. {assessmentEn}");
    }

    private sealed class RamHistoryEvidence
    {
        public StatisticsSummary Last24Hours { get; init; } =
            new();

        public StatisticsSummary Last7Days { get; init; } =
            new();

        public int DaySeriesPointCount { get; init; }

        public int DayHighPointCount { get; init; }

        public int WeekSeriesPointCount { get; init; }

        public int WeekHighPointCount { get; init; }

        public int WeekDaysWithSamples { get; init; }

        public int WeekDaysWithHighLoad { get; init; }
    }
}
