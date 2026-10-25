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
    private static Border CreateSteamSensorCard(
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

        card.BorderThickness =
            new Thickness(2);

        Grid root =
            new();

        root.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(118)
            });

        root.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        UIElement instrument =
            kind == SensorKind.Memory
                ? CreateSteamMemoryPipe(
                    level)
                : CreateSteamRoundGauge(
                    shortName,
                    level);

        root.Children.Add(
            instrument);

        StackPanel labels =
            new()
            {
                Margin =
                    new Thickness(
                        14,
                        2,
                        0,
                        0),

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        TextBlock titleText =
            CreateSensorTitle(
                title);

        titleText.FontFamily =
            new FontFamily(
                "Georgia");

        TextBlock valueText =
            CreateSensorValue(
                value);

        valueText.FontFamily =
            new FontFamily(
                "Georgia");

        TextBlock status =
            new()
            {
                Text =
                    SettingsService.L(
                        "Датчик активен",
                        "Sensor active"),

                FontSize =
                    12,

                Margin =
                    new Thickness(
                        0,
                        12,
                        0,
                        0)
            };

        status.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        labels.Children.Add(
            titleText);

        labels.Children.Add(
            valueText);

        labels.Children.Add(
            status);

        Grid.SetColumn(
            labels,
            1);

        root.Children.Add(
            labels);

        card.Child =
            root;

        return card;
    }


    private static UIElement CreateSteamRoundGauge(
        string shortName,
        double level)
    {
        Grid gauge =
            new()
            {
                Width =
                    104,

                Height =
                    104,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        Ellipse outer =
            new()
            {
                StrokeThickness =
                    4
            };

        outer.SetResourceReference(
            Shape.StrokeProperty,
            "AccentBrush");

        Ellipse inner =
            new()
            {
                Width =
                    82,

                Height =
                    82,

                StrokeThickness =
                    2
            };

        inner.SetResourceReference(
            Shape.StrokeProperty,
            "BorderBrush");

        Border crossVertical =
            new()
            {
                Width =
                    2,

                Height =
                    68,

                Opacity =
                    0.45
            };

        crossVertical.SetResourceReference(
            Border.BackgroundProperty,
            "BorderBrush");

        Border crossHorizontal =
            new()
            {
                Width =
                    68,

                Height =
                    2,

                Opacity =
                    0.45
            };

        crossHorizontal.SetResourceReference(
            Border.BackgroundProperty,
            "BorderBrush");

        double angle =
            -118 +
            236 *
            Math.Clamp(
                level,
                0,
                1);

        Line needle =
            new()
            {
                X1 =
                    52,

                Y1 =
                    52,

                X2 =
                    52,

                Y2 =
                    18,

                StrokeThickness =
                    3,

                RenderTransformOrigin =
                    new Point(
                        0.5,
                        1.0),

                RenderTransform =
                    new RotateTransform(
                        angle)
            };

        needle.SetResourceReference(
            Shape.StrokeProperty,
            "AccentBrush");

        Ellipse hub =
            new()
            {
                Width =
                    13,

                Height =
                    13
            };

        hub.SetResourceReference(
            Shape.FillProperty,
            "AccentBrush");

        TextBlock label =
            new()
            {
                Text =
                    shortName,

                FontSize =
                    10,

                FontWeight =
                    FontWeights.Bold,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Bottom,

                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        15)
            };

        label.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        gauge.Children.Add(
            outer);

        gauge.Children.Add(
            inner);

        gauge.Children.Add(
            crossVertical);

        gauge.Children.Add(
            crossHorizontal);

        gauge.Children.Add(
            needle);

        gauge.Children.Add(
            hub);

        gauge.Children.Add(
            label);

        return gauge;
    }


    private static UIElement CreateSteamMemoryPipe(
        double level)
    {
        Grid root =
            new()
            {
                Width =
                    104,

                Height =
                    104
            };

        Border pipe =
            new()
            {
                Width =
                    82,

                Height =
                    32,

                BorderThickness =
                    new Thickness(3),

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center,

                Padding =
                    new Thickness(5)
            };

        pipe.SetResourceReference(
            Border.BorderBrushProperty,
            "AccentBrush");

        pipe.SetResourceReference(
            Border.CornerRadiusProperty,
            "ProgressCornerRadius");

        Grid track =
            new()
            {
                ClipToBounds =
                    true
            };

        Border fill =
            new()
            {
                Width =
                    66 *
                    Math.Clamp(
                        level,
                        0,
                        1),

                HorizontalAlignment =
                    HorizontalAlignment.Left
            };

        fill.SetResourceReference(
            Border.BackgroundProperty,
            "AccentBrush");

        track.Children.Add(
            fill);

        pipe.Child =
            track;

        TextBlock percent =
            new()
            {
                Text =
                    $"{level * 100:F0}%",

                FontSize =
                    16,

                FontWeight =
                    FontWeights.Bold,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Bottom,

                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        10)
            };

        percent.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        root.Children.Add(
            pipe);

        root.Children.Add(
            percent);

        return root;
    }
}
