using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PCHardwareMonitor;

public partial class SettingsWindow : Window
{
    private const uint SndAsync = 0x0001;

    private const uint SndNoDefault = 0x0002;

    private const uint SndFileName = 0x00020000;


    private readonly bool _firstRun;

    private string _originalSkin;

    private string _selectedSkin;

    private string _originalTheme;

    private string _selectedLanguage;

    private bool _saved;

    public bool Saved =>
        _saved;

    public SettingsWindow(
        bool firstRun)
    {
        InitializeComponent();

        SettingsService.ApplyLanguageToWindow(
            this);

        _firstRun =
            firstRun;

        _originalSkin =
            SettingsService
                .Current
                .SkinName;

        _selectedSkin =
            _originalSkin;

        _originalTheme =
            SettingsService
                .Current
                .ThemeName;

        _selectedLanguage =
            SettingsService
                .Current
                .LanguageName
            ?? "System";

        LoadSettings();

        UpdateVersionText();

        UpdateSkinCards();
    }

    // ============================================================
    // ЗАГРУЗКА НАСТРОЕК В ОКНО
    // ============================================================

    private void LoadSettings()
    {
        AppSettings settings =
            SettingsService.Current;

        SelectLanguageItem(
            settings.LanguageName);

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

        NotificationSoundsCheck.IsChecked =
            settings.NotificationSoundsEnabled;

        SelectSoundItem(
            WarningSoundComboBox,
            settings.WarningSoundName,
            "warning.wav");

        SelectSoundItem(
            CriticalSoundComboBox,
            settings.CriticalSoundName,
            "critical.wav");

        SelectSoundItem(
            InfoSoundComboBox,
            settings.InfoSoundName,
            "info.wav");

        CriticalNotificationsTopmostCheck.IsChecked =
            settings.CriticalNotificationsTopmost;

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
    // ЗВУКИ УВЕДОМЛЕНИЙ
    // ============================================================

    private void SoundComboBox_PreviewMouseWheel(
        object sender,
        MouseWheelEventArgs e)
    {
        if (sender is not ComboBox comboBox ||
            comboBox.IsDropDownOpen)
        {
            return;
        }

        ForwardMouseWheelToParentScrollViewer(
            comboBox,
            e);
    }


    private void PreviewSound_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not string comboBoxName ||
            FindName(comboBoxName) is not ComboBox comboBox)
        {
            return;
        }

        string fileName =
            GetSelectedSoundName(
                comboBox,
                string.Empty);

        if (string.IsNullOrWhiteSpace(
                fileName))
        {
            return;
        }

