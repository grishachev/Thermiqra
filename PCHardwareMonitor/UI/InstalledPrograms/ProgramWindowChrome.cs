using System.Windows;
using System.Windows.Controls;

namespace PCHardwareMonitor;

internal static partial class ProgramWindowChrome
{
    private sealed record HeaderData(string Title, string Subtitle);

    internal static FrameworkElement Header(string title, string subtitle)
    {
        ContentControl heading = new() { Content = new HeaderData(title, subtitle), Focusable = false,
            HorizontalContentAlignment = HorizontalAlignment.Stretch };
        heading.SetResourceReference(ContentControl.ContentTemplateProperty, "ProgramHeaderTemplate");
        return Frame(heading, 16);
    }

    internal static ContentControl Frame(UIElement content, double padding = 14)
    {
        ContentControl frame = new() { Content = content, Padding = new Thickness(padding), Focusable = false,
            HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        frame.SetResourceReference(Control.TemplateProperty, "ProgramPanelFrameTemplate");
        return frame;
    }

    internal static FrameworkElement Footer(UIElement actions)
    {
        DockPanel footer = new() { LastChildFill = true, Margin = new Thickness(0, 8, 0, 0) };
        Border line = new() { Height = 1, Margin = new Thickness(0, 0, 0, 10) };
        line.SetResourceReference(Border.BackgroundProperty, "BorderBrush"); DockPanel.SetDock(line, Dock.Top);
        footer.Children.Add(line); footer.Children.Add(actions); return footer;
    }

    internal static void CheckBox(CheckBox check) => check.SetResourceReference(FrameworkElement.StyleProperty, "ProgramCheckBoxStyle");
}
