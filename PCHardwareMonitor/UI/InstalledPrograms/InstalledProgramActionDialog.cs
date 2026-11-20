using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCHardwareMonitor;

internal static class InstalledProgramActionDialog
{
    internal static bool Show(Window owner, string title, string text, string accept, bool confirm)
        => ShowCore(owner, title, text, accept, confirm, null);

    private static bool ShowCore(Window owner, string title, string text, string accept, bool confirm, FrameworkElement? option)
    {
        Window dialog = new()
        {
            Owner = owner, Title = title, Width = 620, SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false, FontFamily = owner.FontFamily, FontSize = 14
        };
        dialog.SetResourceReference(Window.BackgroundProperty, "WindowBackgroundBrush");
        dialog.SetResourceReference(Window.ForegroundProperty, "PrimaryTextBrush");
        if (owner.TryFindResource(typeof(Button)) is Style style) dialog.Resources[typeof(Button)] = style;
        StackPanel content = new() { Margin = new Thickness(18) };
        var heading = ProgramWindowChrome.Header(title.Replace("Thermiqra — ", ""), "");
        heading.Margin = new Thickness(0, 0, 0, 12); content.Children.Add(heading);
        Border card = new() { Padding = new Thickness(16), CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1) };
        card.SetResourceReference(Border.BackgroundProperty, "CardBackgroundBrush");
        card.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        card.SetResourceReference(Border.CornerRadiusProperty, "PanelCornerRadius");
        TextBlock message = new() { Text = text, FontSize = 14, LineHeight = 21, TextWrapping = TextWrapping.Wrap };
        message.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryTextBrush");
        card.Child = new ScrollViewer { Content = message, MaxHeight = 320,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        content.Children.Add(card);
        if (option != null) content.Children.Add(option);
        StackPanel buttons = new() { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        if (confirm)
        {
            Button cancel = new() { Content = SettingsService.L("Отмена", "Cancel"), IsCancel = true, FontSize = 14,
                MinWidth = 100, Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 10, 0) };
            cancel.Click += (_, _) => dialog.DialogResult = false;
            buttons.Children.Add(cancel);
        }
        Button proceed = new() { Content = accept, FontSize = 14, MinWidth = 110, Padding = new Thickness(14, 8, 14, 8) };
        proceed.Click += (_, _) => dialog.DialogResult = true;
        buttons.Children.Add(proceed);
        content.Children.Add(ProgramWindowChrome.Footer(buttons));
        dialog.Content = content;
        return dialog.ShowDialog() == true;
    }
}
