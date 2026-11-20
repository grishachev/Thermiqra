using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PCHardwareMonitor;

public sealed partial class RingGauge
{
    private sealed record GpuClockRow(TextBlock CoreLabel, TextBlock CoreValue,
        TextBlock MemoryLabel, TextBlock MemoryValue);

    private readonly List<GpuClockRow> _gpuClockRows = new();

    // Reuse each skin's existing mini-card factory so new readings remain
    // inside the original GPU frame and inherit its materials and geometry.
    private void AddGpuClockRow(Grid cards, string skin)
    {
        cards.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cards.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Border MakePlate() => skin switch
        {
            "Steam" => CreateSteamPlate(),
            "Frost" => CreateFrostPlate(),
            "Military" => CreateMilitaryPlate(),
            _ => CreateCyberMiniCard()
        };
        (TextBlock Label, TextBlock Value) AddCell(int column)
        {
            Border plate = MakePlate();
            plate.Margin = new Thickness(0, 8, 0, 0);
            StackPanel content = new();
            TextBlock label = new()
            {
                FontSize = 8, FontWeight = FontWeights.Bold,
                Height = 12, TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            label.SetResourceReference(TextBlock.ForegroundProperty,
                skin == "Military" ? "MilitaryKhakiBrush" : "SecondaryTextBrush");
            TextBlock value = new()
            {
                Text = "—", FontSize = skin == "Cyber" ? 14 : 17,
                FontWeight = FontWeights.Bold, Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.NoWrap
            };
            value.SetResourceReference(TextBlock.ForegroundProperty,
                skin == "Cyber" ? "PrimaryTextBrush" : "AccentBrush");
            if (skin is "Steam" or "Military")
                value.SetResourceReference(TextBlock.FontFamilyProperty, "DisplayFontFamily");
            content.Children.Add(label);
            // A stable value slot: five-digit MHz values scale down to fit,
            // without wrapping or changing either GPU/CPU card's row height.
            content.Children.Add(new Viewbox
            {
                Height = 23, Stretch = System.Windows.Media.Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Child = value
            });
            plate.Child = content;
            Grid.SetRow(plate, 1);
            Grid.SetColumn(plate, column);
            cards.Children.Add(plate);
            return (label, value);
        }
        var core = AddCell(0);
        var memory = AddCell(2);
        _gpuClockRows.Add(new(core.Label, core.Value, memory.Label, memory.Value));
    }

    public void UpdateGpuClocks(IReadOnlyList<HardwareClockInfo> clocks)
    {
        float? core = clocks.FirstOrDefault(c =>
            c.Name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase))?.Megahertz;
        float? memory = clocks.FirstOrDefault(c =>
            c.Name.Equals("GPU Memory", StringComparison.OrdinalIgnoreCase))?.Megahertz;
        foreach (GpuClockRow row in _gpuClockRows)
        {
            row.CoreLabel.Text = SettingsService.L("ЧАСТОТА GPU", "GPU CLOCK");
            row.MemoryLabel.Text = SettingsService.L("ЧАСТОТА VRAM", "VRAM CLOCK");
            row.CoreLabel.ToolTip = row.CoreLabel.Text;
            row.MemoryLabel.ToolTip = row.MemoryLabel.Text;
            row.CoreValue.Text = FormatClock(core);
            row.MemoryValue.Text = FormatClock(memory);
            row.CoreValue.ToolTip = core.HasValue ? null : SettingsService.L("Нет данных датчика", "Sensor data unavailable");
            row.MemoryValue.ToolTip = memory.HasValue ? SettingsService.L(
                "Частота, сообщённая датчиком видеопамяти; без пересчёта в скорость передачи данных",
                "Clock reported by the video memory sensor; not converted to a data transfer rate")
                : SettingsService.L("Нет данных датчика", "Sensor data unavailable");
        }
    }

    private static string FormatClock(float? value) => value.HasValue
        ? $"{value.Value:0} " + SettingsService.L("МГц", "MHz") : "—";
}
