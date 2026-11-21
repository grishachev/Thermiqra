using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCHardwareMonitor;

internal static class UpdatePromptView
{
    internal static Window Create(UpdateInfo update, Window? owner, Action openRelease)
    {
        var work = SystemParameters.WorkArea;
        var dialog = new Window
        {
            Owner = owner,
            Title = SettingsService.L("Thermiqra — Обновление", "Thermiqra — Update"),
            Width = Math.Min(560, Math.Max(300, work.Width - 32)),
            Height = Math.Min(440, Math.Max(260, work.Height - 32)),
            MaxHeight = Math.Max(260, work.Height - 32),
            WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false
        };
        dialog.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(
            new Uri("/Thermiqra;component/UI/Appearance/ProgramInputStyles.xaml", UriKind.Relative)));
        dialog.SetResourceReference(Window.BackgroundProperty, "WindowBackgroundBrush");
        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        TextBlock Text(string text, int size = 13)
        {
            var block = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap };
            block.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryTextBrush");
            return block;
        }
        var title = Text(SettingsService.L($"Доступна Thermiqra {update.VersionText}", $"Thermiqra {update.VersionText} is available"), 20);
        title.FontWeight = FontWeights.Bold;
        title.SetResourceReference(TextBlock.FontFamilyProperty, "DisplayFontFamily");
        root.Children.Add(title);
        var current = Text(SettingsService.L($"Текущая версия: {update.CurrentVersionText}", $"Current version: {update.CurrentVersionText}"), 12);
        current.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
        current.Margin = new Thickness(0, 7, 0, 14);
        Grid.SetRow(current, 1); root.Children.Add(current);
        var content = new StackPanel();
        var heading = Text(SettingsService.L("Главное в обновлении", "Update highlights"), 14);
        heading.FontWeight = FontWeights.SemiBold;
        content.Children.Add(heading);
        var notes = Text(string.IsNullOrWhiteSpace(update.Summary)
            ? SettingsService.L("Описание доступно на странице выпуска.", "Release notes are available on the release page.")
            : update.Summary);
        notes.Margin = new Thickness(0, 10, 0, 16); notes.LineHeight = 21;
        content.Children.Add(notes);
        var hint = Text(SettingsService.L(
            "Обновление установится автоматически. Настройки и статистика сохранятся.",
            "The update installs automatically. Your settings and statistics will be preserved."), 12);
        hint.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
        content.Children.Add(hint);
        var scroll = new ScrollViewer
        {
            Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 0, 0, 16)
        };
        Grid.SetRow(scroll, 2); root.Children.Add(scroll);
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        Button Button(string label)
        {
            var button = new Button { Content = label, Padding = new Thickness(12, 8, 12, 8), MinHeight = 36, Margin = new Thickness(5, 4, 0, 0) };
            button.SetResourceReference(FrameworkElement.StyleProperty, "ThermiqraButtonStyle");
            button.SetResourceReference(Control.BackgroundProperty, "InputBackgroundBrush");
            button.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
            return button;
        }
        var full = Button(SettingsService.L("Все изменения ↗", "Full release notes ↗"));
        full.Click += (_, _) => openRelease();
        buttons.Children.Add(full);
        var later = Button(SettingsService.L("Позже", "Later"));
        later.IsCancel = true; later.Click += (_, _) => dialog.DialogResult = false;
        buttons.Children.Add(later);
        var install = Button(SettingsService.L("Обновить", "Update"));
        install.IsDefault = true; install.SetResourceReference(Control.BorderBrushProperty, "AccentBrush");
        install.Click += (_, _) => dialog.DialogResult = true;
        buttons.Children.Add(install);
        Grid.SetRow(buttons, 3); root.Children.Add(buttons);
        dialog.Content = root;
        return dialog;
    }
}
