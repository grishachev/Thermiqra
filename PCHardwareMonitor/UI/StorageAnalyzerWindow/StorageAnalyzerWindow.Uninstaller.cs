using System;
using System.Windows;
using System.Windows.Controls;

namespace PCHardwareMonitor;

public partial class MainWindow
{
    private InstalledProgramsWindow? _installedProgramsWindow;

    private void InstalledProgramsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_installedProgramsWindow is { IsVisible: true } existing)
        {
            existing.Activate();
            return;
        }

        InstalledProgramsWindow window = new(this);
        _installedProgramsWindow = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_installedProgramsWindow, window))
                _installedProgramsWindow = null;
        };
        window.Show();
    }

    private void UpdateProgramsToolsLanguage()
    {
        ProgramsToolsLabel.Text = SettingsService.L("ИНСТРУМЕНТЫ", "TOOLS");
        InstalledProgramsButton.Content = SettingsService.L("Программы", "Programs");
    }

    private void ProgramsToolsHeader_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // On a narrow main window place the button below the theme-specific disk heading.
        bool compact = e.NewSize.Width < 920;
        Grid.SetRow(ProgramsToolsPanel, compact ? 1 : 0);
        Grid.SetColumn(ProgramsToolsPanel, compact ? 0 : 1);
        Grid.SetColumnSpan(ProgramsToolsPanel, compact ? 2 : 1);
        ProgramsToolsPanel.HorizontalAlignment = compact
            ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        ProgramsToolsPanel.Margin = compact
            ? new Thickness(4, 0, 0, 8)
            : new Thickness(12, 0, 4, 8);
    }

}
