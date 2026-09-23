using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace PCHardwareMonitor;

public sealed class AppSettings
{
    // ============================================================
    // ПЕРВЫЙ ЗАПУСК / ВНЕШНИЙ ВИД
    // ============================================================

    public bool FirstRunCompleted { get; set; } = false;

    public string SkinName { get; set; } = "CyberTech";

    public string ThemeName { get; set; } = "SteelBlue";

    public string LanguageName { get; set; } = "System";


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
    private static readonly string SystemLanguageCode =
        string.Equals(
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
            "ru",
            StringComparison.OrdinalIgnoreCase)
            ? "ru"
            : "en";


    private static readonly Dictionary<string, string> RussianToEnglish =
        new(StringComparer.Ordinal)
        {
        ["Запуск мониторинга..."] = "Starting monitoring...",
        ["Статистика"] = "Statistics",
        ["Система"] = "System",
        ["⚙  Настройки"] = "⚙  Settings",
        ["МЕХАНИЧЕСКАЯ СТАНЦИЯ МОНИТОРИНГА"] = "MECHANICAL MONITORING STATION",
        ["СОСТОЯНИЕ СИСТЕМЫ"] = "SYSTEM STATUS",
        ["МОНИТОРИНГ АКТИВЕН"] = "MONITORING ACTIVE",
        ["СТАТИСТИКА"] = "STATISTICS",
        ["СИСТЕМА"] = "SYSTEM",
        ["⚙  НАСТРОЙКИ"] = "⚙  SETTINGS",
        ["КРИОГЕННАЯ СИСТЕМА МОНИТОРИНГА"] = "CRYOGENIC MONITORING SYSTEM",
        ["КРИОКОНТУР"] = "CRYO CIRCUIT",
        ["ОХЛАЖДЕНИЕ АКТИВНО"] = "COOLING ACTIVE",
        ["ТАКТИЧЕСКИЙ ЦЕНТР МОНИТОРИНГА"] = "TACTICAL MONITORING CENTER",
        ["СТАТУС ПОСТА"] = "STATION STATUS",
        ["ПОСТ АКТИВЕН"] = "STATION ACTIVE",
        ["СЕКТОР 01  •  КАНАЛ A"] = "SECTOR 01  •  CHANNEL A",
        ["ОПЕРАТИВНАЯ ПАМЯТЬ"] = "SYSTEM MEMORY",
        ["Нет данных"] = "No data",
        ["УРОВЕНЬ ЗАПОЛНЕНИЯ ТРУБОПРОВОДА ПАМЯТИ"] = "MEMORY PIPELINE FILL LEVEL",
        ["ЗАНЯТО"] = "USED",
        ["КРИОКАМЕРА ОПЕРАТИВНОЙ ПАМЯТИ"] = "SYSTEM MEMORY CRYO CHAMBER",
        ["КОНТУР ОХЛАЖДАЮЩЕЙ ЖИДКОСТИ / ПАМЯТЬ"] = "COOLANT CIRCUIT / MEMORY",
        ["ЗАПОЛНЕНИЕ"] = "USAGE",
        ["КРИОУРОВЕНЬ"] = "CRYO LEVEL",
        ["ОПЕРАТИВНЫЙ РЕЗЕРВ ПАМЯТИ"] = "SYSTEM MEMORY RESERVE",
        ["РЕСУРС ПАМЯТИ / 10 СЕКТОРОВ"] = "MEMORY CAPACITY / 10 SECTORS",
        ["РЕЗЕРВ"] = "RESERVE",
        ["ДИСКИ"] = "DRIVES",
        ["ДИСКОВЫЕ МЕХАНИЗМЫ"] = "DRIVE MECHANISMS",
        ["  /  НАКОПИТЕЛИ"] = "  /  STORAGE",
        ["КРИОХРАНИЛИЩЕ ДАННЫХ"] = "CRYOGENIC DATA STORAGE",
        ["  /  ГЕРМЕТИЧНЫЕ МОДУЛИ"] = "  /  SEALED MODULES",
        ["СЕКТОР ХРАНИЛИЩ"] = "STORAGE SECTOR",
        ["  /  КОНТЕЙНЕРЫ ДАННЫХ"] = "  /  DATA CONTAINERS",
        ["ТЕМПЕРАТУРА НАКОПИТЕЛЕЙ"] = "STORAGE TEMPERATURE",
        ["ТЕРМОМЕТРЫ НАКОПИТЕЛЕЙ"] = "STORAGE THERMOMETERS",
        ["  /  КОНТРОЛЬ ТЕМПЕРАТУРЫ"] = "  /  TEMPERATURE CONTROL",
        ["ТЕМПЕРАТУРА КРИОМОДУЛЕЙ"] = "CRYO MODULE TEMPERATURE",
        ["  /  ТЕПЛОВОЙ КОНТРОЛЬ"] = "  /  THERMAL CONTROL",
        ["ТЕПЛОВОЙ КОНТРОЛЬ СЕКТОРА"] = "SECTOR THERMAL CONTROL",
        ["  /  ПОЛЕВЫЕ ДАТЧИКИ"] = "  /  FIELD SENSORS",
        ["  •  МЕХАНИЧЕСКАЯ СИСТЕМА МОНИТОРИНГА"] = "  •  MECHANICAL MONITORING SYSTEM",
        ["  •  КРИОГЕННАЯ СИСТЕМА МОНИТОРИНГА"] = "  •  CRYOGENIC MONITORING SYSTEM",
        ["  •  ТАКТИЧЕСКИЙ ЦЕНТР МОНИТОРИНГА"] = "  •  TACTICAL MONITORING CENTER",
        ["Настройки — Thermiqra"] = "Settings — Thermiqra",
        ["НАСТРОЙКИ СИСТЕМЫ"] = "SYSTEM SETTINGS",
        ["ПАНЕЛЬ УПРАВЛЕНИЯ"] = "CONTROL PANEL",
        ["НАСТРОЙКИ"] = "SETTINGS",
        ["ЛОКАЛЬНЫЙ ПРОФИЛЬ"] = "LOCAL PROFILE",
        ["МЕХАНИЧЕСКАЯ ПАНЕЛЬ НАСТРОЕК"] = "MECHANICAL SETTINGS PANEL",
        ["КОНФИГУРАЦИЯ"] = "CONFIGURATION",
        ["КРИОГЕННАЯ ПАНЕЛЬ НАСТРОЕК"] = "CRYOGENIC SETTINGS PANEL",
        ["ТАКТИЧЕСКАЯ ПАНЕЛЬ НАСТРОЕК"] = "TACTICAL SETTINGS PANEL",
        ["ПОЛЕВОЙ ПРОФИЛЬ"] = "FIELD PROFILE",
        ["ВНЕШНИЙ ВИД"] = "APPEARANCE",
        ["ВЫБОР СКИНА ИНТЕРФЕЙСА"] = "INTERFACE SKIN SELECTION",
        ["СКИН ИНТЕРФЕЙСА"] = "INTERFACE SKIN",
        ["ТЕХНИЧЕСКИЙ HUD-ИНТЕРФЕЙС"] = "TECHNICAL HUD INTERFACE",
        ["ЛАТУНЬ / МЕДЬ / МЕХАНИКА"] = "BRASS / COPPER / MECHANICS",
        ["КРИОГЕННАЯ СТАНЦИЯ"] = "CRYOGENIC STATION",
        ["ТАКТИЧЕСКИЙ ЦЕНТР"] = "TACTICAL CENTER",
        ["Каждый скин использует собственное цветовое оформление."] = "Each skin uses its own color scheme.",
        ["ЗАПУСК И ТРЕЙ"] = "STARTUP AND TRAY",
        ["Запускать вместе с Windows"] = "Start with Windows",
        ["При закрытии окна сворачивать в трей"] = "Minimize to tray when closing the window",
        ["Показывать главное окно при запуске"] = "Show the main window on startup",
        ["После первого запуска Thermiqra по умолчанию работает скрыто в системном трее."] = "After the first run, Thermiqra works hidden in the system tray by default.",
        ["УВЕДОМЛЕНИЯ"] = "NOTIFICATIONS",
        ["Показывать уведомления"] = "Show notifications",
        ["Задержка первого предупреждения"] = "Initial alert delay",
        ["СЕКУНДЫ"] = "SECONDS",
        ["Повтор критического предупреждения"] = "Critical alert repeat interval",
        ["МИНУТЫ"] = "MINUTES",
        ["Повторять критические температурные предупреждения"] = "Repeat critical temperature alerts",
        ["Повтор применяется только к критической температуре CPU, GPU и накопителей. Предупреждения о заполнении диска не повторяются."] = "Repeat applies only to critical CPU, GPU, and storage temperatures. Disk usage alerts are not repeated.",
        ["ТЕМПЕРАТУРНЫЕ ПОРОГИ"] = "TEMPERATURE THRESHOLDS",
        ["Контроль температуры CPU"] = "CPU temperature monitoring",
        ["ПРЕДУПРЕЖДЕНИЕ, °C"] = "WARNING, °C",
        ["КРИТИЧЕСКАЯ, °C"] = "CRITICAL, °C",
        ["Контроль температуры GPU"] = "GPU temperature monitoring",
        ["НАКОПИТЕЛИ"] = "STORAGE",
        ["ТЕМПЕРАТУРА И ЗАПОЛНЕНИЕ ДИСКОВ"] = "TEMPERATURE AND DISK USAGE",
        ["Контроль температуры накопителей"] = "Storage temperature monitoring",
        ["Контроль свободного места на дисках"] = "Disk free-space monitoring",
        ["ПРЕДУПРЕЖДЕНИЕ, %"] = "WARNING, %",
        ["КРИТИЧЕСКОЕ, %"] = "CRITICAL, %",
        ["THERMIQRA // НАСТРОЙКИ"] = "THERMIQRA // SETTINGS",
        ["ЛОКАЛЬНЫЕ ПАРАМЕТРЫ"] = "LOCAL PARAMETERS",
        ["ОТМЕНА"] = "CANCEL",
        ["СОХРАНИТЬ"] = "SAVE",
        ["ЯЗЫК ИНТЕРФЕЙСА"] = "INTERFACE LANGUAGE",
        ["Системный (Windows)"] = "System (Windows)",
        ["Английский"] = "English",
        ["Русский"] = "Russian",
        ["По умолчанию используется язык интерфейса Windows. Для всех языков, кроме русского, Thermiqra использует English."] = "By default, Thermiqra follows the Windows display language. For every language except Russian, Thermiqra uses English.",
        ["Thermiqra — Статистика"] = "Thermiqra — Statistics",
        ["THERMIQRA / СТАТИСТИКА"] = "THERMIQRA / STATISTICS",
        ["АРХИВ ДАТЧИКОВ // HISTORY NODE"] = "SENSOR ARCHIVE // HISTORY NODE",
        ["МЕХАНИЧЕСКИЙ ЖУРНАЛ ПОКАЗАНИЙ"] = "MECHANICAL SENSOR LOG",
        ["КРИОТЕЛЕМЕТРИЯ // АРХИВ ДАТЧИКОВ"] = "CRYO TELEMETRY // SENSOR ARCHIVE",
        ["ПОЛЕВАЯ ТЕЛЕМЕТРИЯ // LOG ARCHIVE"] = "FIELD TELEMETRY // LOG ARCHIVE",
        ["ТОЧЕК В ПЕРИОДЕ"] = "SAMPLES IN PERIOD",
        ["ПЕРИОД"] = "PERIOD",
        ["Сегодня"] = "Today",
        ["24 часа"] = "24 hours",
        ["7 дней"] = "7 days",
        ["30 дней"] = "30 days",
        ["КРАТКАЯ СВОДКА"] = "SUMMARY",
        ["CPU / МАКС. ТЕМПЕРАТУРА"] = "CPU / MAX TEMPERATURE",
        ["GPU / МАКС. ТЕМПЕРАТУРА"] = "GPU / MAX TEMPERATURE",
        ["RAM / МАКС. ЗАГРУЗКА"] = "RAM / MAX LOAD",
        ["САМЫЙ ГОРЯЧИЙ НАКОПИТЕЛЬ"] = "HOTTEST STORAGE DEVICE",
        ["Модель процессора"] = "Processor model",
        ["Средняя температура"] = "Average temperature",
        ["Макс. температура"] = "Max temperature",
        ["Средняя загрузка"] = "Average load",
        ["Макс. загрузка"] = "Max load",
        ["Общий объём оперативной памяти"] = "Total system memory",
        ["ГРАФИК ПО ВРЕМЕНИ"] = "TIME CHART",
        ["CPU / ТЕМПЕРАТУРА"] = "CPU / TEMPERATURE",
        ["Статичный график по сохранённым точкам. Обновляется при выборе периода или показателя."] = "Static chart built from saved samples. Updates when the period or metric changes.",
        ["УСТРОЙСТВО"] = "DEVICE",
        ["ПОКАЗАТЕЛЬ"] = "METRIC",
        ["СРЕДНЕЕ"] = "AVERAGE",
        ["МАКСИМУМ"] = "MAXIMUM",
        ["ВРЕМЯ МАКСИМУМА"] = "TIME OF MAXIMUM",
        ["За выбранный период данных для графика пока нет."] = "No chart data is available for the selected period yet.",
        ["ВИДЕОКАРТЫ"] = "GPUS",
        ["ФИЗИЧЕСКИЕ НАКОПИТЕЛИ"] = "PHYSICAL STORAGE DEVICES",
        ["История сохраняется примерно раз в минуту"] = "History is saved approximately once per minute",
        ["Закрыть"] = "Close",
        ["Thermiqra — Информация о системе"] = "Thermiqra — System Information",
        ["ИНФОРМАЦИЯ О СИСТЕМЕ"] = "SYSTEM INFORMATION",
        ["Аппаратный паспорт и статическая диагностика конфигурации"] = "Hardware inventory and static configuration diagnostics",
        ["Компьютер"] = "Computer",
        ["THERMIQRA / МЕХАНИЧЕСКИЙ ПАСПОРТ"] = "THERMIQRA / MECHANICAL HARDWARE PROFILE",
        ["Техническая ведомость аппаратных узлов"] = "Technical inventory of hardware components",
        ["МАШИННЫЙ УЗЕЛ"] = "MACHINE NODE",
        ["СОСТОЯНИЕ: ГОТОВ"] = "STATUS: READY",
        ["Криогенная диагностика аппаратной конфигурации"] = "Cryogenic hardware configuration diagnostics",
        ["КОНТУР: СТАБИЛЕН"] = "CIRCUIT: STABLE",
        ["Тактический аппаратный паспорт и системная сводка"] = "Tactical hardware profile and system summary",
        ["ОБЪЕКТ / HOST"] = "OBJECT / HOST",
        ["СТАТУС: В СЕТИ"] = "STATUS: ONLINE",
        ["ОБЗОР"] = "OVERVIEW",
        ["Обзор системы"] = "System overview",
        ["Процессор"] = "Processor",
        ["Материнская плата"] = "Motherboard",
        ["Оперативная память"] = "System memory",
        ["Видеокарта"] = "Graphics",
        ["Накопители"] = "Storage",
        ["ПРОЦЕССОР"] = "PROCESSOR",
        ["Производитель"] = "Manufacturer",
        ["Модель"] = "Model",
        ["Архитектура"] = "Architecture",
        ["Физические ядра"] = "Physical cores",
        ["Логические потоки"] = "Logical threads",
        ["Текущая частота"] = "Current frequency",
        ["Максимальная частота"] = "Maximum frequency",
        ["Сокет"] = "Socket",
        ["Кэш L2 / L3"] = "L2 / L3 cache",
        ["ПЛАТА"] = "MOTHERBOARD",
        ["Материнская плата и BIOS"] = "Motherboard and BIOS",
        ["Версия платы"] = "Board version",
        ["Тип прошивки"] = "Firmware type",
        ["Версия BIOS"] = "BIOS version",
        ["Дата BIOS"] = "BIOS date",
        ["ПАМЯТЬ"] = "MEMORY",
        ["Установлено"] = "Installed",
        ["Обнаружено модулей"] = "Modules detected",
        ["Подробнее о памяти"] = "Memory details",
        ["← Назад"] = "← Back",
        ["Расширенная диагностика SPD"] = "Advanced SPD diagnostics",
        ["JEDEC и XMP ниже — профили, записанные в модулях памяти. Они не доказывают, какой профиль активен сейчас."] = "JEDEC and XMP below are profiles stored in the memory modules. They do not prove which profile is currently active.",
        ["Текущие тайминги контроллера памяти Thermiqra пока не определяет надёжным способом."] = "Thermiqra does not yet determine the memory controller's current timings reliably.",
        ["ВИДЕОКАРТА"] = "GRAPHICS",
        ["Видеоадаптеры"] = "Graphics adapters",
        ["Физические накопители"] = "Physical storage devices",
        ["Редакция"] = "Edition",
        ["Версия"] = "Version",
        ["Сборка"] = "Build",
        ["Имя компьютера"] = "Computer name",
        ["Дата установки"] = "Installation date",
        ["Загрузка информации..."] = "Loading information...",
        ["Копировать информацию"] = "Copy information",
        ["Температура"] = "Temperature",
        ["Температура накопителя"] = "Storage temperature",
        ["Заполнение диска"] = "Disk usage",
        ["Открыть"] = "Open",
        ["Настройки"] = "Settings",
        ["Выход"] = "Exit",
        ["Для апгрейда"] = "For upgrade",
        ["Конфигурация"] = "Configuration",
        ["Слоты"] = "Slots",
        ["Настроенная скорость"] = "Configured speed",
        ["XMP-ориентир"] = "XMP reference",
        ["Что искать"] = "What to look for",
        ["Наиболее близкое совпадение"] = "Closest match",
        ["Важно"] = "Important",
        ["Ёмкость"] = "Capacity",
        ["Форм-фактор"] = "Form factor",
        ["Серийный номер"] = "Serial number",
        ["Тип памяти"] = "Memory type",
        ["Заявленная скорость"] = "Rated speed",
        ["Ширина данных"] = "Data width",
        ["Полная ширина"] = "Total width",
        ["Настроенное напряжение (WMI)"] = "Configured voltage (WMI)",
        ["Графический процессор"] = "Graphics processor",
        ["Видеопамять"] = "Video memory",
        ["Версия драйвера"] = "Driver version",
        ["Дата драйвера"] = "Driver date",
        ["Прошивка"] = "Firmware",
        ["Интерфейс"] = "Interface",
        ["Тип носителя"] = "Media type",
        ["Частота"] = "Frequency",
        ["Объём"] = "Capacity",
        ["Чтение информации о системе..."] = "Reading system information...",
        ["Данные загружены: "] = "Data loaded: ",
        ["Не удалось получить данные: "] = "Failed to retrieve data: ",
        ["Модули памяти не обнаружены."] = "No memory modules detected.",
        ["Модуль "] = "Module ",
        ["Слот / банк"] = "Slot / bank",
        ["Текущая конфигурация Windows / SMBIOS"] = "Current Windows / SMBIOS configuration",
        ["Модулей"] = "Modules",
        ["Расширенные SPD-данные ещё не готовы. "] = "Advanced SPD data is not ready yet. ",
        ["Подожди несколько секунд и открой "] = "Wait a few seconds and open ",
        ["«Подробнее о памяти» повторно."] = "“Memory details” again.",
        ["JEDEC — поддерживаемые CL"] = "JEDEC — supported CL",
        ["JEDEC — минимумы"] = "JEDEC — minimums",
        [" — профиль "] = " — profile ",
        ["SPD-тайминги"] = "SPD timings",
        ["напряжение не определено"] = "voltage not determined",
        ["до "] = "up to ",
        [" нс"] = " ns",
        [" В"] = " V",
        ["Не подтверждён"] = "Not confirmed",
        ["В первую очередь ищите модуль "] = "First look for a module ",
        ["с Part Number "] = "with Part Number ",
        ["Смешивание разных комплектов памяти "] = "Mixing different memory kits ",
        ["не гарантирует работу XMP на той же скорости. "] = "does not guarantee XMP operation at the same speed. ",
        ["Итоговый режим зависит от материнской платы, "] = "The final operating mode depends on the motherboard, ",
        ["процессора и BIOS."] = "processor, and BIOS.",
        [" из "] = " of ",
        [" занято"] = " used",
        ["свободно "] = "free ",
        ["источник WMI"] = "source: WMI",
        ["общее число слотов не подтверждено"] = "total slot count not confirmed",
        ["на модуль"] = "per module",
        ["Недостаточно подтверждённых данных"] = "Not enough confirmed data",
        [" ГБ"] = " GB",
        ["Видеоадаптеры не обнаружены."] = "No graphics adapters detected.",
        ["Видеоадаптер "] = "Graphics adapter ",
        ["Пока не определяется надёжно"] = "Not determined reliably yet",
        ["Физические накопители не обнаружены."] = "No physical storage devices detected.",
        ["Накопитель "] = "Storage device ",
        [" ГГц"] = " GHz",
        [" МГц"] = " MHz",
        ["Код SMBIOS "] = "SMBIOS code ",
        ["Код "] = "Code ",
        [" бит"] = " bits",
        [" МБ"] = " MB",
        [" КБ"] = " KB",
        [" ТБ"] = " TB",
        ["Информация о системе ещё не загружена."] = "System information has not been loaded yet.",
        ["Информация скопирована в буфер обмена."] = "System information copied to the clipboard.",
        ["Не удалось скопировать данные: "] = "Failed to copy data: ",
        ["THERMIQRA — ИНФОРМАЦИЯ О СИСТЕМЕ"] = "THERMIQRA — SYSTEM INFORMATION",
        ["Версия ОС"] = "OS version",
        ["Дата установки: "] = "Installation date: ",
        ["[ПРОЦЕССОР]"] = "[PROCESSOR]",
        ["Физические ядра: "] = "Physical cores: ",
        ["Логические потоки: "] = "Logical threads: ",
        ["Текущая частота: "] = "Current frequency: ",
        ["Максимальная частота: "] = "Maximum frequency: ",
        ["Кэш L2: "] = "L2 cache: ",
        ["Кэш L3: "] = "L3 cache: ",
        ["[МАТЕРИНСКАЯ ПЛАТА]"] = "[MOTHERBOARD]",
        ["Дата BIOS: "] = "BIOS date: ",
        ["[ОПЕРАТИВНАЯ ПАМЯТЬ]"] = "[SYSTEM MEMORY]",
        ["Установлено: "] = "Installed: ",
        ["Обнаружено модулей: "] = "Modules detected: ",
        ["  Слот / банк"] = "  Slot / bank",
        ["  Объём: "] = "  Capacity: ",
        ["  Производитель"] = "  Manufacturer",
        ["  Серийный номер"] = "  Serial number",
        ["  Частота: "] = "  Frequency: ",
        ["[ВИДЕОКАРТЫ]"] = "[GRAPHICS ADAPTERS]",
        ["  Модель"] = "  Model",
        ["  Графический процессор"] = "  Graphics processor",
        ["  Видеопамять: "] = "  Video memory: ",
        ["  Версия драйвера"] = "  Driver version",
        ["  Дата драйвера: "] = "  Driver date: ",
        ["[ФИЗИЧЕСКИЕ НАКОПИТЕЛИ]"] = "[PHYSICAL STORAGE DEVICES]",
        ["  Прошивка"] = "  Firmware",
        ["  Интерфейс"] = "  Interface",
        ["  Тип носителя"] = "  Media type",
        ["  Ёмкость: "] = "  Capacity: ",
        ["Не удалось определить путь к Thermiqra.exe."] = "Failed to determine the path to Thermiqra.exe.",
        ["Не удалось запустить Планировщик заданий Windows."] = "Failed to start Windows Task Scheduler.",
        };


