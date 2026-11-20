using System.Windows;
using System.Windows.Controls;

namespace PCHardwareMonitor;

internal static partial class ProgramWindowChrome
{
    internal static FrameworkElement Details(string title, UIElement details)
    {
        StackPanel panel = new();
        Button toggle = new() { Content = "▸ " + title, HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 5, 0, 5), FontSize = 14 };
        details.Visibility = Visibility.Collapsed;
        toggle.Click += (_, _) =>
        {
            bool expanded = details.Visibility != Visibility.Visible;
            details.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
            toggle.Content = (expanded ? "▾ " : "▸ ") + title;
        };
        panel.Children.Add(toggle); panel.Children.Add(details); return panel;
    }
}
