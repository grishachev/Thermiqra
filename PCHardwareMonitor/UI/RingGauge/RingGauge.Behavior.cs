using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PCHardwareMonitor;

public sealed partial class RingGauge
{
    // ============================================================
    // DATA UPDATE
    // ============================================================

    public void Update(
        float? temperature,
        float? load,
        string hardwareName,
        string details)
    {
        RefreshLanguageIfNeeded();
        double loadValue =
            load.HasValue
                ? Math.Clamp(
                    load.Value,
                    0,
                    100)
                : 0;

        string temperatureText =
            temperature.HasValue
                ? $"{temperature.Value:F0} °C"
                : "-- °C";

        string loadText =
            load.HasValue
                ? $"{load.Value:F0} %"
                : "-- %";

        Brush temperatureBrush =
            _temperatureType == TemperatureType.Cpu
                ? UiBrushes.CpuTemperature(
                    temperature)
                : UiBrushes.GpuTemperature(
                    temperature);

        Brush loadBrush =
            UiBrushes.Load(
                loadValue);

        _cyberTemperatureText.Text =
            temperatureText;

        _cyberTemperatureText.Foreground =
            temperatureBrush;

        _cyberLoadText.Text =
            loadText;

        _cyberLoadText.Foreground =
            loadBrush;

        _cyberHardwareNameText.Text =
            hardwareName;

        _steamTemperatureText.Text =
            temperatureText;

        _steamTemperatureText.Foreground =
            temperatureBrush;

        _steamLoadText.Text =
            load.HasValue
                ? $"{load.Value:F0} %"
                : "-- %";

        _steamLoadText.Foreground =
            loadBrush;

        _steamHardwareNameText.Text =
            hardwareName;

        _frostTemperatureText.Text =
            temperatureText;

        _frostTemperatureText.Foreground =
            temperatureBrush;

        _frostLoadText.Text =
            loadText;

        _frostLoadText.Foreground =
            loadBrush;

        _frostHardwareNameText.Text =
            hardwareName;

        _militaryTemperatureText.Text =
            temperatureText;

        _militaryTemperatureText.Foreground =
            temperatureBrush;

        _militaryLoadText.Text =
            loadText;

        _militaryLoadText.Foreground =
            loadBrush;

        _militaryHardwareNameText.Text =
            hardwareName;

        bool sensorAvailable =
            temperature.HasValue ||
            load.HasValue;

        if (sensorAvailable)
        {
            _cyberStatusText.Text =
                LocalizeText(
                    "ДАТЧИК АКТИВЕН");

            _cyberStatusText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "AccentBrush");

            _cyberStatusDot.SetResourceReference(
                Shape.FillProperty,
                "AccentBrush");

            _steamStatusText.Text =
                LocalizeText(
                    "ДАТЧИК АКТИВЕН");

            _steamStatusText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "AccentBrush");

            _steamStatusLamp.SetResourceReference(
                Shape.FillProperty,
                "AccentBrush");

            _frostStatusText.Text =
                LocalizeText(
                    "ДАТЧИК АКТИВЕН");