    private static readonly Dictionary<string, string> EnglishToRussian =
        BuildReverseTranslations();


    public static string CurrentLanguageCode { get; private set; } =
        SystemLanguageCode;


    public static bool IsRussian =>
        string.Equals(
            CurrentLanguageCode,
            "ru",
            StringComparison.OrdinalIgnoreCase);


    public static string L(
        string russian,
        string english)
    {
        return IsRussian
            ? russian
            : english;
    }


    public static void ApplyLanguagePreference()
    {
        string preference =
            Current.LanguageName?.Trim()
            ?? "System";

        CurrentLanguageCode =
            preference switch
            {
                "Russian" => "ru",
                "English" => "en",
                _ => SystemLanguageCode
            };
    }


    public static string TranslateKnown(
        string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        if (IsRussian)
        {
            return EnglishToRussian.TryGetValue(
                    text,
                    out string? russian)
                ? russian
                : text;
        }

        return RussianToEnglish.TryGetValue(
                text,
                out string? english)
            ? english
            : text;
    }


    public static string TranslateText(
        string text)
    {
        if (string.IsNullOrEmpty(text) ||
            IsRussian)
        {
            return text;
        }

        if (RussianToEnglish.TryGetValue(
                text,
                out string? exact))
        {
            return exact;
        }

        string result =
            text;

        foreach (KeyValuePair<string, string> pair
                 in RussianToEnglish
                     .OrderByDescending(
                         item =>
                             item.Key.Length))
        {
            if (!result.Contains(
                    pair.Key,
                    StringComparison.Ordinal))
            {
                continue;
            }

            result =
                result.Replace(
                    pair.Key,
                    pair.Value,
                    StringComparison.Ordinal);
        }

        return result;
    }


