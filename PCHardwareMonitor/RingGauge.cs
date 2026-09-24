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

public sealed class RingGauge : Border
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

    // ============================================================
    // CYBER TECH VIEW
    // ============================================================

    private FrameworkElement CreateCyberView(
        string title,
        TemperatureType temperatureType)
    {
        Grid root = new();
        root.SetResourceReference(
            VisibilityProperty,
            "MainCyberTechVisibility");

        Path frame = new()
        {
            Data = Geometry.Parse(
                "M 10,0 L 100,0 L 100,88 L 94,100 L 0,100 L 0,10 Z"),
            Stretch = Stretch.Fill,
            StrokeThickness = 1
        };

        frame.SetResourceReference(
            Shape.FillProperty,
            "CardBackgroundBrush");

        frame.SetResourceReference(
            Shape.StrokeProperty,
            "BorderBrush");

        root.Children.Add(frame);

        Border topAccent = new()
        {
            Height = 2,
            Width = 52,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(52, 0, 0, 0)
        };

        topAccent.SetResourceReference(
            BackgroundProperty,
            "AccentBrush");

        root.Children.Add(topAccent);

        Border bottomAccent = new()
        {
            Height = 2,
            Width = 44,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 36, 0),
            Opacity = 0.7
        };

        bottomAccent.SetResourceReference(
            BackgroundProperty,
            "AccentBrush");

        root.Children.Add(bottomAccent);

        Grid layout = new()
        {
            Margin = new Thickness(18, 14, 18, 13)
        };

        layout.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        layout.RowDefinitions.Add(
            new RowDefinition
            {
                Height = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        layout.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        BuildCyberHeader(
            layout,
            title,
            temperatureType);

        Grid content = new()
        {
            Margin = new Thickness(0, 5, 0, 5)
        };

        content.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(236)
            });

        content.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        Grid gauge =
            CreateCyberGauge();

        Grid.SetColumn(
            gauge,
            0);

        content.Children.Add(gauge);

        Grid info =
            CreateCyberInfoPanel(
                temperatureType);

        Grid.SetColumn(
            info,
            1);

        content.Children.Add(info);

        Grid.SetRow(
            content,
            1);

        layout.Children.Add(content);

        Grid footer =
            CreateCyberFooter(
                temperatureType);

        Grid.SetRow(
            footer,
            2);

        layout.Children.Add(footer);

        root.Children.Add(layout);

        return root;
    }

    private void BuildCyberHeader(
        Grid layout,
        string title,
        TemperatureType temperatureType)
    {
        Grid header = new();

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        TextBlock number = new()
        {
            Text = temperatureType == TemperatureType.Cpu
                ? "01"
                : "02",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };

        number.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        StackPanel titlePanel = new();

        TextBlock titleText = new()
        {
            Text = title,
            FontSize = 23,
            FontWeight = FontWeights.Bold
        };

        titleText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        TextBlock moduleText = new()
        {
            Text = temperatureType == TemperatureType.Cpu
                ? "CENTRAL PROCESSOR // CORE MODULE"
                : "GRAPHICS PROCESSOR // GPU MODULE",
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 1, 0, 0)
        };

        moduleText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        titlePanel.Children.Add(titleText);
        titlePanel.Children.Add(moduleText);

        Border activeBadge = new()
        {
            Padding = new Thickness(8, 4, 8, 4),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 0)
        };

        activeBadge.SetResourceReference(
            Border.BorderBrushProperty,
            "AccentBrush");

        StackPanel badgeRow = new()
        {
            Orientation = Orientation.Horizontal
        };

        Ellipse badgeDot = new()
        {
            Width = 6,
            Height = 6,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        badgeDot.SetResourceReference(
            Shape.FillProperty,
            "AccentBrush");

        TextBlock badgeText = new()
        {
            Text = "ACTIVE",
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };

        badgeText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        badgeRow.Children.Add(badgeDot);
        badgeRow.Children.Add(badgeText);
        activeBadge.Child = badgeRow;

        Grid.SetColumn(number, 0);
        Grid.SetColumn(titlePanel, 1);
        Grid.SetColumn(activeBadge, 2);

        header.Children.Add(number);
        header.Children.Add(titlePanel);
        header.Children.Add(activeBadge);

        Grid.SetRow(header, 0);
        layout.Children.Add(header);
    }

    private Grid CreateCyberGauge()
    {
        Grid gaugeGrid = new()
        {
            Width = CyberGaugeSize,
            Height = CyberGaugeSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        Ellipse outerRing = new()
        {
            Width = 208,
            Height = 208,
            StrokeThickness = 1,
            Opacity = 0.65
        };

        outerRing.SetResourceReference(
            Shape.StrokeProperty,
            "BorderBrush");

        gaugeGrid.Children.Add(outerRing);

        Ellipse dashedRing = new()
        {
            Width = 196,
            Height = 196,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection
            {
                1.4,
                4.6
            },
            Opacity = 0.72
        };

        dashedRing.SetResourceReference(
            Shape.StrokeProperty,
            "AccentBrush");

        gaugeGrid.Children.Add(dashedRing);

        for (int i = 0;
             i < 36;
             i++)
        {
            bool major =
                i % 3 == 0;

            Grid tickLayer = new()
            {
                Width = CyberGaugeSize,
                Height = CyberGaugeSize,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(
                    i * 10)
            };

            Border tick = new()
            {
                Width = major ? 2 : 1,
                Height = major ? 10 : 5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 12, 0, 0),
                Opacity = major ? 0.86 : 0.32
            };

            tick.SetResourceReference(
                BackgroundProperty,
                major
                    ? "AccentBrush"
                    : "BorderBrush");

            tickLayer.Children.Add(tick);
            gaugeGrid.Children.Add(tickLayer);
        }

        Border crossHorizontal = new()
        {
            Width = 214,
            Height = 1,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.28
        };

        crossHorizontal.SetResourceReference(
            BackgroundProperty,
            "BorderBrush");

        gaugeGrid.Children.Add(crossHorizontal);

        Border crossVertical = new()
        {
            Width = 1,
            Height = 214,
            HorizontalAlignment = HorizontalAlignment.Center,
            Opacity = 0.28
        };

        crossVertical.SetResourceReference(
            BackgroundProperty,
            "BorderBrush");

        gaugeGrid.Children.Add(crossVertical);

        Ellipse backgroundRing = new()
        {
            Width = 160,
            Height = 160,
            StrokeThickness = 13
        };

        backgroundRing.SetResourceReference(
            Shape.StrokeProperty,
            "GaugeTrackBrush");

        gaugeGrid.Children.Add(backgroundRing);

        _cyberProgressArc = new Path
        {
            Stroke = UiBrushes.Load(0),
            StrokeThickness = 13,
            StrokeStartLineCap = PenLineCap.Flat,
            StrokeEndLineCap = PenLineCap.Flat
        };

        gaugeGrid.Children.Add(
            _cyberProgressArc);

        Ellipse innerRing = new()
        {
            Width = 134,
            Height = 134,
            StrokeThickness = 1,
            Opacity = 0.7
        };

        innerRing.SetResourceReference(
            Shape.StrokeProperty,
            "BorderBrush");

        gaugeGrid.Children.Add(innerRing);

        Ellipse marker = new()
        {
            Width = 10,
            Height = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 30, 0, 0)
        };

        marker.SetResourceReference(
            Shape.FillProperty,
            "AccentBrush");

        gaugeGrid.Children.Add(marker);

        StackPanel center = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _cyberTemperatureText = new TextBlock
        {
            Text = "-- °C",
            FontSize = 35,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        TextBlock tempLabel =
            CreateAccentLabel(
                "CORE TEMP");

        tempLabel.HorizontalAlignment =
            HorizontalAlignment.Center;

        tempLabel.Margin =
            new Thickness(0, 0, 0, 8);

        _cyberLoadText = new TextBlock
        {
            Text = "0 %",
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        TextBlock loadLabel =
            CreateTechLabel(
                "LOAD");

        loadLabel.HorizontalAlignment =
            HorizontalAlignment.Center;

        center.Children.Add(
            _cyberTemperatureText);

        center.Children.Add(
            tempLabel);

        center.Children.Add(
            _cyberLoadText);

        center.Children.Add(
            loadLabel);

        gaugeGrid.Children.Add(center);

        return gaugeGrid;
    }

    private Grid CreateCyberInfoPanel(
        TemperatureType temperatureType)
    {
        Grid infoPanel = new()
        {
            Margin = new Thickness(8, 16, 0, 12)
        };

        for (int i = 0;
             i < 5;
             i++)
        {
            infoPanel.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = i == 4
                        ? new GridLength(
                            1,
                            GridUnitType.Star)
                        : GridLength.Auto
                });
        }

        Border sideAccent = new()
        {
            Width = 2,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        sideAccent.SetResourceReference(
            BackgroundProperty,
            "AccentBrush");

        Grid.SetRowSpan(
            sideAccent,
            5);

        infoPanel.Children.Add(sideAccent);

        TextBlock modelLabel =
            CreateTechLabel(
                "МОДЕЛЬ");

        modelLabel.Margin =
            new Thickness(14, 0, 0, 4);

        _cyberHardwareNameText = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 42,
            Margin = new Thickness(14, 0, 0, 10)
        };

        _cyberHardwareNameText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        Border separator = new()
        {
            Height = 1,
            Margin = new Thickness(14, 0, 0, 11)
        };

        separator.SetResourceReference(
            BackgroundProperty,
            "BorderBrush");

        StackPanel statusPanel = new()
        {
            Margin = new Thickness(14, 0, 0, 0)
        };

        TextBlock statusLabel =
            CreateTechLabel(
                "СТАТУС");

        StackPanel statusRow = new()
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 5, 0, 0)
        };

        _cyberStatusDot = new Ellipse
        {
            Width = 7,
            Height = 7,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        _cyberStatusDot.SetResourceReference(
            Shape.FillProperty,
            "AccentBrush");

        _cyberStatusText = new TextBlock
        {
            Text = "ДАТЧИК АКТИВЕН",
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };

        _cyberStatusText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        statusRow.Children.Add(
            _cyberStatusDot);

        statusRow.Children.Add(
            _cyberStatusText);

        statusPanel.Children.Add(
            statusLabel);

        statusPanel.Children.Add(
            statusRow);

        Grid.SetRow(modelLabel, 0);
        Grid.SetRow(_cyberHardwareNameText, 1);
        Grid.SetRow(separator, 2);
        Grid.SetRow(statusPanel, 3);

        infoPanel.Children.Add(modelLabel);
        infoPanel.Children.Add(_cyberHardwareNameText);
        infoPanel.Children.Add(separator);
        infoPanel.Children.Add(statusPanel);

        Grid details =
            temperatureType == TemperatureType.Gpu
                ? CreateCyberGpuDetails()
                : CreateCyberCpuDetails();

        Grid.SetRow(details, 4);
        infoPanel.Children.Add(details);

        return infoPanel;
    }

    private Grid CreateCyberGpuDetails()
    {
        Grid wrapper = new()
        {
            Margin = new Thickness(14, 15, 0, 0)
        };

        wrapper.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        wrapper.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        TextBlock sensorLabel =
            CreateTechLabel(
                "THERMAL CHANNELS");

        wrapper.Children.Add(sensorLabel);

        Grid cards = new()
        {
            Margin = new Thickness(0, 7, 0, 0)
        };

        cards.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        cards.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(8)
            });

        cards.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        Border hotSpotCard =
            CreateCyberMiniCard();

        StackPanel hotSpotStack = new();

        hotSpotStack.Children.Add(
            CreateTechLabel(
                "HOT SPOT"));

        _cyberHotSpotValueText = new TextBlock
        {
            Text = "--",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 3, 0, 0)
        };

        _cyberHotSpotValueText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        hotSpotStack.Children.Add(
            _cyberHotSpotValueText);

        hotSpotCard.Child =
            hotSpotStack;

        Border vramCard =
            CreateCyberMiniCard();

        StackPanel vramStack = new();

        vramStack.Children.Add(
            CreateTechLabel(
                "VRAM"));

        _cyberVramValueText = new TextBlock
        {
            Text = "--",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 3, 0, 0)
        };

        _cyberVramValueText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        vramStack.Children.Add(
            _cyberVramValueText);

        vramCard.Child =
            vramStack;

        Grid.SetColumn(
            hotSpotCard,
            0);

        Grid.SetColumn(
            vramCard,
            2);

        cards.Children.Add(
            hotSpotCard);

        cards.Children.Add(
            vramCard);

        Grid.SetRow(
            cards,
            1);

        wrapper.Children.Add(cards);

        return wrapper;
    }

    private Grid CreateCyberCpuDetails()
    {
        _cyberHotSpotValueText = null;
        _cyberVramValueText = null;

        Grid wrapper = new()
        {
            Margin = new Thickness(14, 15, 0, 0)
        };

        wrapper.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        wrapper.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        wrapper.Children.Add(
            CreateTechLabel(
                "MONITORING CHANNEL"));

        Border card =
            CreateCyberMiniCard();

        card.Margin =
            new Thickness(0, 7, 0, 0);

        Grid content = new();

        content.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        content.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        StackPanel stack = new();

        TextBlock realTime =
            CreateAccentLabel(
                "REAL-TIME");

        realTime.FontSize = 11;

        TextBlock sensorBus =
            CreateTechLabel(
                "SENSOR BUS ACTIVE");

        sensorBus.Margin =
            new Thickness(0, 4, 0, 0);

        stack.Children.Add(realTime);
        stack.Children.Add(sensorBus);

        Border threadsButton =
            CreateCpuThreadsButton(
                "InputBackgroundBrush",
                "AccentBrush",
                "AccentBrush",
                new CornerRadius(3));

        Grid.SetColumn(
            stack,
            0);

        Grid.SetColumn(
            threadsButton,
            1);

        content.Children.Add(stack);
        content.Children.Add(threadsButton);

        card.Child = content;

        Grid.SetRow(
            card,
            1);

        wrapper.Children.Add(card);

        return wrapper;
    }

    private static Border CreateCyberMiniCard()
    {
        Border card = new()
        {
            Padding = new Thickness(9, 7, 9, 7),
            BorderThickness = new Thickness(1)
        };

        card.SetResourceReference(
            Border.BackgroundProperty,
            "InputBackgroundBrush");

        card.SetResourceReference(
            Border.BorderBrushProperty,
            "BorderBrush");

        return card;
    }

    private static Grid CreateCyberFooter(
        TemperatureType temperatureType)
    {
        Grid footer = new();

        footer.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        footer.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        TextBlock module = new()
        {
            Text = temperatureType == TemperatureType.Cpu
                ? "01 // CPU MODULE"
                : "02 // GPU MODULE",
            FontSize = 9,
            FontWeight = FontWeights.SemiBold
        };

        module.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        StackPanel right = new()
        {
            Orientation = Orientation.Horizontal
        };

        Border line = new()
        {
            Width = 34,
            Height = 2,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        line.SetResourceReference(
            BackgroundProperty,
            "AccentBrush");

        TextBlock sensor = new()
        {
            Text = "REAL-TIME SENSOR",
            FontSize = 9,
            FontWeight = FontWeights.SemiBold
        };

        sensor.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        right.Children.Add(line);
        right.Children.Add(sensor);

        Grid.SetColumn(module, 0);
        Grid.SetColumn(right, 1);

        footer.Children.Add(module);
        footer.Children.Add(right);

        return footer;
    }

    // ============================================================
    // STEAMPUNK VIEW
    // ============================================================

    private FrameworkElement CreateSteamView(
        string title,
        TemperatureType temperatureType)
    {
        Border outer = new()
        {
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(7),
            MinHeight = 338
        };

        outer.SetResourceReference(
            Border.BackgroundProperty,
            "SteamPanelBrush");

        outer.SetResourceReference(
            Border.BorderBrushProperty,
            "SteamMetalBrush");

        outer.SetResourceReference(
            VisibilityProperty,
            "SteamPunkVisibility");

        Grid root = new();

        Border inner = new()
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11)
        };

        inner.SetResourceReference(
            Border.BackgroundProperty,
            "SteamDarkMetalBrush");

        inner.SetResourceReference(
            Border.BorderBrushProperty,
            "BorderBrush");

        root.Children.Add(inner);

        AddSteamRivet(
            root,
            HorizontalAlignment.Left,
            VerticalAlignment.Top,
            new Thickness(9));

        AddSteamRivet(
            root,
            HorizontalAlignment.Right,
            VerticalAlignment.Top,
            new Thickness(9));

        AddSteamRivet(
            root,
            HorizontalAlignment.Left,
            VerticalAlignment.Bottom,
            new Thickness(9));

        AddSteamRivet(
            root,
            HorizontalAlignment.Right,
            VerticalAlignment.Bottom,
            new Thickness(9));

        Grid layout = new()
        {
            Margin = new Thickness(15, 12, 15, 12)
        };

        layout.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        layout.RowDefinitions.Add(
            new RowDefinition
            {
                Height = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        layout.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        FrameworkElement header =
            CreateSteamHeader(
                title,
                temperatureType);

        Grid.SetRow(
            header,
            0);

        layout.Children.Add(header);

        Grid body = new()
        {
            Margin = new Thickness(0, 7, 0, 7)
        };

        body.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(238)
            });

        body.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        Grid dial =
            CreateSteamDial();

        Grid.SetColumn(
            dial,
            0);

        body.Children.Add(dial);

        FrameworkElement info =
            CreateSteamInfoPanel(
                temperatureType);

        Grid.SetColumn(
            info,
            1);

        body.Children.Add(info);

        Grid.SetRow(
            body,
            1);

        layout.Children.Add(body);

        FrameworkElement footer =
            CreateSteamFooter(
                temperatureType);

        Grid.SetRow(
            footer,
            2);

        layout.Children.Add(footer);

        root.Children.Add(layout);
        outer.Child = root;

        return outer;
    }


    private FrameworkElement CreateSteamHeader(
        string title,
        TemperatureType temperatureType)
    {
        Grid header = new();

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        Grid medallion = new()
        {
            Width = 42,
            Height = 42,
            Margin = new Thickness(0, 0, 10, 0)
        };

        Ellipse medallionOuter = new()
        {
            StrokeThickness = 3
        };

        medallionOuter.SetResourceReference(
            Shape.FillProperty,
            "SteamGlassBrush");

        medallionOuter.SetResourceReference(
            Shape.StrokeProperty,
            "SteamMetalBrush");

        Border verticalSpoke = new()
        {
            Width = 3,
            Height = 27,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        verticalSpoke.SetResourceReference(
            BackgroundProperty,
            "SteamMetalBrush");

        Border horizontalSpoke = new()
        {
            Width = 27,
            Height = 3,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        horizontalSpoke.SetResourceReference(
            BackgroundProperty,
            "SteamMetalBrush");

        Ellipse medallionHub = new()
        {
            Width = 13,
            Height = 13,
            StrokeThickness = 1
        };

        medallionHub.SetResourceReference(
            Shape.FillProperty,
            "SteamRivetBrush");

        medallionHub.SetResourceReference(
            Shape.StrokeProperty,
            "SteamDarkMetalBrush");

        medallion.Children.Add(medallionOuter);
        medallion.Children.Add(verticalSpoke);
        medallion.Children.Add(horizontalSpoke);
        medallion.Children.Add(medallionHub);

        StackPanel titlePanel = new()
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        TextBlock titleText = new()
        {
            Text = title,
            FontSize = 24,
            FontWeight = FontWeights.Bold
        };

        titleText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        titleText.SetResourceReference(
            TextBlock.FontFamilyProperty,
            "DisplayFontFamily");

        TextBlock subtitle = new()
        {
            Text = temperatureType == TemperatureType.Cpu
                ? "МАНОМЕТР НАГРУЗКИ ПРОЦЕССОРА"
                : "МАНОМЕТР НАГРУЗКИ ГРАФИЧЕСКОГО ПРОЦЕССОРА",
            FontSize = 8,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 1, 0, 0)
        };

        subtitle.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        titlePanel.Children.Add(titleText);
        titlePanel.Children.Add(subtitle);

        Border badge = new()
        {
            Padding = new Thickness(9, 5, 9, 5),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            VerticalAlignment = VerticalAlignment.Center
        };

        badge.SetResourceReference(
            Border.BackgroundProperty,
            "SteamGlassBrush");

        badge.SetResourceReference(
            Border.BorderBrushProperty,
            "SteamMetalBrush");

        TextBlock badgeText = new()
        {
            Text = "МЕХАНИЧЕСКИЙ ДАТЧИК",
            FontSize = 8,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };

        badgeText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        badge.Child = badgeText;

        Grid.SetColumn(medallion, 0);
        Grid.SetColumn(titlePanel, 1);
        Grid.SetColumn(badge, 2);

        header.Children.Add(medallion);
        header.Children.Add(titlePanel);
        header.Children.Add(badge);

        return header;
    }


    private Grid CreateSteamDial()
    {
        const double size = 220;

        Grid dial = new()
        {
            Width = size,
            Height = size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        Ellipse outer = new()
        {
            Width = 214,
            Height = 214,
            StrokeThickness = 5
        };

        outer.SetResourceReference(
            Shape.FillProperty,
            "SteamDarkMetalBrush");

        outer.SetResourceReference(
            Shape.StrokeProperty,
            "SteamMetalBrush");

        dial.Children.Add(outer);

        Ellipse bezel = new()
        {
            Width = 194,
            Height = 194,
            StrokeThickness = 2
        };

        bezel.SetResourceReference(
            Shape.FillProperty,
            "SteamGlassBrush");

        bezel.SetResourceReference(
            Shape.StrokeProperty,
            "BorderBrush");

        dial.Children.Add(bezel);

        Ellipse glass = new()
        {
            Width = 176,
            Height = 176,
            Fill = Brushes.Transparent,
            StrokeThickness = 1,
            Opacity = 0.72
        };

        glass.SetResourceReference(
            Shape.StrokeProperty,
            "SteamMetalBrush");

        dial.Children.Add(glass);

        for (int i = 0;
             i < 25;
             i++)
        {
            double angle =
                -120 + i * 10;

            bool major =
                i % 4 == 0;

            Grid tickLayer = new()
            {
                Width = size,
                Height = size,
                RenderTransformOrigin =
                    new Point(0.5, 0.5),
                RenderTransform =
                    new RotateTransform(angle)
            };

            Border tick = new()
            {
                Width = major ? 3 : 1.5,
                Height = major ? 14 : 8,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                VerticalAlignment =
                    VerticalAlignment.Top,
                Margin =
                    new Thickness(0, 23, 0, 0),
                Opacity =
                    major ? 0.95 : 0.58
            };

            tick.SetResourceReference(
                BackgroundProperty,
                major
                    ? "AccentBrush"
                    : "SecondaryTextBrush");

            tickLayer.Children.Add(tick);
            dial.Children.Add(tickLayer);
        }

        AddSteamScaleLabel(
            dial,
            "0",
            29,
            156);

        AddSteamScaleLabel(
            dial,
            "25",
            43,
            74);

        AddSteamScaleLabel(
            dial,
            "50",
            102,
            45);

        AddSteamScaleLabel(
            dial,
            "75",
            160,
            74);

        AddSteamScaleLabel(
            dial,
            "100",
            165,
            156);

        TextBlock loadCaption = new()
        {
            Text = "НАГРУЗКА, %",
            FontSize = 8,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Top,
            Margin =
                new Thickness(0, 64, 0, 0)
        };

        loadCaption.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        dial.Children.Add(loadCaption);

        _steamLoadText = new TextBlock
        {
            Text = "0 %",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Top,
            Margin =
                new Thickness(0, 79, 0, 0)
        };

        _steamLoadText.SetResourceReference(
            TextBlock.FontFamilyProperty,
            "DisplayFontFamily");

        dial.Children.Add(
            _steamLoadText);

        Grid needleLayer = new()
        {
            Width = size,
            Height = size,
            RenderTransformOrigin =
                new Point(0.5, 0.5)
        };

        _steamNeedleTransform =
            new RotateTransform(
                -120);

        needleLayer.RenderTransform =
            _steamNeedleTransform;

        _steamNeedle = new Border
        {
            Width = 3,
            Height = 72,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Top,
            Margin =
                new Thickness(0, 39, 0, 0),
            CornerRadius =
                new CornerRadius(2)
        };

        _steamNeedle.SetResourceReference(
            BackgroundProperty,
            "AccentBrush");

        needleLayer.Children.Add(
            _steamNeedle);

        dial.Children.Add(
            needleLayer);

        Ellipse hubOuter = new()
        {
            Width = 26,
            Height = 26,
            StrokeThickness = 2
        };

        hubOuter.SetResourceReference(
            Shape.FillProperty,
            "SteamMetalBrush");

        hubOuter.SetResourceReference(
            Shape.StrokeProperty,
            "SteamDarkMetalBrush");

        dial.Children.Add(hubOuter);

        Ellipse hubInner = new()
        {
            Width = 10,
            Height = 10
        };

        hubInner.SetResourceReference(
            Shape.FillProperty,
            "SteamDarkMetalBrush");

        dial.Children.Add(hubInner);

        Grid temperatureDial = new()
        {
            Width = 76,
            Height = 76,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Bottom,
            Margin =
                new Thickness(0, 0, 0, 5)
        };

        Ellipse temperatureOuter = new()
        {
            Width = 76,
            Height = 76,
            StrokeThickness = 3
        };

        temperatureOuter.SetResourceReference(
            Shape.FillProperty,
            "SteamDarkMetalBrush");

        temperatureOuter.SetResourceReference(
            Shape.StrokeProperty,
            "SteamMetalBrush");

        Ellipse temperatureInner = new()
        {
            Width = 62,
            Height = 62,
            StrokeThickness = 1
        };

        temperatureInner.SetResourceReference(
            Shape.FillProperty,
            "SteamGlassBrush");

        temperatureInner.SetResourceReference(
            Shape.StrokeProperty,
            "BorderBrush");

        StackPanel temperatureStack = new()
        {
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        TextBlock temperatureLabel = new()
        {
            Text = "ТЕМП.",
            FontSize = 7,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center
        };

        temperatureLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        _steamTemperatureText = new TextBlock
        {
            Text = "-- °C",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            Margin =
                new Thickness(0, 1, 0, 0)
        };

        _steamTemperatureText.SetResourceReference(
            TextBlock.FontFamilyProperty,
            "DisplayFontFamily");

        Border temperaturePointer = new()
        {
            Width = 18,
            Height = 2,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Top,
            Margin =
                new Thickness(0, 5, 0, 0)
        };

        temperaturePointer.SetResourceReference(
            BackgroundProperty,
            "AccentBrush");

        temperatureStack.Children.Add(
            temperatureLabel);

        temperatureStack.Children.Add(
            _steamTemperatureText);

        temperatureDial.Children.Add(
            temperatureOuter);

        temperatureDial.Children.Add(
            temperatureInner);

        temperatureDial.Children.Add(
            temperaturePointer);

        temperatureDial.Children.Add(
            temperatureStack);

        dial.Children.Add(
            temperatureDial);

        return dial;
    }


    private FrameworkElement CreateSteamInfoPanel(
        TemperatureType temperatureType)
    {
        StackPanel panel = new()
        {
            Margin =
                new Thickness(9, 7, 0, 7),
            VerticalAlignment =
                VerticalAlignment.Stretch
        };

        Border modelPlate =
            CreateSteamPlate();

        StackPanel modelStack = new();

        TextBlock modelLabel = new()
        {
            Text = "МОДЕЛЬ",
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        modelLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        _steamHardwareNameText = new TextBlock
        {
            Text = "--",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 42,
            Margin =
                new Thickness(0, 4, 0, 0)
        };

        _steamHardwareNameText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        modelStack.Children.Add(modelLabel);
        modelStack.Children.Add(
            _steamHardwareNameText);

        modelPlate.Child = modelStack;
        panel.Children.Add(modelPlate);

        Grid statusGrid = new()
        {
            Margin =
                new Thickness(0, 9, 0, 0)
        };

        statusGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        statusGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        StackPanel statusLeft = new();

        TextBlock statusLabel = new()
        {
            Text = "СОСТОЯНИЕ ДАТЧИКА",
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        statusLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        StackPanel statusRow = new()
        {
            Orientation =
                Orientation.Horizontal,
            Margin =
                new Thickness(0, 5, 0, 0)
        };

        Border lampHousing = new()
        {
            Width = 17,
            Height = 17,
            CornerRadius =
                new CornerRadius(9),
            BorderThickness =
                new Thickness(2),
            Margin =
                new Thickness(0, 0, 7, 0)
        };

        lampHousing.SetResourceReference(
            Border.BackgroundProperty,
            "SteamDarkMetalBrush");

        lampHousing.SetResourceReference(
            Border.BorderBrushProperty,
            "SteamMetalBrush");

        _steamStatusLamp = new Ellipse
        {
            Width = 7,
            Height = 7,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        _steamStatusLamp.SetResourceReference(
            Shape.FillProperty,
            "AccentBrush");

        lampHousing.Child =
            _steamStatusLamp;

        _steamStatusText = new TextBlock
        {
            Text = "ДАТЧИК АКТИВЕН",
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        _steamStatusText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        statusRow.Children.Add(
            lampHousing);

        statusRow.Children.Add(
            _steamStatusText);

        statusLeft.Children.Add(
            statusLabel);

        statusLeft.Children.Add(
            statusRow);

        Grid.SetColumn(
            statusLeft,
            0);

        statusGrid.Children.Add(
            statusLeft);

        TextBlock gearDecoration = new()
        {
            Text = "⚙",
            FontSize = 30,
            Opacity = 0.22,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        gearDecoration.SetResourceReference(
            TextBlock.ForegroundProperty,
            "BorderBrush");

        Grid.SetColumn(
            gearDecoration,
            1);

        statusGrid.Children.Add(
            gearDecoration);

        panel.Children.Add(
            statusGrid);

        FrameworkElement details =
            temperatureType ==
            TemperatureType.Gpu
                ? CreateSteamGpuDetails()
                : CreateSteamCpuDetails();

        panel.Children.Add(details);

        return panel;
    }


    private FrameworkElement CreateSteamGpuDetails()
    {
        Grid cards = new()
        {
            Margin =
                new Thickness(0, 10, 0, 0)
        };

        cards.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        cards.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(8)
            });

        cards.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        Border hotSpot =
            CreateSteamPlate();

        StackPanel hotSpotStack = new();

        TextBlock hotSpotLabel = new()
        {
            Text = "ГОРЯЧАЯ ТОЧКА",
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        hotSpotLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        _steamHotSpotValueText = new TextBlock
        {
            Text = "--",
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Margin =
                new Thickness(0, 3, 0, 0)
        };

        _steamHotSpotValueText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        _steamHotSpotValueText.SetResourceReference(
            TextBlock.FontFamilyProperty,
            "DisplayFontFamily");

        hotSpotStack.Children.Add(
            hotSpotLabel);

        hotSpotStack.Children.Add(
            _steamHotSpotValueText);

        hotSpot.Child =
            hotSpotStack;

        Border vram =
            CreateSteamPlate();

        StackPanel vramStack = new();

        TextBlock vramLabel = new()
        {
            Text = "ТЕМПЕРАТУРА VRAM",
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        vramLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        _steamVramValueText = new TextBlock
        {
            Text = "--",
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Margin =
                new Thickness(0, 3, 0, 0)
        };

        _steamVramValueText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        _steamVramValueText.SetResourceReference(
            TextBlock.FontFamilyProperty,
            "DisplayFontFamily");

        vramStack.Children.Add(
            vramLabel);

        vramStack.Children.Add(
            _steamVramValueText);

        vram.Child =
            vramStack;

        Grid.SetColumn(
            hotSpot,
            0);

        Grid.SetColumn(
            vram,
            2);

        cards.Children.Add(
            hotSpot);

        cards.Children.Add(
            vram);

        return cards;
    }


    private FrameworkElement CreateSteamCpuDetails()
    {
        _steamHotSpotValueText = null;
        _steamVramValueText = null;

        Border plate =
            CreateSteamPlate();

        plate.Margin =
            new Thickness(0, 10, 0, 0);

        Grid grid = new();

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        Grid valve = new()
        {
            Width = 27,
            Height = 27,
            Margin =
                new Thickness(0, 0, 9, 0),
            VerticalAlignment =
                VerticalAlignment.Center
        };

        Ellipse valveRing = new()
        {
            StrokeThickness = 2
        };

        valveRing.SetResourceReference(
            Shape.StrokeProperty,
            "SteamMetalBrush");

        Border valveVertical = new()
        {
            Width = 2,
            Height = 18,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        valveVertical.SetResourceReference(
            BackgroundProperty,
            "AccentBrush");

        Border valveHorizontal = new()
        {
            Width = 18,
            Height = 2,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        valveHorizontal.SetResourceReference(
            BackgroundProperty,
            "AccentBrush");

        valve.Children.Add(valveRing);
        valve.Children.Add(valveVertical);
        valve.Children.Add(valveHorizontal);

        StackPanel text = new();

        TextBlock top = new()
        {
            Text = "КАНАЛ ДАТЧИКОВ",
            FontSize = 9,
            FontWeight = FontWeights.Bold
        };

        top.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        TextBlock bottom = new()
        {
            Text = "Мониторинг в реальном времени",
            FontSize = 8,
            FontWeight =
                FontWeights.SemiBold,
            Margin =
                new Thickness(0, 3, 0, 0)
        };

        bottom.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        text.Children.Add(top);
        text.Children.Add(bottom);

        Grid.SetColumn(
            valve,
            0);

        Grid.SetColumn(
            text,
            1);

        Border threadsButton =
            CreateCpuThreadsButton(
                "SteamGlassBrush",
                "SteamMetalBrush",
                "AccentBrush",
                new CornerRadius(5));

        Grid.SetColumn(
            threadsButton,
            2);

        grid.Children.Add(valve);
        grid.Children.Add(text);
        grid.Children.Add(threadsButton);
        plate.Child = grid;

        return plate;
    }


    private static FrameworkElement CreateSteamFooter(
        TemperatureType temperatureType)
    {
        Border plate = new()
        {
            Padding =
                new Thickness(9, 4, 9, 4),
            BorderThickness =
                new Thickness(1),
            CornerRadius =
                new CornerRadius(5),
            HorizontalAlignment =
                HorizontalAlignment.Stretch
        };

        plate.SetResourceReference(
            Border.BackgroundProperty,
            "SteamGlassBrush");

        plate.SetResourceReference(
            Border.BorderBrushProperty,
            "BorderBrush");

        Grid grid = new();

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        TextBlock left = new()
        {
            Text = temperatureType ==
                   TemperatureType.Cpu
                ? "МОДУЛЬ CPU"
                : "МОДУЛЬ GPU",
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        left.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        Border pipe = new()
        {
            Height = 2,
            Margin =
                new Thickness(10, 0, 10, 0),
            VerticalAlignment =
                VerticalAlignment.Center
        };

        pipe.SetResourceReference(
            BackgroundProperty,
            "SteamMetalBrush");

        TextBlock right = new()
        {
            Text = "ДАТЧИКИ В РЕАЛЬНОМ ВРЕМЕНИ",
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        right.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        Grid.SetColumn(left, 0);
        Grid.SetColumn(pipe, 1);
        Grid.SetColumn(right, 2);

        grid.Children.Add(left);
        grid.Children.Add(pipe);
        grid.Children.Add(right);

        plate.Child = grid;

        return plate;
    }


    private static Border CreateSteamPlate()
    {
        Border plate = new()
        {
            Padding =
                new Thickness(10, 8, 10, 8),
            BorderThickness =
                new Thickness(1),
            CornerRadius =
                new CornerRadius(6)
        };

        plate.SetResourceReference(
            Border.BackgroundProperty,
            "SteamGlassBrush");

        plate.SetResourceReference(
            Border.BorderBrushProperty,
            "BorderBrush");

        return plate;
    }


    private static void AddSteamRivet(
        Grid root,
        HorizontalAlignment horizontal,
        VerticalAlignment vertical,
        Thickness margin)
    {
        Ellipse rivet = new()
        {
            Width = 8,
            Height = 8,
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical,
            Margin = margin,
            StrokeThickness = 1,
            IsHitTestVisible = false
        };

        rivet.SetResourceReference(
            Shape.FillProperty,
            "SteamRivetBrush");

        rivet.SetResourceReference(
            Shape.StrokeProperty,
            "SteamDarkMetalBrush");

        root.Children.Add(rivet);
    }


    private static void AddSteamScaleLabel(
        Grid root,
        string text,
        double left,
        double top)
    {
        TextBlock label = new()
        {
            Text = text,
            FontSize = 8,
            FontWeight = FontWeights.Bold,
            Margin =
                new Thickness(
                    left,
                    top,
                    0,
                    0),
            HorizontalAlignment =
                HorizontalAlignment.Left,
            VerticalAlignment =
                VerticalAlignment.Top,
            IsHitTestVisible = false
        };

        label.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        root.Children.Add(label);
    }



    // ============================================================
    // FROST CORE VIEW
    // ============================================================

    private FrameworkElement CreateFrostView(
        string title,
        TemperatureType temperatureType)
    {
        Border outer = new()
        {
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(7),
            MinHeight = 338
        };

        outer.SetResourceReference(
            BackgroundProperty,
            "FrostPanelBrush");

        outer.SetResourceReference(
            BorderBrushProperty,
            "FrostSteelBrush");

        outer.SetResourceReference(
            VisibilityProperty,
            "FrostCoreVisibility");

        Grid root = new();

        Border inner = new()
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(15)
        };

        inner.SetResourceReference(
            BackgroundProperty,
            "FrostGlassBrush");

        inner.SetResourceReference(
            BorderBrushProperty,
            "BorderBrush");

        root.Children.Add(inner);

        Border coldStrip = new()
        {
            Height = 4,
            Margin = new Thickness(26, 0, 26, 0),
            VerticalAlignment = VerticalAlignment.Top,
            CornerRadius = new CornerRadius(0, 0, 5, 5),
            Opacity = 0.82
        };

        coldStrip.SetResourceReference(
            BackgroundProperty,
            "FrostIceBrush");

        root.Children.Add(coldStrip);

        Grid layout = new()
        {
            Margin = new Thickness(15, 12, 15, 12)
        };

        layout.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        layout.RowDefinitions.Add(
            new RowDefinition
            {
                Height = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        layout.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        FrameworkElement header =
            CreateFrostHeader(
                title,
                temperatureType);

        Grid.SetRow(header, 0);
        layout.Children.Add(header);

        Grid body = new()
        {
            Margin = new Thickness(0, 7, 0, 7)
        };

        body.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(238)
            });

        body.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        FrameworkElement chamber =
            CreateFrostChamber();

        Grid.SetColumn(chamber, 0);
        body.Children.Add(chamber);

        FrameworkElement info =
            CreateFrostInfoPanel(
                temperatureType);

        Grid.SetColumn(info, 1);
        body.Children.Add(info);

        Grid.SetRow(body, 1);
        layout.Children.Add(body);

        FrameworkElement footer =
            CreateFrostFooter(
                temperatureType);

        Grid.SetRow(footer, 2);
        layout.Children.Add(footer);

        root.Children.Add(layout);
        outer.Child = root;

        return outer;
    }


    private FrameworkElement CreateFrostHeader(
        string title,
        TemperatureType temperatureType)
    {
        Grid header = new();

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        Grid cryoMark = new()
        {
            Width = 44,
            Height = 44,
            Margin = new Thickness(0, 0, 10, 0)
        };

        Border cryoShell = new()
        {
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(13)
        };

        cryoShell.SetResourceReference(
            BackgroundProperty,
            "FrostGlassBrush");

        cryoShell.SetResourceReference(
            BorderBrushProperty,
            "FrostSteelBrush");

        Border vertical = new()
        {
            Width = 3,
            Height = 27,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(2)
        };

        vertical.SetResourceReference(
            BackgroundProperty,
            "FrostIceBrush");

        Border horizontal = new()
        {
            Width = 27,
            Height = 3,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(2)
        };

        horizontal.SetResourceReference(
            BackgroundProperty,
            "FrostIceBrush");

        Border diagonalA = new()
        {
            Width = 25,
            Height = 2,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(45)
        };

        diagonalA.SetResourceReference(
            BackgroundProperty,
            "FrostSteelBrush");

        Border diagonalB = new()
        {
            Width = 25,
            Height = 2,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(-45)
        };

        diagonalB.SetResourceReference(
            BackgroundProperty,
            "FrostSteelBrush");

        Ellipse core = new()
        {
            Width = 11,
            Height = 11
        };

        core.SetResourceReference(
            Shape.FillProperty,
            "FrostIceBrush");

        cryoMark.Children.Add(cryoShell);
        cryoMark.Children.Add(vertical);
        cryoMark.Children.Add(horizontal);
        cryoMark.Children.Add(diagonalA);
        cryoMark.Children.Add(diagonalB);
        cryoMark.Children.Add(core);

        StackPanel titlePanel = new()
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        TextBlock titleText = new()
        {
            Text = title,
            FontSize = 24,
            FontWeight = FontWeights.Bold
        };

        titleText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        TextBlock subtitle = new()
        {
            Text = temperatureType == TemperatureType.Cpu
                ? "КРИОГЕННЫЙ ПРОЦЕССОРНЫЙ МОДУЛЬ"
                : "КРИОГЕННЫЙ ГРАФИЧЕСКИЙ МОДУЛЬ",
            FontSize = 8,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 1, 0, 0)
        };

        subtitle.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        titlePanel.Children.Add(titleText);
        titlePanel.Children.Add(subtitle);

        Border badge = new()
        {
            Padding = new Thickness(9, 5, 9, 5),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            VerticalAlignment = VerticalAlignment.Center
        };

        badge.SetResourceReference(
            BackgroundProperty,
            "FrostGlassBrush");

        badge.SetResourceReference(
            BorderBrushProperty,
            "FrostSteelBrush");

        StackPanel badgeRow = new()
        {
            Orientation = Orientation.Horizontal
        };

        Ellipse badgeDot = new()
        {
            Width = 6,
            Height = 6,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        badgeDot.SetResourceReference(
            Shape.FillProperty,
            "FrostIceBrush");

        TextBlock badgeText = new()
        {
            Text = "КРИОКОНТУР",
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        badgeText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        badgeRow.Children.Add(badgeDot);
        badgeRow.Children.Add(badgeText);
        badge.Child = badgeRow;

        Grid.SetColumn(cryoMark, 0);
        Grid.SetColumn(titlePanel, 1);
        Grid.SetColumn(badge, 2);

        header.Children.Add(cryoMark);
        header.Children.Add(titlePanel);
        header.Children.Add(badge);

        return header;
    }


    private FrameworkElement CreateFrostChamber()
    {
        Grid chamber = new()
        {
            Width = 220,
            Height = 222,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        Border housing = new()
        {
            Width = 206,
            Height = 216,
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(30),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        housing.SetResourceReference(
            BackgroundProperty,
            "FrostPanelBrush");

        housing.SetResourceReference(
            BorderBrushProperty,
            "FrostSteelBrush");

        chamber.Children.Add(housing);

        Border inner = new()
        {
            Width = 182,
            Height = 194,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        inner.SetResourceReference(
            BackgroundProperty,
            "FrostGlassBrush");

        inner.SetResourceReference(
            BorderBrushProperty,
            "FrostIceBrush");

        chamber.Children.Add(inner);

        TextBlock loadCaption = new()
        {
            Text = "УРОВЕНЬ НАГРУЗКИ",
            FontSize = 8,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 17, 0, 0)
        };

        loadCaption.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        chamber.Children.Add(loadCaption);

        Grid tank = new()
        {
            Width = 100,
            Height = 134,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0),
            ClipToBounds = true
        };

        Border tankGlass = new()
        {
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(26)
        };

        tankGlass.SetResourceReference(
            BackgroundProperty,
            "InputBackgroundBrush");

        tankGlass.SetResourceReference(
            BorderBrushProperty,
            "FrostSteelBrush");

        _frostLoadFill = new Border
        {
            Width = 80,
            Height = 0,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 8),
            CornerRadius = new CornerRadius(20),
            Opacity = 0.88
        };

        _frostLoadFill.SetResourceReference(
            BackgroundProperty,
            "AccentBrush");

        Border sheen = new()
        {
            Width = 64,
            Height = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 12, 0, 0),
            Background = new SolidColorBrush(
                Color.FromArgb(55, 255, 255, 255)),
            CornerRadius = new CornerRadius(2)
        };

        TextBlock snow = new()
        {
            Text = "❄",
            FontSize = 70,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.30,
            IsHitTestVisible = false
        };

        snow.SetResourceReference(
            TextBlock.ForegroundProperty,
            "FrostIceBrush");

        tank.Children.Add(tankGlass);
        tank.Children.Add(_frostLoadFill);
        tank.Children.Add(sheen);
        tank.Children.Add(snow);
        chamber.Children.Add(tank);

        // Шкала 0–100 не похожа на круглый манометр: это уровень в криокамере.
        string[] scale =
        {
            "100",
            "75",
            "50",
            "25",
            "0"
        };

        double[] scaleTop =
        {
            47,
            75,
            103,
            131,
            159
        };

        for (int i = 0;
             i < scale.Length;
             i++)
        {
            TextBlock label = new()
            {
                Text = scale[i],
                FontSize = 7,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(
                    23,
                    scaleTop[i],
                    0,
                    0)
            };

            label.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            chamber.Children.Add(label);

            Border tick = new()
            {
                Width = 11,
                Height = 1,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(
                    47,
                    scaleTop[i] + 5,
                    0,
                    0)
            };

            tick.SetResourceReference(
                BackgroundProperty,
                "FrostSteelBrush");

            chamber.Children.Add(tick);
        }

        _frostLoadText = new TextBlock
        {
            Text = "0 %",
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 7, 0, 0)
        };

        _frostLoadText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        chamber.Children.Add(_frostLoadText);

        Border temperaturePlate = new()
        {
            Width = 104,
            Height = 40,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 8),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(12)
        };

        temperaturePlate.SetResourceReference(
            BackgroundProperty,
            "FrostGlassBrush");

        temperaturePlate.SetResourceReference(
            BorderBrushProperty,
            "FrostSteelBrush");

        StackPanel tempStack = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        TextBlock tempLabel = new()
        {
            Text = "ТЕМПЕРАТУРА",
            FontSize = 7,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        tempLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        _frostTemperatureText = new TextBlock
        {
            Text = "-- °C",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 0)
        };

        tempStack.Children.Add(tempLabel);
        tempStack.Children.Add(_frostTemperatureText);
        temperaturePlate.Child = tempStack;
        chamber.Children.Add(temperaturePlate);

        return chamber;
    }


    private FrameworkElement CreateFrostInfoPanel(
        TemperatureType temperatureType)
    {
        StackPanel panel = new()
        {
            Margin = new Thickness(9, 7, 0, 7),
            VerticalAlignment = VerticalAlignment.Stretch
        };

        Border modelPlate =
            CreateFrostPlate();

        StackPanel modelStack = new();

        TextBlock modelLabel = new()
        {
            Text = "МОДЕЛЬ",
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        modelLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        _frostHardwareNameText = new TextBlock
        {
            Text = "--",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 42,
            Margin = new Thickness(0, 4, 0, 0)
        };

        _frostHardwareNameText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        modelStack.Children.Add(modelLabel);
        modelStack.Children.Add(_frostHardwareNameText);
        modelPlate.Child = modelStack;
        panel.Children.Add(modelPlate);

        Border statusPlate =
            CreateFrostPlate();

        statusPlate.Margin =
            new Thickness(0, 9, 0, 0);

        Grid statusGrid = new();

        statusGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        statusGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        _frostStatusDot = new Ellipse
        {
            Width = 11,
            Height = 11,
            Margin = new Thickness(0, 0, 9, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        _frostStatusDot.SetResourceReference(
            Shape.FillProperty,
            "FrostIceBrush");

        StackPanel statusTextStack = new();

        TextBlock statusLabel = new()
        {
            Text = "СОСТОЯНИЕ КРИОКОНТУРА",
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        statusLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        _frostStatusText = new TextBlock
        {
            Text = "ДАТЧИК АКТИВЕН",
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 3, 0, 0)
        };

        _frostStatusText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        statusTextStack.Children.Add(statusLabel);
        statusTextStack.Children.Add(_frostStatusText);

        Grid.SetColumn(_frostStatusDot, 0);
        Grid.SetColumn(statusTextStack, 1);

        statusGrid.Children.Add(_frostStatusDot);
        statusGrid.Children.Add(statusTextStack);
        statusPlate.Child = statusGrid;
        panel.Children.Add(statusPlate);

        FrameworkElement details =
            temperatureType == TemperatureType.Gpu
                ? CreateFrostGpuDetails()
                : CreateFrostCpuDetails();

        panel.Children.Add(details);

        return panel;
    }


    private FrameworkElement CreateFrostGpuDetails()
    {
        Grid cards = new()
        {
            Margin = new Thickness(0, 10, 0, 0)
        };

        cards.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        cards.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(8)
            });

        cards.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        Border hotSpot =
            CreateFrostPlate();

        StackPanel hotSpotStack = new();

        TextBlock hotSpotLabel = new()
        {
            Text = "ГОРЯЧАЯ ТОЧКА",
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        hotSpotLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        _frostHotSpotValueText = new TextBlock
        {
            Text = "--",
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 3, 0, 0)
        };

        _frostHotSpotValueText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        hotSpotStack.Children.Add(hotSpotLabel);
        hotSpotStack.Children.Add(_frostHotSpotValueText);
        hotSpot.Child = hotSpotStack;

        Border vram =
            CreateFrostPlate();

        StackPanel vramStack = new();

        TextBlock vramLabel = new()
        {
            Text = "ТЕМПЕРАТУРА VRAM",
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        vramLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        _frostVramValueText = new TextBlock
        {
            Text = "--",
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 3, 0, 0)
        };

        _frostVramValueText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        vramStack.Children.Add(vramLabel);
        vramStack.Children.Add(_frostVramValueText);
        vram.Child = vramStack;

        Grid.SetColumn(hotSpot, 0);
        Grid.SetColumn(vram, 2);

        cards.Children.Add(hotSpot);
        cards.Children.Add(vram);

        return cards;
    }


    private FrameworkElement CreateFrostCpuDetails()
    {
        _frostHotSpotValueText = null;
        _frostVramValueText = null;

        Border plate =
            CreateFrostPlate();

        plate.Margin =
            new Thickness(0, 10, 0, 0);

        Grid grid = new();

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        Grid snow = new()
        {
            Width = 29,
            Height = 29,
            Margin = new Thickness(0, 0, 9, 0)
        };

        Border shell = new()
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9)
        };

        shell.SetResourceReference(
            BorderBrushProperty,
            "FrostSteelBrush");

        TextBlock mark = new()
        {
            Text = "❄",
            FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        mark.SetResourceReference(
            TextBlock.ForegroundProperty,
            "FrostIceBrush");

        snow.Children.Add(shell);
        snow.Children.Add(mark);

        StackPanel text = new();

        TextBlock top = new()
        {
            Text = "КОНТУР ОХЛАЖДЕНИЯ",
            FontSize = 9,
            FontWeight = FontWeights.Bold
        };

        top.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        TextBlock bottom = new()
        {
            Text = "Мониторинг криоядра в реальном времени",
            FontSize = 8,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 3, 0, 0)
        };

        bottom.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        text.Children.Add(top);
        text.Children.Add(bottom);

        Grid.SetColumn(snow, 0);
        Grid.SetColumn(text, 1);

        Border threadsButton =
            CreateCpuThreadsButton(
                "FrostGlassBrush",
                "FrostSteelBrush",
                "AccentBrush",
                new CornerRadius(8));

        Grid.SetColumn(
            threadsButton,
            2);

        grid.Children.Add(snow);
        grid.Children.Add(text);
        grid.Children.Add(threadsButton);
        plate.Child = grid;

        return plate;
    }


    private static Border CreateFrostPlate()
    {
        Border plate = new()
        {
            Padding = new Thickness(10, 8, 10, 8),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10)
        };

        plate.SetResourceReference(
            BackgroundProperty,
            "FrostGlassBrush");

        plate.SetResourceReference(
            BorderBrushProperty,
            "FrostSteelBrush");

        return plate;
    }


    private static FrameworkElement CreateFrostFooter(
        TemperatureType temperatureType)
    {
        Border plate = new()
        {
            Padding = new Thickness(9, 4, 9, 4),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        plate.SetResourceReference(
            BackgroundProperty,
            "FrostGlassBrush");

        plate.SetResourceReference(
            BorderBrushProperty,
            "BorderBrush");

        Grid grid = new();

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        TextBlock left = new()
        {
            Text = temperatureType == TemperatureType.Cpu
                ? "КРИОЯДРО CPU"
                : "КРИОЯДРО GPU",
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        left.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        Border line = new()
        {
            Height = 2,
            Margin = new Thickness(10, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        line.SetResourceReference(
            BackgroundProperty,
            "FrostSteelBrush");

        TextBlock right = new()
        {
            Text = "ОХЛАЖДАЮЩИЙ КОНТУР АКТИВЕН",
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        right.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        Grid.SetColumn(left, 0);
        Grid.SetColumn(line, 1);
        Grid.SetColumn(right, 2);

        grid.Children.Add(left);
        grid.Children.Add(line);
        grid.Children.Add(right);
        plate.Child = grid;

        return plate;
    }



    // ============================================================
    // MILITARY OPS VIEW
    // ============================================================

    private FrameworkElement CreateMilitaryView(
        string title,
        TemperatureType temperatureType)
    {
        Border outer = new()
        {
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(6),
            MinHeight = 338
        };

        outer.SetResourceReference(
            BackgroundProperty,
            "MilitaryPanelBrush");

        outer.SetResourceReference(
            BorderBrushProperty,
            "MilitaryKhakiBrush");

        outer.SetResourceReference(
            VisibilityProperty,
            "MilitaryOpsVisibility");

        Grid root = new();

        Border inner = new()
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(1)
        };

        inner.SetResourceReference(
            BackgroundProperty,
            "MilitaryPlateBrush");

        inner.SetResourceReference(
            BorderBrushProperty,
            "MilitaryGridBrush");

        root.Children.Add(inner);

        AddMilitaryCornerMark(
            root,
            HorizontalAlignment.Left,
            VerticalAlignment.Top);

        AddMilitaryCornerMark(
            root,
            HorizontalAlignment.Right,
            VerticalAlignment.Top);

        AddMilitaryCornerMark(
            root,
            HorizontalAlignment.Left,
            VerticalAlignment.Bottom);

        AddMilitaryCornerMark(
            root,
            HorizontalAlignment.Right,
            VerticalAlignment.Bottom);

        Grid layout = new()
        {
            Margin = new Thickness(15, 12, 15, 12)
        };

        layout.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        layout.RowDefinitions.Add(
            new RowDefinition
            {
                Height = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        layout.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        FrameworkElement header =
            CreateMilitaryHeader(
                title,
                temperatureType);

        Grid.SetRow(header, 0);
        layout.Children.Add(header);

        Grid body = new()
        {
            Margin = new Thickness(0, 7, 0, 7)
        };

        body.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(238)
            });

        body.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        FrameworkElement instrument =
            CreateMilitaryInstrument();

        Grid.SetColumn(instrument, 0);
        body.Children.Add(instrument);

        FrameworkElement info =
            CreateMilitaryInfoPanel(
                temperatureType);

        Grid.SetColumn(info, 1);
        body.Children.Add(info);

        Grid.SetRow(body, 1);
        layout.Children.Add(body);

        FrameworkElement footer =
            CreateMilitaryFooter(
                temperatureType);

        Grid.SetRow(footer, 2);
        layout.Children.Add(footer);

        root.Children.Add(layout);
        outer.Child = root;

        return outer;
    }


    private FrameworkElement CreateMilitaryHeader(
        string title,
        TemperatureType temperatureType)
    {
        Grid header = new();

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        Grid compass = new()
        {
            Width = 44,
            Height = 44,
            Margin = new Thickness(0, 0, 10, 0)
        };

        Ellipse compassOuter = new()
        {
            StrokeThickness = 2
        };

        compassOuter.SetResourceReference(
            Shape.FillProperty,
            "MilitaryPlateBrush");

        compassOuter.SetResourceReference(
            Shape.StrokeProperty,
            "MilitaryKhakiBrush");

        Border compassVertical = new()
        {
            Width = 1,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        compassVertical.SetResourceReference(
            BackgroundProperty,
            "MilitaryGridBrush");

        Border compassHorizontal = new()
        {
            Width = 32,
            Height = 1,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        compassHorizontal.SetResourceReference(
            BackgroundProperty,
            "MilitaryGridBrush");

        TextBlock north = new()
        {
            Text = "N",
            FontSize = 7,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 0)
        };

        north.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        Path pointer = new()
        {
            Data = Geometry.Parse(
                "M 0,12 L 6,0 L 12,12 L 6,9 Z"),
            Width = 12,
            Height = 12,
            Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        pointer.SetResourceReference(
            Shape.FillProperty,
            "AccentBrush");

        compass.Children.Add(compassOuter);
        compass.Children.Add(compassVertical);
        compass.Children.Add(compassHorizontal);
        compass.Children.Add(north);
        compass.Children.Add(pointer);

        StackPanel titlePanel = new()
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        TextBlock titleText = new()
        {
            Text = title,
            FontSize = 24,
            FontWeight = FontWeights.Bold
        };

        titleText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        titleText.SetResourceReference(
            TextBlock.FontFamilyProperty,
            "DisplayFontFamily");

        TextBlock subtitle = new()
        {
            Text = temperatureType == TemperatureType.Cpu
                ? "ПОЛЕВОЙ МОДУЛЬ ПРОЦЕССОРА"
                : "ПОЛЕВОЙ МОДУЛЬ ГРАФИЧЕСКОГО ПРОЦЕССОРА",
            FontSize = 8,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 1, 0, 0)
        };

        subtitle.SetResourceReference(
            TextBlock.ForegroundProperty,
            "MilitaryKhakiBrush");

        subtitle.SetResourceReference(
            TextBlock.FontFamilyProperty,
            "DisplayFontFamily");

        titlePanel.Children.Add(titleText);
        titlePanel.Children.Add(subtitle);

        Border badge = new()
        {
            Padding = new Thickness(9, 5, 9, 5),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(1),
            VerticalAlignment = VerticalAlignment.Center
        };

        badge.SetResourceReference(
            BackgroundProperty,
            "MilitaryPanelBrush");

        badge.SetResourceReference(
            BorderBrushProperty,
            "MilitaryKhakiBrush");

        TextBlock badgeText = new()
        {
            Text = "ТАКТИЧЕСКИЙ ДАТЧИК",
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        badgeText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        badgeText.SetResourceReference(
            TextBlock.FontFamilyProperty,
            "DisplayFontFamily");

        badge.Child = badgeText;

        Grid.SetColumn(compass, 0);
        Grid.SetColumn(titlePanel, 1);
        Grid.SetColumn(badge, 2);

        header.Children.Add(compass);
        header.Children.Add(titlePanel);
        header.Children.Add(badge);

        return header;
    }


    private FrameworkElement CreateMilitaryInstrument()
    {
        Grid instrument = new()
        {
            Width = 220,
            Height = 220,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        Border housing = new()
        {
            Width = 208,
            Height = 210,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        housing.SetResourceReference(
            BackgroundProperty,
            "MilitaryPanelBrush");

        housing.SetResourceReference(
            BorderBrushProperty,
            "MilitaryKhakiBrush");

        instrument.Children.Add(housing);

        Border inner = new()
        {
            Width = 190,
            Height = 192,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        inner.SetResourceReference(
            BackgroundProperty,
            "MilitaryPlateBrush");

        inner.SetResourceReference(
            BorderBrushProperty,
            "MilitaryGridBrush");

        instrument.Children.Add(inner);

        // Координатная сетка на приборе.
        for (int i = 1;
             i <= 3;
             i++)
        {
            Border vertical = new()
            {
                Width = 1,
                Height = 166,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(54 + i * 31, 0, 0, 0),
                Opacity = 0.45
            };

            vertical.SetResourceReference(
                BackgroundProperty,
                "MilitaryGridBrush");

            instrument.Children.Add(vertical);
        }

        for (int i = 1;
             i <= 4;
             i++)
        {
            Border horizontal = new()
            {
                Width = 166,
                Height = 1,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 35 + i * 31, 0, 0),
                Opacity = 0.45
            };

            horizontal.SetResourceReference(
                BackgroundProperty,
                "MilitaryGridBrush");

            instrument.Children.Add(horizontal);
        }

        TextBlock caption = new()
        {
            Text = "НАГРУЗКА СИСТЕМЫ",
            FontSize = 8,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(24, 20, 0, 0)
        };

        caption.SetResourceReference(
            TextBlock.ForegroundProperty,
            "MilitaryKhakiBrush");

        caption.SetResourceReference(
            TextBlock.FontFamilyProperty,
            "DisplayFontFamily");

        instrument.Children.Add(caption);

        _militaryLoadText = new TextBlock
        {
            Text = "0 %",
            FontSize = 34,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(24, 48, 0, 0)
        };

        _militaryLoadText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        _militaryLoadText.SetResourceReference(
            TextBlock.FontFamilyProperty,
            "DisplayFontFamily");

        instrument.Children.Add(_militaryLoadText);

        TextBlock sectorLabel = new()
        {
            Text = "СЕКТОР LOAD-01",
            FontSize = 7,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(25, 91, 0, 0)
        };

        sectorLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        sectorLabel.SetResourceReference(
            TextBlock.FontFamilyProperty,
            "DisplayFontFamily");

        instrument.Children.Add(sectorLabel);

        // Десятисекторная шкала — как ресурсная кассета.
        Grid segments = new()
        {
            Width = 56,
            Height = 112,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 35, 24, 0)
        };

        for (int i = 0;
             i < 10;
             i++)
        {
            segments.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = new GridLength(
                        1,
                        GridUnitType.Star)
                });

            Border segment = new()
            {
                Margin = new Thickness(0, 1.5, 0, 1.5),
                BorderThickness = new Thickness(1)
            };

            segment.SetResourceReference(
                BackgroundProperty,
                "MilitaryGridBrush");

            segment.SetResourceReference(
                BorderBrushProperty,
                "MilitaryKhakiBrush");

            int row = 9 - i;
            Grid.SetRow(segment, row);

            _militaryLoadSegments[i] = segment;
            segments.Children.Add(segment);
        }

        instrument.Children.Add(segments);

        Border temperaturePlate = new()
        {
            Width = 116,
            Height = 47,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(24, 0, 0, 17),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(1)
        };

        temperaturePlate.SetResourceReference(
            BackgroundProperty,
            "MilitaryPanelBrush");

        temperaturePlate.SetResourceReference(
            BorderBrushProperty,
            "MilitaryKhakiBrush");

        Grid tempGrid = new();

        tempGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        tempGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        StackPanel tempLabelStack = new()
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(9, 0, 6, 0)
        };

        TextBlock tempLabel = new()
        {
            Text = "ТЕМПЕРАТУРА",
            FontSize = 7,
            FontWeight = FontWeights.Bold
        };

        tempLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "MilitaryKhakiBrush");

        tempLabel.SetResourceReference(
            TextBlock.FontFamilyProperty,
            "DisplayFontFamily");

        TextBlock tempCode = new()
        {
            Text = "THERM-01",
            FontSize = 7,
            Margin = new Thickness(0, 2, 0, 0)
        };

        tempCode.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        tempLabelStack.Children.Add(tempLabel);
        tempLabelStack.Children.Add(tempCode);

        _militaryTemperatureText = new TextBlock
        {
            Text = "-- °C",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 9, 0)
        };

        _militaryTemperatureText.SetResourceReference(
            TextBlock.FontFamilyProperty,
            "DisplayFontFamily");

        Grid.SetColumn(tempLabelStack, 0);
        Grid.SetColumn(_militaryTemperatureText, 1);

        tempGrid.Children.Add(tempLabelStack);
        tempGrid.Children.Add(_militaryTemperatureText);
        temperaturePlate.Child = tempGrid;
        instrument.Children.Add(temperaturePlate);

        Grid miniCompass = new()
        {
            Width = 42,
            Height = 42,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(151, 0, 0, 19)
        };

        Ellipse miniOuter = new()
        {
            StrokeThickness = 1.5
        };

        miniOuter.SetResourceReference(
            Shape.StrokeProperty,
            "MilitaryKhakiBrush");

        Border miniVertical = new()
        {
            Width = 1,
            Height = 28,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        miniVertical.SetResourceReference(
            BackgroundProperty,
            "MilitaryGridBrush");

        Border miniHorizontal = new()
        {
            Width = 28,
            Height = 1,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        miniHorizontal.SetResourceReference(
            BackgroundProperty,
            "MilitaryGridBrush");

        Ellipse miniCenter = new()
        {
            Width = 7,
            Height = 7
        };

        miniCenter.SetResourceReference(
            Shape.FillProperty,
            "AccentBrush");

        miniCompass.Children.Add(miniOuter);
        miniCompass.Children.Add(miniVertical);
        miniCompass.Children.Add(miniHorizontal);
        miniCompass.Children.Add(miniCenter);
        instrument.Children.Add(miniCompass);

        return instrument;
    }


    private FrameworkElement CreateMilitaryInfoPanel(
        TemperatureType temperatureType)
    {
        StackPanel panel = new()
        {
            Margin = new Thickness(9, 7, 0, 7),
            VerticalAlignment = VerticalAlignment.Stretch
        };

        Border modelPlate =
            CreateMilitaryPlate();

        StackPanel modelStack = new();

        TextBlock modelLabel = new()
        {
            Text = "ОБОРУДОВАНИЕ",
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        modelLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "MilitaryKhakiBrush");

        modelLabel.SetResourceReference(
            TextBlock.FontFamilyProperty,
            "DisplayFontFamily");

        _militaryHardwareNameText = new TextBlock
        {
            Text = "--",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 42,
            Margin = new Thickness(0, 4, 0, 0)
        };

        _militaryHardwareNameText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        _militaryHardwareNameText.SetResourceReference(
            TextBlock.FontFamilyProperty,
            "DisplayFontFamily");

        modelStack.Children.Add(modelLabel);
        modelStack.Children.Add(_militaryHardwareNameText);
        modelPlate.Child = modelStack;
        panel.Children.Add(modelPlate);

        Border statusPlate =
            CreateMilitaryPlate();

        statusPlate.Margin =
            new Thickness(0, 9, 0, 0);

        Grid statusGrid = new();

        statusGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        statusGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        Grid statusTarget = new()
        {
            Width = 24,
            Height = 24,
            Margin = new Thickness(0, 0, 9, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        Ellipse targetOuter = new()
        {
            StrokeThickness = 1.5
        };

        targetOuter.SetResourceReference(
            Shape.StrokeProperty,
            "MilitaryKhakiBrush");

        _militaryStatusDot = new Ellipse
        {
            Width = 8,
            Height = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _militaryStatusDot.SetResourceReference(
            Shape.FillProperty,
            "AccentBrush");

        statusTarget.Children.Add(targetOuter);
        statusTarget.Children.Add(_militaryStatusDot);

        StackPanel statusTextStack = new();

        TextBlock statusLabel = new()
        {
            Text = "СТАТУС ПОЛЕВОГО ДАТЧИКА",
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        statusLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "MilitaryKhakiBrush");

        statusLabel.SetResourceReference(
            TextBlock.FontFamilyProperty,
            "DisplayFontFamily");

        _militaryStatusText = new TextBlock
        {
            Text = "ДАТЧИК АКТИВЕН",
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 3, 0, 0)
        };

        _militaryStatusText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        _militaryStatusText.SetResourceReference(
            TextBlock.FontFamilyProperty,
            "DisplayFontFamily");

        statusTextStack.Children.Add(statusLabel);
        statusTextStack.Children.Add(_militaryStatusText);

        Grid.SetColumn(statusTarget, 0);
        Grid.SetColumn(statusTextStack, 1);

        statusGrid.Children.Add(statusTarget);
        statusGrid.Children.Add(statusTextStack);
        statusPlate.Child = statusGrid;
        panel.Children.Add(statusPlate);

        FrameworkElement details =
            temperatureType == TemperatureType.Gpu
                ? CreateMilitaryGpuDetails()
                : CreateMilitaryCpuDetails();

        panel.Children.Add(details);

        return panel;
    }


    private FrameworkElement CreateMilitaryGpuDetails()
    {
        Grid cards = new()
        {
            Margin = new Thickness(0, 10, 0, 0)
        };

        cards.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        cards.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(8)
            });

        cards.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        Border hotSpot =
            CreateMilitaryPlate();

        StackPanel hotSpotStack = new();

        TextBlock hotSpotLabel = new()
        {
            Text = "ГОРЯЧАЯ ТОЧКА",
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        hotSpotLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "MilitaryKhakiBrush");

        _militaryHotSpotValueText = new TextBlock
        {
            Text = "--",
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 3, 0, 0)
        };

        _militaryHotSpotValueText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        _militaryHotSpotValueText.SetResourceReference(
            TextBlock.FontFamilyProperty,
            "DisplayFontFamily");

        hotSpotStack.Children.Add(hotSpotLabel);
        hotSpotStack.Children.Add(_militaryHotSpotValueText);
        hotSpot.Child = hotSpotStack;

        Border vram =
            CreateMilitaryPlate();

        StackPanel vramStack = new();

        TextBlock vramLabel = new()
        {
            Text = "ТЕМПЕРАТУРА VRAM",
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        vramLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "MilitaryKhakiBrush");

        _militaryVramValueText = new TextBlock
        {
            Text = "--",
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 3, 0, 0)
        };

        _militaryVramValueText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        _militaryVramValueText.SetResourceReference(
            TextBlock.FontFamilyProperty,
            "DisplayFontFamily");

        vramStack.Children.Add(vramLabel);
        vramStack.Children.Add(_militaryVramValueText);
        vram.Child = vramStack;

        Grid.SetColumn(hotSpot, 0);
        Grid.SetColumn(vram, 2);

        cards.Children.Add(hotSpot);
        cards.Children.Add(vram);

        return cards;
    }


    private FrameworkElement CreateMilitaryCpuDetails()
    {
        _militaryHotSpotValueText = null;
        _militaryVramValueText = null;

        Border plate =
            CreateMilitaryPlate();

        plate.Margin =
            new Thickness(0, 10, 0, 0);

        Grid grid = new();

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        Grid target = new()
        {
            Width = 28,
            Height = 28,
            Margin = new Thickness(0, 0, 9, 0)
        };

        Ellipse targetOuter = new()
        {
            StrokeThickness = 1.5
        };

        targetOuter.SetResourceReference(
            Shape.StrokeProperty,
            "MilitaryKhakiBrush");

        Border targetV = new()
        {
            Width = 1,
            Height = 22,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        targetV.SetResourceReference(
            BackgroundProperty,
            "MilitaryGridBrush");

        Border targetH = new()
        {
            Width = 22,
            Height = 1,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        targetH.SetResourceReference(
            BackgroundProperty,
            "MilitaryGridBrush");

        target.Children.Add(targetOuter);
        target.Children.Add(targetV);
        target.Children.Add(targetH);

        StackPanel text = new();

        TextBlock top = new()
        {
            Text = "КАНАЛ ПОЛЕВОГО КОНТРОЛЯ",
            FontSize = 9,
            FontWeight = FontWeights.Bold
        };

        top.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        top.SetResourceReference(
            TextBlock.FontFamilyProperty,
            "DisplayFontFamily");

        TextBlock bottom = new()
        {
            Text = "Телеметрия в реальном времени",
            FontSize = 8,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 3, 0, 0)
        };

        bottom.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        text.Children.Add(top);
        text.Children.Add(bottom);

        Grid.SetColumn(target, 0);
        Grid.SetColumn(text, 1);

        Border threadsButton =
            CreateCpuThreadsButton(
                "MilitaryPanelBrush",
                "MilitaryKhakiBrush",
                "AccentBrush",
                new CornerRadius(1));

        Grid.SetColumn(
            threadsButton,
            2);

        grid.Children.Add(target);
        grid.Children.Add(text);
        grid.Children.Add(threadsButton);
        plate.Child = grid;

        return plate;
    }


    private static Border CreateMilitaryPlate()
    {
        Border plate = new()
        {
            Padding = new Thickness(10, 8, 10, 8),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(1)
        };

        plate.SetResourceReference(
            BackgroundProperty,
            "MilitaryPanelBrush");

        plate.SetResourceReference(
            BorderBrushProperty,
            "MilitaryGridBrush");

        return plate;
    }


    private static FrameworkElement CreateMilitaryFooter(
        TemperatureType temperatureType)
    {
        Border plate = new()
        {
            Padding = new Thickness(9, 4, 9, 4),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(1),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        plate.SetResourceReference(
            BackgroundProperty,
            "MilitaryPanelBrush");

        plate.SetResourceReference(
            BorderBrushProperty,
            "MilitaryGridBrush");

        Grid grid = new();

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        TextBlock left = new()
        {
            Text = temperatureType == TemperatureType.Cpu
                ? "ПОЛЕВОЙ МОДУЛЬ CPU"
                : "ПОЛЕВОЙ МОДУЛЬ GPU",
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        left.SetResourceReference(
            TextBlock.ForegroundProperty,
            "MilitaryKhakiBrush");

        left.SetResourceReference(
            TextBlock.FontFamilyProperty,
            "DisplayFontFamily");

        Border line = new()
        {
            Height = 2,
            Margin = new Thickness(10, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        line.SetResourceReference(
            BackgroundProperty,
            "MilitaryGridBrush");

        TextBlock right = new()
        {
            Text = "ТАКТИЧЕСКАЯ ТЕЛЕМЕТРИЯ АКТИВНА",
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        right.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        right.SetResourceReference(
            TextBlock.FontFamilyProperty,
            "DisplayFontFamily");

        Grid.SetColumn(left, 0);
        Grid.SetColumn(line, 1);
        Grid.SetColumn(right, 2);

        grid.Children.Add(left);
        grid.Children.Add(line);
        grid.Children.Add(right);
        plate.Child = grid;

        return plate;
    }


    private static void AddMilitaryCornerMark(
        Grid root,
        HorizontalAlignment horizontal,
        VerticalAlignment vertical)
    {
        Grid mark = new()
        {
            Width = 26,
            Height = 26,
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical,
            Margin = new Thickness(7),
            IsHitTestVisible = false
        };

        Border horizontalLine = new()
        {
            Width = 22,
            Height = 3,
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical
        };

        horizontalLine.SetResourceReference(
            BackgroundProperty,
            "MilitaryKhakiBrush");

        Border verticalLine = new()
        {
            Width = 3,
            Height = 22,
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical
        };

        verticalLine.SetResourceReference(
            BackgroundProperty,
            "MilitaryKhakiBrush");

        mark.Children.Add(horizontalLine);
        mark.Children.Add(verticalLine);
        root.Children.Add(mark);
    }


    // ============================================================
    // DATA UPDATE
    // ============================================================

    public void Update(
        float? temperature,
        float? load,
        string hardwareName,
        string details)
    {
        RefreshLanguageIfNeeded();
        double loadValue =
            load.HasValue
                ? Math.Clamp(
                    load.Value,
                    0,
                    100)
                : 0;

        string temperatureText =
            temperature.HasValue
                ? $"{temperature.Value:F0} °C"
                : "-- °C";

        string loadText =
            load.HasValue
                ? $"{load.Value:F0} %"
                : "-- %";

        Brush temperatureBrush =
            _temperatureType == TemperatureType.Cpu
                ? UiBrushes.CpuTemperature(
                    temperature)
                : UiBrushes.GpuTemperature(
                    temperature);

        Brush loadBrush =
            UiBrushes.Load(
                loadValue);

        _cyberTemperatureText.Text =
            temperatureText;

        _cyberTemperatureText.Foreground =
            temperatureBrush;

        _cyberLoadText.Text =
            loadText;

        _cyberLoadText.Foreground =
            loadBrush;

        _cyberHardwareNameText.Text =
            hardwareName;

        _steamTemperatureText.Text =
            temperatureText;

        _steamTemperatureText.Foreground =
            temperatureBrush;

        _steamLoadText.Text =
            load.HasValue
                ? $"{load.Value:F0} %"
                : "-- %";

        _steamLoadText.Foreground =
            loadBrush;

        _steamHardwareNameText.Text =
            hardwareName;

        _frostTemperatureText.Text =
            temperatureText;

        _frostTemperatureText.Foreground =
            temperatureBrush;

        _frostLoadText.Text =
            loadText;

        _frostLoadText.Foreground =
            loadBrush;

        _frostHardwareNameText.Text =
            hardwareName;

        _militaryTemperatureText.Text =
            temperatureText;

        _militaryTemperatureText.Foreground =
            temperatureBrush;

        _militaryLoadText.Text =
            loadText;

        _militaryLoadText.Foreground =
            loadBrush;

        _militaryHardwareNameText.Text =
            hardwareName;

        bool sensorAvailable =
            temperature.HasValue ||
            load.HasValue;

        if (sensorAvailable)
        {
            _cyberStatusText.Text =
                LocalizeText(
                    "ДАТЧИК АКТИВЕН");

            _cyberStatusText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "AccentBrush");

            _cyberStatusDot.SetResourceReference(
                Shape.FillProperty,
                "AccentBrush");

            _steamStatusText.Text =
                LocalizeText(
                    "ДАТЧИК АКТИВЕН");

            _steamStatusText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "AccentBrush");

            _steamStatusLamp.SetResourceReference(
                Shape.FillProperty,
                "AccentBrush");

            _frostStatusText.Text =
                LocalizeText(
                    "ДАТЧИК АКТИВЕН");

            _frostStatusText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "AccentBrush");

            _frostStatusDot.SetResourceReference(
                Shape.FillProperty,
                "FrostIceBrush");

            _militaryStatusText.Text =
                LocalizeText(
                    "ДАТЧИК АКТИВЕН");

            _militaryStatusText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "AccentBrush");

            _militaryStatusDot.SetResourceReference(
                Shape.FillProperty,
                "AccentBrush");
        }
        else
        {
            _cyberStatusText.Text =
                LocalizeText(
                    "НЕТ ДАННЫХ");

            _cyberStatusText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            _cyberStatusDot.Fill =
                Brushes.Gray;

            _steamStatusText.Text =
                LocalizeText(
                    "НЕТ ДАННЫХ");

            _steamStatusText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            _steamStatusLamp.Fill =
                Brushes.Gray;

            _frostStatusText.Text =
                LocalizeText(
                    "НЕТ ДАННЫХ");

            _frostStatusText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            _frostStatusDot.Fill =
                Brushes.Gray;

            _militaryStatusText.Text =
                LocalizeText(
                    "НЕТ ДАННЫХ");

            _militaryStatusText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            _militaryStatusDot.Fill =
                Brushes.Gray;
        }

        if (_temperatureType ==
            TemperatureType.Gpu)
        {
            UpdateGpuDetails(
                details);
        }

        SetProgress(
            loadValue);
    }

    private void UpdateGpuDetails(
        string details)
    {
        string hotSpot =
            ExtractDetailValue(
                details,
                "Hot Spot");

        string vram =
            ExtractDetailValue(
                details,
                "VRAM");

        if (_cyberHotSpotValueText != null)
        {
            _cyberHotSpotValueText.Text =
                hotSpot;
        }

        if (_cyberVramValueText != null)
        {
            _cyberVramValueText.Text =
                vram;
        }

        if (_steamHotSpotValueText != null)
        {
            _steamHotSpotValueText.Text =
                hotSpot;
        }

        if (_steamVramValueText != null)
        {
            _steamVramValueText.Text =
                vram;
        }

        if (_frostHotSpotValueText != null)
        {
            _frostHotSpotValueText.Text =
                hotSpot;
        }

        if (_frostVramValueText != null)
        {
            _frostVramValueText.Text =
                vram;
        }

        if (_militaryHotSpotValueText != null)
        {
            _militaryHotSpotValueText.Text =
                hotSpot;
        }

        if (_militaryVramValueText != null)
        {
            _militaryVramValueText.Text =
                vram;
        }
    }

    private static string ExtractDetailValue(
        string details,
        string label)
    {
        if (string.IsNullOrWhiteSpace(
                details))
        {
            return "--";
        }

        string[] parts =
            details.Split(
                '•',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        foreach (string part in parts)
        {
            if (!part.StartsWith(
                    label,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string value =
                part[label.Length..]
                    .Trim();

            return string.IsNullOrWhiteSpace(
                    value)
                ? "--"
                : value;
        }

        return "--";
    }

    private void SetProgress(
        double percent)
    {
        percent =
            Math.Clamp(
                percent,
                0,
                100);

        Brush loadBrush =
            UiBrushes.Load(
                percent);

        _cyberProgressArc.Stroke =
            loadBrush;

        _steamNeedle.Background =
            loadBrush;

        _steamNeedleTransform.Angle =
            -120 +
            (240.0 * percent / 100.0);

        _frostLoadFill.Height =
            118.0 * percent / 100.0;

        _frostLoadFill.Background =
            loadBrush;

        int activeMilitarySegments =
            percent <= 0
                ? 0
                : Math.Clamp(
                    (int)Math.Ceiling(
                        percent / 10.0),
                    0,
                    10);

        for (int i = 0;
             i < _militaryLoadSegments.Length;
             i++)
        {
            Border segment =
                _militaryLoadSegments[i];

            if (segment == null)
                continue;

            if (i < activeMilitarySegments)
            {
                segment.Background =
                    loadBrush;
            }
            else
            {
                segment.SetResourceReference(
                    BackgroundProperty,
                    "MilitaryGridBrush");
            }
        }

        if (percent <= 0)
        {
            _cyberProgressArc.Data =
                Geometry.Empty;

            return;
        }

        double safePercent =
            percent >= 100
                ? 99.999
                : percent;

        double startAngle =
            -90;

        double endAngle =
            startAngle +
            360.0 * safePercent /
            100.0;

        Point startPoint =
            GetCyberPointOnCircle(
                startAngle);

        Point endPoint =
            GetCyberPointOnCircle(
                endAngle);

        PathFigure figure = new()
        {
            StartPoint = startPoint,
            IsClosed = false
        };

        figure.Segments.Add(
            new ArcSegment
            {
                Point = endPoint,
                Size = new Size(
                    CyberRadius,
                    CyberRadius),
                SweepDirection =
                    SweepDirection.Clockwise,
                IsLargeArc =
                    safePercent > 50
            });

        PathGeometry geometry =
            new();

        geometry.Figures.Add(
            figure);

        _cyberProgressArc.Data =
            geometry;
    }

    private static Point GetCyberPointOnCircle(
        double angleDegrees)
    {
        double angleRadians =
            angleDegrees *
            Math.PI /
            180.0;

        return new Point(
            CyberCenter +
            CyberRadius *
            Math.Cos(
                angleRadians),

            CyberCenter +
            CyberRadius *
            Math.Sin(
                angleRadians));
    }

    private void RefreshLanguageIfNeeded(
        bool force = false)
    {
        string languageCode =
            SettingsService.IsRussian
                ? "ru"
                : "en";

        if (!force &&
            string.Equals(
                _appliedLanguageCode,
                languageCode,
                StringComparison.Ordinal))
        {
            return;
        }

        ApplyLanguageToVisualTree(
            this);

        _appliedLanguageCode =
            languageCode;
    }


    private static void ApplyLanguageToVisualTree(
        DependencyObject root)
    {
        if (root is TextBlock textBlock &&
            !string.IsNullOrEmpty(
                textBlock.Text))
        {
            textBlock.Text =
                LocalizeExistingText(
                    textBlock.Text);
        }

        int childCount =
            VisualTreeHelper.GetChildrenCount(
                root);

        for (int i = 0;
             i < childCount;
             i++)
        {
            ApplyLanguageToVisualTree(
                VisualTreeHelper.GetChild(
                    root,
                    i));
        }
    }


    private static string LocalizeExistingText(
        string text)
    {
        if (SettingsService.IsRussian)
        {
            foreach (KeyValuePair<string, string> pair
                     in RussianToEnglishText)
            {
                if (string.Equals(
                        pair.Value,
                        text,
                        StringComparison.Ordinal))
                {
                    return pair.Key;
                }
            }

            return text;
        }

        return RussianToEnglishText.TryGetValue(
                text,
                out string? english)
            ? english
            : text;
    }


    private static string LocalizeText(
        string russian)
    {
        if (SettingsService.IsRussian)
            return russian;

        return RussianToEnglishText.TryGetValue(
                russian,
                out string? english)
            ? english
            : russian;
    }


    private Border CreateCpuThreadsButton(
        string backgroundResource,
        string borderResource,
        string foregroundResource,
        CornerRadius cornerRadius)
    {
        Border button = new()
        {
            Padding =
                new Thickness(
                    10,
                    6,
                    10,
                    6),
            Margin =
                new Thickness(
                    10,
                    0,
                    0,
                    0),
            BorderThickness =
                new Thickness(1),
            CornerRadius =
                cornerRadius,
            VerticalAlignment =
                VerticalAlignment.Center,
            Cursor =
                System.Windows.Input.Cursors.Hand
        };

        button.SetResourceReference(
            Border.BackgroundProperty,
            backgroundResource);

        button.SetResourceReference(
            Border.BorderBrushProperty,
            borderResource);

        TextBlock text = new()
        {
            Text = "ПОТОКИ",
            FontSize = 9,
            FontWeight =
                FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        text.SetResourceReference(
            TextBlock.ForegroundProperty,
            foregroundResource);

        button.MouseEnter +=
            (_, _) =>
            {
                button.SetResourceReference(
                    Border.BackgroundProperty,
                    "AccentBrush");

                button.SetResourceReference(
                    Border.BorderBrushProperty,
                    "AccentBrush");

                text.SetResourceReference(
                    TextBlock.ForegroundProperty,
                    "WindowBackgroundBrush");
            };

        button.MouseLeave +=
            (_, _) =>
            {
                button.SetResourceReference(
                    Border.BackgroundProperty,
                    backgroundResource);

                button.SetResourceReference(
                    Border.BorderBrushProperty,
                    borderResource);

                text.SetResourceReference(
                    TextBlock.ForegroundProperty,
                    foregroundResource);
            };

        button.Child =
            text;

        button.MouseLeftButtonDown +=
            (_, e) =>
            {
                e.Handled =
                    true;

                CpuThreadsRequested?.Invoke(
                    this,
                    EventArgs.Empty);
            };

        return button;
    }


    private static TextBlock CreateTechLabel(
        string text)
    {
        TextBlock block = new()
        {
            Text = text,
            FontSize = 9,
            FontWeight = FontWeights.SemiBold
        };

        block.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        return block;
    }

    private static TextBlock CreateAccentLabel(
        string text)
    {
        TextBlock block = new()
        {
            Text = text,
            FontSize = 9,
            FontWeight = FontWeights.Bold
        };

        block.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        return block;
    }
}
