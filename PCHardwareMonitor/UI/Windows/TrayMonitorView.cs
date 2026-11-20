using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;

namespace PCHardwareMonitor;

internal sealed record TrayMonitorReading(float? CpuTemperature, float? CpuLoad,
    float? GpuTemperature, float? GpuLoad, float? MemoryLoad, float? UsedGb, float? TotalGb)
{
    internal static readonly TrayMonitorReading Empty = new(null, null, null, null, null, null, null);

    internal static float? Valid(float? value) => value.HasValue && float.IsFinite(value.Value) ? value : null;
    internal static float? Percent(float? value) => Valid(value) is float v && v >= 0 && v <= 100 ? v : null;

    internal static TrayMonitorReading From(HardwareSnapshot snapshot)
    {
        // Temperature and load refer to the SAME adapter, including multi-GPU systems.
        var gpu = snapshot.Gpus.OrderByDescending(g => Valid(g.Temperature) ?? float.MinValue)
            .ThenByDescending(g => Percent(g.Load) ?? -1).FirstOrDefault();
        return new(Valid(snapshot.Cpu.Temperature), Percent(snapshot.Cpu.Load), Valid(gpu?.Temperature),
            Percent(gpu?.Load), Percent(snapshot.Memory.Load), Valid(snapshot.Memory.UsedGb), Valid(snapshot.Memory.TotalGb));
    }
}

// One drawing surface keeps all three instruments aligned at every DPI. Values and
// units are measured as ONE typographic run before centering, including missing data.
internal sealed class TrayMonitorView : FrameworkElement
{
    private TrayMonitorReading _reading = TrayMonitorReading.Empty;
    private string _skin = "CyberTech";
    private readonly ResourceDictionary _palette = new();
    internal TrayMonitorReading Reading => _reading;
    internal string Skin => _skin;
    internal double PanelHeight => _skin == "SteamPunk" ? 128 : 104;

    internal TrayMonitorView()
    {
        Width = 286;
        Focusable = false;
        RefreshAppearance();
    }

    internal void RefreshAppearance()
    {
        _skin = SettingsService.Current.SkinName;
        ThemeManager.PopulateAppearance(_palette, _skin, SettingsService.Current.ThemeName);
        Height = PanelHeight;
        InvalidateMeasure();
        UpdateReading(_reading);
    }

    internal void UpdateReading(TrayMonitorReading reading)
    {
        _reading = reading;
        AutomationProperties.SetName(this,
            $"CPU {Number(reading.CpuTemperature, "°C")}, GPU {Number(reading.GpuTemperature, "°C")}, RAM {Number(reading.MemoryLoad, "%")}");
        InvalidateVisual();
    }

    private Brush Brush(string name) => (Brush)_palette[name];
    private static string Number(float? number, string unit) => TrayMonitorReading.Valid(number) is float v
        ? v.ToString("F0", CultureInfo.CurrentCulture) + " " + unit : "—";
    private static string Load(float? number) => SettingsService.L("Загрузка", "Load") + " " + Number(number, "%");

    internal string[] DetailLabels() => new[]
    {
        Load(_reading.CpuLoad), Load(_reading.GpuLoad),
        _reading.UsedGb is >= 0 && _reading.TotalGb is > 0
            ? $"{_reading.UsedGb.Value.ToString("F1", CultureInfo.CurrentCulture)} / {_reading.TotalGb.Value.ToString("F1", CultureInfo.CurrentCulture)} " + SettingsService.L("ГБ", "GB")
            : SettingsService.L("Нет данных", "No data")
    };

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        bool steam = _skin == "SteamPunk", frost = _skin == "FrostCore", military = _skin == "MilitaryOps";
        var frame = new Rect(1, 1, ActualWidth - 2, ActualHeight - 2);
        var accent = Brush("AccentBrush");
        var border = Brush("BorderBrush");
        var background = Brush("CardBackgroundBrush");
        var pen = new Pen(border, 1);
        double radius = steam ? 10 : frost ? 17 : military ? 0 : 3;
        if (!steam && !frost && !military)
        {
            var path = new StreamGeometry();
            using (var g = path.Open())
            {
                g.BeginFigure(new Point(1, 1), true, true);
                g.PolyLineTo(new[] {new Point(ActualWidth - 13, 1),new Point(ActualWidth - 1, 13),
                    new Point(ActualWidth - 1, ActualHeight - 1), new Point(13, ActualHeight - 1),new Point(1, ActualHeight - 13)}, true, false);
            }
            dc.DrawGeometry(background, pen, path);
            dc.DrawLine(new Pen(accent, 2), new Point(1, 1), new Point(47, 1));
            dc.DrawLine(new Pen(accent, 2), new Point(ActualWidth - 47, ActualHeight - 1), new Point(ActualWidth - 1, ActualHeight - 1));
        }
        else
            dc.DrawRoundedRectangle(background, pen, frame, radius, radius);

        if (steam)
        {
            dc.DrawRoundedRectangle(null, pen, new Rect(4, 4, ActualWidth - 8, ActualHeight - 8), 7, 7);
            foreach (double x in new[] { 8d, ActualWidth - 8 })
                foreach (double y in new[] { 8d, ActualHeight - 8 })
                {
                    dc.DrawEllipse(accent, null, new Point(x, y), 1.7, 1.7);
                    dc.DrawLine(pen, new Point(x - 1, y), new Point(x + 1, y));
                }
        }
        if (frost)
        {
            dc.PushOpacity(.12);
            dc.DrawRoundedRectangle(accent, null, new Rect(2, 2, ActualWidth - 4, 27), 15, 15);
            dc.Pop();
            dc.DrawLine(new Pen(accent, 1), new Point(ActualWidth - 35, 4), new Point(ActualWidth - 15, 4));
        }
        if (military)
        {
            dc.DrawRectangle(border, null, new Rect(1, 1, 4, ActualHeight - 2));
            dc.PushOpacity(.07);
            for (double x = 20; x < ActualWidth; x += 20)
                dc.DrawLine(pen, new Point(x, 1), new Point(x, ActualHeight - 1));
            dc.Pop();
        }

