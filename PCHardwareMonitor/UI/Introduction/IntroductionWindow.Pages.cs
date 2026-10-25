using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PCHardwareMonitor;

public sealed partial class IntroductionWindow
{
    private static List<IntroductionPage>
        CreatePages()
    {
        return
            new List<IntroductionPage>
            {
                new(
                    SettingsService.L(
                        "01 // ГЛАВНЫЙ ЭКРАН",
                        "01 // MAIN SCREEN"),

                    SettingsService.L(
                        "Главное — видно сразу",
                        "The essentials at a glance"),

                    SettingsService.L(
                        "CPU — процессор. GPU — видеокарта. RAM — оперативная память. " +
                        "Накопители — SSD и жёсткие диски.\n\n" +
                        "На главном экране Thermiqra показывает их состояние прямо сейчас: " +
                        "нагрузку, температуры, использование памяти и свободное место.",
                        "CPU is the processor. GPU is the graphics card. RAM is system memory. " +
                        "Storage means SSDs and hard drives.\n\n" +
                        "The Thermiqra main screen shows their current state: " +
                        "load, temperatures, memory usage and free disk space."),

                    SettingsService.L(
                        "Не нужно запоминать все цифры. Главное окно нужно для быстрого взгляда: " +
                        "всё спокойно или какой-то показатель требует внимания.",
                        "You do not need to memorize every number. The main screen is for a quick check: " +
                        "everything is calm, or a value needs attention."),

                    BuildMainScreenVisual),

                new(
                    SettingsService.L(
                        "02 // ЦВЕТА",
                        "02 // COLORS"),

                    SettingsService.L(
                        "Цвет показывает важность",
                        "Color shows importance"),

                    SettingsService.L(
                        "Цвет помогает понять показатель без знания технических норм. " +
                        "Зелёный означает обычное состояние, жёлтый — повышенное, " +
                        "оранжевый — высокое, красный — критическое.\n\n" +
                        "Порог зависит от конкретного показателя: температура и загрузка " +
                        "не оцениваются одинаково.",
                        "Color helps you understand a value without knowing technical limits. " +
                        "Green means normal, yellow means elevated, orange means high, " +
                        "and red means critical.\n\n" +
                        "The threshold depends on the metric: temperature and load " +
                        "are not evaluated in the same way."),

                    SettingsService.L(
                        "Один короткий скачок ещё не означает проблему. Thermiqra использует историю, " +
                        "когда для вывода важна повторяемость.",
                        "One short spike does not automatically mean a problem. Thermiqra uses history " +
                        "when recurrence matters for a conclusion."),

                    BuildColorVisual),

                new(
                    SettingsService.L(
                        "03 // ПОДРОБНОСТИ",
                        "03 // DETAILS"),

                    SettingsService.L(
                        "Куда идти дальше",
                        "Where to go next"),

                    SettingsService.L(
                        "Если на главном экране что-то привлекло внимание, подробности уже разложены " +
                        "по понятным разделам.\n\n" +
                        "«Система» показывает установленное железо и вкладку «Анализ». " +
                        "«Статистика» показывает историю. Кнопка «Анализ» возле диска помогает понять, " +
                        "чем занято место.",
                        "If something on the main screen catches your attention, details are already " +
                        "organized into clear sections.\n\n" +
                        "System shows installed hardware and the Analysis tab. " +
                        "Statistics shows history. The Analyze button next to a drive helps explain " +
                        "where disk space is being used."),

                    SettingsService.L(
                        "Thermiqra старается сначала объяснить ситуацию простым языком, " +
                        "а технические детали оставляет доступными для тех, кому они нужны.",
                        "Thermiqra tries to explain the situation in plain language first, " +
                        "while keeping technical details available for those who need them."),

                    BuildDetailsVisual),

                new(
                    SettingsService.L(
                        "04 // ФОНОВАЯ РАБОТА",
                        "04 // BACKGROUND"),

                    SettingsService.L(
                        "Thermiqra не нужно держать открытой",
                        "Thermiqra does not need to stay open"),

                    SettingsService.L(
                        "После настройки программу можно убрать с экрана. Thermiqra продолжит работать " +
                        "в системном трее и следить за компьютером.\n\n" +
                        "Если температура или заполнение диска достигнут заданного уровня, " +
                        "программа может показать предупреждение. Уведомления и пороги настраиваются.",
                        "After setup, you can remove the program from the screen. Thermiqra keeps running " +
                        "in the system tray and continues monitoring the computer.\n\n" +
                        "If temperature or disk usage reaches a configured level, " +
                        "the program can show an alert. Notifications and thresholds are configurable."),

                    SettingsService.L(
                        "Это знакомство можно открыть снова в Настройках. " +
                        "Настройки и история при повторном просмотре не сбрасываются.",
                        "You can open this introduction again from Settings. " +
                        "Settings and history are not reset when you view it again."),

                    BuildTrayVisual)
            };
    }

    private static UIElement BuildMainScreenVisual()
    {
        Grid root =
            new();

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        TextBlock heading =
            CreateVisualHeading(
                SettingsService.L(
                    "ТАК ЭТО ВЫГЛЯДИТ НА ГЛАВНОМ ЭКРАНЕ",
                    "THIS IS HOW IT LOOKS ON THE MAIN SCREEN"));

        root.Children.Add(
            heading);

        UniformGrid sensors =
            new()
            {
                Rows =
                    2,

                Columns =
                    2,

                Margin =
                    new Thickness(
                        0,
                        18,
                        0,
                        0)
            };

        Grid.SetRow(
            sensors,
            1);

        string skin =
            SettingsService
                .Current
                .SkinName
            ?? "CyberTech";

        sensors.Children.Add(
            CreateSkinSensorCard(
                skin,
                "CPU",
                SettingsService.L(
                    "ПРОЦЕССОР",
                    "PROCESSOR"),
                "42 °C",
                0.42,
                SensorKind.Temperature));

        sensors.Children.Add(
            CreateSkinSensorCard(
                skin,
                "GPU",
                SettingsService.L(
                    "ВИДЕОКАРТА",
                    "GRAPHICS"),
                "51 °C",
                0.51,
                SensorKind.Temperature));

        sensors.Children.Add(
            CreateSkinSensorCard(
                skin,
                "RAM",
                SettingsService.L(
                    "ОПЕРАТИВНАЯ ПАМЯТЬ",
                    "SYSTEM MEMORY"),
                "35 %",
                0.35,
                SensorKind.Memory));

        sensors.Children.Add(
            CreateSkinSensorCard(
                skin,
                "SSD",
                SettingsService.L(
                    "НАКОПИТЕЛЬ",
                    "STORAGE"),
                SettingsService.L(
                    "62 % свободно",
                    "62% free"),
                0.62,
                SensorKind.Storage));

        root.Children.Add(
            sensors);

        return root;
    }


    private static Border CreateSkinSensorCard(
        string skin,
        string shortName,
        string title,
        string value,
        double level,
        SensorKind kind)
    {
        return skin switch
        {
            "SteamPunk" =>
                CreateSteamSensorCard(
                    shortName,
                    title,
                    value,
                    level,
                    kind),

            "FrostCore" =>
                CreateFrostSensorCard(
                    shortName,
                    title,
                    value,
                    level,
                    kind),

            "MilitaryOps" =>
                CreateMilitarySensorCard(
                    shortName,
                    title,
                    value,
                    level,
                    kind),

            _ =>
                CreateCyberSensorCard(
                    shortName,
                    title,
                    value,
                    level,
                    kind)
        };
    }
}
