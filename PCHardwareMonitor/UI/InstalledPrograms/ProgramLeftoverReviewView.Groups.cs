using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCHardwareMonitor;

internal sealed partial class ProgramLeftoverReviewView
{
    private void RenderGroup(ProgramLeftoverGroup group)
    {
        var items = _report.Found.Where(p => p.Group == group).ToList();
        if (items.Count == 0) return;
        string title = group switch
        {
            ProgramLeftoverGroup.Related => SettingsService.L("Файлы и записи программы", "Application files and registrations"),
            ProgramLeftoverGroup.UserData => SettingsService.L("Настройки и данные", "Settings and data"),
            ProgramLeftoverGroup.NeedsReview => SettingsService.L("Требуют проверки", "Review required"),
            _ => SettingsService.L("Защищены от очистки", "Protected from cleanup")
        };
        StackPanel body = new(); List<CheckBox> groupChecks = new(); StackPanel rows = new();
        foreach (var item in items)
        {
            Grid row = new() { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var check = CreateSelection(item);
            if (check != null) { check.Content = ""; check.Margin = new Thickness(0, 3, 8, 0); groupChecks.Add(check); row.Children.Add(check); }
            TextBox path = new() { Text = item.Kind == "Registry" ? ProgramRegistryIdentity.Display(item) : item.Path,
                IsReadOnly = true, BorderThickness = new Thickness(0), Background = Brushes.Transparent,
                Padding = new Thickness(0), FontSize = 14, TextWrapping = TextWrapping.Wrap, IsReadOnlyCaretVisible = false };
            path.SetResourceReference(Control.ForegroundProperty, "PrimaryTextBrush");
            path.SetResourceReference(TextBox.SelectionBrushProperty, "AccentBrush");
            if (check != null) System.Windows.Automation.AutomationProperties.SetName(check, path.Text);
            Grid.SetColumn(path, 1); row.Children.Add(path); rows.Children.Add(row);
        }
        if (groupChecks.Count > 0)
        {
            CheckBox all = new() { Content = title + $" ({items.Count})", IsChecked = true, FontSize = 15, FontWeight = FontWeights.SemiBold };
            ProgramWindowChrome.CheckBox(all); all.SetResourceReference(Control.ForegroundProperty, "PrimaryTextBrush");
            bool changing = false;
            all.Click += (_, _) => { changing = true; foreach (var check in groupChecks) check.IsChecked = all.IsChecked == true; changing = false; };
            void Synchronize(object sender, RoutedEventArgs args)
            {
                if (changing) return;
                int count = groupChecks.Count(c => c.IsChecked == true);
                all.IsChecked = count == groupChecks.Count ? true : count == 0 ? false : null;
            }
            foreach (var check in groupChecks) { check.Checked += Synchronize; check.Unchecked += Synchronize; }
            body.Children.Add(all);
        }
        else body.Children.Add(Text(title + $" ({items.Count})", 15, "AccentBrush", true));
        if (items.Count > 6) body.Children.Add(ProgramWindowChrome.Details(SettingsService.L($"Показать объекты ({items.Count})", $"Show items ({items.Count})"), rows));
        else body.Children.Add(rows);
        body.Children.Add(ProgramWindowChrome.Details(SettingsService.L("Основание и условия очистки", "Cleanup basis and conditions"),
            Text(string.Join("\n\n", items.Select(i => SettingsService.L(i.ReasonRu, i.ReasonEn)).Distinct()), 14, "SecondaryTextBrush")));
        var frame = ProgramWindowChrome.Frame(body, 12); frame.Margin = new Thickness(0, 4, 0, 8); _content.Children.Add(frame);
    }
}