        var values = new[] {Number(_reading.CpuTemperature, "°C"), Number(_reading.GpuTemperature, "°C"), Number(_reading.MemoryLoad, "%")};
        var loads = new[] {_reading.CpuLoad, _reading.GpuLoad, _reading.MemoryLoad};
        var details = DetailLabels();
        string[] names = {"CPU", "GPU", "RAM"};
        const double cellWidth = 88;
        for (int i = 0; i < 3; i++)
        {
            double cx = ActualWidth / 2 + (i - 1) * cellWidth;
            if (military)
                dc.DrawLine(pen, new Point(cx - 34, 29), new Point(cx + 34, 29));
            if (!steam && !frost && !military && i > 0)
                dc.DrawLine(pen, new Point(cx - cellWidth / 2, 14), new Point(cx - cellWidth / 2, ActualHeight - 14));
            DrawText(dc, names[i], cx, steam ? 18 : 19, 11, Brush("SecondaryTextBrush"), "Segoe UI", 72);
            if (steam) DrawDial(dc, new Point(cx, 62), border, accent, background);
            DrawValue(dc, values[i], cx, steam ? 62 : 49, steam ? "Georgia" : military || _skin == "CyberTech" ? "Consolas" : "Segoe UI");
            DrawText(dc, details[i], cx, steam ? 108 : 76, 11, Brush("SecondaryTextBrush"), "Segoe UI", 82);
            if (!steam)
            {
                var track = new Rect(cx - 34, 90, 68, military ? 4 : 3);
                dc.PushOpacity(.15); dc.DrawRoundedRectangle(accent, null, track, frost ? 2 : 0, frost ? 2 : 0); dc.Pop();
                if (loads[i] is float load)
                    dc.DrawRoundedRectangle(accent, null, new Rect(track.X,track.Y,track.Width * load / 100,track.Height), frost ? 2 : 0, frost ? 2 : 0);
            }
        }
    }

    private static void DrawDial(DrawingContext dc, Point center, Brush metal, Brush accent, Brush background)
    {
        var gear = new StreamGeometry();
        using (var g = gear.Open())
            for (int i = 0; i < 96; i++)
            {
                double angle = i * Math.PI / 48;
                double r = i % 4 is 1 or 2 ? 36 : 33;
                var p = new Point(center.X + Math.Cos(angle) * r, center.Y + Math.Sin(angle) * r);
                if (i == 0) g.BeginFigure(p, true, true); else g.LineTo(p, true, false);
            }
        dc.PushOpacity(.35);dc.DrawGeometry(metal, null, gear);dc.Pop();
        dc.DrawEllipse(background, new Pen(metal, 1), center, 31, 31);
        dc.DrawEllipse(null, new Pen(accent, .7), center, 28, 28);
        dc.PushOpacity(.5);
        for (int i = 0; i < 20; i++)
        {
            double a = i * Math.PI / 10;
            dc.DrawLine(new Pen(metal, .8), new Point(center.X + Math.Cos(a) * 29, center.Y + Math.Sin(a) * 29),
                new Point(center.X + Math.Cos(a) * 31, center.Y + Math.Sin(a) * 31));
        }
        dc.Pop();
    }

    private FormattedText Text(string text, double size, Brush brush, string family) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(family), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private void DrawValue(DrawingContext dc, string value, double cx, double cy, string family)
    {
        var text = Text(value, 25, Brush("AccentBrush"), family);
        int separator = value.IndexOf(' ');
        if (separator >= 0)
        {
            text.SetFontSize(11, separator, value.Length - separator);
            text.SetFontFamily("Segoe UI", separator, value.Length - separator);
            text.SetForegroundBrush(Brush("PrimaryTextBrush"), separator, value.Length - separator);
        }
        // Measure ink rather than separate number/unit boxes: symmetric even for 100%,
        // minus signs, Georgia ascenders, missing readings and fractional DPI scales.
        var ink = text.BuildGeometry(new Point(0, 0)).Bounds;
        if (ink.Width > 53)
        {
            double scale = 53 / ink.Width;
            dc.PushTransform(new TranslateTransform(cx, cy));
            dc.PushTransform(new ScaleTransform(scale, scale));
            dc.DrawText(text, new Point(-ink.X - ink.Width / 2, -ink.Y - ink.Height / 2));
            dc.Pop();dc.Pop();
        }
        else dc.DrawText(text, new Point(cx - ink.X - ink.Width / 2, cy - ink.Y - ink.Height / 2));
    }

    private void DrawText(DrawingContext dc, string value, double cx, double cy, double size, Brush brush, string family, double maxWidth)
    {
        var text = Text(value, size, brush, family);
        text.MaxTextWidth = maxWidth;
        text.MaxLineCount = 1;
        text.TextAlignment = TextAlignment.Center;
        text.Trimming = TextTrimming.CharacterEllipsis;
        dc.DrawText(text, new Point(cx - maxWidth / 2, cy - text.Height / 2));
    }
}
