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
    private static Border CreateCyberSensorCard(
        string shortName,
        string title,
        string value,
        double level,
        SensorKind kind)
    {
        Border card =
            CreateSubCard(
                new Thickness(
                    16));

        card.Margin =
            new Thickness(6);

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

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        Border topStrip =
            new()
            {
                Height =
                    3,

                Width =
                    86,

                HorizontalAlignment =
                    HorizontalAlignment.Left,

                VerticalAlignment =
                    VerticalAlignment.Top,

                Margin =
                    new Thickness(
                        0,
                        -16,
                        0,
                        0)
            };

        topStrip.SetResourceReference(
            Border.BackgroundProperty,
            "AccentBrush");

        root.Children.Add(
            topStrip);

        Grid header =
            new()
            {
                Margin =
                    new Thickness(
                        0,
                        4,
                        0,
                        0)
            };

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        Border symbol =
            new()
            {
                Width =
                    56,

                Height =
                    56,

                BorderThickness =
                    new Thickness(2),

                Margin =
                    new Thickness(
                        0,
                        0,
                        14,
                        0)
            };

        symbol.SetResourceReference(
            Border.BorderBrushProperty,
            "AccentBrush");

        TextBlock symbolText =
            new()
            {
                Text =
                    shortName,

                FontSize =
                    14,

                FontWeight =
                    FontWeights.Bold,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        symbolText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        symbol.Child =
            symbolText;

        StackPanel labels =
            new()
            {
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        TextBlock titleText =
            CreateSensorTitle(
                title);

        TextBlock valueText =
            CreateSensorValue(
                value);

        labels.Children.Add(
            titleText);

        labels.Children.Add(
            valueText);

        Grid.SetColumn(
            labels,
            1);

        header.Children.Add(
            symbol);

        header.Children.Add(
            labels);

        Grid.SetRow(
            header,
            1);

        root.Children.Add(
            header);

        Border track =
            CreateSensorTrack(
                level,
                9);

        track.Margin =
            new Thickness(
                0,
                18,
                0,
                0);

        Grid.SetRow(
            track,
            2);

        root.Children.Add(
            track);

        card.Child =
            root;

        return card;
    }
}
