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



    private static Border CreateSteamGridCardBorder(
        int column,
        int columns)
    {
        double rightMargin =
            column < columns - 1
                ? 12
                : 0;

        return new Border
        {
            Padding =
                new Thickness(0),

            BorderThickness =
                new Thickness(0),

            Background =
                Brushes.Transparent,

            Margin =
                new Thickness(
                    0,
                    0,
                    rightMargin,
                    12),

            HorizontalAlignment =
                HorizontalAlignment.Stretch
        };
    }


    private static Border CreateFrostGridCardBorder(
        int column,
        int columns)
    {
        double rightMargin =
            column < columns - 1
                ? 12
                : 0;

        return new Border
        {
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Margin = new Thickness(
                0,
                0,
                rightMargin,
                12),
            HorizontalAlignment =
                HorizontalAlignment.Stretch
        };
    }


    private static Border CreateMilitaryGridCardBorder(
        int column,
        int columns)
    {
        double rightMargin =
            column < columns - 1
                ? 12
                : 0;

        return new Border
        {
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Margin = new Thickness(
                0,
                0,
                rightMargin,
                12),
            HorizontalAlignment =
                HorizontalAlignment.Stretch
        };
    }


    private static void AddSteamCardRivet(
        Grid root,
        HorizontalAlignment horizontal,
        VerticalAlignment vertical)
    {
        System.Windows.Shapes.Ellipse rivet =
            new()
            {
                Width = 7,
                Height = 7,
                Margin =
                    new Thickness(7),
                HorizontalAlignment =
                    horizontal,
                VerticalAlignment =
                    vertical,
                StrokeThickness = 1,
                IsHitTestVisible = false
            };

        rivet.SetResourceReference(
            System.Windows.Shapes.Shape.FillProperty,
            "SteamRivetBrush");

        rivet.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            "SteamDarkMetalBrush");

        root.Children.Add(
            rivet);
    }

    // ============================================================
    // АДАПТИВНАЯ СЕТКА
    // ============================================================

    private void PrepareResponsiveGrid(
        Grid grid,
        int itemCount,
        out int columns,
        double minimumCardWidth = 250)
    {
        grid.Children.Clear();

        grid.ColumnDefinitions.Clear();

        grid.RowDefinitions.Clear();

        if (itemCount <= 0)
        {
            columns = 1;
            return;
        }

        const double gap =
            12;

        double availableWidth =
            grid.ActualWidth;

        if (availableWidth <= 0)
        {
            availableWidth =
                Math.Max(
                    300,
                    Width - 48);
        }

        columns =
            (int)Math.Floor(
                (availableWidth + gap) /
                (minimumCardWidth + gap));

        columns =
            Math.Clamp(
                columns,
                1,
                itemCount);

        int rows =
            (int)Math.Ceiling(
                itemCount /
                (double)columns);

        for (int i = 0;
             i < columns;
             i++)
        {
            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(
                            1,
                            GridUnitType.Star)
                });
        }

        for (int i = 0;
             i < rows;
             i++)
        {
            grid.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Auto
                });
        }
    }

    private static Border CreateGridCardBorder(
    int column,
    int columns)
    {
        double rightMargin =
            column < columns - 1
                ? 12
                : 0;

        Border border =
            new()
            {
                CornerRadius =
                    new CornerRadius(6),

                Padding =
                    new Thickness(0),

                BorderThickness =
                    new Thickness(1),

                Margin =
                    new Thickness(
                        0,
                        0,
                        rightMargin,
                        12),

                HorizontalAlignment =
                    HorizontalAlignment.Stretch
            };

        border.SetResourceReference(
            Border.BackgroundProperty,
            "CardBackgroundBrush");

        border.SetResourceReference(
            Border.BorderBrushProperty,
            "BorderBrush");

        return border;
    }

    // ============================================================
    // ФОРМАТИРОВАНИЕ
    // ============================================================

    private static string FormatTemperature(
        float? value)
    {
        return value.HasValue
            ? $"{value.Value:F1} °C"
            : SettingsService.L(
                    "Нет данных",
                    "No data");
    }

    private static string FormatPercent(
        float? value)
    {
        return value.HasValue
            ? $"{value.Value:F1} %"
            : SettingsService.L(
                    "Нет данных",
                    "No data");
    }

    private static double ClampPercent(
        float? value)
    {
        return value.HasValue
            ? Math.Clamp(
                value.Value,
                0,
                100)
            : 0;
    }

    private static double BytesToGb(
        long bytes)
    {
        return
            bytes /
            1024d /
            1024d /
            1024d;
    }

    private static string FormatSize(
        double gigabytes)
    {
        if (gigabytes >= 1024)
        {
            return
                SettingsService.L(
                $"{gigabytes / 1024:F2} ТБ",
                $"{gigabytes / 1024:F2} TB");
        }

        return
            SettingsService.L(
            $"{gigabytes:F0} ГБ",
            $"{gigabytes:F0} GB");
    }
}
