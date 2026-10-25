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
    private static TextBlock CreateSensorTitle(
        string title)
    {
        TextBlock text =
            new()
            {
                Text =
                    title,

                FontSize =
                    12,

                FontWeight =
                    FontWeights.Bold,

                TextWrapping =
                    TextWrapping.Wrap
            };

        text.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        return text;
    }


    private static TextBlock CreateSensorValue(
        string value)
    {
        TextBlock text =
            new()
            {
                Text =
                    value,

                FontSize =
                    23,

                FontWeight =
                    FontWeights.Bold,

                Margin =
                    new Thickness(
                        0,
                        4,
                        0,
                        0),

                TextWrapping =
                    TextWrapping.Wrap
            };

        text.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        return text;
    }


    private static Border CreateSensorTrack(
        double level,
        double height)
    {
        Border track =
            new()
            {
                Height =
                    height,

                ClipToBounds =
                    true
            };

        track.SetResourceReference(
            Border.BackgroundProperty,
            "GaugeTrackBrush");

        track.SetResourceReference(
            Border.CornerRadiusProperty,
            "ProgressCornerRadius");

        Grid trackGrid =
            new();

        Border fill =
            new()
            {
                HorizontalAlignment =
                    HorizontalAlignment.Left,

                Width =
                    150 *
                    Math.Clamp(
                        level,
                        0,
                        1)
            };

        fill.SetResourceReference(
            Border.BackgroundProperty,
            "AccentBrush");

        fill.SetResourceReference(
            Border.CornerRadiusProperty,
            "ProgressCornerRadius");

        trackGrid.Children.Add(
            fill);

        track.Child =
            trackGrid;

        return track;
    }


    private static TextBlock CreateVisualHeading(
        string text)
    {
        TextBlock heading =
            new()
            {
                Text =
                    text,

                FontSize =
                    12,

                FontWeight =
                    FontWeights.Bold
            };

        heading.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        return heading;
    }

    private enum SensorKind
    {
        Temperature,
        Memory,
        Storage
    }


    private static Border CreateStatusRow(
        Color color,
        string title,
        string description)
    {
        Border card =
            CreateSubCard(
                new Thickness(
                    16,
                    13,
                    16,
                    13));

        card.Margin =
            new Thickness(
                0,
                0,
                0,
                10);

        Grid grid =
            new();

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        Ellipse indicator =
            new()
            {
                Width =
                    20,

                Height =
                    20,

                Fill =
                    new SolidColorBrush(
                        color),

                Margin =
                    new Thickness(
                        0,
                        0,
                        16,
                        0),

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        StackPanel text =
            new()
            {
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        TextBlock titleText =
            new()
            {
                Text =
                    title,

                FontSize =
                    12,

                FontWeight =
                    FontWeights.Bold
            };

        titleText.Foreground =
            new SolidColorBrush(
                color);

        TextBlock descriptionText =
            new()
            {
                Text =
                    description,

                FontSize =
                    13,

                LineHeight =
                    19,

                Margin =
                    new Thickness(
                        0,
                        4,
                        0,
                        0)
            };

        descriptionText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        text.Children.Add(
            titleText);

        text.Children.Add(
            descriptionText);

        Grid.SetColumn(
            text,
            1);

        grid.Children.Add(
            indicator);

        grid.Children.Add(
            text);

        card.Child =
            grid;

        return card;
    }

    private static Border CreateActionCard(
        string number,
        string title,
        string description)
    {
        Border card =
            CreateSubCard(
                new Thickness(
                    18));

        card.Margin =
            new Thickness(6);

        StackPanel content =
            new();

        Border numberBadge =
            new()
            {
                Width =
                    38,

                Height =
                    38,

                HorizontalAlignment =
                    HorizontalAlignment.Left,

                BorderThickness =
                    new Thickness(1)
            };

        numberBadge.SetResourceReference(
            Border.BorderBrushProperty,
            "AccentBrush");

        numberBadge.SetResourceReference(
            Border.CornerRadiusProperty,
            "PanelCornerRadius");

        TextBlock numberText =
            new()
            {
                Text =
                    number,

                FontSize =
                    12,

                FontWeight =
                    FontWeights.Bold,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        numberText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        numberBadge.Child =
            numberText;

        Button visualButton =
            CreateThemedButton(
                title,
                primary: true);

        visualButton.IsHitTestVisible =
            false;

        visualButton.HorizontalAlignment =
            HorizontalAlignment.Stretch;

        visualButton.Margin =
            new Thickness(
                0,
                18,
                0,
                0);

        TextBlock descriptionText =
            new()
            {
                Text =
                    description,

                TextWrapping =
                    TextWrapping.Wrap,

                FontSize =
                    13,

                LineHeight =
                    20,

                Margin =
                    new Thickness(
                        0,
                        16,
                        0,
                        0)
            };

        descriptionText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        content.Children.Add(
            numberBadge);

        content.Children.Add(
            visualButton);

        content.Children.Add(
            descriptionText);

        card.Child =
            content;

        return card;
    }

    private static Border CreateSubCard(
        Thickness padding)
    {
        Border card =
            new()
            {
                Padding =
                    padding,

                BorderThickness =
                    new Thickness(1)
            };

        card.SetResourceReference(
            Border.BackgroundProperty,
            "WindowBackgroundBrush");

        card.SetResourceReference(
            Border.BorderBrushProperty,
            "BorderBrush");

        card.SetResourceReference(
            Border.CornerRadiusProperty,
            "PanelCornerRadius");

        return card;
    }

    private static Border CreateTrayDot()
    {
        Border dot =
            new()
            {
                Width =
                    30,

                Height =
                    30,

                BorderThickness =
                    new Thickness(1)
            };

        dot.SetResourceReference(
            Border.BorderBrushProperty,
            "BorderBrush");

        dot.SetResourceReference(
            Border.CornerRadiusProperty,
            "PanelCornerRadius");

        TextBlock text =
            new()
            {
                Text =
                    "⋯",

                FontSize =
                    17,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        text.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        dot.Child =
            text;

        return dot;
    }
}
