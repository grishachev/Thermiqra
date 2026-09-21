using System;
using System.Drawing;
using System.IO;
using System.Windows;

using Forms = System.Windows.Forms;

namespace PCHardwareMonitor;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;

    private readonly Icon _trayIcon;

    private readonly Action _openAction;

    private readonly Action _settingsAction;

    private readonly Action _exitAction;


    public TrayService(
        Action openAction,
        Action settingsAction,
        Action exitAction)
    {
        _openAction =
            openAction;

        _settingsAction =
            settingsAction;

        _exitAction =
            exitAction;


        // ========================================================
        // ЗАГРУЖАЕМ НАШУ ИКОНКУ ИЗ РЕСУРСОВ
        // ========================================================

        _trayIcon =
            LoadTrayIcon();


        // ========================================================
        // МЕНЮ ТРЕЯ
        // ========================================================

        Forms.ContextMenuStrip menu =
            new();

        Forms.ToolStripMenuItem openItem =
            new("Открыть");

        Forms.ToolStripMenuItem settingsItem =
            new("Настройки");

        Forms.ToolStripSeparator separator =
            new();

        Forms.ToolStripMenuItem exitItem =
            new("Выход");


        openItem.Click +=
            (_, _) =>
                _openAction();

        settingsItem.Click +=
            (_, _) =>
                _settingsAction();

        exitItem.Click +=
            (_, _) =>
                _exitAction();


        menu.Items.Add(
            openItem);

        menu.Items.Add(
            settingsItem);

        menu.Items.Add(
            separator);

        menu.Items.Add(
            exitItem);


        // ========================================================
        // СОЗДАЁМ ЗНАЧОК В ТРЕЕ
        // ========================================================

        _notifyIcon =
            new Forms.NotifyIcon
            {
                Icon =
                    _trayIcon,

                Text =
                    "Thermiqra",

                Visible =
                    true,

                ContextMenuStrip =
                    menu
            };


        // Двойной щелчок открывает главное окно
        _notifyIcon.DoubleClick +=
            (_, _) =>
                _openAction();
    }


    // ============================================================
    // ЗАГРУЗКА ICO ИЗ WPF-РЕСУРСА
    // ============================================================

    private static Icon LoadTrayIcon()
    {
        Uri iconUri =
            new(
                "pack://application:,,,/Assets/Thermiqra_Tray.ico",
                UriKind.Absolute);

        System.Windows.Resources.StreamResourceInfo?
            resourceInfo =
                Application.GetResourceStream(
                    iconUri);

        if (resourceInfo == null)
        {
            // Резервный вариант на случай,
            // если ресурс по какой-либо причине не найден.
            return
                (Icon)SystemIcons.Application.Clone();
        }

        using Stream stream =
            resourceInfo.Stream;

        using Icon originalIcon =
            new(stream);

        return
            (Icon)originalIcon.Clone();
    }


    // ============================================================
    // УВЕДОМЛЕНИЯ WINDOWS
    // ============================================================

    public void ShowNotification(
        string title,
        string message,
        bool critical)
    {
        if (!SettingsService
                .Current
                .NotificationsEnabled)
        {
            return;
        }

        _notifyIcon.BalloonTipTitle =
            title;

        _notifyIcon.BalloonTipText =
            message;

        _notifyIcon.BalloonTipIcon =
            critical
                ? Forms.ToolTipIcon.Error
                : Forms.ToolTipIcon.Warning;

        _notifyIcon.ShowBalloonTip(
            5000);
    }


    // ============================================================
    // ОСВОБОЖДЕНИЕ РЕСУРСОВ
    // ============================================================

    public void Dispose()
    {
        _notifyIcon.Visible =
            false;

        _notifyIcon.Dispose();

        _trayIcon.Dispose();
    }
}
