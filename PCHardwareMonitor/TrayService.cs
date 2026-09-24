using System;
using System.Drawing;
using System.IO;
using System.Windows;

using Forms = System.Windows.Forms;

namespace PCHardwareMonitor;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;

    private readonly Forms.ContextMenuStrip _menu;

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

        _menu =
            new Forms.ContextMenuStrip
            {
                ShowImageMargin =
                    false,

                ShowCheckMargin =
                    false,

                Padding =
                    new Forms.Padding(
                        4)
            };

        _openItem =
            new();

        _settingsItem =
            new();

        Forms.ToolStripSeparator separator =
            new();

        _exitItem =
            new();

        ApplyMenuItemLayout(
            _openItem);

        ApplyMenuItemLayout(
            _settingsItem);

        ApplyMenuItemLayout(
            _exitItem);

        RefreshLanguage();
        RefreshAppearance();


        _openItem.Click +=
            (_, _) =>
                _openAction();

        _settingsItem.Click +=
            (_, _) =>
                _settingsAction();

        _exitItem.Click +=
            (_, _) =>
                _exitAction();


        _menu.Items.Add(
            _openItem);

        _menu.Items.Add(
            _settingsItem);

        _menu.Items.Add(
            separator);

        _menu.Items.Add(
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
                    _menu
            };


        // Двойной щелчок по значку открывает главное окно
        _notifyIcon.DoubleClick +=
            (_, _) =>
                _openAction();

        _menu.Opening +=
            (_, _) =>
                RefreshAppearance();
    }


    public void UpdateTooltip(
        float? cpuTemperature,
        float? gpuTemperature)
    {
        string cpuText =
            cpuTemperature.HasValue
                ? $"{cpuTemperature.Value:F0} °C"
                : "—";

        string gpuText =
            gpuTemperature.HasValue
                ? $"{gpuTemperature.Value:F0} °C"
                : "—";

        string tooltipText =
            $"Thermiqra\n" +
            $"CPU: {cpuText}\n" +
            $"GPU: {gpuText}";

        if (string.Equals(
                _notifyIcon.Text,
                tooltipText,
                StringComparison.Ordinal))
        {
            return;
        }

        _notifyIcon.Text =
            tooltipText;
    }


    public void RefreshAppearance()
    {
        Color background =
            GetThemeColor(
                "CardBackgroundBrush",
                Color.FromArgb(
                    28, 31, 36));

        Color border =
            GetThemeColor(
                "BorderBrush",
                Color.FromArgb(
                    85, 95, 105));

        Color text =
            GetThemeColor(
                "PrimaryTextBrush",
                Color.Gainsboro);

        Color accent =
            GetThemeColor(
                "AccentBrush",
                Color.DeepSkyBlue);

        Color selectedText =
            GetThemeColor(
                "WindowBackgroundBrush",
                Color.Black);

        _menu.BackColor =
            background;

        _menu.ForeColor =
            text;

        _menu.Renderer =
            new TrayMenuRenderer(
                background,
                border,
                text,
                accent,
                selectedText);

        _openItem.ForeColor =
            text;

        _settingsItem.ForeColor =
            text;

        _exitItem.ForeColor =
            text;
    }


    private static void ApplyMenuItemLayout(
        Forms.ToolStripMenuItem item)
    {
        item.AutoSize =
            true;

        item.Padding =
            new Forms.Padding(
                10,
                6,
                10,
                6);

        item.Margin =
            new Forms.Padding(
                1);

        item.Font =
            new System.Drawing.Font(
                new System.Drawing.FontFamily(
                    "Segoe UI"),
                10.0f,
                System.Drawing.FontStyle.Regular);
    }


    private static Color GetThemeColor(
        string resourceKey,
        Color fallback)
    {
        object? resource =
            Application.Current?
                .TryFindResource(
                    resourceKey);

        if (resource is
            System.Windows.Media.SolidColorBrush brush)
        {
            System.Windows.Media.Color color =
                brush.Color;

            return Color.FromArgb(
                color.A,
                color.R,
                color.G,
                color.B);
        }

        return fallback;
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


    private sealed class TrayMenuRenderer :
        Forms.ToolStripProfessionalRenderer
    {
        private readonly Color _textColor;

        private readonly Color _selectedTextColor;


        public TrayMenuRenderer(
            Color background,
            Color border,
            Color textColor,
            Color accent,
            Color selectedTextColor)
            : base(
                new TrayMenuColorTable(
                    background,
                    border,
                    accent))
        {
            _textColor =
                textColor;

            _selectedTextColor =
                selectedTextColor;

            RoundedEdges =
                false;
        }


        protected override void OnRenderItemText(
            Forms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor =
                e.Item.Selected
                    ? _selectedTextColor
                    : _textColor;

            base.OnRenderItemText(
                e);
        }
    }


    private sealed class TrayMenuColorTable :
        Forms.ProfessionalColorTable
    {
        private readonly Color _background;

        private readonly Color _border;

        private readonly Color _accent;


        public TrayMenuColorTable(
            Color background,
            Color border,
            Color accent)
        {
            _background =
                background;

            _border =
                border;

            _accent =
                accent;

            UseSystemColors =
                false;
        }


        public override Color ToolStripDropDownBackground =>
            _background;

        public override Color MenuBorder =>
            _border;

        public override Color MenuItemBorder =>
            _accent;

        public override Color MenuItemSelected =>
            _accent;

        public override Color MenuItemSelectedGradientBegin =>
            _accent;

        public override Color MenuItemSelectedGradientEnd =>
            _accent;

        public override Color SeparatorDark =>
            _border;

        public override Color SeparatorLight =>
            _border;

        public override Color ImageMarginGradientBegin =>
            _background;

        public override Color ImageMarginGradientMiddle =>
            _background;

        public override Color ImageMarginGradientEnd =>
            _background;
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

        _menu.Dispose();

        _trayIcon.Dispose();
    }
}