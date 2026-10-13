﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace PCHardwareMonitor;

public sealed class AlertService
{
    private const double TemperatureRecoveryMargin = 5.0;

    private readonly NotificationService _notifications;

    private readonly Action _openAction;

    private readonly StatisticsService? _statistics;

    private readonly Dictionary<string, AlertState>
        _states = new();

    public AlertService(
        NotificationService notifications,
        Action openAction)
    {
        _notifications =
            notifications;

        _openAction =
            openAction;

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
                    true,

                recoveryMargin:
                    TemperatureRecoveryMargin);
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
                        true,

                    recoveryMargin:
                        TemperatureRecoveryMargin);
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

                StorageTemperatureThresholds thresholds =
                    GetStorageTemperatureThresholds(
                        storage,
                        settings);

                EvaluateValue(
                    key:
                        $"STORAGE_TEMP_{i}",

                    name:
                        storage.Name,

                    value:
                        storage.Temperature,

                    warning:
                        thresholds.Warning,

                    critical:
                        thresholds.Critical,

                    unit:
                        "°C",

                    subject:
                        SettingsService.L(
                            "Температура накопителя",
                            "Storage temperature"),

                    repeatCritical:
                        true,

                    recoveryMargin:
                        TemperatureRecoveryMargin);
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
                        false,

                    recoveryMargin:
                        0);
            }
        }
        else
        {
            RemoveStatesWithPrefix(
                "DISK_");
        }
    }

    public void ResetStates()
    {
        _states.Clear();
    }


    // ============================================================
    // ПОРОГИ ТЕМПЕРАТУРЫ НАКОПИТЕЛЕЙ
    // ============================================================

    private static StorageTemperatureThresholds
        GetStorageTemperatureThresholds(
            StorageDeviceInfo storage,
            AppSettings settings)
    {
        // Автоматический режим включён по умолчанию.
        // Ручные пороги используются только если пользователь
        // сознательно отключил автоматическую настройку.
        if (!settings.AutomaticStorageTemperatureThresholdsEnabled)
        {
            return new StorageTemperatureThresholds(
                settings.StorageWarningTemperature,
                settings.StorageCriticalTemperature);
        }

        StorageKind kind =
            DetectStorageKind(
                storage);

        // Тип определяется по внутреннему Identifier,
        // который LibreHardwareMonitor формирует для класса накопителя.
        // Если тип неизвестен, используем осторожный универсальный профиль:
        // раннее предупреждение без прежнего Critical на 65 °C.
        return kind switch
        {
            StorageKind.Hdd =>
                new StorageTemperatureThresholds(
                    55,
                    60),

            StorageKind.SataSsd =>
                new StorageTemperatureThresholds(
                    60,
                    70),

            StorageKind.Nvme =>
                new StorageTemperatureThresholds(
                    70,
                    80),

            _ =>
                new StorageTemperatureThresholds(
                    65,
                    80)
        };
    }

    private static StorageKind DetectStorageKind(
        StorageDeviceInfo storage)
    {
        string identifier =
            storage.Id ??
            string.Empty;

        if (identifier.Contains(
                "nvme",
                StringComparison.OrdinalIgnoreCase))
        {
            return StorageKind.Nvme;
        }

        if (identifier.Contains(
                "ssd",
                StringComparison.OrdinalIgnoreCase))
        {
            return StorageKind.SataSsd;
        }

        if (identifier.Contains(
                "hdd",
                StringComparison.OrdinalIgnoreCase))
        {
            return StorageKind.Hdd;
        }

        return StorageKind.Unknown;
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
        bool repeatCritical,
        double recoveryMargin)
    {
        if (!value.HasValue)
        {
            RemoveState(key);
            return;
        }

        DateTime now =
            DateTime.UtcNow;

        double currentValue =
            value.Value;

        AlertLevel targetLevel =
            currentValue >= critical
                ? AlertLevel.Critical

                : currentValue >= warning
                    ? AlertLevel.Warning

                    : AlertLevel.None;

        if (!_states.TryGetValue(
                key,
                out AlertState? state))
        {
            if (targetLevel ==
                AlertLevel.None)
            {
                return;
            }

            _states[key] =
                new AlertState
                {
                    Level =
                        targetLevel,

                    SinceUtc =
                        now
                };

            return;
        }

        // ========================================================
        // ВОССТАНОВЛЕНИЕ С ГИСТЕРЕЗИСОМ
        // ========================================================

        double recoveryThreshold =
            warning -
            Math.Max(
                0,
                recoveryMargin);

        // Температурный эпизод заканчивается не сразу при падении
        // ниже Warning, а только после заметного охлаждения.
        // Например, при Warning 80 °C и запасе 5 °C новый эпизод
        // разрешается только после охлаждения до 75 °C или ниже.
        if (targetLevel ==
                AlertLevel.None &&
            currentValue <=
                recoveryThreshold)
        {
            RemoveState(key);
            return;
        }

        // Уровень изменился: начинаем выдержку заново.
        // Это не сбрасывает уже показанные уведомления текущего эпизода.
        if (state.Level !=
            targetLevel)
        {
            state.Level =
                targetLevel;

            state.SinceUtc =
                now;
        }

        // Значение ниже Warning, но ещё не достигло порога восстановления.
        // Эпизод сохраняем, уведомления не показываем.
        if (targetLevel ==
            AlertLevel.None)
        {
            return;
        }

        int delaySeconds =
            Math.Clamp(
                SettingsService
                    .Current
                    .AlertDelaySeconds,
                1,
                120);

        // Порог должен удерживаться заданное время.
        if ((now -
             state.SinceUtc).TotalSeconds <
            delaySeconds)
        {
            return;
        }

        // ========================================================
        // WARNING — ОДИН РАЗ ЗА ЭПИЗОД
        // ========================================================

        if (targetLevel ==
            AlertLevel.Warning)
        {
            // После уже показанного Warning или Critical
            // повторный Warning в том же эпизоде не нужен.
            if (state.WarningNotified ||
                state.CriticalNotified)
            {
                return;
            }

            SendNotification(
                name,
                currentValue,
                unit,
                subject,
                AlertLevel.Warning);

            SaveStatisticsEvent(
                key,
                name,
                currentValue,
                unit,
                subject,
                AlertLevel.Warning);

            state.WarningNotified =
                true;

            state.LastNotificationUtc =
                now;

            return;
        }

        // ========================================================
        // CRITICAL — ПЕРВОЕ СРАБАТЫВАНИЕ / ЭСКАЛАЦИЯ
        // ========================================================

        if (!state.CriticalNotified)
        {
            SendNotification(
                name,
                currentValue,
                unit,
                subject,
                AlertLevel.Critical);

            SaveStatisticsEvent(
                key,
                name,
                currentValue,
                unit,
                subject,
                AlertLevel.Critical);

            state.CriticalNotified =
                true;

            state.LastNotificationUtc =
                now;

            return;
        }

        // ========================================================
        // ПОВТОР CRITICAL
        // ========================================================

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

        if (!state.LastNotificationUtc.HasValue)
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

        if ((now -
             state.LastNotificationUtc.Value)
            .TotalMinutes <
            repeatMinutes)
        {
            return;
        }

        SendNotification(
            name,
            currentValue,
            unit,
            subject,
            AlertLevel.Critical);

        state.LastNotificationUtc =
            now;
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

        _notifications.Show(
            critical
                ? NotificationType.Critical
                : NotificationType.Warning,
            title,
            message,
            _openAction);
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

        public bool WarningNotified { get; set; }

        public bool CriticalNotified { get; set; }
    }

    private readonly record struct StorageTemperatureThresholds(
        double Warning,
        double Critical);

    private enum StorageKind
    {
        Unknown,
        Hdd,
        SataSsd,
        Nvme
    }

    private enum AlertLevel
    {
        None,
        Warning,
        Critical
    }
}