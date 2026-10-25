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
    // ЧТО МОЖНО УЛУЧШИТЬ
    // ============================================================

    private void RefreshUpgradeGuidance()
    {
        if (!_smartGuidanceInitialized ||
            _upgradeGuidanceStatusText == null ||
            _upgradeGuidanceDetailsPanel == null ||
            _upgradeGuidanceNoteText == null)
        {
            return;
        }

        _upgradeGuidanceDetailsPanel
            .Children
            .Clear();

        _upgradeGuidanceNoteText.Text =
            string.Empty;

        _upgradeGuidanceNoteText.Visibility =
            Visibility.Collapsed;

        HardwareSnapshot? snapshot =
            (Owner as MainWindow)?
                .GetSmartGuidanceSnapshot();

        if (snapshot == null)
        {
            _upgradeGuidanceStatusText.Text =
                SettingsService.L(
                    "Нужно немного времени",
                    "A little more time is needed");

            TextBlock waitingText =
                CreateGuidanceValueText();

            waitingText.Text =
                SettingsService.L(
                    "Thermiqra ещё собирает данные для рекомендации.",
                    "Thermiqra is still collecting data for a recommendation.");

            _upgradeGuidanceDetailsPanel
                .Children
                .Add(
                    waitingText);

            return;
        }

        int recommendationCount =
            0;

        if (HasRecurringHighRamLoadForUpgrade())
        {
            AddUpgradeRecommendation(
                SettingsService.L(
                    "Оперативной памяти может не хватать",
                    "System memory may be insufficient"),

                SettingsService.L(
                    "Высокая загрузка оперативной памяти (RAM) повторяется. " +
                    "Сначала посмотрите в Диспетчере задач, какие программы используют больше всего памяти. " +
                    "Если это происходит регулярно при вашей обычной работе, увеличение объёма оперативной памяти может помочь.",
                    "High system-memory (RAM) usage keeps recurring. " +
                    "First check Task Manager to see which applications use the most memory. " +
                    "If this happens regularly during your normal work, adding more memory may help."));

            recommendationCount++;
        }

        CpuGpuUpgradeResult cpuGpuResult =
            GetCpuGpuUpgradeResult();

        if (cpuGpuResult.Outcome ==
            CpuGpuUpgradeOutcome.Cpu)
        {
            AddUpgradeRecommendation(
                SettingsService.L(
                    "Процессор может быть главным ограничением",
                    "The processor may be the main limitation"),

                BuildCpuUpgradeRecommendationText(
                    cpuGpuResult));

            recommendationCount++;
        }
        else if (cpuGpuResult.Outcome ==
                 CpuGpuUpgradeOutcome.CpuCooling)
        {
            AddUpgradeRecommendation(
                SettingsService.L(
                    "Сначала стоит проверить охлаждение процессора",
                    "Check processor cooling first"),

                BuildCpuCoolingRecommendationText(
                    cpuGpuResult));

            recommendationCount++;
        }
        else if (cpuGpuResult.Outcome ==
                 CpuGpuUpgradeOutcome.GpuCooling)
        {
            AddUpgradeRecommendation(
                SettingsService.L(
                    "Сначала стоит проверить охлаждение видеокарты",
                    "Check graphics-card cooling first"),

                BuildGpuCoolingRecommendationText(
                    cpuGpuResult));

            recommendationCount++;
        }

        LogicalDriveInfo? systemDrive =
            FindWindowsLogicalDrive(
                snapshot);

        SystemDriveHardwareEvidence
            hardwareEvidence =
                ReadSystemDriveHardwareEvidence(
                    snapshot);

        if (hardwareEvidence.Kind ==
            PhysicalStorageKind.Hdd)
        {
            if (hardwareEvidence.HasOtherSsd)
            {
                AddUpgradeRecommendation(
                    SettingsService.L(
                        "Windows работает с обычного жёсткого диска (HDD)",
                        "Windows is running from a hard disk drive (HDD)"),

                    SettingsService.L(
                        "В компьютере уже есть более быстрый твердотельный накопитель (SSD). " +
                        "Если на нём достаточно места, перенос Windows на него обычно ускоряет включение компьютера и запуск программ.",
                        "A faster solid-state drive (SSD) is already installed in the computer. " +
                        "If it has enough space, moving Windows to it usually speeds up computer startup and application launch."));
            }
            else
            {
                AddUpgradeRecommendation(
                    SettingsService.L(
                        "Windows работает с обычного жёсткого диска (HDD)",
                        "Windows is running from a hard disk drive (HDD)"),

                    SettingsService.L(
                        "Для Windows обычно быстрее твердотельный накопитель (SSD). " +
                        "Он может заметно ускорить включение компьютера и запуск программ. " +
                        "Если текущая скорость вас устраивает, срочности нет.",
                        "A solid-state drive (SSD) is usually faster for Windows. " +
                        "It can noticeably speed up computer startup and application launch. " +
                        "If you are happy with the current speed, there is no urgency."));
            }

            recommendationCount++;
        }

        if (systemDrive != null &&
            ShouldSuggestLargerSystemDrive(
                systemDrive))
        {
            double totalGb =
                systemDrive.TotalBytes /
                1024d /
                1024d /
                1024d;

            double freeGb =
                systemDrive.FreeBytes /
                1024d /
                1024d /
                1024d;

            AddUpgradeRecommendation(
                SettingsService.L(
                    "На диске Windows становится мало места",
                    "The Windows drive is running low on space"),

                SettingsService.L(
                    $"Свободно примерно {freeGb:F0} ГБ из {totalGb:F0} ГБ. " +
                    "Сначала откройте «Анализ» этого диска и посмотрите, что занимает место. " +
                    "Если после очистки свободное место быстро снова заканчивается, может быть удобнее более вместительный диск для Windows.",
                    $"About {freeGb:F0} GB is free out of {totalGb:F0} GB. " +
                    "First open Analyze for this drive and see what is using the space. " +
                    "If free space quickly runs low again after cleanup, a larger Windows drive may be more convenient."));

            recommendationCount++;
        }

        if (recommendationCount == 0)
        {
            _upgradeGuidanceStatusText.Text =
                SettingsService.L(
                    "Сейчас явных рекомендаций нет",
                    "No clear recommendations right now");

            TextBlock summaryText =
                CreateGuidanceValueText();

            summaryText.Text =
                cpuGpuResult.HasEnoughHistory
                    ? SettingsService.L(
                        "По имеющимся данным явного слабого места в памяти, диске, процессоре или видеокарте не видно.",
                        "The available data does not show a clear weak point in memory, storage, processor, or graphics card.")
                    : SettingsService.L(
                        "По памяти и диску явных проблем не видно. Для оценки процессора и видеокарты Thermiqra сейчас накапливает новую историю с учётом стресс-тестов. Старые данные остаются в статистике, но не используются для советов по апгрейду.",
                        "No clear memory or storage issue is visible. Thermiqra is now building a new processor and graphics-card history that accounts for stress tests. Older data remains in Statistics but is not used for upgrade advice.");

            _upgradeGuidanceDetailsPanel
                .Children
                .Add(
                    summaryText);
        }
        else
        {
            _upgradeGuidanceStatusText.Text =
                recommendationCount == 1
                    ? SettingsService.L(
                        "Есть одна рекомендация",
                        "There is one recommendation")
                    : SettingsService.L(
                        "Есть несколько рекомендаций",
                        "There are several recommendations");
        }
    }


    private void AddUpgradeRecommendation(
        string title,
        string text)
    {
        if (_upgradeGuidanceDetailsPanel == null)
            return;

        StackPanel block =
            new();

        if (_upgradeGuidanceDetailsPanel
                .Children
                .Count > 0)
        {
            block.Margin =
                new Thickness(
                    0,
                    14,
                    0,
                    0);
        }

        TextBlock titleText =
            CreateGuidanceText(
                title,
                13,
                FontWeights.SemiBold,
                "AccentBrush");

        TextBlock valueText =
            CreateGuidanceValueText();

        valueText.Text =
            text;

        valueText.Margin =
            new Thickness(
                0,
                5,
                0,
                0);

        block.Children.Add(
            titleText);

        block.Children.Add(
            valueText);

        _upgradeGuidanceDetailsPanel
            .Children
            .Add(
                block);
    }


    private bool HasRecurringHighRamLoadForUpgrade()
    {
        if (_statistics == null)
            return false;

        try
        {
            StatisticsChartSeries daySeries =
                _statistics.GetAdvisorRamLoadSeries(
                    StatisticsPeriod.Last24Hours,
                    1200);

            StatisticsChartSeries weekSeries =
                _statistics.GetAdvisorRamLoadSeries(
                    StatisticsPeriod.Last7Days,
                    1200);

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

            bool enoughDayHistory =
                dayPoints.Count >= 120;

            bool enoughWeekHistory =
                weekPoints.Count >= 240 &&
                weekDaysWithSamples >= 2;

            double dayHighRatio =
                dayPoints.Count > 0
                    ? dayHighCount /
                      (double)dayPoints.Count
                    : 0;

            double weekHighRatio =
                weekPoints.Count > 0
                    ? weekHighCount /
                      (double)weekPoints.Count
                    : 0;

            bool repeatedHighLoad24Hours =
                enoughDayHistory &&
                dayHighCount >= 20 &&
                dayHighRatio >= 0.10;

            bool repeatedHighLoad7Days =
                enoughWeekHistory &&
                weekHighCount >= 60 &&
                weekDaysWithHighLoad >= 2 &&
                weekHighRatio >= 0.08;

            return
                repeatedHighLoad24Hours ||
                repeatedHighLoad7Days;
        }
        catch
        {
            return false;
        }
    }


    private CpuGpuUpgradeResult GetCpuGpuUpgradeResult()
    {

        if (_statistics == null)
        {
            return new CpuGpuUpgradeResult();
        }

        try
        {
            AdvisorPerformanceHistory history =
                _statistics.GetAdvisorPerformanceHistory(
                    StatisticsPeriod.Last7Days);

            AdvisorGpuPerformanceSeries? activeGpu =
                history.Gpus
                    .Where(
                        gpu =>
                            gpu.Points.Count > 0)
                    .OrderByDescending(
                        gpu =>
                            gpu.Points.Count(
                                point =>
                                    point.GpuLoad >= 40))
                    .ThenByDescending(
                        gpu =>
                            gpu.Points.Average(
                                point =>
                                    point.GpuLoad))
                    .FirstOrDefault();

            if (activeGpu == null)
            {
                return new CpuGpuUpgradeResult();
            }

            List<AdvisorPerformancePoint> points =
                activeGpu.Points
                    .OrderBy(
                        point =>
                            point.TimestampUtc)
                    .ToList();

            int daysWithSamples =
                points
                    .Select(
                        point =>
                            point.TimestampUtc
                                .ToLocalTime()
                                .Date)
                    .Distinct()
                    .Count();

            bool enoughHistory =
                points.Count >= 180 &&
                daysWithSamples >= 2;

            if (!enoughHistory)
            {
                return new CpuGpuUpgradeResult
                {
                    HasEnoughHistory =
                        false
                };
            }

            List<AdvisorPerformancePoint>
                cpuLimitedPoints =
                    points
                        .Where(
                            point =>
                                point.CpuLoad >= 90 &&
                                point.GpuLoad <= 70)
                        .ToList();

            List<AdvisorPerformancePoint>
                gpuLimitedPoints =
                    points
                        .Where(
                            point =>
                                point.GpuLoad >= 95 &&
                                point.CpuLoad <= 80)
                        .ToList();

            List<AdvisorPerformancePoint>
                bothHighPoints =
                    points
                        .Where(
                            point =>
                                point.CpuLoad >= 90 &&
                                point.GpuLoad >= 90)
                        .ToList();

            bool recurringCpuPattern =
                IsRecurringPerformancePattern(
                    points,
                    point =>
                        point.CpuLoad >= 90 &&
                        point.GpuLoad <= 70,
                    cpuLimitedPoints);

            bool recurringGpuPattern =
                IsRecurringPerformancePattern(
                    points,
                    point =>
                        point.GpuLoad >= 95 &&
                        point.CpuLoad <= 80,
                    gpuLimitedPoints);

            bool recurringBothHigh =
                IsRecurringPerformancePattern(
                    points,
                    point =>
                        point.CpuLoad >= 90 &&
                        point.GpuLoad >= 90,
                    bothHighPoints);

            if (recurringBothHigh ||
                (recurringCpuPattern &&
                 recurringGpuPattern))
            {
                return new CpuGpuUpgradeResult
                {
                    Outcome =
                        CpuGpuUpgradeOutcome.Ambiguous,

                    HasEnoughHistory =
                        true
                };
            }

            AppSettings settings =
                SettingsService.Current;

            if (recurringCpuPattern)
            {
                bool thermalConcern =
                    HasThermalConcern(
                        cpuLimitedPoints,
                        point =>
                            point.CpuTemperature,
                        settings.CpuWarningTemperature);

                AdvisorEvidenceSpan evidenceSpan =
                    _statistics.GetAdvisorEvidenceSpan(
                        activeGpu.DeviceId,
                        thermalConcern
                            ? AdvisorEvidenceKind.CpuCooling
                            : AdvisorEvidenceKind.CpuLimited,
                        settings.CpuWarningTemperature);

                return new CpuGpuUpgradeResult
                {
                    Outcome =
                        thermalConcern
                            ? CpuGpuUpgradeOutcome.CpuCooling
                            : CpuGpuUpgradeOutcome.Cpu,

                    HasEnoughHistory =
                        true,

                    EvidenceSpanDays =
                        evidenceSpan.HasEvidence
                            ? evidenceSpan.CalendarSpanDays
                            : GetFallbackEvidenceSpanDays(
                                cpuLimitedPoints),

                    CpuPeakLoad =
                        cpuLimitedPoints.Max(
                            point =>
                                point.CpuLoad),

                    CpuPeakTemperature =
                        GetMaximumTemperature(
                            cpuLimitedPoints,
                            point =>
                                point.CpuTemperature),

                    GpuAverageLoadDuringCpuPattern =
                        cpuLimitedPoints.Average(
                            point =>
                                point.GpuLoad)
                };
            }

            if (recurringGpuPattern)
            {
                bool thermalConcern =
                    HasThermalConcern(
                        gpuLimitedPoints,
                        point =>
                            point.GpuTemperature,
                        settings.GpuWarningTemperature);

                AdvisorEvidenceSpan evidenceSpan =
                    thermalConcern
                        ? _statistics.GetAdvisorEvidenceSpan(
                            activeGpu.DeviceId,
                            AdvisorEvidenceKind.GpuCooling,
                            settings.GpuWarningTemperature)
                        : new AdvisorEvidenceSpan();

                return new CpuGpuUpgradeResult
                {
                    Outcome =
                        thermalConcern
                            ? CpuGpuUpgradeOutcome.GpuCooling
                            : CpuGpuUpgradeOutcome.None,

                    HasEnoughHistory =
                        true,

                    EvidenceSpanDays =
                        thermalConcern &&
                        evidenceSpan.HasEvidence
                            ? evidenceSpan.CalendarSpanDays
                            : GetFallbackEvidenceSpanDays(
                                gpuLimitedPoints),

                    GpuPeakLoad =
                        gpuLimitedPoints.Max(
                            point =>
                                point.GpuLoad),

                    GpuPeakTemperature =
                        GetMaximumTemperature(
                            gpuLimitedPoints,
                            point =>
                                point.GpuTemperature),

                    CpuAverageLoadDuringGpuPattern =
                        gpuLimitedPoints.Average(
                            point =>
                                point.CpuLoad)
                };
            }

            return new CpuGpuUpgradeResult
            {
                Outcome =
                    CpuGpuUpgradeOutcome.None,

                HasEnoughHistory =
                    true
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Не удалось оценить CPU/GPU для Upgrade Advisor: {ex}");

            return new CpuGpuUpgradeResult();
        }
    }


    private static bool IsRecurringPerformancePattern(
        IReadOnlyList<AdvisorPerformancePoint> allPoints,
        Func<AdvisorPerformancePoint, bool> predicate,
        IReadOnlyList<AdvisorPerformancePoint> matchingPoints)
    {
        if (matchingPoints.Count < 30)
            return false;

        int daysWithPattern =
            matchingPoints
                .Select(
                    point =>
                        point.TimestampUtc
                            .ToLocalTime()
                            .Date)
                .Distinct()
                .Count();

        if (daysWithPattern < 2)
            return false;

        int sustainedEpisodes =
            CountSustainedEpisodes(
                allPoints,
                predicate,
                5);

        return sustainedEpisodes >= 2;
    }


    private static int CountSustainedEpisodes(
        IReadOnlyList<AdvisorPerformancePoint> points,
        Func<AdvisorPerformancePoint, bool> predicate,
        int minimumPoints)
    {
        int episodeCount =
            0;

        int currentCount =
            0;

        DateTimeOffset? previousTimestamp =
            null;

        foreach (AdvisorPerformancePoint point in points)
        {
            bool closeToPrevious =
                !previousTimestamp.HasValue ||
                point.TimestampUtc -
                previousTimestamp.Value <=
                    TimeSpan.FromMinutes(2.5);

            if (predicate(
                    point))
            {
                if (!closeToPrevious)
                {
                    if (currentCount >=
                        minimumPoints)
                    {
                        episodeCount++;
                    }

                    currentCount =
                        0;
                }

                currentCount++;
            }
            else
            {
                if (currentCount >=
                    minimumPoints)
                {
                    episodeCount++;
                }

                currentCount =
                    0;
            }

            previousTimestamp =
                point.TimestampUtc;
        }

        if (currentCount >=
            minimumPoints)
        {
            episodeCount++;
        }

        return episodeCount;
    }


    private static int GetFallbackEvidenceSpanDays(
        IReadOnlyList<AdvisorPerformancePoint> points)
    {
        if (points.Count == 0)
            return 1;

        DateTime firstDate =
            points
                .Min(
                    point =>
                        point.TimestampUtc)
                .ToLocalTime()
                .Date;

        DateTime lastDate =
            points
                .Max(
                    point =>
                        point.TimestampUtc)
                .ToLocalTime()
                .Date;

        return Math.Max(
            1,
            (lastDate -
             firstDate).Days +
            1);
    }


    private static double? GetMaximumTemperature(
        IReadOnlyList<AdvisorPerformancePoint> points,
        Func<AdvisorPerformancePoint, double?> selector)
    {
        List<double> values =
            points
                .Select(
                    selector)
                .Where(
                    value =>
                        value.HasValue)
                .Select(
                    value =>
                        value!.Value)
                .ToList();

        return values.Count == 0
            ? null
            : values.Max();
    }


    private static string BuildCpuUpgradeRecommendationText(
        CpuGpuUpgradeResult result)
    {
        string span =
            FormatObservedSpan(
                result.EvidenceSpanDays);

        string loadText =
            result.CpuPeakLoad.HasValue
                ? $"{result.CpuPeakLoad.Value:F0}%"
                : "—";

        string gpuText =
            result.GpuAverageLoadDuringCpuPattern.HasValue
                ? $"{result.GpuAverageLoadDuringCpuPattern.Value:F0}%"
                : "—";

        string temperatureText =
            result.CpuPeakTemperature.HasValue
                ? $"{result.CpuPeakTemperature.Value:F0} °C"
                : "—";

        return SettingsService.L(
            $"На протяжении {span} повторяются длительные периоды высокой загрузки процессора " +
            $"(до {loadText}); видеокарта в такие моменты используется примерно на {gpuText}. " +
            $"Температура процессора доходила до {temperatureText}, признаков устойчивого перегрева не видно. " +
            "Если именно в эти моменты компьютер работает медленно, более производительный процессор может дать заметный эффект.",

            $"Over {span}, there have been repeated sustained periods of high processor load " +
            $"(up to {loadText}); the graphics card was used at about {gpuText} during those periods. " +
            $"Processor temperature reached {temperatureText}, with no sign of persistent overheating. " +
            "If the computer feels slow specifically at those times, a faster processor may make a noticeable difference.");
    }


    private static string BuildCpuCoolingRecommendationText(
        CpuGpuUpgradeResult result)
    {
        string span =
            FormatObservedSpan(
                result.EvidenceSpanDays);

        string loadText =
            result.CpuPeakLoad.HasValue
                ? $"{result.CpuPeakLoad.Value:F0}%"
                : "—";

        string temperatureText =
            result.CpuPeakTemperature.HasValue
                ? $"{result.CpuPeakTemperature.Value:F0} °C"
                : "—";

        return SettingsService.L(
            $"На протяжении {span} высокая загрузка процессора повторяется вместе с повышенной температурой " +
            $"(нагрузка до {loadText}, температура до {temperatureText}). " +
            "До вывода о необходимости замены процессора сначала стоит проверить охлаждение: " +
            "чистоту системы, работу вентиляторов, установку кулера и состояние термопасты.",

            $"Over {span}, high processor load has repeatedly occurred together with elevated temperature " +
            $"(load up to {loadText}, temperature up to {temperatureText}). " +
            "Before concluding that the processor needs replacing, check cooling first: " +
            "dust buildup, fan operation, cooler installation, and thermal paste.");
    }


    private static string BuildGpuCoolingRecommendationText(
        CpuGpuUpgradeResult result)
    {
        string span =
            FormatObservedSpan(
                result.EvidenceSpanDays);

        string loadText =
            result.GpuPeakLoad.HasValue
                ? $"{result.GpuPeakLoad.Value:F0}%"
                : "—";

        string temperatureText =
            result.GpuPeakTemperature.HasValue
                ? $"{result.GpuPeakTemperature.Value:F0} °C"
                : "—";

        return SettingsService.L(
            $"На протяжении {span} высокая загрузка видеокарты повторяется вместе с повышенной температурой " +
            $"(нагрузка до {loadText}, температура до {temperatureText}). " +
            "Сама по себе загрузка видеокарты около 100% нормальна для тяжёлых графических задач, " +
            "но при повышенной температуре сначала стоит проверить вентиляторы и приток воздуха в корпусе.",

            $"Over {span}, high graphics-card load has repeatedly occurred together with elevated temperature " +
            $"(load up to {loadText}, temperature up to {temperatureText}). " +
            "Graphics-card load near 100% is normal by itself in demanding graphics workloads, " +
            "but with elevated temperature it is worth checking the fans and case airflow first.");
    }


    private static string FormatObservedSpan(
        int days)
    {
        days =
            Math.Max(
                1,
                days);

        if (SettingsService.IsRussian)
        {
            int lastTwo =
                days % 100;

            int last =
                days % 10;

            string word =
                lastTwo >= 11 &&
                lastTwo <= 14
                    ? "дней"
                    : last == 1
                        ? "день"
                        : last >= 2 &&
                          last <= 4
                            ? "дня"
                            : "дней";

            return
                $"{days} {word}";
        }

        return days == 1
            ? "1 day"
            : $"{days} days";
    }


    private static bool HasThermalConcern(
        IReadOnlyList<AdvisorPerformancePoint> points,
        Func<AdvisorPerformancePoint, double?> temperatureSelector,
        double warningTemperature)
    {
        List<double> temperatures =
            points
                .Select(
                    temperatureSelector)
                .Where(
                    value =>
                        value.HasValue)
                .Select(
                    value =>
                        value!.Value)
                .ToList();

        if (temperatures.Count < 8)
            return false;

        int hotCount =
            temperatures.Count(
                value =>
                    value >=
                    warningTemperature);

        return
            hotCount >= 8 &&
            hotCount /
            (double)temperatures.Count >=
                0.30;
    }




    private enum CpuGpuUpgradeOutcome
    {
        None,
        Cpu,
        CpuCooling,
        GpuCooling,
        Ambiguous
    }


    private sealed class CpuGpuUpgradeResult
    {
        public CpuGpuUpgradeOutcome Outcome { get; init; } =
            CpuGpuUpgradeOutcome.None;

        public bool HasEnoughHistory { get; init; }

        public int EvidenceSpanDays { get; init; } =
            1;

        public double? CpuPeakLoad { get; init; }

        public double? CpuPeakTemperature { get; init; }

        public double? GpuAverageLoadDuringCpuPattern { get; init; }

        public double? GpuPeakLoad { get; init; }

        public double? GpuPeakTemperature { get; init; }

        public double? CpuAverageLoadDuringGpuPattern { get; init; }
    }



}
