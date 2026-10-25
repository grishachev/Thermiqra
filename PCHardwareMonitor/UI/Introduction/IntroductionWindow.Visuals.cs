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
    private static UIElement BuildColorVisual()
    {
        StackPanel root =
            new();

        root.Children.Add(
            CreateVisualHeading(
                SettingsService.L(
                    "БЫСТРАЯ ОЦЕНКА",
                    "QUICK STATUS")));

        UniformGrid states =
            new()
            {
                Rows =
                    4,

                Columns =
                    1,

                Margin =
                    new Thickness(
                        0,
                        18,
                        0,
                        0)
            };

        states.Children.Add(
            CreateStatusRow(
                Color.FromRgb(
                    57,
                    178,
                    105),
                SettingsService.L(
                    "НОРМАЛЬНО",
                    "NORMAL"),
                SettingsService.L(
                    "Обычное рабочее состояние",
                    "Normal operating state")));

        states.Children.Add(
            CreateStatusRow(
                Color.FromRgb(
                    218,
                    184,
                    52),
                SettingsService.L(
                    "ПОВЫШЕНО",
                    "ELEVATED"),
                SettingsService.L(
                    "Стоит обратить внимание",
                    "Worth paying attention")));

        states.Children.Add(
            CreateStatusRow(
                Color.FromRgb(
                    240,
                    136,
                    38),
                SettingsService.L(
                    "ВЫСОКО",
                    "HIGH"),
                SettingsService.L(
                    "Показатель уже заметно выше обычного",
                    "The value is clearly above normal")));

        states.Children.Add(
            CreateStatusRow(
                Color.FromRgb(
                    213,
                    67,
                    67),
                SettingsService.L(
                    "КРИТИЧЕСКИ",
                    "CRITICAL"),
                SettingsService.L(
                    "Требует внимания",
                    "Needs attention")));

        root.Children.Add(
            states);

        return root;
    }

    private static UIElement BuildDetailsVisual()
    {
        StackPanel root =
            new();

        root.Children.Add(
            CreateVisualHeading(
                SettingsService.L(
                    "ПОДРОБНОСТИ БЕЗ ПЕРЕГРУЗКИ",
                    "DETAILS WITHOUT CLUTTER")));

        UniformGrid cards =
            new()
            {
                Rows =
                    1,

                Columns =
                    3,

                Margin =
                    new Thickness(
                        0,
                        20,
                        0,
                        0)
            };

        cards.Children.Add(
            CreateActionCard(
                "01",
                SettingsService.L(
                    "СИСТЕМА",
                    "SYSTEM"),
                SettingsService.L(
                    "Железо и понятный анализ состояния.",
                    "Hardware details and plain-language analysis.")));

        cards.Children.Add(
            CreateActionCard(
                "02",
                SettingsService.L(
                    "СТАТИСТИКА",
                    "STATISTICS"),
                SettingsService.L(
                    "История температур и нагрузки.",
                    "History of temperatures and load.")));

        cards.Children.Add(
            CreateActionCard(
                "03",
                SettingsService.L(
                    "АНАЛИЗ ДИСКА",
                    "DISK ANALYSIS"),
                SettingsService.L(
                    "Показывает, чем занято место.",
                    "Shows what uses disk space.")));

        root.Children.Add(
            cards);

        TextBlock note =
            new()
            {
                Text =
                    SettingsService.L(
                        "Все подробные разделы открываются из уже знакомого главного окна.",
                        "All detailed sections open from the familiar main window."),

                TextWrapping =
                    TextWrapping.Wrap,

                FontSize =
                    11,

                Margin =
                    new Thickness(
                        2,
                        22,
                        2,
                        0)
            };

        note.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        root.Children.Add(
            note);

        return root;
    }

    private static UIElement BuildTrayVisual()
    {
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

        root.Children.Add(
            CreateVisualHeading(
                SettingsService.L(
                    "МОНИТОРИНГ ПРОДОЛЖАЕТСЯ",
                    "MONITORING CONTINUES")));

        Border notification =
            CreateSubCard(
                new Thickness(
                    22));

        notification.Margin =
            new Thickness(
                0,
                20,
                0,
                18);

        Grid notificationGrid =
            new();

        notificationGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        notificationGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        Border iconFrame =
            new()
            {
                Width =
                    74,

                Height =
                    74,

                Margin =
                    new Thickness(
                        0,
                        0,
                        20,
                        0),

                Padding =
                    new Thickness(10),

                BorderThickness =
                    new Thickness(2),

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        iconFrame.SetResourceReference(
            Border.BorderBrushProperty,
            "AccentBrush");

        iconFrame.SetResourceReference(
            Border.CornerRadiusProperty,
            "PanelCornerRadius");

        iconFrame.Child =
            CreateThermiqraIcon(
                52);

        StackPanel message =
            new()
            {
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        TextBlock messageTitle =
            new()
            {
                Text =
                    SettingsService.L(
                        "Thermiqra работает в фоне",
                        "Thermiqra is running in the background"),

                FontSize =
                    18,

                FontWeight =
                    FontWeights.Bold
            };

        messageTitle.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        TextBlock messageText =
            new()
            {
                Text =
                    SettingsService.L(
                        "Мониторинг активен. Если что-то потребует внимания, программа сообщит.",
                        "Monitoring is active. If something needs attention, the program will let you know."),

                FontSize =
                    13,

                LineHeight =
                    20,

                TextWrapping =
                    TextWrapping.Wrap,

                Margin =
                    new Thickness(
                        0,
                        8,
                        0,
                        0)
            };

        messageText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        message.Children.Add(
            messageTitle);

        message.Children.Add(
            messageText);

        Grid.SetColumn(
            message,
            1);

        notificationGrid.Children.Add(
            iconFrame);

        notificationGrid.Children.Add(
            message);

        notification.Child =
            notificationGrid;

        Grid.SetRow(
            notification,
            1);

        root.Children.Add(
            notification);

        Border tray =
            CreateSubCard(
                new Thickness(
                    16,
                    12,
                    16,
                    12));

        Grid trayGrid =
            new();

        trayGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        trayGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        TextBlock trayText =
            new()
            {
                Text =
                    SettingsService.L(
                        "В трее ищите этот значок Thermiqra",
                        "Look for this Thermiqra icon in the tray"),

                FontSize =
                    13,

                FontWeight =
                    FontWeights.SemiBold,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        trayText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        StackPanel trayIcons =
            new()
            {
                Orientation =
                    Orientation.Horizontal,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        trayIcons.Children.Add(
            CreateTrayDot());

        Border thermiqraTray =
            new()
            {
                Width =
                    38,

                Height =
                    38,

                Margin =
                    new Thickness(
                        10,
                        0,
                        0,
                        0),

                Padding =
                    new Thickness(5),

                BorderThickness =
                    new Thickness(1)
            };

        thermiqraTray.SetResourceReference(
            Border.BorderBrushProperty,
            "AccentBrush");

        thermiqraTray.SetResourceReference(
            Border.CornerRadiusProperty,
            "PanelCornerRadius");

        thermiqraTray.Child =
            CreateThermiqraIcon(
                28);

        trayIcons.Children.Add(
            thermiqraTray);

        Grid.SetColumn(
            trayIcons,
            1);

        trayGrid.Children.Add(
            trayText);

        trayGrid.Children.Add(
            trayIcons);

        tray.Child =
            trayGrid;

        Grid.SetRow(
            tray,
            2);

        root.Children.Add(
            tray);

        return root;
    }


    private static UIElement CreateThermiqraIcon(
        double size)
    {
        string[] resourceUris =
        {
            "pack://application:,,,/Assets/Thermiqra_Tray.ico",
            "pack://application:,,,/Assets/Thermiqra.ico"
        };

        foreach (string resourceUri in
                 resourceUris)
        {
            try
            {
                BitmapDecoder decoder =
                    BitmapDecoder.Create(
                        new Uri(
                            resourceUri,
                            UriKind.Absolute),
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.OnLoad);

                BitmapFrame? bestFrame =
                    null;

                double bestScore =
                    double.MaxValue;

                foreach (BitmapFrame frame in
                         decoder.Frames)
                {
                    double frameSize =
                        Math.Min(
                            frame.PixelWidth,
                            frame.PixelHeight);

                    // Предпочитаем кадр не меньше нужного размера,
                    // чтобы не растягивать маленькую иконку.
                    double score =
                        frameSize >= size
                            ? frameSize - size
                            : (size - frameSize) + 1000;

                    if (score < bestScore)
                    {
                        bestScore =
                            score;

                        bestFrame =
                            frame;
                    }
                }

                if (bestFrame != null)
                {
                    Image image =
                        new()
                        {
                            Source =
                                bestFrame,

                            Width =
                                size,

                            Height =
                                size,

                            Stretch =
                                Stretch.Uniform,

                            SnapsToDevicePixels =
                                true,

                            UseLayoutRounding =
                                true
                        };

                    RenderOptions.SetBitmapScalingMode(
                        image,
                        BitmapScalingMode.HighQuality);

                    return image;
                }
            }
            catch
            {
            }
        }

        Grid fallback =
            new()
            {
                Width =
                    size,

                Height =
                    size
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
                    2,

                Height =
                    size *
                    0.72
            };

        vertical.SetResourceReference(
            Border.BackgroundProperty,
            "AccentBrush");

        Border horizontal =
            new()
            {
                Width =
                    size *
                    0.72,

                Height =
                    2
            };

        horizontal.SetResourceReference(
            Border.BackgroundProperty,
            "AccentBrush");

        Ellipse center =
            new()
            {
                Width =
                    size *
                    0.28,

                Height =
                    size *
                    0.28
            };

        center.SetResourceReference(
            Shape.FillProperty,
            "AccentBrush");

        fallback.Children.Add(
            ring);

        fallback.Children.Add(
            vertical);

        fallback.Children.Add(
            horizontal);

        fallback.Children.Add(
            center);

        return fallback;
    }
}
