using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace PCHardwareMonitor;

public enum NotificationType
{
    Warning,
    Critical,
    Info
}


public sealed class NotificationService : IDisposable
{
    private const int MaxVisibleNotifications = 4;

    private const uint SndAsync = 0x0001;

    private const uint SndNoDefault = 0x0002;

    private const uint SndFileName = 0x00020000;

    private const double ScreenMargin =
        18;

    private const double NotificationGap =
        10;

    private readonly List<NotificationWindow> _windows =
        new();

    private bool _disposed;


    public void Show(
        NotificationType type,
        string title,
        string message,
        Action? clickAction = null)
    {
        if (_disposed ||
            !SettingsService.Current.NotificationsEnabled)
        {
            return;
        }

        Dispatcher dispatcher =
            Application.Current.Dispatcher;

        if (dispatcher.CheckAccess())
        {
            ShowCore(
                type,
                title,
                message,
                clickAction);

            return;
        }

        dispatcher.BeginInvoke(
            () =>
                ShowCore(
                    type,
                    title,
                    message,
                    clickAction));
    }


    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed =
            true;

        Dispatcher dispatcher =
            Application.Current.Dispatcher;

        if (dispatcher.CheckAccess())
        {
            CloseAll();

            return;
        }

        dispatcher.Invoke(
            CloseAll);
    }


    private void ShowCore(
        NotificationType type,
        string title,
        string message,
        Action? clickAction)
    {
        if (_disposed)
        {
            return;
        }

        bool topmostWithoutActivation =
            type ==
            NotificationType.Critical &&
            SettingsService.Current.CriticalNotificationsTopmost;

        NotificationWindow window =
            new(
                type,
                title,
                message,
                topmostWithoutActivation,
                clickAction);

        window.Closed +=
            NotificationWindow_Closed;

        _windows.Add(
            window);

        while (_windows.Count >
               MaxVisibleNotifications)
        {
            NotificationWindow oldest =
                _windows[0];

            _windows.RemoveAt(
                0);

            oldest.Closed -=
                NotificationWindow_Closed;

            oldest.Close();
        }

        RepositionWindows();

        window.Show();

        PlayNotificationSound(
            type);
    }


    private void NotificationWindow_Closed(
        object? sender,
        EventArgs e)
    {
        if (sender is not NotificationWindow window)
        {
            return;
        }

        window.Closed -=
            NotificationWindow_Closed;

        _windows.Remove(
            window);

        RepositionWindows();
    }


    private void RepositionWindows()
    {
        if (_windows.Count ==
            0)
        {
            return;
        }

        Rect workArea =
            SystemParameters.WorkArea;

        double nextBottom =
            workArea.Bottom -
            ScreenMargin;

        foreach (NotificationWindow window
                 in _windows
                     .AsEnumerable()
                     .Reverse())
        {
            double left =
                workArea.Right -
                window.Width -
                ScreenMargin;

            double top =
                nextBottom -
                window.Height;

            window.SetPosition(
                left,
                top);

            nextBottom =
                top -
                NotificationGap;
        }
    }


    private static void PlayNotificationSound(
        NotificationType type)
    {
        if (!SettingsService
                .Current
                .NotificationSoundsEnabled)
        {
            return;
        }

        string fileName =
            GetSelectedSoundFile(
                type);

        string soundPath =
            Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                fileName);

        if (!File.Exists(
                soundPath))
        {
            return;
        }

        try
        {
            PlaySound(
                soundPath,
                IntPtr.Zero,
                SndAsync |
                SndNoDefault |
                SndFileName);
        }
        catch
        {
            // Ошибка воспроизведения звука не должна
            // мешать показу уведомления.
        }
    }


    private static string GetSelectedSoundFile(
        NotificationType type)
    {
        string selected =
            type switch
            {
                NotificationType.Critical =>
                    SettingsService.Current.CriticalSoundName,

                NotificationType.Info =>
                    SettingsService.Current.InfoSoundName,

                _ =>
                    SettingsService.Current.WarningSoundName
            };

        string fallback =
            type switch
            {
                NotificationType.Critical =>
                    "critical.wav",

                NotificationType.Info =>
                    "info.wav",

                _ =>
                    "warning.wav"
            };

        string[] allowed =
            type switch
            {
                NotificationType.Critical =>
                    new[]
                    {
                        "critical.wav",
                        "critical_siren.wav",
                        "critical_emergency.wav"
                    },

                NotificationType.Info =>
                    new[]
                    {
                        "info.wav",
                        "info_soft.wav",
                        "info_signal.wav"
                    },

                _ =>
                    new[]
                    {
                        "warning.wav",
                        "warning_pulse.wav",
                        "warning_technical.wav"
                    }
            };

        foreach (string fileName
                 in allowed)
        {
            if (string.Equals(
                    selected,
                    fileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return fileName;
            }
        }

        return fallback;
    }


    private void CloseAll()
    {
        NotificationWindow[] windows =
            _windows.ToArray();

        _windows.Clear();

        foreach (NotificationWindow window
                 in windows)
        {
            window.Closed -=
                NotificationWindow_Closed;

            window.Close();
        }
    }

    [DllImport(
        "winmm.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern bool PlaySound(
        string? soundName,
        IntPtr moduleHandle,
        uint flags);

}
