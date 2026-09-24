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

    private readonly Forms.ToolStripMenuItem _openItem;

    private readonly Forms.ToolStripMenuItem _settingsItem;

    private readonly Forms.ToolStripMenuItem _exitItem;


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

        _openItem =
            new();

        _settingsItem =
            new();

        Forms.ToolStripSeparator separator =
            new();

        _exitItem =
            new();

        RefreshLanguage();


        _openItem.Click +=
            (_, _) =>
                _openAction();

        _settingsItem.Click +=
            (_, _) =>
                _settingsAction();

        _exitItem.Click +=
            (_, _) =>
                _exitAction();


        menu.Items.Add(
            _openItem);

        menu.Items.Add(
            _settingsItem);

        menu.Items.Add(
            separator);

        menu.Items.Add(
            _exitItem);


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


        // Двойной щелчок по значку открывает главное окно
        _notifyIcon.DoubleClick +=
            (_, _) =>
                _openAction();
    }


    public void RefreshLanguage()
    {
        _openItem.Text =
            SettingsService.L(
                "Открыть",
                "Open");

        _settingsItem.Text =
            SettingsService.L(
                "Настройки",
                "Settings");

        _exitItem.Text =
            SettingsService.L(
                "Выход",
                "Exit");
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