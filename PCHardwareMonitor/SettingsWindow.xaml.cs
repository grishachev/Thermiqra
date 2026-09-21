using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PCHardwareMonitor;

public partial class SettingsWindow : Window
{
    private readonly bool _firstRun;

    private readonly string _originalTheme;

    private string _selectedTheme;

    private bool _saved;

    public bool Saved =>
        _saved;

    public SettingsWindow(
        bool firstRun)
    {
        InitializeComponent();

        _firstRun =
            firstRun;

        _originalTheme =
            SettingsService
                .Current
                .ThemeName;

        _selectedTheme =
            _originalTheme;

        LoadSettings();

        UpdateThemeCards();
    }

    // ============================================================
    // ЗАГРУЗКА НАСТРОЕК В ОКНО
    // ============================================================

    private void LoadSettings()
    {
        AppSettings settings =
            SettingsService.Current;

        // Запуск и трей
        AutoStartCheck.IsChecked =
            settings.AutoStartWithWindows;

        CloseToTrayCheck.IsChecked =
            settings.MinimizeToTrayOnClose;

        ShowWindowOnStartupCheck.IsChecked =
            settings.ShowMainWindowOnStartup;

        // Общие уведомления
        NotificationsCheck.IsChecked =
            settings.NotificationsEnabled;

        AlertDelayText.Text =
            settings.AlertDelaySeconds
                .ToString();

        RepeatCriticalAlertsCheck.IsChecked =
            settings.RepeatCriticalTemperatureAlerts;

        CriticalRepeatMinutesText.Text =
            settings.CriticalRepeatMinutes
                .ToString();

        // CPU
        CpuNotificationsCheck.IsChecked =
            settings.CpuTemperatureNotificationsEnabled;

        CpuWarningText.Text =
            settings.CpuWarningTemperature
                .ToString(
                    CultureInfo.CurrentCulture);

        CpuCriticalText.Text =
            settings.CpuCriticalTemperature
                .ToString(
                    CultureInfo.CurrentCulture);

        // GPU
        GpuNotificationsCheck.IsChecked =
            settings.GpuTemperatureNotificationsEnabled;

        GpuWarningText.Text =
            settings.GpuWarningTemperature
                .ToString(
                    CultureInfo.CurrentCulture);

        GpuCriticalText.Text =
            settings.GpuCriticalTemperature
                .ToString(
                    CultureInfo.CurrentCulture);

        // Накопители
        StorageTemperatureNotificationsCheck.IsChecked =
            settings.StorageTemperatureNotificationsEnabled;

        StorageWarningText.Text =
            settings.StorageWarningTemperature
                .ToString(
                    CultureInfo.CurrentCulture);

        StorageCriticalText.Text =
            settings.StorageCriticalTemperature
                .ToString(
                    CultureInfo.CurrentCulture);

        DiskSpaceNotificationsCheck.IsChecked =
            settings.DiskSpaceNotificationsEnabled;

        DiskWarningText.Text =
            settings.DiskWarningPercent
                .ToString(
                    CultureInfo.CurrentCulture);

        DiskCriticalText.Text =
            settings.DiskCriticalPercent
                .ToString(
                    CultureInfo.CurrentCulture);
    }

    // ============================================================
    // ПРЕДПРОСМОТР ТЕМЫ ПРИ НАВЕДЕНИИ
    // ============================================================

    private void ThemeCard_MouseEnter(
        object sender,
        MouseEventArgs e)
    {
        if (sender is not Border border)
            return;

        if (border.Tag
            is not string themeName)
        {
            return;
        }

        ThemeManager.ApplyTheme(
            themeName);
    }

    // ============================================================
    // УВЕЛИ КУРСОР — ВОЗВРАЩАЕМ ВЫБРАННУЮ ТЕМУ
    // ============================================================

    private void ThemeCard_MouseLeave(
        object sender,
        MouseEventArgs e)
    {
        ThemeManager.ApplyTheme(
            _selectedTheme);
    }

