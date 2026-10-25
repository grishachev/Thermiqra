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
    private static Border CreateFrostSensorCard(
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

        card.SetResourceReference(
            Border.CornerRadiusProperty,
            "PanelCornerRadius");

        Grid root =
            new();

        root.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(106)
            });

        root.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        Grid capsule =
            new()
            {
                Width =
                    78,

                Height =
                    108,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center,

                ClipToBounds =
                    true
            };

        Border capsuleBody =
            new()
            {
                BorderThickness =
                    new Thickness(2),

                CornerRadius =
                    new CornerRadius(28)
            };

        capsuleBody.SetResourceReference(
            Border.BorderBrushProperty,
            "AccentBrush");

        Grid capsuleInside =
            new()
            {
                Margin =
                    new Thickness(7),

                ClipToBounds =
                    true
            };

        Border fill =
            new()
            {
                Height =
                    86 *
                    Math.Clamp(
                        level,
                        0,
                        1),

                VerticalAlignment =
                    VerticalAlignment.Bottom,

                Opacity =
                    0.72,

                CornerRadius =
                    new CornerRadius(18)
            };

        fill.SetResourceReference(
            Border.BackgroundProperty,
            "AccentBrush");

        TextBlock snow =
            new()
            {
                Text =
                    "❄",

                FontSize =
                    25,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center,

                Opacity =
                    0.65
            };

        snow.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        capsuleInside.Children.Add(
            fill);

        capsuleInside.Children.Add(
            snow);

        capsuleBody.Child =
            capsuleInside;

        capsule.Children.Add(
            capsuleBody);

        StackPanel labels =
            new()
            {
                Margin =
                    new Thickness(
                        8,
                        0,
                        0,
                        0),

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        TextBlock code =
            new()
            {
                Text =
                    shortName,

                FontSize =
                    11,

                FontWeight =
                    FontWeights.Bold
            };

        code.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        TextBlock titleText =
            CreateSensorTitle(
                title);

        titleText.Margin =
            new Thickness(
                0,
                5,
                0,
                0);

        TextBlock valueText =
            CreateSensorValue(
                value);

        labels.Children.Add(
            code);

        labels.Children.Add(
            titleText);

        labels.Children.Add(
            valueText);

        Grid.SetColumn(
            labels,
            1);

        root.Children.Add(
            capsule);

        root.Children.Add(
            labels);

        card.Child =
            root;

        return card;
    }
}
