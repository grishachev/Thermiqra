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
    // АНАЛИЗ НАКОПИТЕЛЕЙ
    // ============================================================

    private void RefreshStorageSmartGuidance()
    {
        if (!_smartGuidanceInitialized ||
            _storageGuidanceStatusText == null ||
            _storageGuidanceDetailsPanel == null ||
            _storageGuidanceMeaningText == null ||
            _storageGuidanceActionText == null)
        {
            return;
        }

        HardwareSnapshot? snapshot =
            (Owner as MainWindow)?
                .GetSmartGuidanceSnapshot();

        _storageGuidanceDetailsPanel
            .Children
            .Clear();

        if (snapshot == null)
        {
            SetStorageGuidanceText(
                SettingsService.L(
                    "Недостаточно текущих данных",
                    "Not enough current data"),

                SettingsService.L(
                    "Thermiqra пока не получила свежие данные о накопителях.",
                    "Thermiqra has not yet received fresh storage data."),

                SettingsService.L(
                    "Откройте анализ позже, когда мониторинг успеет получить данные.",
                    "Open the analysis again after monitoring has had time to collect data."));

            AddStorageDetailLine(
                SettingsService.L(
                    "Температура и свободное место сейчас недоступны.",
                    "Temperature and free-space data are currently unavailable."));

            return;
        }

        List<StorageGuidanceDeviceEvidence> devices =
            snapshot.StorageDevices
                .Select(
                    BuildStorageDeviceEvidence)
                .ToList();

        List<StorageGuidanceDeviceEvidence>
            temperatureAttentionDevices =
                devices
                    .Where(
                        device =>
                            device.Level ==
                                StorageGuidanceLevel.Warm ||
                            device.Level ==
                                StorageGuidanceLevel.Warning ||
                            device.Level ==
                                StorageGuidanceLevel.Critical)
                    .ToList();

        if (temperatureAttentionDevices.Count == 0)
        {
            bool hasAnyTemperature =
                devices.Any(
                    device =>
                        device.CurrentTemperature.HasValue ||
                        device.HistoryMaximum.HasValue);

            AddStorageDetailLine(
                hasAnyTemperature
                    ? SettingsService.L(
                        "Температура накопителей: всё в норме.",
                        "Storage temperatures: everything looks normal.")
                    : SettingsService.L(
                        "Температура накопителей: данные доступны не для всех устройств.",
                        "Storage temperatures: data is not available for every device."));
        }
        else
        {
            foreach (StorageGuidanceDeviceEvidence device
                     in temperatureAttentionDevices)
            {
                AddStorageDetailLine(
                    BuildStorageDeviceLine(
                        device));
            }
        }

        List<DriveSpaceGuidance> driveSpaces =
            snapshot.Drives
                .Where(
                    drive =>
                        drive.TotalBytes > 0)
                .Select(
                    BuildDriveSpaceGuidance)
                .ToList();

        List<DriveSpaceGuidance> lowSpaceDrives =
            driveSpaces
                .Where(
                    drive =>
                        drive.Level !=
                        StorageGuidanceLevel.Normal)
                .ToList();

        if (lowSpaceDrives.Count == 0)
        {
            AddStorageDetailLine(
                SettingsService.L(
                    "Свободное место: всё в норме.",
                    "Free space: everything looks normal."));
        }
        else
        {
            foreach (DriveSpaceGuidance drive
                     in lowSpaceDrives)
            {
                AddStorageDetailLine(
                    BuildDriveSpaceLine(
                        drive));
            }
        }

        StorageGuidanceLevel temperatureLevel =
            devices.Count == 0
                ? StorageGuidanceLevel.Unknown
                : devices.Max(
                    device =>
                        device.Level);

        StorageGuidanceLevel spaceLevel =
            lowSpaceDrives.Count == 0
                ? StorageGuidanceLevel.Normal
                : lowSpaceDrives.Max(
                    drive =>
                        drive.Level);

        StorageGuidanceLevel overallLevel =
            (StorageGuidanceLevel)
            Math.Max(
                (int)temperatureLevel,
                (int)spaceLevel);

        bool hasTemperatureData =
            devices.Any(
                device =>
                    device.CurrentTemperature.HasValue ||
                    device.HistoryMaximum.HasValue);

        ApplyStorageGuidanceConclusion(
            overallLevel,
            temperatureLevel,
            spaceLevel,
            hasTemperatureData);
    }


    private StorageGuidanceDeviceEvidence
        BuildStorageDeviceEvidence(
            StorageDeviceInfo storage)
    {
        StorageGuidanceThresholds thresholds =
            GetStorageGuidanceThresholds(
                storage);

        double? historyAverage =
            null;

        double? historyMaximum =
            null;

        string? historyPeriod =
            null;

        bool repeatedWarmHistory =
            false;

        if (_statistics != null)
        {
            try
            {
                string deviceId =
                    string.IsNullOrWhiteSpace(
                        storage.Id)
                        ? storage.Name
                        : storage.Id;

                StatisticsSummary daySummary =
                    _statistics.GetSummary(
                        StatisticsPeriod.Last24Hours);

                StatisticsSummary weekSummary =
                    _statistics.GetSummary(
                        StatisticsPeriod.Last7Days);

                StorageStatisticsSummary? dayDevice =
                    FindStorageSummary(
                        daySummary,
                        deviceId,
                        storage.Name);

                StorageStatisticsSummary? weekDevice =
                    FindStorageSummary(
                        weekSummary,
                        deviceId,
                        storage.Name);

                StatisticsChartSeries daySeries =
                    _statistics.GetChartSeries(
                        StatisticsPeriod.Last24Hours,
                        StatisticsChartMetric.StorageTemperature,
                        deviceId,
                        maxPoints: 1200);

                StatisticsChartSeries weekSeries =
                    _statistics.GetChartSeries(
                        StatisticsPeriod.Last7Days,
                        StatisticsChartMetric.StorageTemperature,
                        deviceId,
                        maxPoints: 1200);

                if (dayDevice?.Temperature.Maximum.HasValue ==
                        true ||
                    daySeries.Points.Count > 0)
                {
                    historyAverage =
                        dayDevice?
                            .Temperature
                            .Average;

                    historyMaximum =
                        dayDevice?
                            .Temperature
                            .Maximum;

                    historyPeriod =
                        SettingsService.L(
                            "24 часа",
                            "24 hours");

                    repeatedWarmHistory =
                        HasRepeatedStorageHeat(
                            daySeries.Points,
                            thresholds.Warning);
                }
                else if (weekDevice?.Temperature.Maximum.HasValue ==
                             true ||
                         weekSeries.Points.Count > 0)
                {
                    historyAverage =
                        weekDevice?
                            .Temperature
                            .Average;

                    historyMaximum =
                        weekDevice?
                            .Temperature
                            .Maximum;

                    historyPeriod =
                        SettingsService.L(
                            "7 дней",
                            "7 days");

                    repeatedWarmHistory =
                        HasRepeatedStorageHeat(
                            weekSeries.Points,
                            thresholds.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Не удалось получить данные Smart Guidance для накопителя {storage.Name}: {ex}");
            }
        }

        StorageGuidanceLevel level =
            GetStorageTemperatureLevel(
                storage.Temperature,
                historyMaximum,
                repeatedWarmHistory,
                thresholds);

        return new StorageGuidanceDeviceEvidence
        {
            Name =
                string.IsNullOrWhiteSpace(
                    storage.Name)
                    ? SettingsService.L(
                        "Накопитель",
                        "Storage device")
                    : storage.Name,

            HumanType =
                GetStorageHumanType(
                    storage),

            CurrentTemperature =
                storage.Temperature,

            HistoryAverage =
                historyAverage,

            HistoryMaximum =
                historyMaximum,

            HistoryPeriod =
                historyPeriod,

            Level =
                level
        };
    }


    private static StorageStatisticsSummary?
        FindStorageSummary(
            StatisticsSummary summary,
            string deviceId,
            string deviceName)
    {
        return summary.StorageDevices
            .FirstOrDefault(
                item =>
                    string.Equals(
                        item.DeviceId,
                        deviceId,
                        StringComparison.OrdinalIgnoreCase)) ??
            summary.StorageDevices
                .FirstOrDefault(
                    item =>
                        string.Equals(
                            item.DeviceName,
                            deviceName,
                            StringComparison.OrdinalIgnoreCase));
    }


    private static bool HasRepeatedStorageHeat(
        IReadOnlyList<StatisticsChartPoint> points,
        double warningTemperature)
    {
        if (points.Count < 20)
            return false;

        int highCount =
            points.Count(
                point =>
                    point.Value >=
                    warningTemperature);

        if (highCount < 5)
            return false;

        double ratio =
            highCount /
            (double)points.Count;

        return ratio >= 0.10;
    }


    private static StorageGuidanceLevel
        GetStorageTemperatureLevel(
            double? currentTemperature,
            double? historyMaximum,
            bool repeatedWarmHistory,
            StorageGuidanceThresholds thresholds)
    {
        if (currentTemperature.HasValue)
        {
            double current =
                currentTemperature.Value;

            if (current >=
                thresholds.Critical)
            {
                return StorageGuidanceLevel.Critical;
            }

            if (current >=
                thresholds.Warning)
            {
                return StorageGuidanceLevel.Warning;
            }

            if (current >=
                thresholds.Warning - 5)
            {
                return StorageGuidanceLevel.Warm;
            }
        }

        if (repeatedWarmHistory)
        {
            return StorageGuidanceLevel.Warm;
        }

        if (historyMaximum.HasValue &&
            historyMaximum.Value >=
                thresholds.Warning - 5)
        {
            return StorageGuidanceLevel.Warm;
        }

        if (!currentTemperature.HasValue &&
            !historyMaximum.HasValue)
        {
            return StorageGuidanceLevel.Unknown;
        }

        return StorageGuidanceLevel.Normal;
    }


    private static StorageGuidanceThresholds
        GetStorageGuidanceThresholds(
            StorageDeviceInfo storage)
    {
        AppSettings settings =
            SettingsService.Current;

        if (!settings
                .AutomaticStorageTemperatureThresholdsEnabled)
        {
            return new StorageGuidanceThresholds(
                settings.StorageWarningTemperature,
                settings.StorageCriticalTemperature);
        }

        string identifier =
            storage.Id ??
            string.Empty;

        if (identifier.Contains(
                "nvme",
                StringComparison.OrdinalIgnoreCase))
        {
            return new StorageGuidanceThresholds(
                70,
                80);
        }

        if (identifier.Contains(
                "ssd",
                StringComparison.OrdinalIgnoreCase))
        {
            return new StorageGuidanceThresholds(
                60,
                70);
        }

        if (identifier.Contains(
                "hdd",
                StringComparison.OrdinalIgnoreCase))
        {
            return new StorageGuidanceThresholds(
                55,
                60);
        }

        return new StorageGuidanceThresholds(
            65,
            80);
    }


    private static string GetStorageHumanType(
        StorageDeviceInfo storage)
    {
        string identifier =
            storage.Id ??
            string.Empty;

        if (identifier.Contains(
                "nvme",
                StringComparison.OrdinalIgnoreCase))
        {
            return SettingsService.L(
                "Твердотельный накопитель (SSD, NVMe)",
                "Solid-state drive (SSD, NVMe)");
        }

        if (identifier.Contains(
                "ssd",
                StringComparison.OrdinalIgnoreCase))
        {
            return SettingsService.L(
                "Твердотельный накопитель (SSD)",
                "Solid-state drive (SSD)");
        }

        if (identifier.Contains(
                "hdd",
                StringComparison.OrdinalIgnoreCase))
        {
            return SettingsService.L(
                "Обычный жёсткий диск (HDD)",
                "Hard disk drive (HDD)");
        }

        return SettingsService.L(
            "Накопитель данных",
            "Storage device");
    }


    private static string BuildStorageDeviceLine(
        StorageGuidanceDeviceEvidence device)
    {
        List<string> parts =
            new();

        if (device.CurrentTemperature.HasValue)
        {
            parts.Add(
                SettingsService.L(
                    $"сейчас {device.CurrentTemperature.Value:F0} °C",
                    $"now {device.CurrentTemperature.Value:F0} °C"));
        }

        if (device.HistoryMaximum.HasValue)
        {
            string period =
                string.IsNullOrWhiteSpace(
                    device.HistoryPeriod)
                    ? SettingsService.L(
                        "в истории",
                        "in history")
                    : SettingsService.L(
                        $"за {device.HistoryPeriod}",
                        $"over {device.HistoryPeriod}");

            parts.Add(
                SettingsService.L(
                    $"максимум {period} — {device.HistoryMaximum.Value:F0} °C",
                    $"maximum {period} — {device.HistoryMaximum.Value:F0} °C"));
        }

        string state =
            device.Level switch
            {
                StorageGuidanceLevel.Critical =>
                    SettingsService.L(
                        "температура слишком высокая",
                        "temperature is too high"),

                StorageGuidanceLevel.Warning =>
                    SettingsService.L(
                        "накопитель заметно нагрет",
                        "the drive is running hot"),

                StorageGuidanceLevel.Warm =>
                    SettingsService.L(
                        "нагрев заметный, но температура остаётся в допустимом диапазоне",
                        "heat is noticeable, but temperature remains within an acceptable range"),

                StorageGuidanceLevel.Normal =>
                    SettingsService.L(
                        "температура нормальная",
                        "temperature is normal"),

                _ =>
                    SettingsService.L(
                        "температура не определяется",
                        "temperature is not available")
            };

        string readings =
            parts.Count > 0
                ? string.Join(
                    "; ",
                    parts)
                : SettingsService.L(
                    "нет данных о температуре",
                    "no temperature data");

        return
            $"{device.HumanType}: {device.Name} — {readings}. {state}.";
    }


    private static DriveSpaceGuidance
        BuildDriveSpaceGuidance(
            LogicalDriveInfo drive)
    {
        double usedPercent =
            drive.TotalBytes > 0
                ? (drive.TotalBytes -
                   drive.FreeBytes) /
                  (double)drive.TotalBytes *
                  100.0
                : 0;

        AppSettings settings =
            SettingsService.Current;

        StorageGuidanceLevel level =
            usedPercent >=
                settings.DiskCriticalPercent
                ? StorageGuidanceLevel.Critical
                : usedPercent >=
                    settings.DiskWarningPercent
                    ? StorageGuidanceLevel.Warning
                    : StorageGuidanceLevel.Normal;

        return new DriveSpaceGuidance
        {
            Name =
                FormatDriveName(
                    drive.Name),

            FreeBytes =
                drive.FreeBytes,

            FreePercent =
                Math.Clamp(
                    100.0 - usedPercent,
                    0,
                    100),

            Level =
                level
        };
    }


    private static string BuildDriveSpaceLine(
        DriveSpaceGuidance drive)
    {
        string state =
            drive.Level ==
                StorageGuidanceLevel.Critical
                ? SettingsService.L(
                    "свободного места почти не осталось",
                    "almost no free space remains")
                : SettingsService.L(
                    "свободного места осталось мало",
                    "free space is getting low");

        return SettingsService.L(
            $"Диск {drive.Name}: свободно {drive.FreePercent:F0} % " +
            $"({FormatStorageBytes(drive.FreeBytes)}). {state}.",
            $"Drive {drive.Name}: {drive.FreePercent:F0}% free " +
            $"({FormatStorageBytes(drive.FreeBytes)}). {state}.");
    }


    private static string FormatDriveName(
        string name)
    {
        if (string.IsNullOrWhiteSpace(
                name))
        {
            return "—";
        }

        return name
            .Trim()
            .TrimEnd(
                '\\',
                '/');
    }


    private static string FormatStorageBytes(
        long bytes)
    {
        if (bytes <= 0)
        {
            return SettingsService.L(
                "0 ГБ",
                "0 GB");
        }

        double gigabytes =
            bytes /
            1024d /
            1024d /
            1024d;

        if (gigabytes >= 1024)
        {
            return SettingsService.L(
                $"{gigabytes / 1024d:F1} ТБ",
                $"{gigabytes / 1024d:F1} TB");
        }

        return SettingsService.L(
            $"{gigabytes:F1} ГБ",
            $"{gigabytes:F1} GB");
    }


    private void ApplyStorageGuidanceConclusion(
        StorageGuidanceLevel overallLevel,
        StorageGuidanceLevel temperatureLevel,
        StorageGuidanceLevel spaceLevel,
        bool hasTemperatureData)
    {
        bool criticalTemperature =
            temperatureLevel ==
            StorageGuidanceLevel.Critical;

        bool warningTemperature =
            temperatureLevel ==
            StorageGuidanceLevel.Warning;

        bool warmTemperature =
            temperatureLevel ==
            StorageGuidanceLevel.Warm;

        bool criticalSpace =
            spaceLevel ==
            StorageGuidanceLevel.Critical;

        bool warningSpace =
            spaceLevel ==
            StorageGuidanceLevel.Warning;

        if (criticalTemperature &&
            criticalSpace)
        {
            SetStorageGuidanceText(
                SettingsService.L(
                    "Накопители требуют внимания",
                    "Storage needs attention"),

                SettingsService.L(
                    "Одновременно обнаружены слишком высокая температура накопителя " +
                    "и очень малый запас свободного места на одном из дисков.",
                    "Both an excessively high storage temperature and critically low free space " +
                    "have been detected."),

                SettingsService.L(
                    "Сначала снизьте нагрузку и проверьте охлаждение накопителя. " +
                    "Затем освободите место на переполненном диске. " +
                    "На главном экране Thermiqra рядом с диском можно нажать «Анализ».",
                    "First reduce load and check the drive cooling. " +
                    "Then free space on the nearly full drive. " +
                    "On the Thermiqra main screen, use “Analyze” next to the drive."));

            return;
        }

        if (criticalTemperature)
        {
            SetStorageGuidanceText(
                SettingsService.L(
                    "Температура одного из накопителей слишком высокая",
                    "One storage device is too hot"),

                SettingsService.L(
                    "Температура достигла уровня, при котором длительная работа нежелательна. " +
                    "Thermiqra учитывает тип накопителя, а не применяет один порог ко всем устройствам.",
                    "Temperature has reached a level where prolonged operation is undesirable. " +
                    "Thermiqra accounts for the storage type instead of using one threshold for every device."),

                SettingsService.L(
                    "Снизьте нагрузку и проверьте охлаждение в области накопителя: " +
                    "пыль, обдув и, если установлен радиатор, его прилегание. " +
                    "Если температура не снижается, не оставляйте накопитель под длительной высокой нагрузкой.",
                    "Reduce load and check cooling around the drive: " +
                    "dust, airflow and, if a heatsink is installed, its contact. " +
                    "If temperature does not drop, avoid prolonged heavy load on the drive."));

            return;
        }

        if (criticalSpace)
        {
            SetStorageGuidanceText(
                SettingsService.L(
                    "На одном из дисков почти не осталось свободного места",
                    "One drive is almost out of free space"),

                SettingsService.L(
                    "Когда диск почти заполнен, Windows и программы могут работать хуже, " +
                    "а обновлениям и временным файлам может не хватать места.",
                    "When a drive is nearly full, Windows and applications can work less smoothly, " +
                    "and updates or temporary files may run out of room."),

                SettingsService.L(
                    "Освободите место. На главном экране Thermiqra рядом с нужным диском " +
                    "нажмите «Анализ», чтобы увидеть, какие папки и файлы занимают больше всего места.",
                    "Free some space. On the Thermiqra main screen, use “Analyze” next to that drive " +
                    "to see which folders and files occupy the most space."));

            return;
        }

        if (warningTemperature)
        {
            SetStorageGuidanceText(
                SettingsService.L(
                    "Один из накопителей заметно нагрет",
                    "One storage device is running hot"),

                SettingsService.L(
                    "Температура высокая для этого типа накопителя, но это ещё не означает неисправность. " +
                    "Важно, держится ли такой нагрев долго и повторяется ли он под обычной нагрузкой.",
                    "Temperature is high for this type of storage device, but this does not by itself indicate a fault. " +
                    "What matters is whether this heat lasts and keeps returning under normal use."),

                SettingsService.L(
                    "Если температура долго держится высокой, проверьте пыль и обдув в корпусе. " +
                    "Если накопитель установлен под радиатором, убедитесь, что радиатор установлен нормально.",
                    "If the temperature stays high for a long time, check dust and airflow in the case. " +
                    "If the drive uses a heatsink, make sure the heatsink is properly installed."));

            return;
        }

        if (warningSpace)
        {
            SetStorageGuidanceText(
                SettingsService.L(
                    "На одном из дисков мало свободного места",
                    "One drive is low on free space"),

                SettingsService.L(
                    "Диск уже близок к заполнению. Это не авария, но запас места лучше увеличить " +
                    "до того, как он закончится.",
                    "The drive is getting close to full. This is not an emergency, " +
                    "but it is better to increase the free-space margin before it runs out."),

                SettingsService.L(
                    "На главном экране Thermiqra рядом с этим диском нажмите «Анализ», " +
                    "чтобы быстро увидеть самые крупные папки и файлы.",
                    "On the Thermiqra main screen, use “Analyze” next to that drive " +
                    "to quickly see the largest folders and files."));

            return;
        }

        if (warmTemperature)
        {
            SetStorageGuidanceText(
                SettingsService.L(
                    "Накопитель нагревался заметно, но остаётся в допустимом диапазоне",
                    "A storage device ran noticeably warm but remains in an acceptable range"),

                SettingsService.L(
                    "Для этого типа накопителя зафиксированный нагрев ещё находится в допустимом рабочем диапазоне. " +
                    "Сам по себе такой максимум не является критическим состоянием.",
                    "For this type of storage device, the observed heat is still within an acceptable operating range. " +
                    "That maximum by itself is not a critical condition."),

                SettingsService.L(
                    "Срочных действий не требуется. Если температура часто держится близко к верхней границе " +
                    "во время длительной нагрузки, проверьте радиатор и обдув.",
                    "No urgent action is required. If temperature often stays close to the upper end " +
                    "during sustained load, check the heatsink and airflow."));

            return;
        }

        if (!hasTemperatureData)
        {
            SetStorageGuidanceText(
                SettingsService.L(
                    "Свободное место в норме",
                    "Free space looks normal"),

                SettingsService.L(
                    "Проблем со свободным местом не видно. Температуру некоторых накопителей " +
                    "оборудование может не предоставлять.",
                    "No free-space issue is visible. Some storage devices may not expose temperature data."),

                SettingsService.L(
                    "Ничего делать не требуется.",
                    "No action is required."));

            return;
        }

        SetStorageGuidanceText(
            SettingsService.L(
                "С накопителями всё выглядит нормально",
                "Storage looks normal"),

            SettingsService.L(
                "Температуры накопителей и запас свободного места сейчас не требуют внимания.",
                "Storage temperatures and available free space do not currently require attention."),

            SettingsService.L(
                "Ничего делать не требуется.",
                "No action is required."));
    }


    private void SetStorageGuidanceText(
        string status,
        string meaning,
        string action)
    {
        if (_storageGuidanceStatusText != null)
        {
            _storageGuidanceStatusText.Text =
                status;
        }

        if (_storageGuidanceMeaningText != null)
        {
            _storageGuidanceMeaningText.Text =
                meaning;
        }

        if (_storageGuidanceActionText != null)
        {
            _storageGuidanceActionText.Text =
                action;
        }
    }


    private void AddStorageDetailLine(
        string text)
    {
        if (_storageGuidanceDetailsPanel == null)
            return;

        TextBlock line =
            CreateGuidanceValueText();

        line.Text =
            text;

        if (_storageGuidanceDetailsPanel
                .Children
                .Count > 0)
        {
            line.Margin =
                new Thickness(
                    0,
                    6,
                    0,
                    0);
        }

        _storageGuidanceDetailsPanel
            .Children
            .Add(
                line);
    }


    private readonly record struct
        StorageGuidanceThresholds(
            double Warning,
            double Critical);


    private sealed class StorageGuidanceDeviceEvidence
    {
        public string Name { get; init; } = "";

        public string HumanType { get; init; } = "";

        public double? CurrentTemperature { get; init; }

        public double? HistoryAverage { get; init; }

        public double? HistoryMaximum { get; init; }

        public string? HistoryPeriod { get; init; }

        public StorageGuidanceLevel Level { get; init; }
    }


    private sealed class DriveSpaceGuidance
    {
        public string Name { get; init; } = "";

        public long FreeBytes { get; init; }

        public double FreePercent { get; init; }

        public StorageGuidanceLevel Level { get; init; }
    }


    private enum StorageGuidanceLevel
    {
        Unknown = -1,
        Normal = 0,
        Warm = 1,
        Warning = 2,
        Critical = 3
    }



}
