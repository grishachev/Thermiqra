using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PCHardwareMonitor;

public sealed partial class RingGauge
{
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

}