    // ============================================================
    // ЩЕЛЧОК ПО ТЕМЕ
    // ============================================================

    private void ThemeCard_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not Border border)
            return;

        if (border.Tag
            is not string themeName)
        {
            return;
        }

        _selectedTheme =
            themeName;

        ThemeManager.ApplyTheme(
            themeName);

        UpdateThemeCards();
    }

    // ============================================================
    // ВЫДЕЛЕНИЕ ВЫБРАННОЙ ТЕМЫ
    // ============================================================

    private void UpdateThemeCards()
    {
        SetThemeCardState(
            SteelBlueThemeCard,
            "SteelBlue");

        SetThemeCardState(
            GraphiteGreenThemeCard,
            "GraphiteGreen");

        SetThemeCardState(
            VioletNeonThemeCard,
            "VioletNeon");

        SetThemeCardState(
            LightTechThemeCard,
            "LightTech");
    }

    private void SetThemeCardState(
        Border card,
        string themeName)
    {
        bool selected =
            _selectedTheme ==
            themeName;

        card.BorderThickness =
            selected
                ? new Thickness(3)
                : new Thickness(2);

        if (selected)
        {
            card.BorderBrush =
                UiBrushes.Theme(
                    "AccentBrush");

            card.Effect =
                new System.Windows.Media.Effects
                    .DropShadowEffect
                {
                    Color =
                        Colors.Black,

                    BlurRadius = 12,

                    ShadowDepth = 0,

                    Opacity = 0.25
                };
        }
        else
        {
            card.BorderBrush =
                UiBrushes.Theme(
                    "BorderBrush");

            card.Effect =
                null;
        }
    }

    // ============================================================
    // СОХРАНЕНИЕ
    // ============================================================

    private void Save_Click(
        object sender,
        RoutedEventArgs e)
    {
        // --------------------------------------------------------
        // Задержка первого предупреждения
        // --------------------------------------------------------

        if (!int.TryParse(
                AlertDelayText.Text,
                out int alertDelay) ||
            alertDelay < 1 ||
            alertDelay > 120)
        {
            ShowError(
                "Задержка предупреждения должна быть от 1 до 120 секунд.");

            return;
        }

        // --------------------------------------------------------
        // Интервал повтора критических предупреждений
        // --------------------------------------------------------

        if (!int.TryParse(
                CriticalRepeatMinutesText.Text,
                out int criticalRepeatMinutes) ||
            criticalRepeatMinutes < 1 ||
            criticalRepeatMinutes > 120)
        {
            ShowError(
                "Интервал повтора критического предупреждения должен быть от 1 до 120 минут.");

            return;
        }

        // --------------------------------------------------------
        // CPU
        // --------------------------------------------------------

        if (!TryReadNumber(
                CpuWarningText,
                20,
                110,
                out double cpuWarning) ||
            !TryReadNumber(
                CpuCriticalText,
                20,
                120,
                out double cpuCritical) ||
            cpuWarning >= cpuCritical)
        {
            ShowError(
                "Проверь температуры CPU. Предупреждение должно быть ниже критической температуры.");

            return;
        }

        // --------------------------------------------------------
        // GPU
        // --------------------------------------------------------

        if (!TryReadNumber(
                GpuWarningText,
                20,
                110,
                out double gpuWarning) ||
            !TryReadNumber(
                GpuCriticalText,
                20,
                120,
                out double gpuCritical) ||
            gpuWarning >= gpuCritical)
        {
            ShowError(
                "Проверь температуры GPU. Предупреждение должно быть ниже критической температуры.");

            return;
        }

        // --------------------------------------------------------
        // Температура накопителей
        // --------------------------------------------------------

        if (!TryReadNumber(
                StorageWarningText,
                20,
                100,
                out double storageWarning) ||
            !TryReadNumber(
                StorageCriticalText,
                20,
                110,
                out double storageCritical) ||
            storageWarning >= storageCritical)
        {
            ShowError(
                "Проверь температуры накопителей. Предупреждение должно быть ниже критической температуры.");

            return;
        }

        // --------------------------------------------------------
        // Заполнение дисков
        // --------------------------------------------------------

        if (!TryReadNumber(
                DiskWarningText,
                1,
                99,
                out double diskWarning) ||
            !TryReadNumber(
                DiskCriticalText,
                2,
                100,
                out double diskCritical) ||
            diskWarning >= diskCritical)
        {
            ShowError(
                "Проверь пороги заполнения дисков. Предупреждение должно быть ниже критического значения.");

            return;
        }

        // --------------------------------------------------------
        // Автозапуск Windows
        // --------------------------------------------------------

        bool autoStart =
            AutoStartCheck.IsChecked ==
            true;

        if (!StartupService.SetEnabled(
                autoStart,
                out string? startupError))
        {
            ShowError(
                "Не удалось изменить автозапуск.\n\n" +
                startupError);

            return;
        }

        // --------------------------------------------------------
        // Записываем настройки
        // --------------------------------------------------------

        AppSettings settings =
            SettingsService.Current;

        settings.ThemeName =
            _selectedTheme;

        // Запуск / трей
        settings.AutoStartWithWindows =
            autoStart;

        settings.MinimizeToTrayOnClose =
            CloseToTrayCheck.IsChecked ==
            true;

        settings.ShowMainWindowOnStartup =
            ShowWindowOnStartupCheck.IsChecked ==
            true;

        // Общие уведомления
        settings.NotificationsEnabled =
            NotificationsCheck.IsChecked ==
            true;

        settings.AlertDelaySeconds =
            alertDelay;

        settings.RepeatCriticalTemperatureAlerts =
            RepeatCriticalAlertsCheck.IsChecked ==
            true;

        settings.CriticalRepeatMinutes =
            criticalRepeatMinutes;

        // CPU
        settings.CpuTemperatureNotificationsEnabled =
            CpuNotificationsCheck.IsChecked ==
            true;

        settings.CpuWarningTemperature =
            cpuWarning;

        settings.CpuCriticalTemperature =
            cpuCritical;

        // GPU
        settings.GpuTemperatureNotificationsEnabled =
            GpuNotificationsCheck.IsChecked ==
            true;

        settings.GpuWarningTemperature =
            gpuWarning;

        settings.GpuCriticalTemperature =
            gpuCritical;

        // Температура накопителей
        settings.StorageTemperatureNotificationsEnabled =
            StorageTemperatureNotificationsCheck.IsChecked ==
            true;

        settings.StorageWarningTemperature =
            storageWarning;

        settings.StorageCriticalTemperature =
            storageCritical;

        // Свободное место
        settings.DiskSpaceNotificationsEnabled =
            DiskSpaceNotificationsCheck.IsChecked ==
            true;

        settings.DiskWarningPercent =
            diskWarning;

        settings.DiskCriticalPercent =
            diskCritical;

        // Первый запуск завершён
        if (_firstRun)
        {
            settings.FirstRunCompleted =
                true;
        }

        SettingsService.Save();

        ThemeManager.ApplyTheme(
            settings.ThemeName);

        _saved =
            true;

        DialogResult =
            true;

        Close();
    }

    // ============================================================
    // ОТМЕНА
    // ============================================================

    private void Cancel_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(
        EventArgs e)
    {
        if (!_saved)
        {
            ThemeManager.ApplyTheme(
                _originalTheme);
        }

        base.OnClosed(e);
    }

    // ============================================================
    // ЧТЕНИЕ ЧИСЛОВОГО ЗНАЧЕНИЯ
    // ============================================================

    private static bool TryReadNumber(
        TextBox textBox,
        double minimum,
        double maximum,
        out double value)
    {
        if (!double.TryParse(
                textBox.Text,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out value))
        {
            return false;
        }

        return
            value >= minimum &&
            value <= maximum;
    }

    // ============================================================
    // ОШИБКА
    // ============================================================

    private static void ShowError(
        string message)
    {
        System.Windows.MessageBox.Show(
            message,
            "Thermiqra",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
