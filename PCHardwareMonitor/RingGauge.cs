using System;
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
    private const double GaugeSize = 226;
    private const double Radius = 80;
    private const double Center = 113;

    private Path _progressArc = null!;
    private TextBlock _temperatureText = null!;
    private TextBlock _loadText = null!;
    private TextBlock _hardwareNameText = null!;
    private TextBlock _statusText = null!;
    private Ellipse _statusDot = null!;
    private TextBlock? _hotSpotValueText;
    private TextBlock? _vramValueText;

    private readonly TemperatureType _temperatureType;

    public RingGauge(string title, TemperatureType temperatureType)
    {
        _temperatureType = temperatureType;

        Width = 530;
        MinHeight = 338;
        Margin = new Thickness(7, 0, 7, 14);
        Padding = new Thickness(0);
        BorderThickness = new Thickness(0);
        Background = Brushes.Transparent;

        Grid root = new();

        Path frame = new()
        {
            Data = Geometry.Parse(
                "M 10,0 L 100,0 L 100,88 L 94,100 L 0,100 L 0,10 Z"),
            Stretch = Stretch.Fill,
            StrokeThickness = 1
        };
        frame.SetResourceReference(Shape.FillProperty, "CardBackgroundBrush");
        frame.SetResourceReference(Shape.StrokeProperty, "BorderBrush");
        root.Children.Add(frame);

        Border topAccent = new()
        {
            Height = 2,
            Width = 52,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(52, 0, 0, 0)
        };
        topAccent.SetResourceReference(BackgroundProperty, "AccentBrush");
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
        bottomAccent.SetResourceReference(BackgroundProperty, "AccentBrush");
        root.Children.Add(bottomAccent);

        Grid layout = new()
        {
            Margin = new Thickness(18, 14, 18, 13)
        };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition
        {
            Height = new GridLength(1, GridUnitType.Star)
        });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        BuildHeader(layout, title, temperatureType);

        Grid content = new()
        {
            Margin = new Thickness(0, 5, 0, 5)
        };
        content.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(236)
        });
        content.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });

        Grid gaugeGrid = CreateGauge();
        Grid.SetColumn(gaugeGrid, 0);
        content.Children.Add(gaugeGrid);

        Grid infoPanel = CreateInfoPanel(temperatureType);
        Grid.SetColumn(infoPanel, 1);
        content.Children.Add(infoPanel);

        Grid.SetRow(content, 1);
        layout.Children.Add(content);

        Grid footer = CreateFooter(temperatureType);
        Grid.SetRow(footer, 2);
        layout.Children.Add(footer);

        root.Children.Add(layout);
        Child = root;

        SetProgress(0);
    }

    private void BuildHeader(
        Grid layout,
        string title,
        TemperatureType temperatureType)
    {
        Grid header = new();
        header.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = GridLength.Auto
        });
        header.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });
        header.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = GridLength.Auto
        });

        TextBlock number = new()
        {
            Text = temperatureType == TemperatureType.Cpu ? "01" : "02",
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
        badgeDot.SetResourceReference(Shape.FillProperty, "AccentBrush");

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

    private Grid CreateGauge()
    {
        Grid gaugeGrid = new()
        {
            Width = GaugeSize,
            Height = GaugeSize,
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
        outerRing.SetResourceReference(Shape.StrokeProperty, "BorderBrush");
        gaugeGrid.Children.Add(outerRing);

        Ellipse dashedRing = new()
        {
            Width = 196,
            Height = 196,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 1.4, 4.6 },
            Opacity = 0.72
        };
        dashedRing.SetResourceReference(Shape.StrokeProperty, "AccentBrush");
        gaugeGrid.Children.Add(dashedRing);

        for (int i = 0; i < 36; i++)
        {
            bool major = i % 3 == 0;

            Grid tickLayer = new()
            {
                Width = GaugeSize,
                Height = GaugeSize,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(i * 10)
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
                major ? "AccentBrush" : "BorderBrush");

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

        _progressArc = new Path
        {
            Stroke = UiBrushes.Load(0),
            StrokeThickness = 13,
            StrokeStartLineCap = PenLineCap.Flat,
            StrokeEndLineCap = PenLineCap.Flat
        };
        gaugeGrid.Children.Add(_progressArc);

        Ellipse innerRing = new()
        {
            Width = 134,
            Height = 134,
            StrokeThickness = 1,
            Opacity = 0.7
        };
        innerRing.SetResourceReference(Shape.StrokeProperty, "BorderBrush");
        gaugeGrid.Children.Add(innerRing);

        Ellipse marker = new()
        {
            Width = 10,
            Height = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 30, 0, 0)
        };
        marker.SetResourceReference(Shape.FillProperty, "AccentBrush");
        gaugeGrid.Children.Add(marker);

        StackPanel center = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _temperatureText = new TextBlock
        {
            Text = "-- °C",
            FontSize = 35,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        TextBlock tempLabel = CreateAccentLabel("CORE TEMP");
        tempLabel.HorizontalAlignment = HorizontalAlignment.Center;
        tempLabel.Margin = new Thickness(0, 0, 0, 8);

        _loadText = new TextBlock
        {
            Text = "0 %",
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        TextBlock loadLabel = CreateTechLabel("LOAD");
        loadLabel.HorizontalAlignment = HorizontalAlignment.Center;

        center.Children.Add(_temperatureText);
        center.Children.Add(tempLabel);
        center.Children.Add(_loadText);
        center.Children.Add(loadLabel);

        gaugeGrid.Children.Add(center);

        return gaugeGrid;
    }

    private Grid CreateInfoPanel(TemperatureType temperatureType)
    {
        Grid infoPanel = new()
        {
            Margin = new Thickness(8, 16, 0, 12)
        };

        infoPanel.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });
        infoPanel.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });
        infoPanel.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });
        infoPanel.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });
        infoPanel.RowDefinitions.Add(new RowDefinition
        {
            Height = new GridLength(1, GridUnitType.Star)
        });

        Border sideAccent = new()
        {
            Width = 2,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        sideAccent.SetResourceReference(BackgroundProperty, "AccentBrush");
        Grid.SetRowSpan(sideAccent, 5);
        infoPanel.Children.Add(sideAccent);

        TextBlock modelLabel = CreateTechLabel("МОДЕЛЬ");
        modelLabel.Margin = new Thickness(14, 0, 0, 4);

        _hardwareNameText = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 42,
            Margin = new Thickness(14, 0, 0, 10)
        };
        _hardwareNameText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        Border separator = new()
        {
            Height = 1,
            Margin = new Thickness(14, 0, 0, 11)
        };
        separator.SetResourceReference(BackgroundProperty, "BorderBrush");

        StackPanel statusPanel = new()
        {
            Margin = new Thickness(14, 0, 0, 0)
        };

        TextBlock statusLabel = CreateTechLabel("СТАТУС");

        StackPanel statusRow = new()
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 5, 0, 0)
        };

        _statusDot = new Ellipse
        {
            Width = 7,
            Height = 7,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _statusDot.SetResourceReference(Shape.FillProperty, "AccentBrush");

        _statusText = new TextBlock
        {
            Text = "ДАТЧИК АКТИВЕН",
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        _statusText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        statusRow.Children.Add(_statusDot);
        statusRow.Children.Add(_statusText);

        statusPanel.Children.Add(statusLabel);
        statusPanel.Children.Add(statusRow);

        Grid.SetRow(modelLabel, 0);
        Grid.SetRow(_hardwareNameText, 1);
        Grid.SetRow(separator, 2);
        Grid.SetRow(statusPanel, 3);

        infoPanel.Children.Add(modelLabel);
        infoPanel.Children.Add(_hardwareNameText);
        infoPanel.Children.Add(separator);
        infoPanel.Children.Add(statusPanel);

        if (temperatureType == TemperatureType.Gpu)
        {
            Grid details = CreateGpuDetails();
            Grid.SetRow(details, 4);
            infoPanel.Children.Add(details);
        }
        else
        {
            _hotSpotValueText = null;
            _vramValueText = null;

            Grid details = CreateCpuDetails();
            Grid.SetRow(details, 4);
            infoPanel.Children.Add(details);
        }

        return infoPanel;
    }

    private Grid CreateGpuDetails()
    {
        Grid wrapper = new()
        {
            Margin = new Thickness(14, 15, 0, 0)
        };
        wrapper.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });
        wrapper.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });

        TextBlock sensorLabel = CreateTechLabel("THERMAL CHANNELS");
        wrapper.Children.Add(sensorLabel);

        Grid cards = new()
        {
            Margin = new Thickness(0, 7, 0, 0)
        };
        cards.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });
        cards.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(8)
        });
        cards.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });

        Border hotSpotCard = CreateMiniCard();
        StackPanel hotSpotStack = new();

        hotSpotStack.Children.Add(CreateTechLabel("HOT SPOT"));

        _hotSpotValueText = new TextBlock
        {
            Text = "--",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 3, 0, 0)
        };
        _hotSpotValueText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");
        hotSpotStack.Children.Add(_hotSpotValueText);
        hotSpotCard.Child = hotSpotStack;

        Border vramCard = CreateMiniCard();
        StackPanel vramStack = new();

        vramStack.Children.Add(CreateTechLabel("VRAM"));

        _vramValueText = new TextBlock
        {
            Text = "--",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 3, 0, 0)
        };
        _vramValueText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");
        vramStack.Children.Add(_vramValueText);
        vramCard.Child = vramStack;

        Grid.SetColumn(hotSpotCard, 0);
        Grid.SetColumn(vramCard, 2);

        cards.Children.Add(hotSpotCard);
        cards.Children.Add(vramCard);

        Grid.SetRow(cards, 1);
        wrapper.Children.Add(cards);

        return wrapper;
    }

    private Grid CreateCpuDetails()
    {
        Grid wrapper = new()
        {
            Margin = new Thickness(14, 15, 0, 0)
        };
        wrapper.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });
        wrapper.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });

        wrapper.Children.Add(CreateTechLabel("MONITORING CHANNEL"));

        Border card = CreateMiniCard();
        card.Margin = new Thickness(0, 7, 0, 0);

        StackPanel stack = new();

        TextBlock realTime = CreateAccentLabel("REAL-TIME");
        realTime.FontSize = 11;

        TextBlock sensorBus = CreateTechLabel("SENSOR BUS ACTIVE");
        sensorBus.Margin = new Thickness(0, 4, 0, 0);

        stack.Children.Add(realTime);
        stack.Children.Add(sensorBus);
        card.Child = stack;

        Grid.SetRow(card, 1);
        wrapper.Children.Add(card);

        return wrapper;
    }

    private static Grid CreateFooter(TemperatureType temperatureType)
    {
        Grid footer = new();
        footer.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });
        footer.ColumnDefinitions.Add(new ColumnDefinition
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
        line.SetResourceReference(BackgroundProperty, "AccentBrush");

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

    private static Border CreateMiniCard()
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

    private static TextBlock CreateTechLabel(string text)
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

    private static TextBlock CreateAccentLabel(string text)
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

    public void Update(
        float? temperature,
        float? load,
        string hardwareName,
        string details)
    {
        double loadValue = load.HasValue
            ? Math.Clamp(load.Value, 0, 100)
            : 0;

        _temperatureText.Text = temperature.HasValue
            ? $"{temperature.Value:F0} °C"
            : "-- °C";

        _temperatureText.Foreground =
            _temperatureType == TemperatureType.Cpu
                ? UiBrushes.CpuTemperature(temperature)
                : UiBrushes.GpuTemperature(temperature);

        _loadText.Text = load.HasValue
            ? $"{load.Value:F0} %"
            : "-- %";

        _loadText.Foreground =
            UiBrushes.Load(loadValue);

        _hardwareNameText.Text =
            hardwareName;

        bool sensorAvailable =
            temperature.HasValue ||
            load.HasValue;

        if (sensorAvailable)
        {
            _statusText.Text = "ДАТЧИК АКТИВЕН";
            _statusText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "AccentBrush");
            _statusDot.SetResourceReference(
                Shape.FillProperty,
                "AccentBrush");
        }
        else
        {
            _statusText.Text = "НЕТ ДАННЫХ";
            _statusText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");
            _statusDot.Fill = Brushes.Gray;
        }

        if (_temperatureType == TemperatureType.Gpu)
            UpdateGpuDetails(details);

        SetProgress(loadValue);
    }

    private void UpdateGpuDetails(string details)
    {
        if (_hotSpotValueText == null ||
            _vramValueText == null)
        {
            return;
        }

        _hotSpotValueText.Text =
            ExtractDetailValue(details, "Hot Spot");

        _vramValueText.Text =
            ExtractDetailValue(details, "VRAM");
    }

    private static string ExtractDetailValue(
        string details,
        string label)
    {
        if (string.IsNullOrWhiteSpace(details))
            return "--";

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
                part[label.Length..].Trim();

            return string.IsNullOrWhiteSpace(value)
                ? "--"
                : value;
        }

        return "--";
    }

    private void SetProgress(double percent)
    {
        percent =
            Math.Clamp(percent, 0, 100);

        _progressArc.Stroke =
            UiBrushes.Load(percent);

        if (percent <= 0)
        {
            _progressArc.Data = Geometry.Empty;
            return;
        }

        double safePercent =
            percent >= 100
                ? 99.999
                : percent;

        double startAngle = -90;
        double endAngle =
            startAngle +
            360.0 * safePercent / 100.0;

        Point startPoint =
            GetPointOnCircle(startAngle);

        Point endPoint =
            GetPointOnCircle(endAngle);

        PathFigure figure = new()
        {
            StartPoint = startPoint,
            IsClosed = false
        };

        figure.Segments.Add(
            new ArcSegment
            {
                Point = endPoint,
                Size = new Size(Radius, Radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = safePercent > 50
            });

        PathGeometry geometry = new();
        geometry.Figures.Add(figure);
        _progressArc.Data = geometry;
    }

    private static Point GetPointOnCircle(double angleDegrees)
    {
        double angleRadians =
            angleDegrees * Math.PI / 180.0;

        return new Point(
            Center + Radius * Math.Cos(angleRadians),
            Center + Radius * Math.Sin(angleRadians));
    }
}
