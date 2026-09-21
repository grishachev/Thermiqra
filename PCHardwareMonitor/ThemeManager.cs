using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace PCHardwareMonitor;

public sealed record ThemeOption(
    string Key,
    string DisplayName);


public static class ThemeManager
{
    public static IReadOnlyList<ThemeOption> Themes { get; } =
        new List<ThemeOption>
        {
            new("SteelBlue", "Steel Blue"),
            new("GraphiteGreen", "Graphite Green"),
            new("VioletNeon", "Violet Neon"),
            new("LightTech", "Light Tech")
        };


    public static void ApplyTheme(
        string themeName)
    {
        ThemeDefinition theme =
            GetTheme(themeName);

        ResourceDictionary resources =
            Application.Current.Resources;

        resources["WindowBackgroundBrush"] =
            Brush(theme.WindowBackground);

        resources["CardBackgroundBrush"] =
            Brush(theme.CardBackground);

        resources["PrimaryTextBrush"] =
            Brush(theme.PrimaryText);

        resources["SecondaryTextBrush"] =
            Brush(theme.SecondaryText);

        resources["AccentBrush"] =
            Brush(theme.Accent);

        resources["GaugeTrackBrush"] =
            Brush(theme.GaugeTrack);

        resources["InputBackgroundBrush"] =
            Brush(theme.InputBackground);

        resources["BorderBrush"] =
            Brush(theme.Border);
    }


    public static string GetDisplayName(
        string key)
    {
        return Themes
            .FirstOrDefault(
                theme => theme.Key == key)
            ?.DisplayName
            ?? "Steel Blue";
    }


    private static ThemeDefinition GetTheme(
        string themeName)
    {
        return themeName switch
        {
            // =====================================================
            // GRAPHITE GREEN
            // =====================================================

            "GraphiteGreen" =>
                new ThemeDefinition(
                    WindowBackground:
                        "#08100E",

                    CardBackground:
                        "#0E1A17",

                    PrimaryText:
                        "#EAF8F2",

                    SecondaryText:
                        "#739487",

                    Accent:
                        "#35E0A1",

                    GaugeTrack:
                        "#17372D",

                    InputBackground:
                        "#0B1613",

                    Border:
                        "#244C3E"),


            // =====================================================
            // VIOLET NEON
            // =====================================================

            "VioletNeon" =>
                new ThemeDefinition(
                    WindowBackground:
                        "#0E0A16",

                    CardBackground:
                        "#181122",

                    PrimaryText:
                        "#F5F0FF",

                    SecondaryText:
                        "#9C87B6",

                    Accent:
                        "#B56CFF",

                    GaugeTrack:
                        "#342448",

                    InputBackground:
                        "#130D1C",

                    Border:
                        "#4A3263"),


            // =====================================================
            // LIGHT TECH
            // =====================================================

            "LightTech" =>
                new ThemeDefinition(
                    WindowBackground:
                        "#E8F0F4",

                    CardBackground:
                        "#F8FBFD",

                    PrimaryText:
                        "#15242C",

                    SecondaryText:
                        "#607781",

                    Accent:
                        "#087FA8",

                    GaugeTrack:
                        "#D1E0E6",

                    InputBackground:
                        "#FFFFFF",

                    Border:
                        "#B4CBD4"),


            // =====================================================
            // STEEL BLUE — ОСНОВНАЯ КИБЕРТЕХ-ТЕМА
            // =====================================================

            _ =>
                new ThemeDefinition(
                    WindowBackground:
                        "#071116",

                    CardBackground:
                        "#0D1A21",

                    PrimaryText:
                        "#EAF8FF",

                    SecondaryText:
                        "#74909E",

                    Accent:
                        "#2ED9FF",

                    GaugeTrack:
                        "#183642",

                    InputBackground:
                        "#0A171D",

                    Border:
                        "#234754")
        };
    }


    private static SolidColorBrush Brush(
        string color)
    {
        return new SolidColorBrush(
            (Color)
            ColorConverter.ConvertFromString(
                color));
    }


    private sealed record ThemeDefinition(
        string WindowBackground,
        string CardBackground,
        string PrimaryText,
        string SecondaryText,
        string Accent,
        string GaugeTrack,
        string InputBackground,
        string Border);
}



public static class UiBrushes
{
    public static Brush Theme(
        string resourceName)
    {
        return
            Application.Current.TryFindResource(
                resourceName) as Brush
            ?? Brushes.White;
    }


    // ============================================================
    // НАГРУЗКА CPU / GPU
    // Норма — цвет темы.
    // Повышенная нагрузка — предупреждающие цвета.
    // ============================================================

    public static Brush Load(
        double load)
    {
        if (load >= 90)
            return Brush("#FF453A");

        if (load >= 75)
            return Brush("#FF9F0A");

        if (load >= 55)
            return Brush("#FFD60A");

        return Theme(
            "AccentBrush");
    }


    // ============================================================
    // ЗАПОЛНЕНИЕ ДИСКОВ
    // ============================================================

    public static Brush DiskUsage(
        double percent)
    {
        if (percent >= 95)
            return Brush("#FF453A");

        if (percent >= 85)
            return Brush("#FF8C00");

        if (percent >= 70)
            return Brush("#FFD60A");

        return Theme(
            "AccentBrush");
    }


    // ============================================================
    // ТЕМПЕРАТУРА НАКОПИТЕЛЕЙ
    // ============================================================

    public static Brush StorageTemperature(
        float? temperature)
    {
        if (!temperature.HasValue)
            return Brushes.Gray;

        if (temperature.Value >= 70)
            return Brush("#FF453A");

        if (temperature.Value >= 60)
            return Brush("#FF9F0A");

        if (temperature.Value >= 50)
            return Brush("#FFD60A");

        return Theme(
            "AccentBrush");
    }


    // ============================================================
    // ТЕМПЕРАТУРА CPU
    // ============================================================

    public static Brush CpuTemperature(
        float? temperature)
    {
        if (!temperature.HasValue)
            return Brushes.Gray;

        if (temperature.Value >= 90)
            return Brush("#FF453A");

        if (temperature.Value >= 80)
            return Brush("#FF9F0A");

        if (temperature.Value >= 70)
            return Brush("#FFD60A");

        return Theme(
            "PrimaryTextBrush");
    }


    // ============================================================
    // ТЕМПЕРАТУРА GPU
    // ============================================================

    public static Brush GpuTemperature(
        float? temperature)
    {
        if (!temperature.HasValue)
            return Brushes.Gray;

        if (temperature.Value >= 85)
            return Brush("#FF453A");

        if (temperature.Value >= 78)
            return Brush("#FF9F0A");

        if (temperature.Value >= 70)
            return Brush("#FFD60A");

        return Theme(
            "PrimaryTextBrush");
    }


    private static Brush Brush(
        string color)
    {
        return new SolidColorBrush(
            (Color)
            ColorConverter.ConvertFromString(
                color));
    }
}
