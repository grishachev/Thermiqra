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

        // Keep the percentage above the fill: it stays readable without
        // covering the ice chamber or adding an opaque readout panel.
        Grid loadHeader = new()
        {
            Width = 172,
            Height = 30,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 15, 0, 0),
            IsHitTestVisible = false
        };

        loadHeader.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });
        loadHeader.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = GridLength.Auto
        });

        TextBlock loadCaption = new()
        {
            Text = "УРОВЕНЬ НАГРУЗКИ",
            FontSize = 8,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };

        loadCaption.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        _frostLoadText = new TextBlock
        {
            Text = "0 %",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        _frostLoadText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "FrostIceBrush");

        Grid.SetColumn(loadCaption, 0);
        Grid.SetColumn(_frostLoadText, 1);
        loadHeader.Children.Add(loadCaption);
        loadHeader.Children.Add(_frostLoadText);
        chamber.Children.Add(loadHeader);

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



}