            _frostStatusText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "AccentBrush");

            _frostStatusDot.SetResourceReference(
                Shape.FillProperty,
                "FrostIceBrush");

            _militaryStatusText.Text =
                LocalizeText(
                    "ДАТЧИК АКТИВЕН");

            _militaryStatusText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "AccentBrush");

            _militaryStatusDot.SetResourceReference(
                Shape.FillProperty,
                "AccentBrush");
        }
        else
        {
            _cyberStatusText.Text =
                LocalizeText(
                    "НЕТ ДАННЫХ");

            _cyberStatusText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            _cyberStatusDot.Fill =
                Brushes.Gray;

            _steamStatusText.Text =
                LocalizeText(
                    "НЕТ ДАННЫХ");

            _steamStatusText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            _steamStatusLamp.Fill =
                Brushes.Gray;

            _frostStatusText.Text =
                LocalizeText(
                    "НЕТ ДАННЫХ");

            _frostStatusText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            _frostStatusDot.Fill =
                Brushes.Gray;

            _militaryStatusText.Text =
                LocalizeText(
                    "НЕТ ДАННЫХ");

            _militaryStatusText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            _militaryStatusDot.Fill =
                Brushes.Gray;
        }

        if (_temperatureType ==
            TemperatureType.Gpu)
        {
            UpdateGpuDetails(
                details);
        }

        SetProgress(
            loadValue);
    }

    private void UpdateGpuDetails(
        string details)
    {
        string hotSpot =
            ExtractDetailValue(
                details,
                "Hot Spot");

        string vram =
            ExtractDetailValue(
                details,
                "VRAM");

        if (_cyberHotSpotValueText != null)
        {
            _cyberHotSpotValueText.Text =
                hotSpot;
        }

        if (_cyberVramValueText != null)
        {
            _cyberVramValueText.Text =
                vram;
        }

        if (_steamHotSpotValueText != null)
        {
            _steamHotSpotValueText.Text =
                hotSpot;
        }

        if (_steamVramValueText != null)
        {
            _steamVramValueText.Text =
                vram;
        }

        if (_frostHotSpotValueText != null)
        {
            _frostHotSpotValueText.Text =
                hotSpot;
        }

        if (_frostVramValueText != null)
        {
            _frostVramValueText.Text =
                vram;
        }

        if (_militaryHotSpotValueText != null)
        {
            _militaryHotSpotValueText.Text =
                hotSpot;
        }

        if (_militaryVramValueText != null)
        {
            _militaryVramValueText.Text =
                vram;
        }
    }

    private static string ExtractDetailValue(
        string details,
        string label)
    {
        if (string.IsNullOrWhiteSpace(
                details))
        {
            return "--";
        }

        string[] parts =
            details.Split(
                '•',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        foreach (string part in parts)
        {
            if (!part.StartsWith(
                    label,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string value =
                part[label.Length..]
                    .Trim();

            return string.IsNullOrWhiteSpace(
                    value)
                ? "--"
                : value;
        }

        return "--";
    }

    private void SetProgress(
        double percent)
    {
        percent =
            Math.Clamp(
                percent,
                0,
                100);

        Brush loadBrush =
            UiBrushes.Load(
                percent);

        _cyberProgressArc.Stroke =
            loadBrush;

        _steamNeedle.Background =
            loadBrush;

        _steamNeedleTransform.Angle =
            -120 +
            (240.0 * percent / 100.0);

        _frostLoadFill.Height =
            118.0 * percent / 100.0;

        _frostLoadFill.Background =
            loadBrush;

        int activeMilitarySegments =
            percent <= 0
                ? 0
                : Math.Clamp(
                    (int)Math.Ceiling(
                        percent / 10.0),
                    0,
                    10);

        for (int i = 0;
             i < _militaryLoadSegments.Length;
             i++)
        {
            Border segment =
                _militaryLoadSegments[i];

            if (segment == null)
                continue;

            if (i < activeMilitarySegments)
            {
                segment.Background =
                    loadBrush;
            }
            else
            {
                segment.SetResourceReference(
                    BackgroundProperty,
                    "MilitaryGridBrush");
            }
        }

        if (percent <= 0)
        {
            _cyberProgressArc.Data =
                Geometry.Empty;

            return;
        }

        double safePercent =
            percent >= 100
                ? 99.999
                : percent;

        double startAngle =
            -90;

        double endAngle =
            startAngle +
            360.0 * safePercent /
            100.0;

        Point startPoint =
            GetCyberPointOnCircle(
                startAngle);

        Point endPoint =
            GetCyberPointOnCircle(
                endAngle);

        PathFigure figure = new()
        {
            StartPoint = startPoint,
            IsClosed = false
        };

        figure.Segments.Add(
            new ArcSegment
            {
                Point = endPoint,
                Size = new Size(
                    CyberRadius,
                    CyberRadius),
                SweepDirection =
                    SweepDirection.Clockwise,
                IsLargeArc =
                    safePercent > 50
            });

        PathGeometry geometry =
            new();

        geometry.Figures.Add(
            figure);

        _cyberProgressArc.Data =
            geometry;
    }

    private static Point GetCyberPointOnCircle(
        double angleDegrees)
    {
        double angleRadians =
            angleDegrees *
            Math.PI /
            180.0;

        return new Point(
            CyberCenter +
            CyberRadius *
            Math.Cos(
                angleRadians),

            CyberCenter +
            CyberRadius *
            Math.Sin(
                angleRadians));
    }

    private void RefreshLanguageIfNeeded(
        bool force = false)
    {
        string languageCode =
            SettingsService.IsRussian
                ? "ru"
                : "en";

        if (!force &&
            string.Equals(
                _appliedLanguageCode,
                languageCode,
                StringComparison.Ordinal))
        {
            return;
        }

        ApplyLanguageToVisualTree(
            this);

        _appliedLanguageCode =
            languageCode;
    }


    private static void ApplyLanguageToVisualTree(
        DependencyObject root)
    {
        if (root is TextBlock textBlock &&
            !string.IsNullOrEmpty(
                textBlock.Text))
        {
            textBlock.Text =
                LocalizeExistingText(
                    textBlock.Text);
        }

        int childCount =
            VisualTreeHelper.GetChildrenCount(
                root);

        for (int i = 0;
             i < childCount;
             i++)
        {
            ApplyLanguageToVisualTree(
                VisualTreeHelper.GetChild(
                    root,
                    i));
        }
    }


    private static string LocalizeExistingText(
        string text)
    {
        if (SettingsService.IsRussian)
        {
            foreach (KeyValuePair<string, string> pair
                     in RussianToEnglishText)
            {
                if (string.Equals(
                        pair.Value,
                        text,
                        StringComparison.Ordinal))
                {
                    return pair.Key;
                }
            }

            return text;
        }

        return RussianToEnglishText.TryGetValue(
                text,
                out string? english)
            ? english
            : text;
    }


    private static string LocalizeText(
        string russian)
    {
        if (SettingsService.IsRussian)
            return russian;

        return RussianToEnglishText.TryGetValue(
                russian,
                out string? english)
            ? english
            : russian;
    }


    private Border CreateCpuThreadsButton(
        string backgroundResource,
        string borderResource,
        string foregroundResource,
        CornerRadius cornerRadius)
    {
        Border button = new()
        {
            Padding =
                new Thickness(
                    10,
                    6,
                    10,
                    6),
            Margin =
                new Thickness(
                    10,
                    0,
                    0,
                    0),
            BorderThickness =
                new Thickness(1),
            CornerRadius =
                cornerRadius,
            VerticalAlignment =
                VerticalAlignment.Center,
            Cursor =
                System.Windows.Input.Cursors.Hand
        };

        button.SetResourceReference(
            Border.BackgroundProperty,
            backgroundResource);

        button.SetResourceReference(
            Border.BorderBrushProperty,
            borderResource);

        TextBlock text = new()
        {
            Text = "ПОТОКИ",
            FontSize = 9,
            FontWeight =
                FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        text.SetResourceReference(
            TextBlock.ForegroundProperty,
            foregroundResource);

        button.MouseEnter +=
            (_, _) =>
            {
                button.SetResourceReference(
                    Border.BackgroundProperty,
                    "AccentBrush");

                button.SetResourceReference(
                    Border.BorderBrushProperty,
                    "AccentBrush");

                text.SetResourceReference(
                    TextBlock.ForegroundProperty,
                    "WindowBackgroundBrush");
            };

        button.MouseLeave +=
            (_, _) =>
            {
                button.SetResourceReference(
                    Border.BackgroundProperty,
                    backgroundResource);

                button.SetResourceReference(
                    Border.BorderBrushProperty,
                    borderResource);

                text.SetResourceReference(
                    TextBlock.ForegroundProperty,
                    foregroundResource);
            };

        button.Child =
            text;

        button.MouseLeftButtonDown +=
            (_, e) =>
            {
                e.Handled =
                    true;

                CpuThreadsRequested?.Invoke(
                    this,
                    EventArgs.Empty);
            };

        return button;
    }


    private static TextBlock CreateTechLabel(
        string text)
    {
        TextBlock block = new()
        {
            Text = text,
            FontSize = 9,
            FontWeight = FontWeights.SemiBold
        };

        block.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        return block;
    }

    private static TextBlock CreateAccentLabel(
        string text)
    {
        TextBlock block = new()
        {
            Text = text,
            FontSize = 9,
            FontWeight = FontWeights.Bold
        };

        block.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        return block;
    }
}
