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



}
