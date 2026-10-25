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

    private readonly Action _checkUpdatesAction;

    private readonly Action _exitAction;

    private readonly Forms.ToolStripMenuItem _openItem;

    private readonly Forms.ToolStripMenuItem _settingsItem;

    private readonly Forms.ToolStripMenuItem _checkUpdatesItem;

    private readonly Forms.ToolStripMenuItem _exitItem;


    public TrayService(
        Action openAction,
        Action settingsAction,
        Action checkUpdatesAction,
        Action exitAction)
    {
        _openAction =
            openAction;

        _settingsAction =
            settingsAction;

        _checkUpdatesAction =
            checkUpdatesAction;

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

        _checkUpdatesItem =
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
            _checkUpdatesItem);

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

        _checkUpdatesItem.Click +=
            (_, _) =>
                _checkUpdatesAction();

        _exitItem.Click +=
            (_, _) =>
                _exitAction();


        _menu.Items.Add(
            _openItem);

        _menu.Items.Add(
            _settingsItem);

        _menu.Items.Add(
            _checkUpdatesItem);

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
                border);

        // A muted tint keeps menu text readable in every theme, while
        // the outline still uses the theme's actual accent color.
        Color selectedBackground = BlendColors(background, accent, 0.36);

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
                selectedBackground);

        _openItem.ForeColor =
            text;

        _settingsItem.ForeColor =
            text;

        _checkUpdatesItem.ForeColor =
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
        // Theme dictionaries may be attached to the main window rather
        // than directly to Application.Resources.
        object? resource =
            Application.Current?.MainWindow?.TryFindResource(resourceKey)
            ?? Application.Current?.TryFindResource(resourceKey);

        if (resource is System.Windows.Media.SolidColorBrush solid)
            return ToOpaqueColor(solid.Color);

        // AccentBrush may be a gradient in some themes. Previously that
        // fell back to Windows-like blue regardless of the selected skin.
        if (resource is System.Windows.Media.GradientBrush gradient &&
            gradient.GradientStops.Count > 0)
        {
            System.Windows.Media.GradientStop nearest = gradient.GradientStops[0];
            double distance = Math.Abs(nearest.Offset - 0.5);
            foreach (System.Windows.Media.GradientStop stop in gradient.GradientStops)
            {
                double candidate = Math.Abs(stop.Offset - 0.5);
                if (candidate < distance)
                {
                    nearest = stop;
                    distance = candidate;
                }
            }

            return ToOpaqueColor(nearest.Color);
        }

        return fallback;
    }

    private static Color ToOpaqueColor(System.Windows.Media.Color color) =>
        Color.FromArgb(color.R, color.G, color.B);

    private static Color BlendColors(Color background, Color accent, double amount)
    {
        amount = Math.Clamp(amount, 0d, 1d);
        return Color.FromArgb(
            (int)Math.Round(background.R * (1 - amount) + accent.R * amount),
            (int)Math.Round(background.G * (1 - amount) + accent.G * amount),
            (int)Math.Round(background.B * (1 - amount) + accent.B * amount));
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

        _checkUpdatesItem.Text =
            SettingsService.L(
                "Проверить обновления",
                "Check for updates");

        _exitItem.Text =
            SettingsService.L(
                "Выход",
                "Exit");
    }


    private sealed class TrayMenuRenderer :
        Forms.ToolStripProfessionalRenderer
    {
        private readonly Color _textColor;
        private readonly Color _accent;
        private readonly Color _selectedBackground;

        public TrayMenuRenderer(
            Color background,
            Color border,
            Color textColor,
            Color accent,
            Color selectedBackground)
            : base(
                new TrayMenuColorTable(
                    background,
                    border,
                    accent))
        {
            _textColor = textColor;
            _accent = accent;
            _selectedBackground = selectedBackground;
            RoundedEdges = false;
        }

        // Paint selection explicitly so WinForms cannot substitute its
        // usual blue hover highlight for the current Thermiqra palette.
        protected override void OnRenderMenuItemBackground(
            Forms.ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled)
            {
                base.OnRenderMenuItemBackground(e);
                return;
            }

            System.Drawing.Rectangle rectangle = new(
                0, 0,
                Math.Max(1, e.Item.Width - 1),
                Math.Max(1, e.Item.Height - 1));

            using System.Drawing.SolidBrush fill = new(_selectedBackground);
            using System.Drawing.Pen outline = new(_accent);
            e.Graphics.FillRectangle(fill, rectangle);
            e.Graphics.DrawRectangle(outline, rectangle);
        }

        protected override void OnRenderItemText(
            Forms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = _textColor;
            base.OnRenderItemText(e);
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