    public static void ApplyLanguageToWindow(
        Window window)
    {
        if (window == null)
            return;

        window.Title =
            TranslateKnown(
                window.Title);

        ApplyLanguageToElement(
            window);
    }


    public static void ApplyLanguageToElement(
        DependencyObject root)
    {
        if (root == null)
            return;

        if (root is TextBlock textBlock &&
            !string.IsNullOrEmpty(
                textBlock.Text))
        {
            textBlock.Text =
                TranslateKnown(
                    textBlock.Text);
        }

        if (root is ContentControl contentControl &&
            contentControl.Content is string content)
        {
            contentControl.Content =
                TranslateKnown(
                    content);
        }

        if (root is HeaderedContentControl headeredContent &&
            headeredContent.Header is string header)
        {
            headeredContent.Header =
                TranslateKnown(
                    header);
        }

        if (root is HeaderedItemsControl headeredItems &&
            headeredItems.Header is string itemsHeader)
        {
            headeredItems.Header =
                TranslateKnown(
                    itemsHeader);
        }

        object? toolTip =
            ToolTipService.GetToolTip(
                root);

        if (toolTip is string toolTipText)
        {
            ToolTipService.SetToolTip(
                root,
                TranslateKnown(
                    toolTipText));
        }

        foreach (object child in
                 LogicalTreeHelper.GetChildren(
                     root))
        {
            if (child is DependencyObject dependencyObject)
            {
                ApplyLanguageToElement(
                    dependencyObject);
            }
        }
    }


    private static Dictionary<string, string>
        BuildReverseTranslations()
    {
        Dictionary<string, string> result =
            new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> pair
                 in RussianToEnglish)
        {
            if (!result.ContainsKey(
                    pair.Value))
            {
                result[pair.Value] =
                    pair.Key;
            }
        }

        return result;
    }


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