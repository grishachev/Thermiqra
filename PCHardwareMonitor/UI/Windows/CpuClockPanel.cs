using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace PCHardwareMonitor;

/// <summary>CPU clocks are separate from logical-thread load: no guessed SMT mapping.</summary>
internal sealed class CpuClockPanel : Border
{
    private readonly TextBlock _title = new() { FontSize = 11, FontWeight = FontWeights.Bold };
    private readonly TextBlock _empty = new() { FontSize = 11, Margin = new Thickness(0, 8, 0, 0) };
    private readonly WrapPanel _tiles = new();
    private readonly Dictionary<string, (TextBlock Label, TextBlock Value)> _rows = new();
    private List<(string Key, int? Core, float Value)> _readings = new();
    private readonly Dictionary<string, float> _effectiveClocks = new(StringComparer.OrdinalIgnoreCase);

    internal CpuClockPanel()
    {
        Padding = new Thickness(14, 10, 6, 10);
        Margin = new Thickness(0, 0, 0, 12);
        CornerRadius = new CornerRadius(6);
        BorderThickness = new Thickness(1);
        SetResourceReference(BackgroundProperty, "CardBackgroundBrush");
        SetResourceReference(BorderBrushProperty, "BorderBrush");
        _title.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
        _empty.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
        StackPanel content = new();
        content.Children.Add(_title);
        content.Children.Add(_empty);
        content.Children.Add(_tiles);
        Child = content;
        RefreshLanguage();
    }

    internal void Update(IReadOnlyList<HardwareClockInfo> clocks)
    {
        _effectiveClocks.Clear();
        foreach (HardwareClockInfo clock in clocks)
        {
            const string suffix = " (Effective)";
            if (clock.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                _effectiveClocks[clock.Name[..^suffix.Length]] = clock.Megahertz;
        }
        List<(string Key, int? Core, float Value)> readings = new();
        foreach (HardwareClockInfo clock in clocks)
        {
            // Core clocks only. Excludes bus, memory and effective-clock
            // sensors, which have different meanings and cannot form one range.
            Match match = Regex.Match(clock.Name, @"^(?:CPU )?Core #([0-9]+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int core))
                readings.Add((clock.Name, core, clock.Megahertz));
        }
        if (readings.Count == 0)
        {
            HardwareClockInfo? average = clocks.FirstOrDefault(c =>
                c.Name.Equals("Cores (Average)", StringComparison.OrdinalIgnoreCase));
            if (average != null) readings.Add((average.Name, null, average.Megahertz));
        }
        readings = readings.OrderBy(r => r.Item2).ThenBy(r => r.Item1).ToList();
        if (!_readings.Select(r => r.Key).SequenceEqual(readings.Select(r => r.Item1)))
        {
            _tiles.Children.Clear();
            _rows.Clear();
            foreach (var reading in readings)
            {
                TextBlock label = new() { FontSize = 10 };
                label.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
                TextBlock value = new() { FontSize = 15, Height = 21,
                    TextWrapping = TextWrapping.NoWrap, FontWeight = FontWeights.SemiBold };
                value.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
                StackPanel tile = new() { Width = 126, Margin = new Thickness(0, 8, 8, 0) };
                tile.Children.Add(label);
                tile.Children.Add(value);
                _tiles.Children.Add(tile);
                _rows[reading.Item1] = (label, value);
            }
        }
        _readings = readings;
        _empty.Visibility = readings.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshLanguage();
    }

    internal void RefreshLanguage()
    {
        _title.Text = SettingsService.L("ЧАСТОТЫ ЯДЕР", "CORE CLOCKS");
        _empty.Text = SettingsService.L("Нет данных датчиков частоты", "Core clock data unavailable");
        foreach (var reading in _readings)
        {
            var row = _rows[reading.Key];
            row.Label.Text = reading.Core.HasValue
                ? SettingsService.L($"Ядро {reading.Core}", $"Core {reading.Core}")
                : SettingsService.L("Средняя частота", "Average clock");
            row.Value.Text = $"{reading.Value / 1000f:0.00} " + SettingsService.L("ГГц", "GHz");
            string effective = _effectiveClocks.TryGetValue(reading.Key, out float value)
                ? SettingsService.L($"\nЭффективная частота: {value:0} МГц",
                    $"\nEffective clock: {value:0} MHz")
                : SettingsService.L("\nЭффективная частота недоступна", "\nEffective clock unavailable");
            row.Value.ToolTip = SettingsService.L(
                $"Датчик {reading.Key}: {reading.Value:0} МГц\nЧастота ядра может оставаться высокой при малой нагрузке.",
                $"Sensor {reading.Key}: {reading.Value:0} MHz\nCore clock may remain high at low load.") + effective;
        }
    }
}
