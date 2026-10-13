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


public sealed class IntroductionWindow : Window
{
    private const string PrimaryButtonTag =
        "Primary";

    private const string SecondaryButtonTag =
        "Secondary";

    private readonly List<IntroductionPage>
        _pages;

    private readonly Border _visualHost;

    private readonly TextBlock _sectionText;

    private readonly TextBlock _titleText;

    private readonly TextBlock _bodyText;

    private readonly TextBlock _hintText;

    private readonly TextBlock _pageText;

    private readonly Button _backButton;

    private readonly Button _nextButton;

    private int _pageIndex;

    public IntroductionWindow()
    {
        Title =
            SettingsService.L(
                "Thermiqra — знакомство",
                "Thermiqra — introduction");

        double availableWidth =
            Math.Max(
                700,
                SystemParameters.WorkArea.Width -
                40);

        double availableHeight =
            Math.Max(
                560,
                SystemParameters.WorkArea.Height -
                40);

        Width =
            Math.Min(
                1400,
                availableWidth);

        Height =
            Math.Min(
                820,
                availableHeight);

        MinWidth =
            Math.Min(
                900,
                availableWidth);

        MinHeight =
            Math.Min(
                620,
                availableHeight);

        WindowStartupLocation =
            WindowStartupLocation.CenterOwner;

        ResizeMode =
            ResizeMode.CanResize;

        FontFamily =
            new FontFamily(
                "Segoe UI");

        UseLayoutRounding =
            true;

        SnapsToDevicePixels =
            true;

        SetResourceReference(
            BackgroundProperty,
            "WindowBackgroundBrush");

        SetResourceReference(
            ForegroundProperty,
            "PrimaryTextBrush");

        _pages =
            CreatePages();

        Grid root =
            new()
            {
                Margin =
                    new Thickness(
                        28,
                        22,
                        28,
                        22)
            };

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        Border header =
            CreateFrame(
                new Thickness(
                    20,
                    14,
                    20,
                    14));

        header.Margin =
            new Thickness(
                0,
                0,
                0,
                16);

        Grid headerGrid =
            new();

        headerGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        headerGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        StackPanel headerText =
            new()
            {
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        TextBlock brand =
            new()
            {
                Text =
                    "THERMIQRA",

                FontSize =
                    23,

                FontWeight =
                    FontWeights.Bold
            };

        brand.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        StackPanel subtitleRow =
            new()
            {
                Orientation =
                    Orientation.Horizontal,

                Margin =
                    new Thickness(
                        0,
                        4,
                        0,
                        0)
            };

        Border accentLine =
            new()
            {
                Width =
                    34,

                Height =
                    2,

                Margin =
                    new Thickness(
                        0,
                        0,
                        9,
                        0),

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        accentLine.SetResourceReference(
            Border.BackgroundProperty,
            "AccentBrush");

        TextBlock subtitle =
            new()
            {
                Text =
                    SettingsService.L(
                        "КОРОТКО О ГЛАВНОМ",
                        "THE ESSENTIALS"),

                FontSize =
                    9,

                FontWeight =
                    FontWeights.SemiBold
            };

        subtitle.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        subtitleRow.Children.Add(
            accentLine);

        subtitleRow.Children.Add(
            subtitle);

        headerText.Children.Add(
            brand);

        headerText.Children.Add(
            subtitleRow);

        Border counterBadge =
            new()
            {
                Padding =
                    new Thickness(
                        14,
                        8,
                        14,
                        8),

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        counterBadge.SetResourceReference(
            Border.BackgroundProperty,
            "CardBackgroundBrush");

        counterBadge.SetResourceReference(
            Border.BorderBrushProperty,
            "BorderBrush");

        counterBadge.BorderThickness =
            new Thickness(1);

        counterBadge.SetResourceReference(
            Border.CornerRadiusProperty,
            "PanelCornerRadius");

        _pageText =
            new TextBlock
            {
                FontSize =
                    13,

                FontWeight =
                    FontWeights.Bold
            };

        _pageText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        counterBadge.Child =
            _pageText;

        Grid.SetColumn(
            counterBadge,
            1);

        headerGrid.Children.Add(
            headerText);

        headerGrid.Children.Add(
            counterBadge);

        header.Child =
            headerGrid;

        Grid.SetRow(
            header,
            0);

        root.Children.Add(
            header);

        Grid pageGrid =
            new();

        pageGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1.17,
                        GridUnitType.Star)
            });

        pageGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(22)
            });

        pageGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        0.83,
                        GridUnitType.Star)
            });

        _visualHost =
            CreateFrame(
                new Thickness(
                    24));

        _visualHost.MinHeight =
            470;

        Grid.SetColumn(
            _visualHost,
            0);

        pageGrid.Children.Add(
            _visualHost);

        Border textCard =
            CreateFrame(
                new Thickness(
                    30,
                    28,
                    30,
                    28));

        Grid.SetColumn(
            textCard,
            2);

        StackPanel textContent =
            new()
            {
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        _sectionText =
            new TextBlock
            {
                FontSize =
                    10,

                FontWeight =
                    FontWeights.Bold
            };

        _sectionText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        _titleText =
            new TextBlock
            {
                FontSize =
                    28,

                FontWeight =
                    FontWeights.Bold,

                TextWrapping =
                    TextWrapping.Wrap,

                Margin =
                    new Thickness(
                        0,
                        12,
                        0,
                        0)
            };

        _titleText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        _bodyText =
            new TextBlock
            {
                FontSize =
                    14,

                LineHeight =
                    23,

                TextWrapping =
                    TextWrapping.Wrap,

                Margin =
                    new Thickness(
                        0,
                        22,
                        0,
                        0)
            };

        _bodyText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        Border hintFrame =
            new()
            {
                Padding =
                    new Thickness(
                        15,
                        12,
                        15,
                        12),

                BorderThickness =
                    new Thickness(
                        0,
                        0,
                        0,
                        0),

                Margin =
                    new Thickness(
                        0,
                        24,
                        0,
                        0)
            };

        hintFrame.SetResourceReference(
            Border.BackgroundProperty,
            "WindowBackgroundBrush");

        hintFrame.SetResourceReference(
            Border.CornerRadiusProperty,
            "PanelCornerRadius");

        _hintText =
            new TextBlock
            {
                FontSize =
                    11,

                LineHeight =
                    18,

                TextWrapping =
                    TextWrapping.Wrap
            };

        _hintText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        hintFrame.Child =
            _hintText;

        textContent.Children.Add(
            _sectionText);

        textContent.Children.Add(
            _titleText);

        textContent.Children.Add(
            _bodyText);

        textContent.Children.Add(
            hintFrame);

        textCard.Child =
            textContent;

        pageGrid.Children.Add(
            textCard);

        Grid.SetRow(
            pageGrid,
            1);

        root.Children.Add(
            pageGrid);

        Grid footer =
            new()
            {
                Margin =
                    new Thickness(
                        0,
                        18,
                        0,
                        0)
            };

        footer.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        footer.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        footer.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        footer.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(12)
            });

        footer.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        Button closeButton =
            CreateThemedButton(
                SettingsService.L(
                    "ЗАКРЫТЬ",
                    "CLOSE"),
                primary: false);

        closeButton.Click +=
            (_, _) =>
                Close();

        _backButton =
            CreateThemedButton(
                SettingsService.L(
                    "НАЗАД",
                    "BACK"),
                primary: false);

        _backButton.Click +=
            BackButton_Click;

        _nextButton =
            CreateThemedButton(
                SettingsService.L(
                    "ДАЛЕЕ",
                    "NEXT"),
                primary: true);

        _nextButton.MinWidth =
            126;

        _nextButton.Click +=
            NextButton_Click;

        Grid.SetColumn(
            closeButton,
            0);

        Grid.SetColumn(
            _backButton,
            2);

        Grid.SetColumn(
            _nextButton,
            4);

        footer.Children.Add(
            closeButton);

        footer.Children.Add(
            _backButton);

        footer.Children.Add(
            _nextButton);

        Grid.SetRow(
            footer,
            2);

        root.Children.Add(
            footer);

        Content =
            root;

        UpdatePage();
    }

    internal static Button CreateThemedButton(
        string text,
        bool primary)
    {
        Button button =
            new()
            {
                Content =
                    text,

                Tag =
                    primary
                        ? PrimaryButtonTag
                        : SecondaryButtonTag,

                MinWidth =
                    108,

                Height =
                    40,

                Padding =
                    new Thickness(
                        15,
                        6,
                        15,
                        6),

                FontSize =
                    10,

                FontWeight =
                    FontWeights.Bold,

                Cursor =
                    Cursors.Hand,

                BorderThickness =
                    new Thickness(1),

                Template =
                    CreateButtonTemplate()
            };

        button.MouseEnter +=
            Button_MouseEnter;

        button.MouseLeave +=
            Button_MouseLeave;

        button.PreviewMouseLeftButtonDown +=
            Button_MouseDown;

        button.PreviewMouseLeftButtonUp +=
            Button_MouseUp;

        button.IsEnabledChanged +=
            Button_IsEnabledChanged;

        ApplyButtonVisual(
            button,
            hovered: false,
            pressed: false);

        return button;
    }

    private static ControlTemplate
        CreateButtonTemplate()
    {
        FrameworkElementFactory border =
            new(
                typeof(Border));

        border.Name =
            "ButtonBorder";

        border.SetValue(
            Border.BackgroundProperty,
            new TemplateBindingExtension(
                Control.BackgroundProperty));

        border.SetValue(
            Border.BorderBrushProperty,
            new TemplateBindingExtension(
                Control.BorderBrushProperty));

        border.SetValue(
            Border.BorderThicknessProperty,
            new TemplateBindingExtension(
                Control.BorderThicknessProperty));

        border.SetValue(
            Border.CornerRadiusProperty,
            new CornerRadius(4));

        border.SetValue(
            Border.PaddingProperty,
            new TemplateBindingExtension(
                Control.PaddingProperty));

        FrameworkElementFactory presenter =
            new(
                typeof(ContentPresenter));

        presenter.SetValue(
            FrameworkElement.HorizontalAlignmentProperty,
            HorizontalAlignment.Center);

        presenter.SetValue(
            FrameworkElement.VerticalAlignmentProperty,
            VerticalAlignment.Center);

        border.AppendChild(
            presenter);

        return new ControlTemplate(
            typeof(Button))
        {
            VisualTree =
                border
        };
    }

    private static void Button_MouseEnter(
        object sender,
        MouseEventArgs e)
    {
        if (sender is Button button)
        {
            ApplyButtonVisual(
                button,
                hovered: true,
                pressed: false);
        }
    }

    private static void Button_MouseLeave(
        object sender,
        MouseEventArgs e)
    {
        if (sender is Button button)
        {
            ApplyButtonVisual(
                button,
                hovered: false,
                pressed: false);
        }
    }

    private static void Button_MouseDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is Button button)
        {
            ApplyButtonVisual(
                button,
                hovered: true,
                pressed: true);
        }
    }

    private static void Button_MouseUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is Button button)
        {
            ApplyButtonVisual(
                button,
                hovered:
                    button.IsMouseOver,
                pressed:
                    false);
        }
    }

    private static void Button_IsEnabledChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (sender is Button button)
        {
            ApplyButtonVisual(
                button,
                hovered:
                    false,
                pressed:
                    false);
        }
    }

    private static void ApplyButtonVisual(
        Button button,
        bool hovered,
        bool pressed)
    {
        if (!button.IsEnabled)
        {
            button.SetResourceReference(
                Control.BackgroundProperty,
                "WindowBackgroundBrush");

            button.SetResourceReference(
                Control.BorderBrushProperty,
                "BorderBrush");

            button.SetResourceReference(
                Control.ForegroundProperty,
                "SecondaryTextBrush");

            button.Opacity =
                0.42;

            button.Cursor =
                Cursors.Arrow;

            return;
        }

        button.Opacity =
            pressed
                ? 0.68
                : 1.0;

        button.Cursor =
            Cursors.Hand;

        if (hovered)
        {
            button.SetResourceReference(
                Control.BackgroundProperty,
                "AccentBrush");

            button.SetResourceReference(
                Control.BorderBrushProperty,
                "AccentBrush");

            button.SetResourceReference(
                Control.ForegroundProperty,
                "WindowBackgroundBrush");

            return;
        }

        bool primary =
            string.Equals(
                button.Tag as string,
                PrimaryButtonTag,
                StringComparison.Ordinal);

        button.SetResourceReference(
            Control.BackgroundProperty,
            "CardBackgroundBrush");

        button.SetResourceReference(
            Control.BorderBrushProperty,
            primary
                ? "AccentBrush"
                : "BorderBrush");

        button.SetResourceReference(
            Control.ForegroundProperty,
            primary
                ? "AccentBrush"
                : "PrimaryTextBrush");
    }

    private static Border CreateFrame(
        Thickness padding)
    {
        Border border =
            new()
            {
                Padding =
                    padding,

                BorderThickness =
                    new Thickness(1)
            };

        border.SetResourceReference(
            Border.BackgroundProperty,
            "CardBackgroundBrush");

        border.SetResourceReference(
            Border.BorderBrushProperty,
            "BorderBrush");

        border.SetResourceReference(
            Border.CornerRadiusProperty,
            "PanelCornerRadius");

        return border;
    }

    private static List<IntroductionPage>
        CreatePages()
    {
        return
            new List<IntroductionPage>
            {
                new(
                    SettingsService.L(
                        "01 // ГЛАВНЫЙ ЭКРАН",
                        "01 // MAIN SCREEN"),

                    SettingsService.L(
                        "Главное — видно сразу",
                        "The essentials at a glance"),

                    SettingsService.L(
                        "CPU — процессор. GPU — видеокарта. RAM — оперативная память. " +
                        "Накопители — SSD и жёсткие диски.\n\n" +
                        "На главном экране Thermiqra показывает их состояние прямо сейчас: " +
                        "нагрузку, температуры, использование памяти и свободное место.",
                        "CPU is the processor. GPU is the graphics card. RAM is system memory. " +
                        "Storage means SSDs and hard drives.\n\n" +
                        "The Thermiqra main screen shows their current state: " +
                        "load, temperatures, memory usage and free disk space."),

                    SettingsService.L(
                        "Не нужно запоминать все цифры. Главное окно нужно для быстрого взгляда: " +
                        "всё спокойно или какой-то показатель требует внимания.",
                        "You do not need to memorize every number. The main screen is for a quick check: " +
                        "everything is calm, or a value needs attention."),

                    BuildMainScreenVisual),

                new(
                    SettingsService.L(
                        "02 // ЦВЕТА",
                        "02 // COLORS"),

                    SettingsService.L(
                        "Цвет показывает важность",
                        "Color shows importance"),

                    SettingsService.L(
                        "Цвет помогает понять показатель без знания технических норм. " +
                        "Зелёный означает обычное состояние, жёлтый — повышенное, " +
                        "оранжевый — высокое, красный — критическое.\n\n" +
                        "Порог зависит от конкретного показателя: температура и загрузка " +
                        "не оцениваются одинаково.",
                        "Color helps you understand a value without knowing technical limits. " +
                        "Green means normal, yellow means elevated, orange means high, " +
                        "and red means critical.\n\n" +
                        "The threshold depends on the metric: temperature and load " +
                        "are not evaluated in the same way."),

                    SettingsService.L(
                        "Один короткий скачок ещё не означает проблему. Thermiqra использует историю, " +
                        "когда для вывода важна повторяемость.",
                        "One short spike does not automatically mean a problem. Thermiqra uses history " +
                        "when recurrence matters for a conclusion."),

                    BuildColorVisual),

                new(
                    SettingsService.L(
                        "03 // ПОДРОБНОСТИ",
                        "03 // DETAILS"),

                    SettingsService.L(
                        "Куда идти дальше",
                        "Where to go next"),

                    SettingsService.L(
                        "Если на главном экране что-то привлекло внимание, подробности уже разложены " +
                        "по понятным разделам.\n\n" +
                        "«Система» показывает установленное железо и вкладку «Анализ». " +
                        "«Статистика» показывает историю. Кнопка «Анализ» возле диска помогает понять, " +
                        "чем занято место.",
                        "If something on the main screen catches your attention, details are already " +
                        "organized into clear sections.\n\n" +
                        "System shows installed hardware and the Analysis tab. " +
                        "Statistics shows history. The Analyze button next to a drive helps explain " +
                        "where disk space is being used."),

                    SettingsService.L(
                        "Thermiqra старается сначала объяснить ситуацию простым языком, " +
                        "а технические детали оставляет доступными для тех, кому они нужны.",
                        "Thermiqra tries to explain the situation in plain language first, " +
                        "while keeping technical details available for those who need them."),

                    BuildDetailsVisual),

                new(
                    SettingsService.L(
                        "04 // ФОНОВАЯ РАБОТА",
                        "04 // BACKGROUND"),

                    SettingsService.L(
                        "Thermiqra не нужно держать открытой",
                        "Thermiqra does not need to stay open"),

                    SettingsService.L(
                        "После настройки программу можно убрать с экрана. Thermiqra продолжит работать " +
                        "в системном трее и следить за компьютером.\n\n" +
                        "Если температура или заполнение диска достигнут заданного уровня, " +
                        "программа может показать предупреждение. Уведомления и пороги настраиваются.",
                        "After setup, you can remove the program from the screen. Thermiqra keeps running " +
                        "in the system tray and continues monitoring the computer.\n\n" +
                        "If temperature or disk usage reaches a configured level, " +
                        "the program can show an alert. Notifications and thresholds are configurable."),

                    SettingsService.L(
                        "Это знакомство можно открыть снова в Настройках. " +
                        "Настройки и история при повторном просмотре не сбрасываются.",
                        "You can open this introduction again from Settings. " +
                        "Settings and history are not reset when you view it again."),

                    BuildTrayVisual)
            };
    }

    private static UIElement BuildMainScreenVisual()
    {
        Grid root =
            new();

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        TextBlock heading =
            CreateVisualHeading(
                SettingsService.L(
                    "ТАК ЭТО ВЫГЛЯДИТ НА ГЛАВНОМ ЭКРАНЕ",
                    "THIS IS HOW IT LOOKS ON THE MAIN SCREEN"));

        root.Children.Add(
            heading);

        UniformGrid sensors =
            new()
            {
                Rows =
                    2,

                Columns =
                    2,

                Margin =
                    new Thickness(
                        0,
                        18,
                        0,
                        0)
            };

        Grid.SetRow(
            sensors,
            1);

        string skin =
            SettingsService
                .Current
                .SkinName
            ?? "CyberTech";

        sensors.Children.Add(
            CreateSkinSensorCard(
                skin,
                "CPU",
                SettingsService.L(
                    "ПРОЦЕССОР",
                    "PROCESSOR"),
                "42 °C",
                0.42,
                SensorKind.Temperature));

        sensors.Children.Add(
            CreateSkinSensorCard(
                skin,
                "GPU",
                SettingsService.L(
                    "ВИДЕОКАРТА",
                    "GRAPHICS"),
                "51 °C",
                0.51,
                SensorKind.Temperature));

        sensors.Children.Add(
            CreateSkinSensorCard(
                skin,
                "RAM",
                SettingsService.L(
                    "ОПЕРАТИВНАЯ ПАМЯТЬ",
                    "SYSTEM MEMORY"),
                "35 %",
                0.35,
                SensorKind.Memory));

        sensors.Children.Add(
            CreateSkinSensorCard(
                skin,
                "SSD",
                SettingsService.L(
                    "НАКОПИТЕЛЬ",
                    "STORAGE"),
                SettingsService.L(
                    "62 % свободно",
                    "62% free"),
                0.62,
                SensorKind.Storage));

        root.Children.Add(
            sensors);

        return root;
    }


    private static Border CreateSkinSensorCard(
        string skin,
        string shortName,
        string title,
        string value,
        double level,
        SensorKind kind)
    {
        return skin switch
        {
            "SteamPunk" =>
                CreateSteamSensorCard(
                    shortName,
                    title,
                    value,
                    level,
                    kind),

            "FrostCore" =>
                CreateFrostSensorCard(
                    shortName,
                    title,
                    value,
                    level,
                    kind),

            "MilitaryOps" =>
                CreateMilitarySensorCard(
                    shortName,
                    title,
                    value,
                    level,
                    kind),

            _ =>
                CreateCyberSensorCard(
                    shortName,
                    title,
                    value,
                    level,
                    kind)
        };
    }


    private static Border CreateCyberSensorCard(
        string shortName,
        string title,
        string value,
        double level,
        SensorKind kind)
    {
        Border card =
            CreateSubCard(
                new Thickness(
                    16));

        card.Margin =
            new Thickness(6);

        Grid root =
            new();

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        Border topStrip =
            new()
            {
                Height =
                    3,

                Width =
                    86,

                HorizontalAlignment =
                    HorizontalAlignment.Left,

                VerticalAlignment =
                    VerticalAlignment.Top,

                Margin =
                    new Thickness(
                        0,
                        -16,
                        0,
                        0)
            };

        topStrip.SetResourceReference(
            Border.BackgroundProperty,
            "AccentBrush");

        root.Children.Add(
            topStrip);

        Grid header =
            new()
            {
                Margin =
                    new Thickness(
                        0,
                        4,
                        0,
                        0)
            };

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        Border symbol =
            new()
            {
                Width =
                    56,

                Height =
                    56,

                BorderThickness =
                    new Thickness(2),

                Margin =
                    new Thickness(
                        0,
                        0,
                        14,
                        0)
            };

        symbol.SetResourceReference(
            Border.BorderBrushProperty,
            "AccentBrush");

        TextBlock symbolText =
            new()
            {
                Text =
                    shortName,

                FontSize =
                    14,

                FontWeight =
                    FontWeights.Bold,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        symbolText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        symbol.Child =
            symbolText;

        StackPanel labels =
            new()
            {
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        TextBlock titleText =
            CreateSensorTitle(
                title);

        TextBlock valueText =
            CreateSensorValue(
                value);

        labels.Children.Add(
            titleText);

        labels.Children.Add(
            valueText);

        Grid.SetColumn(
            labels,
            1);

        header.Children.Add(
            symbol);

        header.Children.Add(
            labels);

        Grid.SetRow(
            header,
            1);

        root.Children.Add(
            header);

        Border track =
            CreateSensorTrack(
                level,
                9);

        track.Margin =
            new Thickness(
                0,
                18,
                0,
                0);

        Grid.SetRow(
            track,
            2);

        root.Children.Add(
            track);

        card.Child =
            root;

        return card;
    }


    private static Border CreateSteamSensorCard(
        string shortName,
        string title,
        string value,
        double level,
        SensorKind kind)
    {
        Border card =
            CreateSubCard(
                new Thickness(
                    16));

        card.Margin =
            new Thickness(6);

        card.BorderThickness =
            new Thickness(2);

        Grid root =
            new();

        root.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(118)
            });

        root.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        UIElement instrument =
            kind == SensorKind.Memory
                ? CreateSteamMemoryPipe(
                    level)
                : CreateSteamRoundGauge(
                    shortName,
                    level);

        root.Children.Add(
            instrument);

        StackPanel labels =
            new()
            {
                Margin =
                    new Thickness(
                        14,
                        2,
                        0,
                        0),

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        TextBlock titleText =
            CreateSensorTitle(
                title);

        titleText.FontFamily =
            new FontFamily(
                "Georgia");

        TextBlock valueText =
            CreateSensorValue(
                value);

        valueText.FontFamily =
            new FontFamily(
                "Georgia");

        TextBlock status =
            new()
            {
                Text =
                    SettingsService.L(
                        "Датчик активен",
                        "Sensor active"),

                FontSize =
                    12,

                Margin =
                    new Thickness(
                        0,
                        12,
                        0,
                        0)
            };

        status.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        labels.Children.Add(
            titleText);

        labels.Children.Add(
            valueText);

        labels.Children.Add(
            status);

        Grid.SetColumn(
            labels,
            1);

        root.Children.Add(
            labels);

        card.Child =
            root;

        return card;
    }


    private static UIElement CreateSteamRoundGauge(
        string shortName,
        double level)
    {
        Grid gauge =
            new()
            {
                Width =
                    104,

                Height =
                    104,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        Ellipse outer =
            new()
            {
                StrokeThickness =
                    4
            };

        outer.SetResourceReference(
            Shape.StrokeProperty,
            "AccentBrush");

        Ellipse inner =
            new()
            {
                Width =
                    82,

                Height =
                    82,

                StrokeThickness =
                    2
            };

        inner.SetResourceReference(
            Shape.StrokeProperty,
            "BorderBrush");

        Border crossVertical =
            new()
            {
                Width =
                    2,

                Height =
                    68,

                Opacity =
                    0.45
            };

        crossVertical.SetResourceReference(
            Border.BackgroundProperty,
            "BorderBrush");

        Border crossHorizontal =
            new()
            {
                Width =
                    68,

                Height =
                    2,

                Opacity =
                    0.45
            };

        crossHorizontal.SetResourceReference(
            Border.BackgroundProperty,
            "BorderBrush");

        double angle =
            -118 +
            236 *
            Math.Clamp(
                level,
                0,
                1);

        Line needle =
            new()
            {
                X1 =
                    52,

                Y1 =
                    52,

                X2 =
                    52,

                Y2 =
                    18,

                StrokeThickness =
                    3,

                RenderTransformOrigin =
                    new Point(
                        0.5,
                        1.0),

                RenderTransform =
                    new RotateTransform(
                        angle)
            };

        needle.SetResourceReference(
            Shape.StrokeProperty,
            "AccentBrush");

        Ellipse hub =
            new()
            {
                Width =
                    13,

                Height =
                    13
            };

        hub.SetResourceReference(
            Shape.FillProperty,
            "AccentBrush");

        TextBlock label =
            new()
            {
                Text =
                    shortName,

                FontSize =
                    10,

                FontWeight =
                    FontWeights.Bold,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Bottom,

                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        15)
            };

        label.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        gauge.Children.Add(
            outer);

        gauge.Children.Add(
            inner);

        gauge.Children.Add(
            crossVertical);

        gauge.Children.Add(
            crossHorizontal);

        gauge.Children.Add(
            needle);

        gauge.Children.Add(
            hub);

        gauge.Children.Add(
            label);

        return gauge;
    }


    private static UIElement CreateSteamMemoryPipe(
        double level)
    {
        Grid root =
            new()
            {
                Width =
                    104,

                Height =
                    104
            };

        Border pipe =
            new()
            {
                Width =
                    82,

                Height =
                    32,

                BorderThickness =
                    new Thickness(3),

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center,

                Padding =
                    new Thickness(5)
            };

        pipe.SetResourceReference(
            Border.BorderBrushProperty,
            "AccentBrush");

        pipe.SetResourceReference(
            Border.CornerRadiusProperty,
            "ProgressCornerRadius");

        Grid track =
            new()
            {
                ClipToBounds =
                    true
            };

        Border fill =
            new()
            {
                Width =
                    66 *
                    Math.Clamp(
                        level,
                        0,
                        1),

                HorizontalAlignment =
                    HorizontalAlignment.Left
            };

        fill.SetResourceReference(
            Border.BackgroundProperty,
            "AccentBrush");

        track.Children.Add(
            fill);

        pipe.Child =
            track;

        TextBlock percent =
            new()
            {
                Text =
                    $"{level * 100:F0}%",

                FontSize =
                    16,

                FontWeight =
                    FontWeights.Bold,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Bottom,

                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        10)
            };

        percent.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        root.Children.Add(
            pipe);

        root.Children.Add(
            percent);

        return root;
    }


    private static Border CreateFrostSensorCard(
        string shortName,
        string title,
        string value,
        double level,
        SensorKind kind)
    {
        Border card =
            CreateSubCard(
                new Thickness(
                    16));

        card.Margin =
            new Thickness(6);

        card.BorderThickness =
            new Thickness(2);

        card.SetResourceReference(
            Border.CornerRadiusProperty,
            "PanelCornerRadius");

        Grid root =
            new();

        root.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(106)
            });

        root.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        Grid capsule =
            new()
            {
                Width =
                    78,

                Height =
                    108,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center,

                ClipToBounds =
                    true
            };

        Border capsuleBody =
            new()
            {
                BorderThickness =
                    new Thickness(2),

                CornerRadius =
                    new CornerRadius(28)
            };

        capsuleBody.SetResourceReference(
            Border.BorderBrushProperty,
            "AccentBrush");

        Grid capsuleInside =
            new()
            {
                Margin =
                    new Thickness(7),

                ClipToBounds =
                    true
            };

        Border fill =
            new()
            {
                Height =
                    86 *
                    Math.Clamp(
                        level,
                        0,
                        1),

                VerticalAlignment =
                    VerticalAlignment.Bottom,

                Opacity =
                    0.72,

                CornerRadius =
                    new CornerRadius(18)
            };

        fill.SetResourceReference(
            Border.BackgroundProperty,
            "AccentBrush");

        TextBlock snow =
            new()
            {
                Text =
                    "❄",

                FontSize =
                    25,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center,

                Opacity =
                    0.65
            };

        snow.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        capsuleInside.Children.Add(
            fill);

        capsuleInside.Children.Add(
            snow);

        capsuleBody.Child =
            capsuleInside;

        capsule.Children.Add(
            capsuleBody);

        StackPanel labels =
            new()
            {
                Margin =
                    new Thickness(
                        8,
                        0,
                        0,
                        0),

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        TextBlock code =
            new()
            {
                Text =
                    shortName,

                FontSize =
                    11,

                FontWeight =
                    FontWeights.Bold
            };

        code.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        TextBlock titleText =
            CreateSensorTitle(
                title);

        titleText.Margin =
            new Thickness(
                0,
                5,
                0,
                0);

        TextBlock valueText =
            CreateSensorValue(
                value);

        labels.Children.Add(
            code);

        labels.Children.Add(
            titleText);

        labels.Children.Add(
            valueText);

        Grid.SetColumn(
            labels,
            1);

        root.Children.Add(
            capsule);

        root.Children.Add(
            labels);

        card.Child =
            root;

        return card;
    }


    private static Border CreateMilitarySensorCard(
        string shortName,
        string title,
        string value,
        double level,
        SensorKind kind)
    {
        Border card =
            CreateSubCard(
                new Thickness(
                    15));

        card.Margin =
            new Thickness(6);

        card.BorderThickness =
            new Thickness(2);

        card.CornerRadius =
            new CornerRadius(2);

        Grid root =
            new();

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        Grid header =
            new();

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        Grid reticle =
            new()
            {
                Width =
                    52,

                Height =
                    52,

                Margin =
                    new Thickness(
                        0,
                        0,
                        14,
                        0)
            };

        Ellipse ring =
            new()
            {
                StrokeThickness =
                    2
            };

        ring.SetResourceReference(
            Shape.StrokeProperty,
            "AccentBrush");

        Border vertical =
            new()
            {
                Width =
                    1,

                Height =
                    44
            };

        vertical.SetResourceReference(
            Border.BackgroundProperty,
            "BorderBrush");

        Border horizontal =
            new()
            {
                Width =
                    44,

                Height =
                    1
            };

        horizontal.SetResourceReference(
            Border.BackgroundProperty,
            "BorderBrush");

        TextBlock code =
            new()
            {
                Text =
                    shortName,

                FontSize =
                    9,

                FontWeight =
                    FontWeights.Bold,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        code.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        reticle.Children.Add(
            ring);

        reticle.Children.Add(
            vertical);

        reticle.Children.Add(
            horizontal);

        reticle.Children.Add(
            code);

        StackPanel labels =
            new()
            {
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        labels.Children.Add(
            CreateSensorTitle(
                title));

        labels.Children.Add(
            CreateSensorValue(
                value));

        Grid.SetColumn(
            labels,
            1);

        header.Children.Add(
            reticle);

        header.Children.Add(
            labels);

        root.Children.Add(
            header);

        TextBlock state =
            new()
            {
                Text =
                    SettingsService.L(
                        "СТАТУС: НОРМА",
                        "STATUS: NORMAL"),

                FontSize =
                    11,

                FontWeight =
                    FontWeights.Bold,

                Margin =
                    new Thickness(
                        0,
                        16,
                        0,
                        0)
            };

        state.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        Grid.SetRow(
            state,
            1);

        root.Children.Add(
            state);

        Grid bars =
            new()
            {
                Height =
                    12,

                Margin =
                    new Thickness(
                        0,
                        12,
                        0,
                        0)
            };

        for (int i = 0;
             i < 10;
             i++)
        {
            bars.ColumnDefinitions.Add(
                new ColumnDefinition());
        }

        int active =
            Math.Max(
                1,
                (int)Math.Round(
                    Math.Clamp(
                        level,
                        0,
                        1) *
                    10));

        for (int i = 0;
             i < 10;
             i++)
        {
            Border segment =
                new()
                {
                    Margin =
                        new Thickness(
                            2,
                            0,
                            2,
                            0),

                    Opacity =
                        i < active
                            ? 1.0
                            : 0.22
                };

            segment.SetResourceReference(
                Border.BackgroundProperty,
                i < active
                    ? "AccentBrush"
                    : "BorderBrush");

            Grid.SetColumn(
                segment,
                i);

            bars.Children.Add(
                segment);
        }

        Grid.SetRow(
            bars,
            2);

        root.Children.Add(
            bars);

        card.Child =
            root;

        return card;
    }


    private static TextBlock CreateSensorTitle(
        string title)
    {
        TextBlock text =
            new()
            {
                Text =
                    title,

                FontSize =
                    12,

                FontWeight =
                    FontWeights.Bold,

                TextWrapping =
                    TextWrapping.Wrap
            };

        text.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        return text;
    }


    private static TextBlock CreateSensorValue(
        string value)
    {
        TextBlock text =
            new()
            {
                Text =
                    value,

                FontSize =
                    23,

                FontWeight =
                    FontWeights.Bold,

                Margin =
                    new Thickness(
                        0,
                        4,
                        0,
                        0),

                TextWrapping =
                    TextWrapping.Wrap
            };

        text.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        return text;
    }


    private static Border CreateSensorTrack(
        double level,
        double height)
    {
        Border track =
            new()
            {
                Height =
                    height,

                ClipToBounds =
                    true
            };

        track.SetResourceReference(
            Border.BackgroundProperty,
            "GaugeTrackBrush");

        track.SetResourceReference(
            Border.CornerRadiusProperty,
            "ProgressCornerRadius");

        Grid trackGrid =
            new();

        Border fill =
            new()
            {
                HorizontalAlignment =
                    HorizontalAlignment.Left,

                Width =
                    150 *
                    Math.Clamp(
                        level,
                        0,
                        1)
            };

        fill.SetResourceReference(
            Border.BackgroundProperty,
            "AccentBrush");

        fill.SetResourceReference(
            Border.CornerRadiusProperty,
            "ProgressCornerRadius");

        trackGrid.Children.Add(
            fill);

        track.Child =
            trackGrid;

        return track;
    }


    private static UIElement BuildColorVisual()
    {
        StackPanel root =
            new();

        root.Children.Add(
            CreateVisualHeading(
                SettingsService.L(
                    "БЫСТРАЯ ОЦЕНКА",
                    "QUICK STATUS")));

        UniformGrid states =
            new()
            {
                Rows =
                    4,

                Columns =
                    1,

                Margin =
                    new Thickness(
                        0,
                        18,
                        0,
                        0)
            };

        states.Children.Add(
            CreateStatusRow(
                Color.FromRgb(
                    57,
                    178,
                    105),
                SettingsService.L(
                    "НОРМАЛЬНО",
                    "NORMAL"),
                SettingsService.L(
                    "Обычное рабочее состояние",
                    "Normal operating state")));

        states.Children.Add(
            CreateStatusRow(
                Color.FromRgb(
                    218,
                    184,
                    52),
                SettingsService.L(
                    "ПОВЫШЕНО",
                    "ELEVATED"),
                SettingsService.L(
                    "Стоит обратить внимание",
                    "Worth paying attention")));

        states.Children.Add(
            CreateStatusRow(
                Color.FromRgb(
                    240,
                    136,
                    38),
                SettingsService.L(
                    "ВЫСОКО",
                    "HIGH"),
                SettingsService.L(
                    "Показатель уже заметно выше обычного",
                    "The value is clearly above normal")));

        states.Children.Add(
            CreateStatusRow(
                Color.FromRgb(
                    213,
                    67,
                    67),
                SettingsService.L(
                    "КРИТИЧЕСКИ",
                    "CRITICAL"),
                SettingsService.L(
                    "Требует внимания",
                    "Needs attention")));

        root.Children.Add(
            states);

        return root;
    }

    private static UIElement BuildDetailsVisual()
    {
        StackPanel root =
            new();

        root.Children.Add(
            CreateVisualHeading(
                SettingsService.L(
                    "ПОДРОБНОСТИ БЕЗ ПЕРЕГРУЗКИ",
                    "DETAILS WITHOUT CLUTTER")));

        UniformGrid cards =
            new()
            {
                Rows =
                    1,

                Columns =
                    3,

                Margin =
                    new Thickness(
                        0,
                        20,
                        0,
                        0)
            };

        cards.Children.Add(
            CreateActionCard(
                "01",
                SettingsService.L(
                    "СИСТЕМА",
                    "SYSTEM"),
                SettingsService.L(
                    "Железо и понятный анализ состояния.",
                    "Hardware details and plain-language analysis.")));

        cards.Children.Add(
            CreateActionCard(
                "02",
                SettingsService.L(
                    "СТАТИСТИКА",
                    "STATISTICS"),
                SettingsService.L(
                    "История температур и нагрузки.",
                    "History of temperatures and load.")));

        cards.Children.Add(
            CreateActionCard(
                "03",
                SettingsService.L(
                    "АНАЛИЗ ДИСКА",
                    "DISK ANALYSIS"),
                SettingsService.L(
                    "Показывает, чем занято место.",
                    "Shows what uses disk space.")));

        root.Children.Add(
            cards);

        TextBlock note =
            new()
            {
                Text =
                    SettingsService.L(
                        "Все подробные разделы открываются из уже знакомого главного окна.",
                        "All detailed sections open from the familiar main window."),

                TextWrapping =
                    TextWrapping.Wrap,

                FontSize =
                    11,

                Margin =
                    new Thickness(
                        2,
                        22,
                        2,
                        0)
            };

        note.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        root.Children.Add(
            note);

        return root;
    }

    private static UIElement BuildTrayVisual()
    {
        Grid root =
            new();

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        root.Children.Add(
            CreateVisualHeading(
                SettingsService.L(
                    "МОНИТОРИНГ ПРОДОЛЖАЕТСЯ",
                    "MONITORING CONTINUES")));

        Border notification =
            CreateSubCard(
                new Thickness(
                    22));

        notification.Margin =
            new Thickness(
                0,
                20,
                0,
                18);

        Grid notificationGrid =
            new();

        notificationGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        notificationGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        Border iconFrame =
            new()
            {
                Width =
                    74,

                Height =
                    74,

                Margin =
                    new Thickness(
                        0,
                        0,
                        20,
                        0),

                Padding =
                    new Thickness(10),

                BorderThickness =
                    new Thickness(2),

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        iconFrame.SetResourceReference(
            Border.BorderBrushProperty,
            "AccentBrush");

        iconFrame.SetResourceReference(
            Border.CornerRadiusProperty,
            "PanelCornerRadius");

        iconFrame.Child =
            CreateThermiqraIcon(
                52);

        StackPanel message =
            new()
            {
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        TextBlock messageTitle =
            new()
            {
                Text =
                    SettingsService.L(
                        "Thermiqra работает в фоне",
                        "Thermiqra is running in the background"),

                FontSize =
                    18,

                FontWeight =
                    FontWeights.Bold
            };

        messageTitle.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        TextBlock messageText =
            new()
            {
                Text =
                    SettingsService.L(
                        "Мониторинг активен. Если что-то потребует внимания, программа сообщит.",
                        "Monitoring is active. If something needs attention, the program will let you know."),

                FontSize =
                    13,

                LineHeight =
                    20,

                TextWrapping =
                    TextWrapping.Wrap,

                Margin =
                    new Thickness(
                        0,
                        8,
                        0,
                        0)
            };

        messageText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        message.Children.Add(
            messageTitle);

        message.Children.Add(
            messageText);

        Grid.SetColumn(
            message,
            1);

        notificationGrid.Children.Add(
            iconFrame);

        notificationGrid.Children.Add(
            message);

        notification.Child =
            notificationGrid;

        Grid.SetRow(
            notification,
            1);

        root.Children.Add(
            notification);

        Border tray =
            CreateSubCard(
                new Thickness(
                    16,
                    12,
                    16,
                    12));

        Grid trayGrid =
            new();

        trayGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        trayGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        TextBlock trayText =
            new()
            {
                Text =
                    SettingsService.L(
                        "В трее ищите этот значок Thermiqra",
                        "Look for this Thermiqra icon in the tray"),

                FontSize =
                    13,

                FontWeight =
                    FontWeights.SemiBold,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        trayText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        StackPanel trayIcons =
            new()
            {
                Orientation =
                    Orientation.Horizontal,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        trayIcons.Children.Add(
            CreateTrayDot());

        Border thermiqraTray =
            new()
            {
                Width =
                    38,

                Height =
                    38,

                Margin =
                    new Thickness(
                        10,
                        0,
                        0,
                        0),

                Padding =
                    new Thickness(5),

                BorderThickness =
                    new Thickness(1)
            };

        thermiqraTray.SetResourceReference(
            Border.BorderBrushProperty,
            "AccentBrush");

        thermiqraTray.SetResourceReference(
            Border.CornerRadiusProperty,
            "PanelCornerRadius");

        thermiqraTray.Child =
            CreateThermiqraIcon(
                28);

        trayIcons.Children.Add(
            thermiqraTray);

        Grid.SetColumn(
            trayIcons,
            1);

        trayGrid.Children.Add(
            trayText);

        trayGrid.Children.Add(
            trayIcons);

        tray.Child =
            trayGrid;

        Grid.SetRow(
            tray,
            2);

        root.Children.Add(
            tray);

        return root;
    }


    private static UIElement CreateThermiqraIcon(
        double size)
    {
        string[] resourceUris =
        {
            "pack://application:,,,/Assets/Thermiqra_Tray.ico",
            "pack://application:,,,/Assets/Thermiqra.ico"
        };

        foreach (string resourceUri in
                 resourceUris)
        {
            try
            {
                BitmapDecoder decoder =
                    BitmapDecoder.Create(
                        new Uri(
                            resourceUri,
                            UriKind.Absolute),
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.OnLoad);

                BitmapFrame? bestFrame =
                    null;

                double bestScore =
                    double.MaxValue;

                foreach (BitmapFrame frame in
                         decoder.Frames)
                {
                    double frameSize =
                        Math.Min(
                            frame.PixelWidth,
                            frame.PixelHeight);

                    // Предпочитаем кадр не меньше нужного размера,
                    // чтобы не растягивать маленькую иконку.
                    double score =
                        frameSize >= size
                            ? frameSize - size
                            : (size - frameSize) + 1000;

                    if (score < bestScore)
                    {
                        bestScore =
                            score;

                        bestFrame =
                            frame;
                    }
                }

                if (bestFrame != null)
                {
                    Image image =
                        new()
                        {
                            Source =
                                bestFrame,

                            Width =
                                size,

                            Height =
                                size,

                            Stretch =
                                Stretch.Uniform,

                            SnapsToDevicePixels =
                                true,

                            UseLayoutRounding =
                                true
                        };

                    RenderOptions.SetBitmapScalingMode(
                        image,
                        BitmapScalingMode.HighQuality);

                    return image;
                }
            }
            catch
            {
            }
        }

        Grid fallback =
            new()
            {
                Width =
                    size,

                Height =
                    size
            };

        Ellipse ring =
            new()
            {
                StrokeThickness =
                    2
            };

        ring.SetResourceReference(
            Shape.StrokeProperty,
            "AccentBrush");

        Border vertical =
            new()
            {
                Width =
                    2,

                Height =
                    size *
                    0.72
            };

        vertical.SetResourceReference(
            Border.BackgroundProperty,
            "AccentBrush");

        Border horizontal =
            new()
            {
                Width =
                    size *
                    0.72,

                Height =
                    2
            };

        horizontal.SetResourceReference(
            Border.BackgroundProperty,
            "AccentBrush");

        Ellipse center =
            new()
            {
                Width =
                    size *
                    0.28,

                Height =
                    size *
                    0.28
            };

        center.SetResourceReference(
            Shape.FillProperty,
            "AccentBrush");

        fallback.Children.Add(
            ring);

        fallback.Children.Add(
            vertical);

        fallback.Children.Add(
            horizontal);

        fallback.Children.Add(
            center);

        return fallback;
    }


    private static TextBlock CreateVisualHeading(
        string text)
    {
        TextBlock heading =
            new()
            {
                Text =
                    text,

                FontSize =
                    12,

                FontWeight =
                    FontWeights.Bold
            };

        heading.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        return heading;
    }

    private enum SensorKind
    {
        Temperature,
        Memory,
        Storage
    }


    private static Border CreateStatusRow(
        Color color,
        string title,
        string description)
    {
        Border card =
            CreateSubCard(
                new Thickness(
                    16,
                    13,
                    16,
                    13));

        card.Margin =
            new Thickness(
                0,
                0,
                0,
                10);

        Grid grid =
            new();

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        Ellipse indicator =
            new()
            {
                Width =
                    20,

                Height =
                    20,

                Fill =
                    new SolidColorBrush(
                        color),

                Margin =
                    new Thickness(
                        0,
                        0,
                        16,
                        0),

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        StackPanel text =
            new()
            {
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        TextBlock titleText =
            new()
            {
                Text =
                    title,

                FontSize =
                    12,

                FontWeight =
                    FontWeights.Bold
            };

        titleText.Foreground =
            new SolidColorBrush(
                color);

        TextBlock descriptionText =
            new()
            {
                Text =
                    description,

                FontSize =
                    13,

                LineHeight =
                    19,

                Margin =
                    new Thickness(
                        0,
                        4,
                        0,
                        0)
            };

        descriptionText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        text.Children.Add(
            titleText);

        text.Children.Add(
            descriptionText);

        Grid.SetColumn(
            text,
            1);

        grid.Children.Add(
            indicator);

        grid.Children.Add(
            text);

        card.Child =
            grid;

        return card;
    }

    private static Border CreateActionCard(
        string number,
        string title,
        string description)
    {
        Border card =
            CreateSubCard(
                new Thickness(
                    18));

        card.Margin =
            new Thickness(6);

        StackPanel content =
            new();

        Border numberBadge =
            new()
            {
                Width =
                    38,

                Height =
                    38,

                HorizontalAlignment =
                    HorizontalAlignment.Left,

                BorderThickness =
                    new Thickness(1)
            };

        numberBadge.SetResourceReference(
            Border.BorderBrushProperty,
            "AccentBrush");

        numberBadge.SetResourceReference(
            Border.CornerRadiusProperty,
            "PanelCornerRadius");

        TextBlock numberText =
            new()
            {
                Text =
                    number,

                FontSize =
                    12,

                FontWeight =
                    FontWeights.Bold,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        numberText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        numberBadge.Child =
            numberText;

        Button visualButton =
            CreateThemedButton(
                title,
                primary: true);

        visualButton.IsHitTestVisible =
            false;

        visualButton.HorizontalAlignment =
            HorizontalAlignment.Stretch;

        visualButton.Margin =
            new Thickness(
                0,
                18,
                0,
                0);

        TextBlock descriptionText =
            new()
            {
                Text =
                    description,

                TextWrapping =
                    TextWrapping.Wrap,

                FontSize =
                    13,

                LineHeight =
                    20,

                Margin =
                    new Thickness(
                        0,
                        16,
                        0,
                        0)
            };

        descriptionText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        content.Children.Add(
            numberBadge);

        content.Children.Add(
            visualButton);

        content.Children.Add(
            descriptionText);

        card.Child =
            content;

        return card;
    }

    private static Border CreateSubCard(
        Thickness padding)
    {
        Border card =
            new()
            {
                Padding =
                    padding,

                BorderThickness =
                    new Thickness(1)
            };

        card.SetResourceReference(
            Border.BackgroundProperty,
            "WindowBackgroundBrush");

        card.SetResourceReference(
            Border.BorderBrushProperty,
            "BorderBrush");

        card.SetResourceReference(
            Border.CornerRadiusProperty,
            "PanelCornerRadius");

        return card;
    }

    private static Border CreateTrayDot()
    {
        Border dot =
            new()
            {
                Width =
                    30,

                Height =
                    30,

                BorderThickness =
                    new Thickness(1)
            };

        dot.SetResourceReference(
            Border.BorderBrushProperty,
            "BorderBrush");

        dot.SetResourceReference(
            Border.CornerRadiusProperty,
            "PanelCornerRadius");

        TextBlock text =
            new()
            {
                Text =
                    "⋯",

                FontSize =
                    17,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        text.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        dot.Child =
            text;

        return dot;
    }

    private void BackButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_pageIndex <= 0)
            return;

        _pageIndex--;

        UpdatePage();
    }

    private void NextButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_pageIndex >=
            _pages.Count - 1)
        {
            Close();

            return;
        }

        _pageIndex++;

        UpdatePage();
    }

    private void UpdatePage()
    {
        IntroductionPage page =
            _pages[_pageIndex];

        _sectionText.Text =
            page.Section;

        _titleText.Text =
            page.Title;

        _bodyText.Text =
            page.Body;

        _hintText.Text =
            page.Hint;

        _pageText.Text =
            $"{_pageIndex + 1} / {_pages.Count}";

        _visualHost.Child =
            page.VisualFactory();

        _backButton.IsEnabled =
            _pageIndex > 0;

        _nextButton.Content =
            _pageIndex ==
            _pages.Count - 1
                ? SettingsService.L(
                    "ГОТОВО",
                    "DONE")
                : SettingsService.L(
                    "ДАЛЕЕ",
                    "NEXT");

        ApplyButtonVisual(
            _backButton,
            hovered:
                false,
            pressed:
                false);

        ApplyButtonVisual(
            _nextButton,
            hovered:
                false,
            pressed:
                false);
    }

    private sealed class IntroductionPage
    {
        public IntroductionPage(
            string section,
            string title,
            string body,
            string hint,
            Func<UIElement> visualFactory)
        {
            Section =
                section;

            Title =
                title;

            Body =
                body;

            Hint =
                hint;

            VisualFactory =
                visualFactory;
        }

        public string Section { get; }

        public string Title { get; }

        public string Body { get; }

        public string Hint { get; }

        public Func<UIElement> VisualFactory { get; }
    }
}
