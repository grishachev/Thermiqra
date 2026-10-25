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
    private static Border CreateMilitarySensorCard(
        string shortName,
        string title,
        string value,
        double level,
        SensorKind kind)
    {
        Border card =
            CreateSubCard(
                new Thickness(
                    15));

        card.Margin =
            new Thickness(6);

        card.BorderThickness =
            new Thickness(2);

        card.CornerRadius =
            new CornerRadius(2);

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

        Grid header =
            new();

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

        Grid reticle =
            new()
            {
                Width =
                    52,

                Height =
                    52,

                Margin =
                    new Thickness(
                        0,
                        0,
                        14,
                        0)
            };

        Ellipse ring =
            new()
            {
                StrokeThickness =
                    2
            };

        ring.SetResourceReference(
            Shape.StrokeProperty,
            "AccentBrush");

        Border vertical =
            new()
            {
                Width =
                    1,

                Height =
                    44
            };

        vertical.SetResourceReference(
            Border.BackgroundProperty,
            "BorderBrush");

        Border horizontal =
            new()
            {
                Width =
                    44,

                Height =
                    1
            };

        horizontal.SetResourceReference(
            Border.BackgroundProperty,
            "BorderBrush");

        TextBlock code =
            new()
            {
                Text =
                    shortName,

                FontSize =
                    9,

                FontWeight =
                    FontWeights.Bold,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        code.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        reticle.Children.Add(
            ring);

        reticle.Children.Add(
            vertical);

        reticle.Children.Add(
            horizontal);

        reticle.Children.Add(
            code);

        StackPanel labels =
            new()
            {
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        labels.Children.Add(
            CreateSensorTitle(
                title));

        labels.Children.Add(
            CreateSensorValue(
                value));

        Grid.SetColumn(
            labels,
            1);

        header.Children.Add(
            reticle);

        header.Children.Add(
            labels);

        root.Children.Add(
            header);

        TextBlock state =
            new()
            {
                Text =
                    SettingsService.L(
                        "СТАТУС: НОРМА",
                        "STATUS: NORMAL"),

                FontSize =
                    11,

                FontWeight =
                    FontWeights.Bold,

                Margin =
                    new Thickness(
                        0,
                        16,
                        0,
                        0)
            };

        state.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        Grid.SetRow(
            state,
            1);

        root.Children.Add(
            state);

        Grid bars =
            new()
            {
                Height =
                    12,

                Margin =
                    new Thickness(
                        0,
                        12,
                        0,
                        0)
            };

        for (int i = 0;
             i < 10;
             i++)
        {
            bars.ColumnDefinitions.Add(
                new ColumnDefinition());
        }

        int active =
            Math.Max(
                1,
                (int)Math.Round(
                    Math.Clamp(
                        level,
                        0,
                        1) *
                    10));

        for (int i = 0;
             i < 10;
             i++)
        {
            Border segment =
                new()
                {
                    Margin =
                        new Thickness(
                            2,
                            0,
                            2,
                            0),

                    Opacity =
                        i < active
                            ? 1.0
                            : 0.22
                };

            segment.SetResourceReference(
                Border.BackgroundProperty,
                i < active
                    ? "AccentBrush"
                    : "BorderBrush");

            Grid.SetColumn(
                segment,
                i);

            bars.Children.Add(
                segment);
        }

        Grid.SetRow(
            bars,
            2);

        root.Children.Add(
            bars);

        card.Child =
            root;

        return card;
    }
}