        string path =
            Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                fileName);

        if (!File.Exists(
                path))
        {
            ShowError(
                SettingsService.L(
                    "Файл звука не найден.",
                    "The sound file was not found."));

            return;
        }

        try
        {
            PlaySound(
                path,
                IntPtr.Zero,
                SndAsync |
                SndNoDefault |
                SndFileName);
        }
        catch
        {
            ShowError(
                SettingsService.L(
                    "Не удалось воспроизвести звук.",
                    "The sound could not be played."));
        }
    }


    private static void SelectSoundItem(
        ComboBox comboBox,
        string? soundName,
        string fallbackSoundName)
    {
        string target =
            string.IsNullOrWhiteSpace(
                soundName)
                ? fallbackSoundName
                : soundName;

        foreach (object itemObject
                 in comboBox.Items)
        {
            if (itemObject is not ComboBoxItem item ||
                item.Tag is not string tag)
            {
                continue;
            }

            if (!string.Equals(
                    tag,
                    target,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            comboBox.SelectedItem =
                item;

            return;
        }

        foreach (object itemObject
                 in comboBox.Items)
        {
            if (itemObject is ComboBoxItem item &&
                item.Tag is string tag &&
                string.Equals(
                    tag,
                    fallbackSoundName,
                    StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem =
                    item;

                return;
            }
        }

        if (comboBox.Items.Count >
            0)
        {
            comboBox.SelectedIndex =
                0;
        }
    }


    private static string GetSelectedSoundName(
        ComboBox comboBox,
        string fallbackSoundName)
    {
        return comboBox.SelectedItem
                   is ComboBoxItem item &&
               item.Tag is string tag &&
               !string.IsNullOrWhiteSpace(
                   tag)
            ? tag
            : fallbackSoundName;
    }


    private static void ForwardMouseWheelToParentScrollViewer(
        DependencyObject start,
        MouseWheelEventArgs e)
    {
        e.Handled =
            true;

        DependencyObject? current =
            start;

        while (current != null)
        {
            current =
                VisualTreeHelper.GetParent(
                    current);

            if (current is not ScrollViewer scrollViewer)
            {
                continue;
            }

            int steps =
                Math.Max(
                    1,
                    Math.Abs(e.Delta) / 120);

            for (int i = 0;
                 i < steps;
                 i++)
            {
                if (e.Delta >
                    0)
                {
                    scrollViewer.LineUp();
                }
                else
                {
                    scrollViewer.LineDown();
                }
            }

            break;
        }
    }


    // ============================================================
    // ЯЗЫК
    // ============================================================

    private void UpdateVersionText()
    {
        Version? version =
            typeof(SettingsWindow)
                .Assembly
                .GetName()
                .Version;

        string versionText =
            version == null
                ? "—"
                : version.Revision > 0
                    ? $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}"
                    : $"{version.Major}.{version.Minor}.{version.Build}";

        VersionText.Text =
            SettingsService.L(
                $"Версия {versionText}",
                $"Version {versionText}");
    }


    private void LanguageComboBox_PreviewMouseWheel(
        object sender,
        MouseWheelEventArgs e)
    {
        if (LanguageComboBox.IsDropDownOpen)
        {
            return;
        }

        ForwardMouseWheelToParentScrollViewer(
            LanguageComboBox,
            e);
    }


    private void LanguageComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (LanguageComboBox.SelectedItem
            is not ComboBoxItem item ||
            item.Tag is not string languageName)
        {
            return;
        }

        _selectedLanguage =
            languageName;
    }


    private void SelectLanguageItem(
        string? languageName)
    {
        string target =
            languageName switch
            {
                "English" => "English",
                "Russian" => "Russian",
                _ => "System"
            };

        foreach (object itemObject
                 in LanguageComboBox.Items)
        {
            if (itemObject is not ComboBoxItem item ||
                item.Tag is not string tag)
            {
                continue;
            }

            if (!string.Equals(
                    tag,
                    target,
                    StringComparison.Ordinal))
            {
                continue;
            }

            LanguageComboBox.SelectedItem =
                item;

            _selectedLanguage =
                target;

            return;
        }

        _selectedLanguage =
            "System";
    }


    // ============================================================
    // ВЫБОР СКИНА
    // ============================================================

    private void SkinCard_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not Border border)
            return;

        if (border.Tag
            is not string skinName)
        {
            return;
        }

        _selectedSkin =
            skinName;

        ThemeManager.ApplyAppearance(
            _selectedSkin,
            _originalTheme);

        UpdateSkinCards();
    }


    private void UpdateSkinCards()
    {
        SetSkinCardState(
            CyberTechSkinCard,
            "CyberTech");

        SetSkinCardState(
            SteamPunkSkinCard,
            "SteamPunk");

        SetSkinCardState(
            FrostCoreSkinCard,
            "FrostCore");

        SetSkinCardState(
            MilitaryOpsSkinCard,
            "MilitaryOps");
    }


    private void SetSkinCardState(
        Border card,
        string skinName)
    {
        bool selected =
            _selectedSkin ==
            skinName;

        card.BorderThickness =
            selected
                ? new Thickness(3)
                : new Thickness(2);

        string borderColor =
            skinName switch
            {
                "SteamPunk" =>
                    selected ? "#F39A32" : "#6E4A2A",

                "FrostCore" =>
                    selected ? "#76E7FF" : "#4A6570",

                "MilitaryOps" =>
                    selected ? "#9BCF4A" : "#4B5A37",

                _ =>
                    selected ? "#2ED9FF" : "#234754"
            };

        card.BorderBrush =
            new SolidColorBrush(
                (Color)
                ColorConverter.ConvertFromString(
                    borderColor));
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
                SettingsService.L(
                    "Задержка предупреждения должна быть от 1 до 120 секунд.",
                    "The alert delay must be between 1 and 120 seconds."));

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
                SettingsService.L(
                    "Интервал повтора критического предупреждения должен быть от 1 до 120 минут.",
                    "The critical alert repeat interval must be between 1 and 120 minutes."));

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
                SettingsService.L(
                    "Проверь температуры CPU. Предупреждение должно быть ниже критической температуры.",
                    "Check the CPU temperature thresholds. Warning must be lower than critical."));

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
                SettingsService.L(
                    "Проверь температуры GPU. Предупреждение должно быть ниже критической температуры.",
                    "Check the GPU temperature thresholds. Warning must be lower than critical."));

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
                SettingsService.L(
                    "Проверь температуры накопителей. Предупреждение должно быть ниже критической температуры.",
                    "Check the storage temperature thresholds. Warning must be lower than critical."));

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
                SettingsService.L(
                    "Проверь пороги заполнения дисков. Предупреждение должно быть ниже критического значения.",
                    "Check the disk usage thresholds. Warning must be lower than critical."));

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
                SettingsService.L(
                    "Не удалось изменить автозапуск.\n\n",
                    "Failed to change startup settings.\n\n") +
                SettingsService.TranslateText(
                    startupError ?? string.Empty));

            return;
        }

        // --------------------------------------------------------
        // Записываем настройки
        // --------------------------------------------------------

        AppSettings settings =
            SettingsService.Current;

        settings.SkinName =
            _selectedSkin;

        settings.LanguageName =
            _selectedLanguage;

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

        settings.NotificationSoundsEnabled =
            NotificationSoundsCheck.IsChecked ==
            true;

        settings.WarningSoundName =
            GetSelectedSoundName(
                WarningSoundComboBox,
                "warning.wav");

        settings.CriticalSoundName =
            GetSelectedSoundName(
                CriticalSoundComboBox,
                "critical.wav");

        settings.InfoSoundName =
            GetSelectedSoundName(
                InfoSoundComboBox,
                "info.wav");

        settings.CriticalNotificationsTopmost =
            CriticalNotificationsTopmostCheck.IsChecked ==
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

        SettingsService.ApplyLanguagePreference();

        SettingsService.ApplyLanguageToWindow(
            this);

        UpdateVersionText();

        ThemeManager.ApplyAppearance(
            settings.SkinName,
            settings.ThemeName);

        UpdateSkinCards();

        if (Owner is MainWindow mainWindow)
        {
            mainWindow.RefreshLanguage();

            mainWindow.ResetAlertStatesAfterSettingsChange();
        }

        _originalSkin =
            settings.SkinName;

        _originalTheme =
            settings.ThemeName;

        _saved =
            true;
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
        ThemeManager.ApplyAppearance(
            _originalSkin,
            _originalTheme);

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

    [DllImport(
        "winmm.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern bool PlaySound(
        string? soundName,
        IntPtr moduleHandle,
        uint flags);

}