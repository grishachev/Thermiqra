using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCHardwareMonitor;

public partial class MainWindow : Window
{


    private Border CreateSteamStorageCard(
        StorageDeviceInfo storage,
        int column,
        int columns)
    {
        Brush statusBrush =
            UiBrushes.StorageTemperature(
                storage.Temperature);

        double rawTemperature =
            storage.Temperature ?? 0;

        double normalized =
            Math.Clamp(
                (rawTemperature - 20.0) /
                50.0,
                0,
                1);

        Border outer =
            CreateSteamGridCardBorder(
                column,
                columns);

        Grid root = new()
        {
            Height = 112
        };

        // Центральная часть выглядит как секция котловой магистрали.
        Border body =
            new()
            {
                Margin =
                    new Thickness(
                        34, 8, 38, 8),

                Padding =
                    new Thickness(
                        53, 11, 67, 11),

                BorderThickness =
                    new Thickness(2),

                CornerRadius =
                    new CornerRadius(22)
            };

        body.SetResourceReference(
            Border.BackgroundProperty,
            "SteamPanelBrush");

        body.SetResourceReference(
            Border.BorderBrushProperty,
            "SteamMetalBrush");

        StackPanel information =
            new()
            {
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        TextBlock caption =
            new()
            {
                Text =
                    SettingsService.L(
                    "ТЕМПЕРАТУРА НАКОПИТЕЛЯ",
                    "STORAGE TEMPERATURE"),

                FontSize = 8,

                FontWeight =
                    FontWeights.Bold
            };

        caption.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        TextBlock name =
            new()
            {
                Text =
                    storage.Name,

                FontSize = 12,

                FontWeight =
                    FontWeights.SemiBold,

                TextWrapping =
                    TextWrapping.Wrap,

                TextTrimming =
                    TextTrimming.CharacterEllipsis,

                Margin =
                    new Thickness(
                        0, 3, 0, 7)
            };

        name.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        StackPanel statusRow =
            new()
            {
                Orientation =
                    Orientation.Horizontal
            };

        System.Windows.Shapes.Ellipse lamp =
            new()
            {
                Width = 6,
                Height = 6,

                Fill =
                    storage.Temperature.HasValue
                        ? statusBrush
                        : Brushes.Gray,

                Margin =
                    new Thickness(
                        0, 0, 6, 0),

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        TextBlock status =
            new()
            {
                Text =
                    storage.Temperature.HasValue
                        ? SettingsService.L(
                        "Датчик активен",
                        "Sensor active")
                        : SettingsService.L(
                    "Нет данных",
                    "No data"),

                FontSize = 9,

                FontWeight =
                    FontWeights.SemiBold
            };

        status.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        statusRow.Children.Add(
            lamp);

        statusRow.Children.Add(
            status);

        information.Children.Add(
            caption);

        information.Children.Add(
            name);

        information.Children.Add(
            statusRow);

        body.Child =
            information;

        root.Children.Add(
            body);

        // Слева — настоящий вертикальный термометр с колбой.
        Grid thermometerHousing =
            new()
            {
                Width = 72,
                Height = 88,
                HorizontalAlignment =
                    HorizontalAlignment.Left,
                VerticalAlignment =
                    VerticalAlignment.Center,
                Margin =
                    new Thickness(
                        3, 0, 0, 0)
            };

        System.Windows.Shapes.Ellipse plate =
            new()
            {
                StrokeThickness = 4
            };

        plate.SetResourceReference(
            System.Windows.Shapes.Shape.FillProperty,
            "SteamGlassBrush");

        plate.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "SteamMetalBrush");

        Grid thermometer =
            new()
            {
                Width = 28,
                Height = 63,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        Border tube =
            new()
            {
                Width = 11,
                Height = 43,
                CornerRadius =
                    new CornerRadius(6),
                BorderBrush =
                    statusBrush,
                BorderThickness =
                    new Thickness(2),
                VerticalAlignment =
                    VerticalAlignment.Top,
                HorizontalAlignment =
                    HorizontalAlignment.Center
            };

        Border fill =
            new()
            {
                Width = 5,
                Height =
                    7 +
                    (28 * normalized),
                CornerRadius =
                    new CornerRadius(3),
                Background =
                    statusBrush,
                VerticalAlignment =
                    VerticalAlignment.Bottom,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                Margin =
                    new Thickness(
                        0, 0, 0, 16)
            };

        System.Windows.Shapes.Ellipse bulb =
            new()
            {
                Width = 18,
                Height = 18,
                Fill =
                    statusBrush,
                Stroke =
                    statusBrush,
                StrokeThickness = 1,
                VerticalAlignment =
                    VerticalAlignment.Bottom,
                HorizontalAlignment =
                    HorizontalAlignment.Center
            };

        thermometer.Children.Add(
            tube);

        thermometer.Children.Add(
            fill);

        thermometer.Children.Add(
            bulb);

        thermometerHousing.Children.Add(
            plate);

        thermometerHousing.Children.Add(
            thermometer);

        root.Children.Add(
            thermometerHousing);

        // Справа — встроенный круглый цифровой термометр.
        Grid meter =
            new()
            {
                Width = 78,
                Height = 78,
                HorizontalAlignment =
                    HorizontalAlignment.Right,
                VerticalAlignment =
                    VerticalAlignment.Center,
                Margin =
                    new Thickness(
                        0, 0, 5, 0)
            };

        System.Windows.Shapes.Ellipse meterOuter =
            new()
            {
                StrokeThickness = 4
            };

        meterOuter.SetResourceReference(
            System.Windows.Shapes.Shape.FillProperty,
            "SteamGlassBrush");

        meterOuter.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "SteamMetalBrush");

        System.Windows.Shapes.Ellipse meterInner =
            new()
            {
                Width = 60,
                Height = 60,
                StrokeThickness = 1
            };

        meterInner.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "BorderBrush");

        StackPanel temperatureStack =
            new()
            {
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        TextBlock tempLabel =
            new()
            {
                Text = SettingsService.L(
                "ТЕМП.",
                "TEMP."),
                FontSize = 7,
                FontWeight =
                    FontWeights.Bold,
                HorizontalAlignment =
                    HorizontalAlignment.Center
            };

        tempLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        TextBlock temperature =
            new()
            {
                Text =
                    FormatTemperature(
                        storage.Temperature),

                Foreground =
                    statusBrush,

                FontSize = 16,
                FontWeight =
                    FontWeights.Bold,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                Margin =
                    new Thickness(
                        0, 1, 0, 0)
            };

        temperatureStack.Children.Add(
            tempLabel);

        temperatureStack.Children.Add(
            temperature);

        meter.Children.Add(
            meterOuter);

        meter.Children.Add(
            meterInner);

        meter.Children.Add(
            temperatureStack);

        root.Children.Add(
            meter);

        outer.Child =
            root;

        return outer;
    }


    private Border CreateFrostStorageCard(
        StorageDeviceInfo storage,
        int column,
        int columns)
    {
        Brush statusBrush =
            UiBrushes.StorageTemperature(
                storage.Temperature);

        double rawTemperature =
            storage.Temperature ?? 0;

        double normalized =
            Math.Clamp(
                (rawTemperature - 20.0) /
                50.0,
                0,
                1);

        Border outer =
            CreateFrostGridCardBorder(
                column,
                columns);

        Border body = new()
        {
            Height = 112,
            Padding = new Thickness(12),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(18)
        };

        body.SetResourceReference(
            Border.BackgroundProperty,
            "FrostPanelBrush");

        body.SetResourceReference(
            Border.BorderBrushProperty,
            "FrostSteelBrush");

        Grid layout = new();

        layout.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(84)
            });

