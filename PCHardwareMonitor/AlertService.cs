using System;
using System.Collections.Generic;
using System.Linq;

namespace PCHardwareMonitor;

public sealed class AlertService
{
    private readonly TrayService _tray;

    private readonly StatisticsService? _statistics;

    private readonly Dictionary<string, AlertState>
        _states = new();

    public AlertService(
        TrayService tray)
    {
        _tray = tray;

        try
        {
            _statistics =
                new StatisticsService();
        }
        catch (Exception ex)
        {
            _statistics = null;

            System.Diagnostics.Debug.WriteLine(
                $"Не удалось подключить журнал событий статистики: {ex}");
        }
    }

    public void Evaluate(
        HardwareSnapshot snapshot)
    {
        AppSettings settings =
            SettingsService.Current;

        // Главный выключатель всех уведомлений.
        if (!settings.NotificationsEnabled)
        {
            _states.Clear();
            return;
        }

        // ========================================================
        // CPU
        // ========================================================

        if (settings.CpuTemperatureNotificationsEnabled)
        {
            EvaluateValue(
                key: "CPU_TEMP",

                name: "CPU",

                value:
                    snapshot.Cpu.Temperature,

                warning:
                    settings.CpuWarningTemperature,

                critical:
                    settings.CpuCriticalTemperature,

                unit: "°C",

                subject:
                    SettingsService.L(
                        "Температура",
                        "Temperature"),

                repeatCritical:
                    true);
        }
        else
        {
            RemoveState(
                "CPU_TEMP");
        }

        // ========================================================
        // GPU
        // ========================================================

        if (settings.GpuTemperatureNotificationsEnabled)
        {
            for (int i = 0;
                 i < snapshot.Gpus.Count;
                 i++)
            {
                GpuInfo gpu =
                    snapshot.Gpus[i];

                EvaluateValue(
                    key:
                        $"GPU_TEMP_{i}",

                    name:
                        string.IsNullOrWhiteSpace(
                            gpu.Name)

                            ? $"GPU {i + 1}"
                            : gpu.Name,

                    value:
                        gpu.Temperature,

                    warning:
                        settings.GpuWarningTemperature,

                    critical:
                        settings.GpuCriticalTemperature,

                    unit:
                        "°C",

                    subject:
                        SettingsService.L(
                            "Температура",
                            "Temperature"),

                    repeatCritical:
                        true);
            }
        }
        else
        {
            RemoveStatesWithPrefix(
                "GPU_TEMP_");
        }

        // ========================================================
        // ТЕМПЕРАТУРА НАКОПИТЕЛЕЙ
        // ========================================================

        if (settings.StorageTemperatureNotificationsEnabled)
        {
            for (int i = 0;
                 i < snapshot.StorageDevices.Count;
                 i++)
            {
                StorageDeviceInfo storage =
                    snapshot.StorageDevices[i];

                EvaluateValue(
                    key:
                        $"STORAGE_TEMP_{i}",

                    name:
                        storage.Name,

                    value:
                        storage.Temperature,

                    warning:
                        settings.StorageWarningTemperature,

                    critical:
                        settings.StorageCriticalTemperature,

                    unit:
                        "°C",

                    subject:
                        SettingsService.L(
                            "Температура накопителя",
                            "Storage temperature"),

                    repeatCritical:
                        true);
            }
        }
        else
        {
            RemoveStatesWithPrefix(
                "STORAGE_TEMP_");
        }

        // ========================================================
        // ЗАПОЛНЕНИЕ ДИСКОВ
        // ========================================================

        if (settings.DiskSpaceNotificationsEnabled)
        {
            foreach (LogicalDriveInfo drive
                     in snapshot.Drives)
            {
                if (drive.TotalBytes <= 0)
                    continue;

                double usedPercent =
                    100.0 -
                    (
                        drive.FreeBytes /
                        (double)drive.TotalBytes *
                        100.0
                    );

                EvaluateValue(
                    key:
                        $"DISK_{drive.Name}",

                    name:
                        drive.Name,

                    value:
                        usedPercent,

                    warning:
                        settings.DiskWarningPercent,

                    critical:
                        settings.DiskCriticalPercent,

                    unit:
                        "%",

                    subject:
                        SettingsService.L(
                            "Заполнение диска",
                            "Disk usage"),

                    // Диски не повторяем.
                    repeatCritical:
                        false);
            }
        }
        else
        {
            RemoveStatesWithPrefix(
                "DISK_");
        }
    }

    // ============================================================
    // ПРОВЕРКА ОДНОГО ПОКАЗАТЕЛЯ
    // ============================================================

