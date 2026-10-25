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


    private UIElement CreateSteamDriveCard(
        LogicalDriveInfo drive,
        int column,
        int columns)
    {
        string label =
            string.IsNullOrWhiteSpace(
                drive.VolumeLabel)

                ? drive.Name

                : $"{drive.Name} {drive.VolumeLabel}";

        double totalGb =
            BytesToGb(
                drive.TotalBytes);

        double freeGb =
            BytesToGb(
                drive.FreeBytes);

        double usedPercent =
            0;

        if (drive.TotalBytes > 0)
        {
            usedPercent =
                100.0 -
                (
                    drive.FreeBytes /
                    (double)drive.TotalBytes *
                    100.0
                );
        }

        Brush statusBrush =
            UiBrushes.DiskUsage(
                usedPercent);

        Border outer =
            CreateSteamGridCardBorder(
                column,
                columns);

        Grid root = new()
        {
            Height = 116
        };

        // Центральный корпус — не карточка, а горизонтальный механический модуль.
        Border body = new()
        {
            Margin =
                new Thickness(
                    38, 8, 38, 8),

            Padding =
                new Thickness(
                    54, 10, 66, 10),

            BorderThickness =
                new Thickness(2),

            CornerRadius =
                new CornerRadius(24)
        };

        body.SetResourceReference(
            Border.BackgroundProperty,
            "SteamPanelBrush");

        body.SetResourceReference(
            Border.BorderBrushProperty,
            "SteamMetalBrush");

        StackPanel information = new()
        {
            VerticalAlignment =
                VerticalAlignment.Center
        };

        TextBlock name = new()
        {
            Text = label,
            FontSize = 15,
            FontWeight =
                FontWeights.Bold,
            TextTrimming =
                TextTrimming.CharacterEllipsis
        };

        name.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        TextBlock free = new()
        {
            Text =
                SettingsService.L(
                    "Свободно: ",
                    "Free: ") +
                $"{FormatSize(freeGb)} " +
                SettingsService.L(
                    $"из {FormatSize(totalGb)}",
                    $"of {FormatSize(totalGb)}"),

            FontSize = 10,

            Margin =
                new Thickness(
                    0, 3, 0, 7)
        };

        free.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        Grid fillRow = new();

        fillRow.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        fillRow.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        fillRow.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        ProgressBar fillBar = new()
        {
            Minimum = 0,
            Maximum = 100,
            Value =
                Math.Clamp(
                    usedPercent,
                    0,
                    100),
            Height = 22,
            Foreground =
                statusBrush,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        if (TryFindResource(
                "SteamPipeProgressStyle")
            is Style pipeStyle)
        {
            fillBar.Style =
                pipeStyle;
        }

        TextBlock fillText = new()
        {
            Text =
                SettingsService.L(
                $"Занято {usedPercent:F0} %",
                $"Used {usedPercent:F0} %"),
            Foreground =
                statusBrush,
            FontSize = 10,
            FontWeight =
                FontWeights.Bold,
            VerticalAlignment =
                VerticalAlignment.Center,
            Margin =
                new Thickness(
                    10, 0, 0, 0)
        };

        Grid.SetColumn(
            fillBar,
            0);

        Grid.SetColumn(
            fillText,
            1);

        Button steamAnalyzeButton =
            CreateDriveAnalyzeButton(
                drive);

        steamAnalyzeButton.Margin =
            new Thickness(
                10, 0, 0, 0);

        steamAnalyzeButton.VerticalAlignment =
            VerticalAlignment.Center;

        Grid.SetColumn(
            steamAnalyzeButton,
            2);

        fillRow.Children.Add(
            fillBar);

        fillRow.Children.Add(
            fillText);

        fillRow.Children.Add(
            steamAnalyzeButton);

        information.Children.Add(
            name);

        information.Children.Add(
            free);

        information.Children.Add(
            fillRow);

        body.Child =
            information;

        root.Children.Add(
            body);

        // Левый барабан с буквой диска.
        Grid drum = new()
        {
            Width = 82,
            Height = 82,
            HorizontalAlignment =
                HorizontalAlignment.Left,
            VerticalAlignment =
                VerticalAlignment.Center,
            Margin =
                new Thickness(
                    5, 0, 0, 0)
        };

        System.Windows.Shapes.Ellipse drumOuter =
            new()
            {
                StrokeThickness = 4
            };

        drumOuter.SetResourceReference(
            System.Windows.Shapes.Shape.FillProperty,
            "SteamGlassBrush");

        drumOuter.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "SteamMetalBrush");

        System.Windows.Shapes.Ellipse drumMiddle =
            new()
            {
                Width = 60,
                Height = 60,
                StrokeThickness = 2
            };

        drumMiddle.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "BorderBrush");

        Border drumVertical =
            new()
            {
                Width = 3,
                Height = 46,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        drumVertical.SetResourceReference(
            Border.BackgroundProperty,
            "SteamMetalBrush");

        Border drumHorizontal =
            new()
            {
                Width = 46,
                Height = 3,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        drumHorizontal.SetResourceReference(
            Border.BackgroundProperty,
            "SteamMetalBrush");

        System.Windows.Shapes.Ellipse drumHub =
            new()
            {
                Width = 35,
                Height = 35,
                StrokeThickness = 2
            };

        drumHub.SetResourceReference(
            System.Windows.Shapes.Shape.FillProperty,
            "SteamDarkMetalBrush");

        drumHub.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "AccentBrush");

        TextBlock driveLetter =
            new()
            {
                Text =
                    drive.Name
                        .TrimEnd('\\'),

                FontSize = 11,

                FontWeight =
                    FontWeights.Bold,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        driveLetter.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        drum.Children.Add(
            drumOuter);

        drum.Children.Add(
            drumMiddle);

        drum.Children.Add(
            drumVertical);

        drum.Children.Add(
            drumHorizontal);

        drum.Children.Add(
            drumHub);

        drum.Children.Add(
            driveLetter);

        root.Children.Add(
            drum);

        // Правый круглый счётчик.
        Grid meter = new()
        {
            Width = 74,
            Height = 74,
            HorizontalAlignment =
                HorizontalAlignment.Right,
            VerticalAlignment =
                VerticalAlignment.Center,
            Margin =
                new Thickness(
                    0, 0, 7, 0)
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
                Width = 58,
                Height = 58,
                StrokeThickness = 1
            };

        meterInner.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "BorderBrush");

        StackPanel meterText = new()
        {
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        TextBlock meterLabel =
            new()
            {
                Text = SettingsService.L(
                "ЗАНЯТО",
                "USED"),
                FontSize = 7,
                FontWeight =
                    FontWeights.Bold,
                HorizontalAlignment =
                    HorizontalAlignment.Center
            };

        meterLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        TextBlock usage =
            new()
            {
                Text =
                    $"{usedPercent:F0}%",

                FontSize = 18,
                FontWeight =
                    FontWeights.Bold,
                Foreground =
                    statusBrush,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                Margin =
                    new Thickness(
                        0, 1, 0, 0)
            };

        meterText.Children.Add(
            meterLabel);

        meterText.Children.Add(
            usage);

        meter.Children.Add(
            meterOuter);

        meter.Children.Add(
            meterInner);

        meter.Children.Add(
            meterText);

        root.Children.Add(
            meter);

        outer.Child =
            root;

        return outer;
    }


    private UIElement CreateFrostDriveCard(
        LogicalDriveInfo drive,
        int column,
        int columns)
    {
        string label =
            string.IsNullOrWhiteSpace(
                drive.VolumeLabel)
                ? drive.Name
                : $"{drive.Name} {drive.VolumeLabel}";

        double totalGb =
            BytesToGb(
                drive.TotalBytes);

        double freeGb =
            BytesToGb(
                drive.FreeBytes);

        double usedPercent = 0;

        if (drive.TotalBytes > 0)
        {
            usedPercent =
                100.0 -
                (
                    drive.FreeBytes /
                    (double)drive.TotalBytes *
                    100.0
                );
        }

        Brush statusBrush =
            UiBrushes.DiskUsage(
                usedPercent);

        Border outer =
            CreateFrostGridCardBorder(
                column,
                columns);

        Border body = new()
        {
            Height = 116,
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
                Width = new GridLength(86)
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
                Width = new GridLength(94)
            });

        // Герметичная криокассета.
        Grid capsule = new()
        {
            Width = 68,
            Height = 84,
            HorizontalAlignment =
                HorizontalAlignment.Left,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        Border capsuleOuter = new()
        {
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(22)
        };

        capsuleOuter.SetResourceReference(
            Border.BackgroundProperty,
            "FrostGlassBrush");

        capsuleOuter.SetResourceReference(
            Border.BorderBrushProperty,
            "FrostSteelBrush");

        Border capsuleInner = new()
        {
            Margin = new Thickness(9),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(15)
        };

        capsuleInner.SetResourceReference(
            Border.BorderBrushProperty,
            "FrostIceBrush");

        TextBlock frostMark = new()
        {
            Text = "❄",
            FontSize = 20,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Top,
            Margin = new Thickness(0, 11, 0, 0),
            Opacity = 0.72
        };

        frostMark.SetResourceReference(
            TextBlock.ForegroundProperty,
            "FrostIceBrush");

        Border divider = new()
        {
            Width = 32,
            Height = 1,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        divider.SetResourceReference(
            Border.BackgroundProperty,
            "FrostSteelBrush");

        TextBlock driveLetter = new()
        {
            Text = drive.Name.TrimEnd('\\'),
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 12)
        };

        driveLetter.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        capsule.Children.Add(capsuleOuter);
        capsule.Children.Add(capsuleInner);
        capsule.Children.Add(frostMark);
        capsule.Children.Add(divider);
        capsule.Children.Add(driveLetter);

        StackPanel information = new()
        {
            VerticalAlignment =
                VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0)
        };

        TextBlock caption = new()
        {
            Text = SettingsService.L(
                "КРИОМОДУЛЬ ХРАНЕНИЯ",
                "CRYO STORAGE MODULE"),
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        caption.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        TextBlock name = new()
        {
            Text = label,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            TextTrimming =
                TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 2, 0, 3)
        };

        name.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        TextBlock free = new()
        {
            Text =
                SettingsService.L(
                    "Свободно: ",
                    "Free: ") +
                $"{FormatSize(freeGb)} " +
                SettingsService.L(
                    $"из {FormatSize(totalGb)}",
                    $"of {FormatSize(totalGb)}"),
            FontSize = 10,
            Margin = new Thickness(0, 0, 0, 7)
        };

        free.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        ProgressBar fillBar = new()
        {
            Minimum = 0,
            Maximum = 100,
            Value = Math.Clamp(
                usedPercent,
                0,
                100),
            Height = 16,
            Foreground = statusBrush
        };

        if (TryFindResource(
                "FrostCapsuleProgressStyle")
            is Style frostProgressStyle)
        {
            fillBar.Style =
                frostProgressStyle;
        }

        Grid frostActionRow =
            new();

        frostActionRow.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        frostActionRow.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        Button frostAnalyzeButton =
            CreateDriveAnalyzeButton(
                drive);

        frostAnalyzeButton.Margin =
            new Thickness(
                10, 0, 0, 0);

        frostAnalyzeButton.VerticalAlignment =
            VerticalAlignment.Center;

        Grid.SetColumn(
            fillBar,
            0);

        Grid.SetColumn(
            frostAnalyzeButton,
            1);

        frostActionRow.Children.Add(
            fillBar);

        frostActionRow.Children.Add(
            frostAnalyzeButton);

        information.Children.Add(caption);
        information.Children.Add(name);
        information.Children.Add(free);
        information.Children.Add(frostActionRow);

        Border meter = new()
        {
            Padding = new Thickness(10),
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

        StackPanel meterStack = new();

        TextBlock meterLabel = new()
        {
            Text = SettingsService.L(
                "ЗАНЯТО",
                "USED"),
            FontSize = 7,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center
        };

        meterLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        TextBlock usage = new()
        {
            Text = $"{usedPercent:F0}%",
            FontSize = 21,
            FontWeight = FontWeights.Bold,
            Foreground = statusBrush,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 2)
        };

        TextBlock levelLabel = new()
        {
            Text = SettingsService.L(
                "КРИОУРОВЕНЬ",
                "CRYO LEVEL"),
            FontSize = 7,
            HorizontalAlignment =
                HorizontalAlignment.Center
        };

        levelLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        meterStack.Children.Add(meterLabel);
        meterStack.Children.Add(usage);
        meterStack.Children.Add(levelLabel);
        meter.Child = meterStack;

        Grid.SetColumn(capsule, 0);
        Grid.SetColumn(information, 1);
        Grid.SetColumn(meter, 2);

        layout.Children.Add(capsule);
        layout.Children.Add(information);
        layout.Children.Add(meter);
        body.Child = layout;
        outer.Child = body;

        return outer;
    }


    private UIElement CreateMilitaryDriveCard(
        LogicalDriveInfo drive,
        int column,
        int columns)
    {
        string label =
            string.IsNullOrWhiteSpace(
                drive.VolumeLabel)
                ? drive.Name
                : $"{drive.Name} {drive.VolumeLabel}";

        double totalGb =
            BytesToGb(
                drive.TotalBytes);

        double freeGb =
            BytesToGb(
                drive.FreeBytes);

        double usedPercent = 0;

        if (drive.TotalBytes > 0)
        {
            usedPercent =
                100.0 -
                (
                    drive.FreeBytes /
                    (double)drive.TotalBytes *
                    100.0
                );
        }

        Brush statusBrush =
            UiBrushes.DiskUsage(
                usedPercent);

        Border outer =
            CreateMilitaryGridCardBorder(
                column,
                columns);

        Grid root = new()
        {
            Height = 108
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
                Width = new GridLength(82)
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
                Width = new GridLength(96)
            });

        // Компас / маркировка контейнера.
        Grid compass = new()
        {
            Width = 62,
            Height = 62,
            HorizontalAlignment =
                HorizontalAlignment.Left,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        System.Windows.Shapes.Ellipse compassOuter =
            new()
            {
                StrokeThickness = 2
            };

        compassOuter.SetResourceReference(
            System.Windows.Shapes.Shape.FillProperty,
            "MilitaryPlateBrush");

        compassOuter.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "MilitaryKhakiBrush");

        Border compassVertical = new()
        {
            Width = 1,
            Height = 44,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        compassVertical.SetResourceReference(
            Border.BackgroundProperty,
            "MilitaryGridBrush");

        Border compassHorizontal = new()
        {
            Width = 44,
            Height = 1,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        compassHorizontal.SetResourceReference(
            Border.BackgroundProperty,
            "MilitaryGridBrush");

        TextBlock north = new()
        {
            Text = "N",
            FontSize = 8,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 0, 0)
        };

        north.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        Border centerPlate = new()
        {
            Width = 30,
            Height = 23,
            BorderThickness = new Thickness(1),
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        centerPlate.SetResourceReference(
            Border.BackgroundProperty,
            "MilitaryPanelBrush");

        centerPlate.SetResourceReference(
            Border.BorderBrushProperty,
            "MilitaryKhakiBrush");

        TextBlock driveLetter = new()
        {
            Text = drive.Name.TrimEnd('\\'),
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        driveLetter.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        centerPlate.Child = driveLetter;
        compass.Children.Add(compassOuter);
        compass.Children.Add(compassVertical);
        compass.Children.Add(compassHorizontal);
        compass.Children.Add(north);
        compass.Children.Add(centerPlate);

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
        information.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        TextBlock caption = new()
        {
            Text = SettingsService.L(
                "КОНТЕЙНЕР ДАННЫХ / СЕКТОР",
                "DATA CONTAINER / SECTOR"),
            FontSize = 8,
            FontWeight = FontWeights.Bold
        };

        caption.SetResourceReference(
            TextBlock.ForegroundProperty,
            "MilitaryKhakiBrush");

        TextBlock name = new()
        {
            Text = label,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            TextTrimming =
                TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 2, 0, 2)
        };

        name.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        TextBlock free = new()
        {
            Text =
                SettingsService.L(
                    "Свободно: ",
                    "Free: ") +
                $"{FormatSize(freeGb)} " +
                SettingsService.L(
                    $"из {FormatSize(totalGb)}",
                    $"of {FormatSize(totalGb)}"),
            FontSize = 9,
            Margin = new Thickness(0, 0, 0, 6)
        };

        free.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        Grid segments = new()
        {
            Height = 14
        };

        int activeSegments =
            usedPercent <= 0
                ? 0
                : Math.Clamp(
                    (int)Math.Ceiling(
                        usedPercent / 10.0),
                    0,
                    10);

        for (int i = 0;
             i < 10;
             i++)
        {
            segments.ColumnDefinitions.Add(
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
                    i == 9 ? 0 : 3,
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

            segments.Children.Add(
                segment);
        }

        Grid militaryActionRow =
            new();

        militaryActionRow.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        militaryActionRow.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        Button militaryAnalyzeButton =
            CreateDriveAnalyzeButton(
                drive);

        militaryAnalyzeButton.Margin =
            new Thickness(
                10, 0, 0, 0);

        militaryAnalyzeButton.VerticalAlignment =
            VerticalAlignment.Center;

        Grid.SetColumn(
            segments,
            0);

        Grid.SetColumn(
            militaryAnalyzeButton,
            1);

        militaryActionRow.Children.Add(
            segments);

        militaryActionRow.Children.Add(
            militaryAnalyzeButton);

        Grid.SetRow(caption, 0);
        Grid.SetRow(name, 1);
        Grid.SetRow(free, 2);
        Grid.SetRow(militaryActionRow, 3);

        information.Children.Add(caption);
        information.Children.Add(name);
        information.Children.Add(free);
        information.Children.Add(militaryActionRow);

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
            Text = SettingsService.L(
                "ЗАНЯТО",
                "USED"),
            FontSize = 7,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center
        };

        meterLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "MilitaryKhakiBrush");

        TextBlock usage = new()
        {
            Text = $"{usedPercent:F0}%",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = statusBrush,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 2)
        };

        TextBlock sector = new()
        {
            Text = SettingsService.L(
                "СЕКТОР STG",
                "STG SECTOR"),
            FontSize = 7,
            HorizontalAlignment =
                HorizontalAlignment.Center
        };

        sector.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        meterStack.Children.Add(meterLabel);
        meterStack.Children.Add(usage);
        meterStack.Children.Add(sector);
        meter.Child = meterStack;

        Grid.SetColumn(compass, 0);
        Grid.SetColumn(information, 1);
        Grid.SetColumn(meter, 2);

        layout.Children.Add(compass);
        layout.Children.Add(information);
        layout.Children.Add(meter);
        body.Child = layout;
        root.Children.Add(body);

        Border topLeft = new()
        {
            Width = 24,
            Height = 3,
            HorizontalAlignment =
                HorizontalAlignment.Left,
            VerticalAlignment =
                VerticalAlignment.Top,
            Margin = new Thickness(7)
        };

        topLeft.SetResourceReference(
            Border.BackgroundProperty,
            "MilitaryKhakiBrush");

        Border bottomRight = new()
        {
            Width = 24,
            Height = 3,
            HorizontalAlignment =
                HorizontalAlignment.Right,
            VerticalAlignment =
                VerticalAlignment.Bottom,
            Margin = new Thickness(7)
        };

        bottomRight.SetResourceReference(
            Border.BackgroundProperty,
            "MilitaryKhakiBrush");

        root.Children.Add(topLeft);
        root.Children.Add(bottomRight);
        outer.Child = root;

        return outer;
    }
}
