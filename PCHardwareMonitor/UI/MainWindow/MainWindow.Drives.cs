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
    // ДИСКИ
    // ============================================================

    private void UpdateDrives(
        HardwareSnapshot snapshot)
    {
        PrepareResponsiveGrid(
            DrivesPanel,
            snapshot.Drives.Count,
            out int columns,
            minimumCardWidth: 340);

        for (int i = 0;
             i < snapshot.Drives.Count;
             i++)
        {
            LogicalDriveInfo drive =
                snapshot.Drives[i];

            int row =
                i / columns;

            int column =
                i % columns;

            UIElement card =
                CreateDriveCard(
                    drive,
                    column,
                    columns);

            Grid.SetRow(
                card,
                row);

            Grid.SetColumn(
                card,
                column);

            DrivesPanel.Children.Add(
                card);
        }
    }

    private UIElement CreateDriveCard(
    LogicalDriveInfo drive,
    int column,
    int columns)
    {
        string skinName =
            SettingsService
                .Current
                .SkinName;

        if (skinName == "SteamPunk")
        {
            return CreateSteamDriveCard(
                drive,
                column,
                columns);
        }

        if (skinName == "FrostCore")
        {
            return CreateFrostDriveCard(
                drive,
                column,
                columns);
        }

        if (skinName == "MilitaryOps")
        {
            return CreateMilitaryDriveCard(
                drive,
                column,
                columns);
        }

        Border border =
            CreateGridCardBorder(
                column,
                columns);

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
                    new GridLength(46)
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
        // ИКОНКА НАКОПИТЕЛЯ
        // ========================================================

        Grid driveIcon =
            new()
            {
                Width = 34,
                Height = 38,

                VerticalAlignment =
                    VerticalAlignment.Center,

                HorizontalAlignment =
                    HorizontalAlignment.Left
            };


        Border iconOutline =
            new()
            {
                Width = 30,
                Height = 34,

                BorderThickness =
                    new Thickness(1),

                CornerRadius =
                    new CornerRadius(3),

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        iconOutline.SetResourceReference(
            Border.BorderBrushProperty,
            "AccentBrush");


        Border iconSlot =
            new()
            {
                Width = 16,
                Height = 2,

                VerticalAlignment =
                    VerticalAlignment.Top,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                Margin =
                    new Thickness(
                        0, 8, 0, 0)
            };

        iconSlot.SetResourceReference(
            Border.BackgroundProperty,
            "AccentBrush");


        TextBlock iconText =
            new()
            {
                Text =
                    drive.Name
                        .TrimEnd('\\'),

                FontSize = 8,

                FontWeight =
                    FontWeights.Bold,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Bottom,

                Margin =
                    new Thickness(
                        0, 0, 0, 6)
            };

        iconText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");


        driveIcon.Children.Add(
            iconOutline);

        driveIcon.Children.Add(
            iconSlot);

        driveIcon.Children.Add(
            iconText);


        // ========================================================
        // ОСНОВНАЯ ИНФОРМАЦИЯ
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

        information.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });


        TextBlock moduleLabel =
            new()
            {
                Text = "STORAGE VOLUME",

                FontSize = 8,

                FontWeight =
                    FontWeights.SemiBold,

                Margin =
                    new Thickness(
                        0, 0, 0, 2)
            };

        moduleLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");


        TextBlock name =
            new()
            {
                Text = label,

                FontSize = 16,

                FontWeight =
                    FontWeights.Bold,

                TextTrimming =
                    TextTrimming.CharacterEllipsis,

                Margin =
                    new Thickness(
                        0, 0, 0, 7)
            };

        name.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");


        TextBlock free =
            new()
            {
                Text =
                    SettingsService.L(
                    "Свободно: ",
                    "Free: ") +
                    $"{FormatSize(freeGb)} " +
                    SettingsService.L(
                    $"из {FormatSize(totalGb)}",
                    $"of {FormatSize(totalGb)}"),

                FontSize = 12,

                Margin =
                    new Thickness(
                        0, 0, 0, 7)
            };

        free.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");


        ProgressBar bar =
            new()
            {
                Minimum = 0,
                Maximum = 100,

                Value =
                    Math.Clamp(
                        usedPercent,
                        0,
                        100),

                Height = 7,

                Foreground =
                    statusBrush
            };


        Grid.SetRow(
            moduleLabel,
            0);

        Grid.SetRow(
            name,
            1);

        Grid.SetRow(
            free,
            2);

        Grid.SetRow(
            bar,
            3);


        information.Children.Add(
            moduleLabel);

        information.Children.Add(
            name);

        information.Children.Add(
            free);

        information.Children.Add(
            bar);


        // ========================================================
        // ПРОЦЕНТ ЗАПОЛНЕНИЯ
        // ========================================================

        StackPanel usagePanel =
            new()
            {
                VerticalAlignment =
                    VerticalAlignment.Center,

                HorizontalAlignment =
                    HorizontalAlignment.Right
            };


        TextBlock usageLabel =
            new()
            {
                Text = SettingsService.L(
                "ЗАНЯТО",
                "USED"),

                FontSize = 8,

                FontWeight =
                    FontWeights.SemiBold,

                HorizontalAlignment =
                    HorizontalAlignment.Right
            };

        usageLabel.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");


        TextBlock usageValue =
            new()
            {
                Text =
                    $"{usedPercent:F0} %",

                FontSize = 18,

                FontWeight =
                    FontWeights.Bold,

                Foreground =
                    statusBrush,

                HorizontalAlignment =
                    HorizontalAlignment.Right,

                Margin =
                    new Thickness(
                        0, 3, 0, 0)
            };


        Border statusLine =
            new()
            {
                Width = 30,
                Height = 2,

                Background =
                    statusBrush,

                HorizontalAlignment =
                    HorizontalAlignment.Right,

                Margin =
                    new Thickness(
                        0, 8, 0, 0)
            };


        usagePanel.Children.Add(
            usageLabel);

        usagePanel.Children.Add(
            usageValue);

        usagePanel.Children.Add(
            statusLine);


        Button analyzeButton =
            CreateDriveAnalyzeButton(
                drive);

        analyzeButton.HorizontalAlignment =
            HorizontalAlignment.Right;

        analyzeButton.Margin =
            new Thickness(
                0, 8, 0, 0);

        usagePanel.Children.Add(
            analyzeButton);


        Grid.SetColumn(
            driveIcon,
            0);

        Grid.SetColumn(
            information,
            1);

        Grid.SetColumn(
            usagePanel,
            2);


        body.Children.Add(
            driveIcon);

        body.Children.Add(
            information);

        body.Children.Add(
            usagePanel);


        Grid.SetRow(
            body,
            1);

        root.Children.Add(
            body);


        border.Child =
            root;

        return border;
    }


    private Button CreateDriveAnalyzeButton(
        LogicalDriveInfo drive)
    {
        Button button =
            new()
            {
                Content =
                    SettingsService.L(
                        "Анализ",
                        "Analyze"),

                Tag =
                    drive.Name,

                MinWidth =
                    72,

                MinHeight =
                    24,

                Height =
                    24,

                Padding =
                    new Thickness(
                        9, 2, 9, 2),

                FontSize =
                    9,

                FontWeight =
                    FontWeights.Bold,

                BorderThickness =
                    new Thickness(1),

                Cursor =
                    System.Windows.Input.Cursors.Hand
            };

        string skinName =
            SettingsService
                .Current
                .SkinName;

        ApplyDriveAnalyzeButtonVisual(
            button,
            skinName,
            hovered: false);

        button.MouseEnter +=
            (_, _) =>
            {
                ApplyDriveAnalyzeButtonVisual(
                    button,
                    skinName,
                    hovered: true);
            };

        button.MouseLeave +=
            (_, _) =>
            {
                ApplyDriveAnalyzeButtonVisual(
                    button,
                    skinName,
                    hovered: false);
            };

        button.Click +=
            DriveAnalyzeButton_Click;

        return button;
    }


    private static void ApplyDriveAnalyzeButtonVisual(
        Button button,
        string skinName,
        bool hovered)
    {
        if (hovered)
        {
            button.SetResourceReference(
                Control.BackgroundProperty,
                "AccentBrush");

            button.SetResourceReference(
                Control.BorderBrushProperty,
                "AccentBrush");

            button.SetResourceReference(
                Control.ForegroundProperty,
                "WindowBackgroundBrush");

            return;
        }

        if (skinName == "SteamPunk")
        {
            button.SetResourceReference(
                Control.BackgroundProperty,
                "SteamDarkMetalBrush");

            button.SetResourceReference(
                Control.BorderBrushProperty,
                "SteamMetalBrush");

            button.SetResourceReference(
                Control.ForegroundProperty,
                "AccentBrush");

            return;
        }

        if (skinName == "FrostCore")
        {
            button.SetResourceReference(
                Control.BackgroundProperty,
                "FrostGlassBrush");

            button.SetResourceReference(
                Control.BorderBrushProperty,
                "FrostIceBrush");

            button.SetResourceReference(
                Control.ForegroundProperty,
                "FrostIceBrush");

            return;
        }

        if (skinName == "MilitaryOps")
        {
            button.SetResourceReference(
                Control.BackgroundProperty,
                "MilitaryPanelBrush");

            button.SetResourceReference(
                Control.BorderBrushProperty,
                "MilitaryKhakiBrush");

            button.SetResourceReference(
                Control.ForegroundProperty,
                "MilitaryKhakiBrush");

            return;
        }

        button.SetResourceReference(
            Control.BackgroundProperty,
            "InputBackgroundBrush");

        button.SetResourceReference(
            Control.BorderBrushProperty,
            "AccentBrush");

        button.SetResourceReference(
            Control.ForegroundProperty,
            "AccentBrush");
    }


    private void DriveAnalyzeButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not string driveName ||
            string.IsNullOrWhiteSpace(
                driveName))
        {
            return;
        }

        StorageAnalyzerWindow window =
            new(
                driveName)
            {
                Owner =
                    this
            };

        window.Show();
    }
}
