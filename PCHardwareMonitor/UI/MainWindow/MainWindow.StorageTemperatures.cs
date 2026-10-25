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



    // ============================================================
    // ТЕМПЕРАТУРА НАКОПИТЕЛЕЙ
    // ============================================================

    private void UpdateStorageTemperatures(
    HardwareSnapshot snapshot)
    {
        PrepareResponsiveGrid(
            StorageTemperaturePanel,
            snapshot.StorageDevices.Count,
            out int columns);

        for (int i = 0;
             i < snapshot.StorageDevices.Count;
             i++)
        {
            StorageDeviceInfo storage =
                snapshot.StorageDevices[i];

            int row =
                i / columns;

            int column =
                i % columns;

            string skinName =
                SettingsService
                    .Current
                    .SkinName;

            if (skinName == "SteamPunk")
            {
                Border steamCard =
                    CreateSteamStorageCard(
                        storage,
                        column,
                        columns);

                Grid.SetRow(
                    steamCard,
                    row);

                Grid.SetColumn(
                    steamCard,
                    column);

                StorageTemperaturePanel
                    .Children
                    .Add(
                        steamCard);

                continue;
            }

            if (skinName == "FrostCore")
            {
                Border frostCard =
                    CreateFrostStorageCard(
                        storage,
                        column,
                        columns);

                Grid.SetRow(
                    frostCard,
                    row);

                Grid.SetColumn(
                    frostCard,
                    column);

                StorageTemperaturePanel
                    .Children
                    .Add(
                        frostCard);

                continue;
            }

            if (skinName == "MilitaryOps")
            {
                Border militaryCard =
                    CreateMilitaryStorageCard(
                        storage,
                        column,
                        columns);

                Grid.SetRow(
                    militaryCard,
                    row);

                Grid.SetColumn(
                    militaryCard,
                    column);

                StorageTemperaturePanel
                    .Children
                    .Add(
                        militaryCard);

                continue;
            }

            Border border =
                CreateGridCardBorder(
                    column,
                    columns);


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


            Grid root =
                new();

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        new GridLength(3)
                });

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Auto
                });


            Border topAccent =
                new()
                {
                    Width = 72,
                    Height = 3,

                    HorizontalAlignment =
                        HorizontalAlignment.Left,

                    Margin =
                        new Thickness(
                            14, 0, 0, 0)
                };

            topAccent.SetResourceReference(
                Border.BackgroundProperty,
                "AccentBrush");


            Grid.SetRow(
                topAccent,
                0);

            root.Children.Add(
                topAccent);


            Grid body =
                new()
                {
                    Margin =
                        new Thickness(
                            15, 12, 15, 13)
                };

            body.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(44)
                });

            body.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(
                            1,
                            GridUnitType.Star)
                });

            body.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        GridLength.Auto
                });


            // ========================================================
            // ТЕРМОМЕТР
            // ========================================================

            Grid thermometer =
                new()
                {
                    Width = 27,
                    Height = 42,

                    HorizontalAlignment =
                        HorizontalAlignment.Left,

                    VerticalAlignment =
                        VerticalAlignment.Center
                };


            Border tubeOutline =
                new()
                {
                    Width = 9,
                    Height = 27,

                    CornerRadius =
                        new CornerRadius(5),

                    BorderBrush =
                        statusBrush,

                    BorderThickness =
                        new Thickness(1.5),

                    Background =
                        Brushes.Transparent,

                    HorizontalAlignment =
                        HorizontalAlignment.Center,

                    VerticalAlignment =
                        VerticalAlignment.Top,

                    Margin =
                        new Thickness(
                            0, 0, 0, 10)
                };


            double fillHeight =
                5 +
                (17 * normalized);


            Border tubeFill =
                new()
                {
                    Width = 4,
                    Height =
                        fillHeight,

                    CornerRadius =
                        new CornerRadius(2),

                    Background =
                        statusBrush,

                    HorizontalAlignment =
                        HorizontalAlignment.Center,

                    VerticalAlignment =
                        VerticalAlignment.Bottom,

                    Margin =
                        new Thickness(
                            0, 0, 0, 13)
                };


            System.Windows.Shapes.Ellipse bulb =
                new()
                {
                    Width = 15,
                    Height = 15,

                    Fill =
                        statusBrush,

                    Stroke =
                        statusBrush,

                    StrokeThickness = 1,

                    HorizontalAlignment =
                        HorizontalAlignment.Center,

                    VerticalAlignment =
                        VerticalAlignment.Bottom
                };


            thermometer.Children.Add(
                tubeOutline);

            thermometer.Children.Add(
                tubeFill);

            thermometer.Children.Add(
                bulb);


            // ========================================================
            // ИНФОРМАЦИЯ
            // ========================================================

            Grid information =
                new()
                {
                    Margin =
                        new Thickness(
                            3, 0, 12, 0)
                };

            information.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Auto
                });

            information.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Auto
                });

            information.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Auto
                });


            TextBlock sensorLabel =
                new()
                {
                    Text = "THERMAL SENSOR",

                    FontSize = 8,

                    FontWeight =
                        FontWeights.SemiBold,

                    Margin =
                        new Thickness(
                            0, 0, 0, 3)
                };

            sensorLabel.SetResourceReference(
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
                            0, 0, 0, 8)
                };

            name.SetResourceReference(
                TextBlock.ForegroundProperty,
                "PrimaryTextBrush");


            ProgressBar thermalBar =
                new()
                {
                    Minimum = 0,
                    Maximum = 100,

                    Value =
                        normalized * 100,

                    Height = 5,

                    Foreground =
                        statusBrush
                };


            Grid.SetRow(
                sensorLabel,
                0);

            Grid.SetRow(
                name,
                1);

            Grid.SetRow(
                thermalBar,
                2);


            information.Children.Add(
                sensorLabel);

            information.Children.Add(
                name);

            information.Children.Add(
                thermalBar);


            // ========================================================
            // ТЕМПЕРАТУРА / СТАТУС
            // ========================================================

            StackPanel valuePanel =
                new()
                {
                    HorizontalAlignment =
                        HorizontalAlignment.Right,

                    VerticalAlignment =
                        VerticalAlignment.Center
                };


            TextBlock temperature =
                new()
                {
                    Text =
                        FormatTemperature(
                            storage.Temperature),

                    Foreground =
                        statusBrush,

                    FontSize = 19,

                    FontWeight =
                        FontWeights.Bold,

                    HorizontalAlignment =
                        HorizontalAlignment.Right
                };


            StackPanel sensorStatus =
                new()
                {
                    Orientation =
                        Orientation.Horizontal,

                    HorizontalAlignment =
                        HorizontalAlignment.Right,

                    Margin =
                        new Thickness(
                            0, 5, 0, 0)
                };


            System.Windows.Shapes.Ellipse statusDot =
                new()
                {
                    Width = 5,
                    Height = 5,

                    Fill =
                        storage.Temperature.HasValue
                            ? statusBrush
                            : Brushes.Gray,

                    VerticalAlignment =
                        VerticalAlignment.Center,

                    Margin =
                        new Thickness(
                            0, 0, 5, 0)
                };


            TextBlock statusText =
                new()
                {
                    Text =
                        storage.Temperature.HasValue
                            ? "SENSOR ACTIVE"
                            : "NO DATA",

                    FontSize = 8,

                    FontWeight =
                        FontWeights.SemiBold
                };

            statusText.SetResourceReference(
                TextBlock.ForegroundProperty,
                storage.Temperature.HasValue
                    ? "SecondaryTextBrush"
                    : "SecondaryTextBrush");


            sensorStatus.Children.Add(
                statusDot);

            sensorStatus.Children.Add(
                statusText);


            valuePanel.Children.Add(
                temperature);

            valuePanel.Children.Add(
                sensorStatus);


            Grid.SetColumn(
                thermometer,
                0);

            Grid.SetColumn(
                information,
                1);

            Grid.SetColumn(
                valuePanel,
                2);


            body.Children.Add(
                thermometer);

            body.Children.Add(
                information);

            body.Children.Add(
                valuePanel);


            Grid.SetRow(
                body,
                1);

            root.Children.Add(
                body);


            border.Child =
                root;


            Grid.SetRow(
                border,
                row);

            Grid.SetColumn(
                border,
                column);


            StorageTemperaturePanel
                .Children
                .Add(
                    border);
        }
    }
}
