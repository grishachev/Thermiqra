using System;
using System.IO;
using System.Text.Json;

namespace PCHardwareMonitor;

public sealed class AppSettings
{
    // ============================================================
    // ПЕРВЫЙ ЗАПУСК / ВНЕШНИЙ ВИД
    // ============================================================

    public bool FirstRunCompleted { get; set; } = false;

    public string SkinName { get; set; } = "CyberTech";

    public string ThemeName { get; set; } = "SteelBlue";


    // ============================================================
    // ЗАПУСК / ТРЕЙ
    // ============================================================

    public bool AutoStartWithWindows { get; set; } = false;

    public bool MinimizeToTrayOnClose { get; set; } = true;

    public bool ShowMainWindowOnStartup { get; set; } = false;


    // ============================================================
    // ОБЩИЕ УВЕДОМЛЕНИЯ
    // ============================================================

    public bool NotificationsEnabled { get; set; } = true;

    public int AlertDelaySeconds { get; set; } = 8;


    // ============================================================
    // ОТДЕЛЬНЫЕ ТИПЫ УВЕДОМЛЕНИЙ
    // ============================================================

    public bool CpuTemperatureNotificationsEnabled { get; set; } = true;

    public bool GpuTemperatureNotificationsEnabled { get; set; } = true;

    public bool StorageTemperatureNotificationsEnabled { get; set; } = true;

    public bool DiskSpaceNotificationsEnabled { get; set; } = true;


    // ============================================================
    // ПОВТОР КРИТИЧЕСКИХ ТЕМПЕРАТУРНЫХ УВЕДОМЛЕНИЙ
    // ============================================================

    public bool RepeatCriticalTemperatureAlerts { get; set; } = true;

    public int CriticalRepeatMinutes { get; set; } = 5;


    // ============================================================
    // CPU
    // ============================================================

    public double CpuWarningTemperature { get; set; } = 80;

    public double CpuCriticalTemperature { get; set; } = 90;


    // ============================================================
    // GPU
    // ============================================================

    public double GpuWarningTemperature { get; set; } = 78;

    public double GpuCriticalTemperature { get; set; } = 85;


    // ============================================================
    // ТЕМПЕРАТУРА НАКОПИТЕЛЕЙ
    // ============================================================

    public double StorageWarningTemperature { get; set; } = 55;

    public double StorageCriticalTemperature { get; set; } = 65;


    // ============================================================
    // ЗАПОЛНЕНИЕ ДИСКОВ
    // ============================================================

    public double DiskWarningPercent { get; set; } = 90;

    public double DiskCriticalPercent { get; set; } = 95;
}


public static class SettingsService
{
    private static readonly string LocalAppData =
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);


    // Новая папка Thermiqra
    private static readonly string SettingsDirectory =
        Path.Combine(
            LocalAppData,
            "Thermiqra");


    // Старая папка предыдущих тестовых версий
    private static readonly string LegacySettingsDirectory =
        Path.Combine(
            LocalAppData,
            "PCHardwareMonitor");


    private static readonly string SettingsPath =
        Path.Combine(
            SettingsDirectory,
            "settings.json");


    private static readonly string LegacySettingsPath =
        Path.Combine(
            LegacySettingsDirectory,
            "settings.json");


    public static AppSettings Current { get; private set; } =
        new();


    // ============================================================
    // ЗАГРУЗКА НАСТРОЕК
    // ============================================================

    public static void Load()
    {
        try
        {
            MigrateLegacySettings();

            if (!File.Exists(SettingsPath))
            {
                Current =
                    new AppSettings();

                return;
            }

            string json =
                File.ReadAllText(
                    SettingsPath);

            Current =
                JsonSerializer.Deserialize<AppSettings>(
                    json)
                ?? new AppSettings();
        }
        catch
        {
            Current =
                new AppSettings();
        }
    }


    // ============================================================
    // СОХРАНЕНИЕ
    // ============================================================

    public static void Save()
    {
        Directory.CreateDirectory(
            SettingsDirectory);

        string json =
            JsonSerializer.Serialize(
                Current,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        File.WriteAllText(
            SettingsPath,
            json);
    }


    // ============================================================
    // ПЕРЕНОС НАСТРОЕК СО СТАРОГО ИМЕНИ
    // ============================================================

    private static void MigrateLegacySettings()
    {
        try
        {
            // Если новая версия уже имеет настройки —
            // ничего переносить не нужно.
            if (File.Exists(SettingsPath))
                return;

            // Старых настроек нет.
            if (!File.Exists(LegacySettingsPath))
                return;

            Directory.CreateDirectory(
                SettingsDirectory);

            File.Copy(
                LegacySettingsPath,
                SettingsPath,
                overwrite: false);
        }
        catch
        {
            // Ошибка переноса не должна мешать запуску Thermiqra.
        }
    }
}