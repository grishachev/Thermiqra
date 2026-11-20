using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCHardwareMonitor;

// The same compact summary is used by all four header designs.
public sealed class DriveCapacitySummary : UserControl
{
    private readonly TextBlock[] _values = new TextBlock[3];
    public DriveCapacitySummary()
    {
        Focusable = false;
        Grid grid = new();
        string[] labels = { SettingsService.L("СВОБОДНО", "FREE"), SettingsService.L("ЗАНЯТО", "USED"), SettingsService.L("ВСЕГО", "TOTAL") };
        for (int i = 0; i < 3; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            StackPanel cell = new() { Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            TextBlock label = new() { Text = labels[i], FontSize = 10, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
            label.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
            _values[i] = new TextBlock { Text = "—", FontSize = 30, FontWeight = FontWeights.Bold };
            _values[i].SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            cell.Children.Add(label);
            cell.Children.Add(new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly,
                HorizontalAlignment = HorizontalAlignment.Left, Height = 38, Child = _values[i] });
            Grid.SetColumn(cell, i); grid.Children.Add(cell);
        }
        Content = grid;
        AutomationProperties.SetName(this, SettingsService.L("Место на диске", "Drive capacity"));
    }

    internal void Update(long? total, long? free)
    {
        var values = DriveCapacityValues.Create(total, free);
        long?[] bytes = { values.Free, values.Used, values.Total };
        for (int i = 0; i < bytes.Length; i++) _values[i].Text = bytes[i] is long value ? StorageAnalyzerWindow.FormatBytes(value) : "—";
        ToolTip = SettingsService.L("Свободное и занятое место по сведениям Windows.", "Free and used space reported by Windows.");
    }
}

internal sealed record DriveCapacityValues(long? Total, long? Free, long? Used)
{
    internal static DriveCapacityValues Create(long? total, long? free) => total is > 0 && free is >= 0 && free <= total
        ? new(total, free, total - free) : new(null, null, null);
}
