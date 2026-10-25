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


}