    private void EvaluateValue(
        string key,
        string name,
        double? value,
        double warning,
        double critical,
        string unit,
        string subject,
        bool repeatCritical)
    {
        if (!value.HasValue)
        {
            RemoveState(key);
            return;
        }

        AlertLevel targetLevel =
            value.Value >= critical
                ? AlertLevel.Critical

                : value.Value >= warning
                    ? AlertLevel.Warning

                    : AlertLevel.None;

        // Показатель вернулся в норму.
        // Сбрасываем состояние полностью.
        if (targetLevel ==
            AlertLevel.None)
        {
            RemoveState(key);
            return;
        }

        // Первый переход через порог
        // или изменение уровня Warning -> Critical.
        if (!_states.TryGetValue(
                key,
                out AlertState? state) ||
            state.Level != targetLevel)
        {
            _states[key] =
                new AlertState
                {
                    Level =
                        targetLevel,

                    SinceUtc =
                        DateTime.UtcNow,

                    LastNotificationUtc =
                        null
                };

            return;
        }

        int delaySeconds =
            Math.Clamp(
                SettingsService
                    .Current
                    .AlertDelaySeconds,
                1,
                120);

        // Ждём, пока проблема продержится
        // заданное количество секунд.
        if ((DateTime.UtcNow -
             state.SinceUtc).TotalSeconds <
            delaySeconds)
        {
            return;
        }

        // ========================================================
        // ПЕРВОЕ УВЕДОМЛЕНИЕ
        // ========================================================

        if (!state.LastNotificationUtc.HasValue)
        {
            SendNotification(
                name,
                value.Value,
                unit,
                subject,
                targetLevel);

            SaveStatisticsEvent(
                key,
                name,
                value.Value,
                unit,
                subject,
                targetLevel);

            state.LastNotificationUtc =
                DateTime.UtcNow;

            return;
        }

        // ========================================================
        // ПОВТОР КРИТИЧЕСКИХ ТЕМПЕРАТУР
        // ========================================================

        if (targetLevel !=
            AlertLevel.Critical)
        {
            return;
        }

        if (!repeatCritical)
        {
            return;
        }

        if (!SettingsService
                .Current
                .RepeatCriticalTemperatureAlerts)
        {
            return;
        }

        int repeatMinutes =
            Math.Clamp(
                SettingsService
                    .Current
                    .CriticalRepeatMinutes,
                1,
                120);

        if ((DateTime.UtcNow -
             state.LastNotificationUtc.Value)
            .TotalMinutes <
            repeatMinutes)
        {
            return;
        }

        SendNotification(
            name,
            value.Value,
            unit,
            subject,
            targetLevel);

        state.LastNotificationUtc =
            DateTime.UtcNow;
    }

    // ============================================================
    // ОТПРАВКА УВЕДОМЛЕНИЯ
    // ============================================================

    private void SendNotification(
        string name,
        double value,
        string unit,
        string subject,
        AlertLevel level)
    {
        bool critical =
            level ==
            AlertLevel.Critical;

        string title =
            critical
                ? SettingsService.L(
                    $"Критическое состояние: {name}",
                    $"Critical condition: {name}")
                : SettingsService.L(
                    $"Предупреждение: {name}",
                    $"Warning: {name}");

        string message =
            $"{subject}: " +
            $"{value:F0} {unit}";

        _tray.ShowNotification(
            title,
            message,
            critical);
    }

    private void SaveStatisticsEvent(
        string key,
        string name,
        double value,
        string unit,
        string subject,
        AlertLevel level)
    {
        if (_statistics == null)
            return;

        try
        {
            StatisticsAlertLevel statisticsLevel =
                level == AlertLevel.Critical
                    ? StatisticsAlertLevel.Critical
                    : StatisticsAlertLevel.Warning;

            _statistics.SaveAlertEvent(
                key,
                statisticsLevel,
                name,
                subject,
                value,
                unit);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Не удалось сохранить событие предупреждения: {ex}");
        }
    }

    // ============================================================
    // СБРОС СОСТОЯНИЙ
    // ============================================================

    private void RemoveState(
        string key)
    {
        _states.Remove(key);
    }

    private void RemoveStatesWithPrefix(
        string prefix)
    {
        string[] keys =
            _states.Keys
                .Where(
                    key =>
                        key.StartsWith(
                            prefix,
                            StringComparison.Ordinal))
                .ToArray();

        foreach (string key in keys)
        {
            _states.Remove(key);
        }
    }

    // ============================================================
    // ВНУТРЕННЕЕ СОСТОЯНИЕ
    // ============================================================

    private sealed class AlertState
    {
        public AlertLevel Level { get; set; }

        public DateTime SinceUtc { get; set; }

        public DateTime? LastNotificationUtc { get; set; }
    }

    private enum AlertLevel
    {
        None,
        Warning,
        Critical
    }
}