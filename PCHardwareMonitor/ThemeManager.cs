using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace PCHardwareMonitor;

public sealed record ThemeOption(
    string Key,
    string DisplayName);

public sealed record SkinOption(
    string Key,
    string DisplayName);


public static class ThemeManager
{
    // ============================================================
    // СКИНЫ
    // ============================================================

    public static IReadOnlyList<SkinOption> Skins { get; } =
        new List<SkinOption>
        {
            new("CyberTech", "Cyber Tech"),
            new("SteamPunk", "Steampunk"),
            new("FrostCore", "Frost Core"),
            new("MilitaryOps", "Military Ops")
        };


    // ============================================================
    // ЦВЕТОВЫЕ СХЕМЫ CYBER TECH
    // ============================================================

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
        ApplyAppearance(
            SettingsService.Current.SkinName,
            themeName);
    }


    public static void ApplyAppearance(
        string skinName,
        string themeName)
    {
        // ThemeName сохраняется только для совместимости со старыми settings.json.
        // Начиная с системы скинов каждый скин имеет собственную палитру.
        ThemeDefinition theme =
            skinName switch
            {
                "SteamPunk" => GetSteamPunkTheme(),
                "FrostCore" => GetFrostCoreTheme(),
                "MilitaryOps" => GetMilitaryOpsTheme(),
                _ => GetCyberTechTheme("SteelBlue")
            };

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

        bool steamPunk =
            skinName == "SteamPunk";

        bool frostCore =
            skinName == "FrostCore";

        bool militaryOps =
            skinName == "MilitaryOps";

        // Декор Cyber Tech в окне настроек показывается только
        // для самого Cyber Tech. Frost Core и Military Ops имеют
        // собственное оформление SettingsWindow.
        resources["CyberTechVisibility"] =
            !steamPunk &&
            !frostCore &&
            !militaryOps
                ? Visibility.Visible
                : Visibility.Collapsed;

        // Главный экран и RingGauge имеют отдельные полноценные
        // представления для всех четырёх скинов.
        resources["MainCyberTechVisibility"] =
            !steamPunk &&
            !frostCore &&
            !militaryOps
                ? Visibility.Visible
                : Visibility.Collapsed;

        resources["SteamPunkVisibility"] =
            steamPunk
                ? Visibility.Visible
                : Visibility.Collapsed;

        resources["FrostCoreVisibility"] =
            frostCore
                ? Visibility.Visible
                : Visibility.Collapsed;

        resources["MilitaryOpsVisibility"] =
            militaryOps
                ? Visibility.Visible
                : Visibility.Collapsed;

        resources["PanelCornerRadius"] =
            steamPunk
                ? new CornerRadius(10)
                : frostCore
                    ? new CornerRadius(12)
                    : militaryOps
                        ? new CornerRadius(2)
                        : new CornerRadius(6);

        resources["ButtonCornerRadius"] =
            steamPunk
                ? new CornerRadius(8)
                : frostCore
                    ? new CornerRadius(8)
                    : militaryOps
                        ? new CornerRadius(2)
                        : new CornerRadius(4);

        resources["MiniCardCornerRadius"] =
            steamPunk
                ? new CornerRadius(6)
                : frostCore
                    ? new CornerRadius(8)
                    : militaryOps
                        ? new CornerRadius(1)
                        : new CornerRadius(0);

        resources["ProgressCornerRadius"] =
            steamPunk
                ? new CornerRadius(4)
                : frostCore
                    ? new CornerRadius(6)
                    : militaryOps
                        ? new CornerRadius(1)
                        : new CornerRadius(2);

        resources["DisplayFontFamily"] =
            steamPunk
                ? new FontFamily("Georgia")
                : militaryOps
                    ? new FontFamily("Bahnschrift")
                    : new FontFamily("Segoe UI");

        resources["SteamPanelBrush"] =
            steamPunk
                ? SteamPanelBrush()
                : Brush(theme.CardBackground);

        resources["SteamMetalBrush"] =
            steamPunk
                ? SteamMetalBrush()
                : Brush(theme.Border);

        resources["SteamDarkMetalBrush"] =
            steamPunk
                ? Brush("#171009")
                : Brush(theme.InputBackground);

        resources["SteamRivetBrush"] =
            steamPunk
                ? Brush("#C98942")
                : Brush(theme.Border);

        resources["SteamGlassBrush"] =
            steamPunk
                ? Brush("#24170D")
                : Brush(theme.CardBackground);

        resources["FrostPanelBrush"] =
            frostCore
                ? FrostPanelBrush()
                : Brush(theme.CardBackground);

        resources["FrostSteelBrush"] =
            frostCore
                ? FrostSteelBrush()
                : Brush(theme.Border);

        resources["FrostGlassBrush"] =
            frostCore
                ? Brush("#0D1A21")
                : Brush(theme.InputBackground);

        resources["FrostIceBrush"] =
            frostCore
                ? Brush("#BFF6FF")
                : Brush(theme.Accent);

        resources["MilitaryPanelBrush"] =
            militaryOps
                ? MilitaryPanelBrush()
                : Brush(theme.CardBackground);

        resources["MilitaryPlateBrush"] =
            militaryOps
                ? Brush("#202815")
                : Brush(theme.InputBackground);

        resources["MilitaryKhakiBrush"] =
            militaryOps
                ? Brush("#B6AD79")
                : Brush(theme.SecondaryText);

        resources["MilitaryGridBrush"] =
            militaryOps
                ? Brush("#344224")
                : Brush(theme.Border);

        // Унифицированные ресурсы окна настроек. Они позволяют
        // сохранить общую рабочую разметку и при этом дать каждому
        // скину собственный материал, рамки и геометрию.
        resources["SettingsCardBrush"] =
            steamPunk
                ? SteamPanelBrush()
                : frostCore
                    ? FrostPanelBrush()
                    : militaryOps
                        ? MilitaryPanelBrush()
                        : Brush(theme.CardBackground);

        resources["SettingsCardBorderBrush"] =
            steamPunk
                ? SteamMetalBrush()
                : frostCore
                    ? FrostSteelBrush()
                    : militaryOps
                        ? Brush("#B6AD79")
                        : Brush(theme.Border);

        resources["SettingsInputBrush"] =
            steamPunk
                ? Brush("#24170D")
                : frostCore
                    ? Brush("#0D1A21")
                    : militaryOps
                        ? Brush("#151A10")
                        : Brush(theme.InputBackground);
    }


    public static string GetSkinDisplayName(
        string key)
    {
        return Skins
            .FirstOrDefault(
                skin => skin.Key == key)
            ?.DisplayName
            ?? "Cyber Tech";
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


    private static ThemeDefinition GetSteamPunkTheme()
    {
        return new ThemeDefinition(
            WindowBackground:
                "#0E0A07",

            CardBackground:
                "#1A120C",

            PrimaryText:
                "#F4E3C1",

            SecondaryText:
                "#B99A73",

            Accent:
                "#F39A32",

            GaugeTrack:
                "#3A2718",

            InputBackground:
                "#120D09",

            Border:
                "#6E4A2A");
    }


    private static ThemeDefinition GetFrostCoreTheme()
    {
        return new ThemeDefinition(
            WindowBackground:
                "#081117",

            CardBackground:
                "#111C22",

            PrimaryText:
                "#EAF8FC",

            SecondaryText:
                "#8FAAB4",

            Accent:
                "#76E7FF",

            GaugeTrack:
                "#223841",

            InputBackground:
                "#0C161B",

            Border:
                "#4A6570");
    }


    private static ThemeDefinition GetMilitaryOpsTheme()
    {
        return new ThemeDefinition(
            WindowBackground:
                "#0A0D08",

            CardBackground:
                "#151B10",

            PrimaryText:
                "#E6E8D3",

            SecondaryText:
                "#96A07A",

            Accent:
                "#9BCF4A",

            GaugeTrack:
                "#2A351F",

            InputBackground:
                "#10140C",

            Border:
                "#4B5A37");
    }


    private static ThemeDefinition GetCyberTechTheme(
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


    private static Brush SteamPanelBrush()
    {
        LinearGradientBrush brush = new()
        {
            StartPoint =
                new Point(0, 0),
            EndPoint =
                new Point(1, 1)
        };

        brush.GradientStops.Add(
            new GradientStop(
                (Color)ColorConverter.ConvertFromString("#2A1B10"),
                0.0));

        brush.GradientStops.Add(
            new GradientStop(
                (Color)ColorConverter.ConvertFromString("#171009"),
                0.55));

        brush.GradientStops.Add(
            new GradientStop(
                (Color)ColorConverter.ConvertFromString("#0E0906"),
                1.0));

        return brush;
    }


    private static Brush SteamMetalBrush()
    {
        LinearGradientBrush brush = new()
        {
            StartPoint =
                new Point(0, 0),
            EndPoint =
                new Point(1, 0)
        };

        brush.GradientStops.Add(
            new GradientStop(
                (Color)ColorConverter.ConvertFromString("#6F4521"),
                0.0));

        brush.GradientStops.Add(
            new GradientStop(
                (Color)ColorConverter.ConvertFromString("#D08A3E"),
                0.45));

        brush.GradientStops.Add(
            new GradientStop(
                (Color)ColorConverter.ConvertFromString("#7C4E24"),
                1.0));

        return brush;
    }


    private static Brush FrostPanelBrush()
    {
        LinearGradientBrush brush = new()
        {
            StartPoint =
                new Point(0, 0),
            EndPoint =
                new Point(1, 1)
        };

        brush.GradientStops.Add(
            new GradientStop(
                (Color)ColorConverter.ConvertFromString("#1C2B33"),
                0.0));

        brush.GradientStops.Add(
            new GradientStop(
                (Color)ColorConverter.ConvertFromString("#111C22"),
                0.55));

        brush.GradientStops.Add(
            new GradientStop(
                (Color)ColorConverter.ConvertFromString("#0A1419"),
                1.0));

        return brush;
    }


    private static Brush FrostSteelBrush()
    {
        LinearGradientBrush brush = new()
        {
            StartPoint =
                new Point(0, 0),
            EndPoint =
                new Point(1, 0)
        };

        brush.GradientStops.Add(
            new GradientStop(
                (Color)ColorConverter.ConvertFromString("#3E5964"),
                0.0));

        brush.GradientStops.Add(
            new GradientStop(
                (Color)ColorConverter.ConvertFromString("#91B6C3"),
                0.48));

        brush.GradientStops.Add(
            new GradientStop(
                (Color)ColorConverter.ConvertFromString("#4B6873"),
                1.0));

        return brush;
    }


    private static Brush MilitaryPanelBrush()
    {
        // Статичный цифровой камуфляж. Это обычный DrawingBrush:
        // никаких Storyboard/анимаций и дополнительной нагрузки от кадровой отрисовки.
        DrawingGroup pattern = new();

        AddMilitaryCamoBlock(
            pattern,
            "#171D11",
            0, 0, 64, 64);

        AddMilitaryCamoBlock(
            pattern,
            "#10150C",
            0, 0, 24, 16);

        AddMilitaryCamoBlock(
            pattern,
            "#252D19",
            24, 0, 16, 8);

        AddMilitaryCamoBlock(
            pattern,
            "#202817",
            40, 0, 24, 16);

        AddMilitaryCamoBlock(
            pattern,
            "#2C341D",
            8, 16, 16, 16);

        AddMilitaryCamoBlock(
            pattern,
            "#11170D",
            24, 16, 24, 8);

        AddMilitaryCamoBlock(
            pattern,
            "#232B18",
            48, 16, 16, 24);

        AddMilitaryCamoBlock(
            pattern,
            "#29311B",
            0, 32, 16, 24);

        AddMilitaryCamoBlock(
            pattern,
            "#1E2615",
            16, 32, 24, 16);

        AddMilitaryCamoBlock(
            pattern,
            "#0F140B",
            40, 40, 16, 16);

        AddMilitaryCamoBlock(
            pattern,
            "#303720",
            56, 40, 8, 16);

        AddMilitaryCamoBlock(
            pattern,
            "#12180E",
            8, 56, 24, 8);

        AddMilitaryCamoBlock(
            pattern,
            "#28301B",
            32, 56, 24, 8);

        DrawingBrush brush = new(pattern)
        {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, 64, 64),
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, 64, 64),
            Stretch = Stretch.None
        };

        return brush;
    }


    private static void AddMilitaryCamoBlock(
        DrawingGroup group,
        string color,
        double x,
        double y,
        double width,
        double height)
    {
        group.Children.Add(
            new GeometryDrawing(
                Brush(color),
                null,
                new RectangleGeometry(
                    new Rect(
                        x,
                        y,
                        width,
                        height))));
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
