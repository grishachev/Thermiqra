using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PCHardwareMonitor;

public static class IntroductionFeature
{
    private const string ReplayButtonTag =
        "ThermiqraIntroductionReplayButton";

    private const int DwmwaUseImmersiveDarkMode =
        20;

    private const int DwmwaUseImmersiveDarkModeLegacy =
        19;

    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
            return;

        _initialized =
            true;

        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(
                Window_Loaded));

        EventManager.RegisterClassHandler(
            typeof(SettingsWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(
                SettingsWindow_Loaded));
    }

    public static void Show(
        Window? owner = null)
    {
        IntroductionWindow window =
            new();

        if (owner != null)
        {
            window.Owner =
                owner;
        }

        window.ShowDialog();
    }

    private static void Window_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Window window)
            return;

        TryApplyDarkTitleBar(
            window);
    }

    private static void SettingsWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not SettingsWindow window)
            return;

        ApplySettingsCheckBoxHitAreas(
            window);

        AttachSettingsSaveLanguageSync(
            window);

        if (window.FindName(
                "VersionText") is not TextBlock versionText)
        {
            return;
        }

        if (versionText.Parent is not StackPanel panel)
            return;

        foreach (UIElement child in panel.Children)
        {
            if (child is Button existingButton &&
                string.Equals(
                    existingButton.Tag as string,
                    ReplayButtonTag,
                    StringComparison.Ordinal))
            {
                UpdateReplayButtonLanguage(
                    window);

                return;
            }
        }

        Button button =
            IntroductionWindow.CreateThemedButton(
                SettingsService.L(
                    "ПОКАЗАТЬ ЗНАКОМСТВО",
                    "SHOW INTRODUCTION"),
                primary: false);

        button.Tag =
            ReplayButtonTag;

        button.HorizontalAlignment =
            HorizontalAlignment.Left;

        button.MinWidth =
            156;

        button.Height =
            28;

        button.Padding =
            new Thickness(
                10,
                2,
                10,
                2);

        button.Margin =
            new Thickness(
                0,
                7,
                0,
                0);

        button.FontSize =
            9;

        button.Click +=
            (_, _) =>
                Show(
                    window);

        panel.Children.Add(
            button);

        UpdateReplayButtonLanguage(
            window);
    }


    private static void AttachSettingsSaveLanguageSync(
        SettingsWindow window)
    {
        foreach (Button button in
                 FindVisualDescendants<Button>(
                     window))
        {
            if (button.Content is not string content)
                continue;

            if (!string.Equals(
                    content,
                    "СОХРАНИТЬ",
                    StringComparison.Ordinal) &&
                !string.Equals(
                    content,
                    "SAVE",
                    StringComparison.Ordinal))
            {
                continue;
            }

            button.Click -=
                SettingsSaveButton_Click;

            button.Click +=
                SettingsSaveButton_Click;

            return;
        }
    }


    private static void SettingsSaveButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        SettingsWindow? window =
            Window.GetWindow(
                button) as SettingsWindow;

        if (window == null)
            return;

        // Save_Click окна настроек уже выполнился или завершится
        // в текущем проходе события. Обновляем динамическую кнопку
        // после применения сохранённого языка.
        window.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ContextIdle,
            new Action(
                () =>
                    UpdateReplayButtonLanguage(
                        window)));
    }


    private static void UpdateReplayButtonLanguage(
        SettingsWindow window)
    {
        string expectedText =
            GetReplayButtonText(
                window);

        foreach (Button button in
                 FindVisualDescendants<Button>(
                     window))
        {
            if (!string.Equals(
                    button.Tag as string,
                    ReplayButtonTag,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(
                    button.Content as string,
                    expectedText,
                    StringComparison.Ordinal))
            {
                button.Content =
                    expectedText;
            }

            return;
        }
    }


    private static string GetReplayButtonText(
        SettingsWindow window)
    {
        return SettingsService.L(
            "ПОКАЗАТЬ ЗНАКОМСТВО",
            "SHOW INTRODUCTION");
    }


    private static void ApplySettingsCheckBoxHitAreas(
        SettingsWindow window)
    {
        foreach (CheckBox checkBox in
                 FindVisualDescendants<CheckBox>(
                     window))
        {
            // Кликабельной остаётся только реальная область:
            // квадрат переключателя + его подпись.
            // Пустая часть строки справа больше не переключает значение.
            checkBox.HorizontalAlignment =
                HorizontalAlignment.Left;
        }
    }


    private static IEnumerable<T>
        FindVisualDescendants<T>(
            DependencyObject root)
        where T : DependencyObject
    {
        int childCount =
            VisualTreeHelper.GetChildrenCount(
                root);

        for (int i = 0;
             i < childCount;
             i++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(
                    root,
                    i);

            if (child is T match)
            {
                yield return match;
            }

            foreach (T descendant in
                     FindVisualDescendants<T>(
                         child))
            {
                yield return descendant;
            }
        }
    }


    private static void TryApplyDarkTitleBar(
        Window window)
    {
        try
        {
            IntPtr handle =
                new WindowInteropHelper(
                    window)
                .Handle;

            if (handle ==
                IntPtr.Zero)
            {
                return;
            }

            int enabled =
                1;

            int result =
                DwmSetWindowAttribute(
                    handle,
                    DwmwaUseImmersiveDarkMode,
                    ref enabled,
                    sizeof(int));

            if (result != 0)
            {
                DwmSetWindowAttribute(
                    handle,
                    DwmwaUseImmersiveDarkModeLegacy,
                    ref enabled,
                    sizeof(int));
            }
        }
        catch
        {
            // На старых версиях Windows просто остаётся системный заголовок.
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}
