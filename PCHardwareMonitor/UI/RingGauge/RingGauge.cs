using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PCHardwareMonitor;

public enum TemperatureType
{
    Cpu,
    Gpu
}

public sealed partial class RingGauge : Border
{
    private const double CyberGaugeSize = 226;
    private const double CyberRadius = 80;
    private const double CyberCenter = 113;

    private Path _cyberProgressArc = null!;
    private TextBlock _cyberTemperatureText = null!;
    private TextBlock _cyberLoadText = null!;
    private TextBlock _cyberHardwareNameText = null!;
    private TextBlock _cyberStatusText = null!;
    private Ellipse _cyberStatusDot = null!;
    private TextBlock? _cyberHotSpotValueText;
    private TextBlock? _cyberVramValueText;

    private TextBlock _steamTemperatureText = null!;
    private TextBlock _steamLoadText = null!;
    private TextBlock _steamHardwareNameText = null!;
    private TextBlock _steamStatusText = null!;
    private Ellipse _steamStatusLamp = null!;
    private Border _steamNeedle = null!;
    private RotateTransform _steamNeedleTransform = null!;
    private TextBlock? _steamHotSpotValueText;
    private TextBlock? _steamVramValueText;

    private Border _frostLoadFill = null!;
    private TextBlock _frostTemperatureText = null!;
    private TextBlock _frostLoadText = null!;
    private TextBlock _frostHardwareNameText = null!;
    private TextBlock _frostStatusText = null!;
    private Ellipse _frostStatusDot = null!;
    private TextBlock? _frostHotSpotValueText;
    private TextBlock? _frostVramValueText;

    private TextBlock _militaryTemperatureText = null!;
    private TextBlock _militaryLoadText = null!;
    private TextBlock _militaryHardwareNameText = null!;
    private TextBlock _militaryStatusText = null!;
    private Ellipse _militaryStatusDot = null!;
    private TextBlock? _militaryHotSpotValueText;
    private TextBlock? _militaryVramValueText;
    private readonly Border[] _militaryLoadSegments =
        new Border[10];

    private readonly TemperatureType _temperatureType;

    public event EventHandler? CpuThreadsRequested;

    private string _appliedLanguageCode =
        string.Empty;

    private static readonly Dictionary<string, string>
        RussianToEnglishText =
            new(StringComparer.Ordinal)
            {
                ["ГОРЯЧАЯ ТОЧКА"] = "HOT SPOT",
                ["ДАТЧИК АКТИВЕН"] = "SENSOR ACTIVE",
                ["ДАТЧИКИ В РЕАЛЬНОМ ВРЕМЕНИ"] = "REAL-TIME SENSORS",
                ["КАНАЛ ДАТЧИКОВ"] = "SENSOR CHANNEL",
                ["КАНАЛ ПОЛЕВОГО КОНТРОЛЯ"] = "FIELD MONITORING CHANNEL",
                ["КОНТУР ОХЛАЖДЕНИЯ"] = "COOLING CIRCUIT",
                ["КРИОГЕННЫЙ ГРАФИЧЕСКИЙ МОДУЛЬ"] = "CRYOGENIC GPU MODULE",
                ["КРИОГЕННЫЙ ПРОЦЕССОРНЫЙ МОДУЛЬ"] = "CRYOGENIC CPU MODULE",
                ["КРИОКОНТУР"] = "CRYO CIRCUIT",
                ["КРИОЯДРО CPU"] = "CPU CRYO CORE",
                ["КРИОЯДРО GPU"] = "GPU CRYO CORE",
                ["МАНОМЕТР НАГРУЗКИ ГРАФИЧЕСКОГО ПРОЦЕССОРА"] = "GPU LOAD GAUGE",
                ["МАНОМЕТР НАГРУЗКИ ПРОЦЕССОРА"] = "CPU LOAD GAUGE",
                ["МЕХАНИЧЕСКИЙ ДАТЧИК"] = "MECHANICAL SENSOR",
                ["МОДЕЛЬ"] = "MODEL",
                ["МОДУЛЬ CPU"] = "CPU MODULE",
                ["МОДУЛЬ GPU"] = "GPU MODULE",
                ["Мониторинг в реальном времени"] = "Real-time monitoring",
                ["Мониторинг криоядра в реальном времени"] = "Real-time cryo-core monitoring",
                ["НАГРУЗКА СИСТЕМЫ"] = "SYSTEM LOAD",
                ["НАГРУЗКА, %"] = "LOAD, %",
                ["НЕТ ДАННЫХ"] = "NO DATA",
                ["ОБОРУДОВАНИЕ"] = "HARDWARE",
                ["ОХЛАЖДАЮЩИЙ КОНТУР АКТИВЕН"] = "COOLING CIRCUIT ACTIVE",
                ["ПОЛЕВОЙ МОДУЛЬ CPU"] = "CPU FIELD MODULE",
                ["ПОЛЕВОЙ МОДУЛЬ GPU"] = "GPU FIELD MODULE",
                ["ПОЛЕВОЙ МОДУЛЬ ГРАФИЧЕСКОГО ПРОЦЕССОРА"] = "GPU FIELD MODULE",
                ["ПОЛЕВОЙ МОДУЛЬ ПРОЦЕССОРА"] = "CPU FIELD MODULE",
                ["СЕКТОР LOAD-01"] = "SECTOR LOAD-01",
                ["СОСТОЯНИЕ ДАТЧИКА"] = "SENSOR STATUS",
                ["СОСТОЯНИЕ КРИОКОНТУРА"] = "CRYO CIRCUIT STATUS",
                ["СТАТУС"] = "STATUS",
                ["СТАТУС ПОЛЕВОГО ДАТЧИКА"] = "FIELD SENSOR STATUS",
                ["ТАКТИЧЕСКАЯ ТЕЛЕМЕТРИЯ АКТИВНА"] = "TACTICAL TELEMETRY ACTIVE",
                ["ТАКТИЧЕСКИЙ ДАТЧИК"] = "TACTICAL SENSOR",
                ["ТЕМП."] = "TEMP.",
                ["ТЕМПЕРАТУРА"] = "TEMPERATURE",
                ["ТЕМПЕРАТУРА VRAM"] = "VRAM TEMPERATURE",
                ["Телеметрия в реальном времени"] = "Real-time telemetry",
                ["ПОТОКИ"] = "THREADS",
                ["УРОВЕНЬ НАГРУЗКИ"] = "LOAD LEVEL"
            };


    public RingGauge(
        string title,
        TemperatureType temperatureType)
    {
        _temperatureType =
            temperatureType;

        Width = 530;
        MinHeight = 338;
        Margin = new Thickness(7, 0, 7, 14);
        Padding = new Thickness(0);
        BorderThickness = new Thickness(0);
        Background = Brushes.Transparent;

        Grid root = new();

        FrameworkElement cyberView =
            CreateCyberView(
                title,
                temperatureType);

        FrameworkElement steamView =
            CreateSteamView(
                title,
                temperatureType);

        FrameworkElement frostView =
            CreateFrostView(
                title,
                temperatureType);

        FrameworkElement militaryView =
            CreateMilitaryView(
                title,
                temperatureType);

        root.Children.Add(cyberView);
        root.Children.Add(steamView);
        root.Children.Add(frostView);
        root.Children.Add(militaryView);

        Child = root;

        SetProgress(0);

        RefreshLanguageIfNeeded(
            force: true);
    }

}
