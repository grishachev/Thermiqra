using System;
using System.Linq;
using System.Threading;
using System.Windows;

namespace PCHardwareMonitor;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;

    private MainWindow? _mainWindow;

    protected override void OnStartup(
        StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex =
            new Mutex(
                true,
                @"Local\PCHardwareMonitor_SingleInstance",
                out bool createdNew);

        if (!createdNew)
        {
            MessageBox.Show(
                "Thermiqra уже запущен.\n" +
                "Проверь значок программы в системном трее.",
                "Thermiqra",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Shutdown();
            return;
        }

        SettingsService.Load();

        ThemeManager.ApplyTheme(
            SettingsService
                .Current
                .ThemeName);

        _mainWindow =
            new MainWindow();

        MainWindow =
            _mainWindow;

        _mainWindow.StartMonitoring();

        bool firstRun =
            !SettingsService
                .Current
                .FirstRunCompleted;

        bool backgroundArgument =
            e.Args.Any(
                argument =>
                    string.Equals(
                        argument,
                        "--background",
                        StringComparison.OrdinalIgnoreCase));

        if (firstRun)
        {
            _mainWindow.ShowMainWindow();

            _mainWindow.OpenSettings(
                firstRun: true);

            return;
        }

        if (!backgroundArgument &&
            SettingsService
                .Current
                .ShowMainWindowOnStartup)
        {
            _mainWindow.ShowMainWindow();
        }

        // Иначе окно не показываем.
        // Программа уже работает в трее.
    }

    protected override void OnExit(
        ExitEventArgs e)
    {
        try
        {
            _singleInstanceMutex?
                .ReleaseMutex();
        }
        catch
        {
        }

        _singleInstanceMutex?
            .Dispose();

        base.OnExit(e);
    }
}
