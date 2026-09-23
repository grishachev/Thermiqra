using System;
using System.Linq;
using System.Threading;
using System.Windows;

namespace PCHardwareMonitor;

public partial class App : Application
{
    private const string SingleInstanceMutexName =
        @"Local\PCHardwareMonitor_SingleInstance";

    private const string ShutdownEventName =
        @"Local\Thermiqra_Shutdown_Request";

    private Mutex? _singleInstanceMutex;

    private EventWaitHandle? _shutdownEvent;

    private RegisteredWaitHandle? _shutdownRegistration;

    private MainWindow? _mainWindow;

    protected override void OnStartup(
        StartupEventArgs e)
    {
        base.OnStartup(e);

        SettingsService.Load();

        SettingsService.ApplyLanguagePreference();

        bool shutdownArgument =
            e.Args.Any(
                argument =>
                    string.Equals(
                        argument,
                        "--shutdown",
                        StringComparison.OrdinalIgnoreCase));

        if (shutdownArgument)
        {
            TrySignalShutdown();

            Shutdown();

            return;
        }

        _singleInstanceMutex =
            new Mutex(
                true,
                SingleInstanceMutexName,
                out bool createdNew);

        if (!createdNew)
        {
            MessageBox.Show(
                SettingsService.L(
                    "Thermiqra уже запущен.\n" +
                    "Проверь значок программы в системном трее.",
                    "Thermiqra is already running.\n" +
                    "Check the Thermiqra icon in the system tray."),
                "Thermiqra",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Shutdown();

            return;
        }

        _shutdownEvent =
            new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                ShutdownEventName);

        _shutdownRegistration =
            ThreadPool.RegisterWaitForSingleObject(
                _shutdownEvent,
                (_, timedOut) =>
                {
                    if (timedOut)
                        return;

                    Dispatcher.BeginInvoke(
                        new Action(
                            () =>
                            {
                                if (_mainWindow != null)
                                {
                                    _mainWindow.ExitApplication();
                                }
                                else
                                {
                                    Shutdown();
                                }
                            }));
                },
                null,
                Timeout.Infinite,
                false);

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

    private static void TrySignalShutdown()
    {
        try
        {
            using EventWaitHandle shutdownEvent =
                EventWaitHandle.OpenExisting(
                    ShutdownEventName);

            shutdownEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    protected override void OnExit(
        ExitEventArgs e)
    {
        _shutdownRegistration?
            .Unregister(null);

        _shutdownRegistration =
            null;

        _shutdownEvent?
            .Dispose();

        _shutdownEvent =
            null;

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