        layout.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        layout.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(98)
            });

        Grid cryoProbe = new()
        {
            Width = 62,
            Height = 82,
            HorizontalAlignment =
                HorizontalAlignment.Left,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        Border probeOuter = new()
        {
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(21)
        };

        probeOuter.SetResourceReference(
            Border.BackgroundProperty,
            "FrostGlassBrush");

        probeOuter.SetResourceReference(
            Border.BorderBrushProperty,
            "FrostSteelBrush");

        Border probeInner = new()
        {
            Width = 28,
            Height = 58,
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(1),
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        probeInner.SetResourceReference(
            Border.BorderBrushProperty,
            "FrostIceBrush");

        Border fill = new()
        {
            Width = 20,
            Height = 6 + 44 * normalized,
            CornerRadius = new CornerRadius(10),
            Background = statusBrush,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 13),
            Opacity = 0.9
        };

        TextBlock snow = new()
        {
            Text = "❄",
            FontSize = 13,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Top,
            Margin = new Thickness(0, 8, 0, 0),
            Opacity = 0.65
        };

        snow.SetResourceReference(
            TextBlock.ForegroundProperty,
            "FrostIceBrush");

        cryoProbe.Children.Add(probeOuter);
        cryoProbe.Children.Add(probeInner);
        cryoProbe.Children.Add(fill);
        cryoProbe.Children.Add(snow);

        StackPanel information = new()
        {
            VerticalAlignment =
                VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0)
        };

        TextBlock caption = new()
        {
            Text = SettingsService.L(
                "КРИОТЕРМОДАТЧИК НАКОПИТЕЛЯ",
                "STORAGE CRYO SENSOR"),
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        caption.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        TextBlock name = new()
        {
            Text = storage.Name,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming =
                TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 3, 0, 7)
        };

        name.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        StackPanel statusRow = new()
        {
            Orientation = Orientation.Horizontal
        };

        System.Windows.Shapes.Ellipse lamp =
            new()
            {
                Width = 6,
                Height = 6,
                Fill = storage.Temperature.HasValue
                    ? statusBrush
                    : Brushes.Gray,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        TextBlock status = new()
        {
            Text = storage.Temperature.HasValue
                ? SettingsService.L(
                    "Контур датчика активен",
                    "Sensor circuit active")
                : SettingsService.L(
                    "Нет данных",
                    "No data"),
            FontSize = 9,
            FontWeight = FontWeights.SemiBold
        };

        status.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        statusRow.Children.Add(lamp);
        statusRow.Children.Add(status);

        information.Children.Add(caption);
        information.Children.Add(name);
        information.Children.Add(statusRow);

        Border meter = new()
        {
            Padding = new Thickness(9),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(15),
            VerticalAlignment =
                VerticalAlignment.Center
        };

        meter.SetResourceReference(
            Border.BackgroundProperty,
            "FrostGlassBrush");

        meter.SetResourceReference(
            Border.BorderBrushProperty,
            "FrostSteelBrush");

        StackPanel temperatureStack = new();

        TextBlock tempLabel = new()
        {
            Text = SettingsService.L(
                "ТЕМП.",
                "TEMP."),
            FontSize = 7,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center
        };

        tempLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        TextBlock temperature = new()
        {
            Text = FormatTemperature(
                storage.Temperature),
            Foreground = statusBrush,
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 2)
        };

        TextBlock state = new()
        {
            Text = SettingsService.L(
                "КРИОКОНТРОЛЬ",
                "CRYO CONTROL"),
            FontSize = 7,
            HorizontalAlignment =
                HorizontalAlignment.Center
        };

        state.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        temperatureStack.Children.Add(tempLabel);
        temperatureStack.Children.Add(temperature);
        temperatureStack.Children.Add(state);
        meter.Child = temperatureStack;

        Grid.SetColumn(cryoProbe, 0);
        Grid.SetColumn(information, 1);
        Grid.SetColumn(meter, 2);

        layout.Children.Add(cryoProbe);
        layout.Children.Add(information);
        layout.Children.Add(meter);
        body.Child = layout;
        outer.Child = body;

        return outer;
    }


    private Border CreateMilitaryStorageCard(
        StorageDeviceInfo storage,
        int column,
        int columns)
    {
        Brush statusBrush =
            UiBrushes.StorageTemperature(
                storage.Temperature);

        double rawTemperature =
            storage.Temperature ?? 0;

        double normalized =
            Math.Clamp(
                (rawTemperature - 20.0) /
                50.0,
                0,
                1);

        Border outer =
            CreateMilitaryGridCardBorder(
                column,
                columns);

        Grid root = new()
        {
            Height = 104
        };

        Border body = new()
        {
            Padding = new Thickness(12, 10, 12, 10),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(1)
        };

        body.SetResourceReference(
            Border.BackgroundProperty,
            "MilitaryPanelBrush");

        body.SetResourceReference(
            Border.BorderBrushProperty,
            "MilitaryKhakiBrush");

        Grid layout = new();

        layout.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(72)
            });

        layout.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        layout.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(102)
            });

        // Прицельная сетка датчика.
        Grid sensor = new()
        {
            Width = 56,
            Height = 56,
            HorizontalAlignment =
                HorizontalAlignment.Left,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        System.Windows.Shapes.Ellipse sensorOuter =
            new()
            {
                StrokeThickness = 2
            };

        sensorOuter.SetResourceReference(
            System.Windows.Shapes.Shape.FillProperty,
            "MilitaryPlateBrush");

        sensorOuter.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "MilitaryKhakiBrush");

        System.Windows.Shapes.Ellipse sensorInner =
            new()
            {
                Width = 34,
                Height = 34,
                StrokeThickness = 1
            };

        sensorInner.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "MilitaryGridBrush");

        Border sensorVertical = new()
        {
            Width = 1,
            Height = 46,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        sensorVertical.SetResourceReference(
            Border.BackgroundProperty,
            "MilitaryGridBrush");

        Border sensorHorizontal = new()
        {
            Width = 46,
            Height = 1,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        sensorHorizontal.SetResourceReference(
            Border.BackgroundProperty,
            "MilitaryGridBrush");

        System.Windows.Shapes.Ellipse sensorCenter =
            new()
            {
                Width = 9,
                Height = 9,
                Fill = statusBrush,
                StrokeThickness = 1
            };

        sensorCenter.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "MilitaryKhakiBrush");

        sensor.Children.Add(sensorOuter);
        sensor.Children.Add(sensorInner);
        sensor.Children.Add(sensorVertical);
        sensor.Children.Add(sensorHorizontal);
        sensor.Children.Add(sensorCenter);

        Grid information = new()
        {
            Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment =
                VerticalAlignment.Center
        };

        information.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });
        information.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });
        information.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        TextBlock caption = new()
        {
            Text = SettingsService.L(
                "ПОЛЕВОЙ ТЕРМОДАТЧИК",
                "FIELD THERMAL SENSOR"),
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        caption.SetResourceReference(
            TextBlock.ForegroundProperty,
            "MilitaryKhakiBrush");

        TextBlock name = new()
        {
            Text = storage.Name,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming =
                TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 3, 0, 7)
        };

        name.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        Grid threatBar = new()
        {
            Height = 13
        };

        int activeSegments =
            normalized <= 0
                ? 0
                : Math.Clamp(
                    (int)Math.Ceiling(
                        normalized * 5),
                    0,
                    5);

        for (int i = 0;
             i < 5;
             i++)
        {
            threatBar.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = new GridLength(
                        1,
                        GridUnitType.Star)
                });

            Border segment = new()
            {
                Margin = new Thickness(
                    0,
                    0,
                    i == 4 ? 0 : 3,
                    0),
                BorderThickness = new Thickness(1),
                Background =
                    i < activeSegments
                        ? statusBrush
                        : UiBrushes.Theme(
                            "MilitaryGridBrush")
            };

            segment.SetResourceReference(
                Border.BorderBrushProperty,
                "MilitaryKhakiBrush");

            Grid.SetColumn(
                segment,
                i);

            threatBar.Children.Add(
                segment);
        }

        Grid.SetRow(caption, 0);
        Grid.SetRow(name, 1);
        Grid.SetRow(threatBar, 2);

        information.Children.Add(caption);
        information.Children.Add(name);
        information.Children.Add(threatBar);

        Border meter = new()
        {
            Padding = new Thickness(9),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(1),
            VerticalAlignment =
                VerticalAlignment.Center
        };

        meter.SetResourceReference(
            Border.BackgroundProperty,
            "MilitaryPlateBrush");

        meter.SetResourceReference(
            Border.BorderBrushProperty,
            "MilitaryKhakiBrush");

        StackPanel meterStack = new();

        TextBlock meterLabel = new()
        {
            Text = storage.Temperature.HasValue
                ? SettingsService.L(
                    "ТЕМПЕРАТУРА",
                    "TEMPERATURE")
                : SettingsService.L(
                    "НЕТ ДАННЫХ",
                    "NO DATA"),
            FontSize = 7,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center
        };

        meterLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "MilitaryKhakiBrush");

        TextBlock temperature = new()
        {
            Text = FormatTemperature(
                storage.Temperature),
            Foreground = statusBrush,
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 2)
        };

        TextBlock sector = new()
        {
            Text = SettingsService.L(
                "СЕКТОР THM",
                "THM SECTOR"),
            FontSize = 7,
            HorizontalAlignment =
                HorizontalAlignment.Center
        };

        sector.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        meterStack.Children.Add(meterLabel);
        meterStack.Children.Add(temperature);
        meterStack.Children.Add(sector);
        meter.Child = meterStack;

        Grid.SetColumn(sensor, 0);
        Grid.SetColumn(information, 1);
        Grid.SetColumn(meter, 2);

        layout.Children.Add(sensor);
        layout.Children.Add(information);
        layout.Children.Add(meter);
        body.Child = layout;
        root.Children.Add(body);
        outer.Child = root;

        return outer;
    }
}
