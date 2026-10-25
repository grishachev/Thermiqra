using System;
using System.Collections.Generic;
using System.IO;
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
    private const int MaxPendingNotifications =
        8;

    private const uint SndAsync = 0x0001;

    private const uint SndNoDefault = 0x0002;

    private const uint SndFileName = 0x00020000;

    private const double ScreenMargin =
        18;

    private readonly Queue<PendingNotification>
        _pendingNotifications =
            new();

    private NotificationWindow? _activeWindow;

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
            EnqueueNotification(
                type,
                title,
                message,
                clickAction);

            return;
        }

        dispatcher.BeginInvoke(
            () =>
                EnqueueNotification(
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


    private void EnqueueNotification(
        NotificationType type,
        string title,
        string message,
        Action? clickAction)
    {
        if (_disposed)
        {
            return;
        }

        while (_pendingNotifications.Count >=
               MaxPendingNotifications)
        {
            _pendingNotifications.Dequeue();
        }

        _pendingNotifications.Enqueue(
            new PendingNotification
            {
                Type =
                    type,

                Title =
                    title,

                Message =
                    message,

                ClickAction =
                    clickAction
            });

        ShowNextNotification();
    }


    private void ShowNextNotification()
    {
        if (_disposed ||
            _activeWindow != null ||
            _pendingNotifications.Count == 0)
        {
            return;
        }

        PendingNotification pending =
            _pendingNotifications.Dequeue();

        bool topmostWithoutActivation =
            pending.Type ==
            NotificationType.Critical &&
            SettingsService.Current.CriticalNotificationsTopmost;

        NotificationWindow window =
            new(
                pending.Type,
                pending.Title,
                pending.Message,
                topmostWithoutActivation,
                pending.ClickAction);

        _activeWindow =
            window;

        window.Closed +=
            NotificationWindow_Closed;

        PositionAtBottomRight(
            window);

        window.Show();

        PlayNotificationSound(
            pending.Type);
    }


    private static void PositionAtBottomRight(
        NotificationWindow window)
    {
        Rect workArea =
            SystemParameters.WorkArea;

        double left =
            workArea.Right -
            window.Width -
            ScreenMargin;

        double top =
            workArea.Bottom -
            window.Height -
            ScreenMargin;

        window.SetPosition(
            left,
            top);
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

        if (ReferenceEquals(
                _activeWindow,
                window))
        {
            _activeWindow =
                null;
        }

        if (!_disposed)
        {
            ShowNextNotification();
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
        _pendingNotifications.Clear();

        if (_activeWindow == null)
        {
            return;
        }

        NotificationWindow window =
            _activeWindow;

        _activeWindow =
            null;

        window.Closed -=
            NotificationWindow_Closed;

        window.Close();
    }


    private sealed class PendingNotification
    {
        public NotificationType Type { get; init; }

        public string Title { get; init; } = "";

        public string Message { get; init; } = "";

        public Action? ClickAction { get; init; }
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
