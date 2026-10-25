using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCHardwareMonitor;


public partial class SystemInfoWindow
{
    private static void SmartGuidanceWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not SystemInfoWindow window)
            return;

        // Вкладку создаём сразу при открытии окна, до тяжёлого чтения
        // системной информации. Поэтому она появляется одновременно
        // со всеми штатными вкладками и больше не «догружается» позже.
        window.EnsureSmartGuidanceTab();

        // Сам анализ обновляем после штатного LoadSystemInformation(),
        // чтобы использовать уже полученные данные и не создавать
        // отдельный опрос оборудования.
        window.Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(
                () =>
                {
                    window.RefreshRamSmartGuidance();
                    window.RefreshStorageSmartGuidance();
                    window.RefreshUpgradeGuidance();
                }));
    }


    // ============================================================
    // ВКЛАДКА «АНАЛИЗ»
    // ============================================================

    private void EnsureSmartGuidanceTab()
    {
        if (_smartGuidanceInitialized)
            return;

        TabControl? tabControl =
            FindVisualChild<TabControl>(
                this);

        if (tabControl == null)
            return;

        foreach (object item in
                 tabControl.Items)
        {
            if (item is TabItem existingTab &&
                string.Equals(
                    existingTab.Tag as string,
                    SmartGuidanceTabTag,
                    StringComparison.Ordinal))
            {
                _smartGuidanceInitialized =
                    true;

                return;
            }
        }

        TabItem analysisTab =
            new()
            {
                Header =
                    SettingsService.L(
                        "АНАЛИЗ",
                        "ANALYSIS"),

                Tag =
                    SmartGuidanceTabTag
            };

        ScrollViewer scrollViewer =
            new()
            {
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Auto,

                HorizontalScrollBarVisibility =
                    ScrollBarVisibility.Disabled
            };

        StackPanel rootPanel =
            new()
            {
                Margin =
                    new Thickness(
                        0,
                        18,
                        6,
                        0)
            };

        TextBlock title =
            new()
            {
                Text =
                    SettingsService.L(
                        "Анализ системы",
                        "System analysis")
            };

        if (TryFindResource(
                "SectionTitleStyle")
            is Style sectionTitleStyle)
        {
            title.Style =
                sectionTitleStyle;
        }

        rootPanel.Children.Add(
            title);

        Border ramCard =
            new();

        if (TryFindResource(
                "InfoCardStyle")
            is Style infoCardStyle)
        {
            ramCard.Style =
                infoCardStyle;
        }
        else
        {
            ramCard.Padding =
                new Thickness(18);

            ramCard.Margin =
                new Thickness(
                    0,
                    0,
                    0,
                    12);

            ramCard.BorderThickness =
                new Thickness(1);

            ramCard.SetResourceReference(
                Border.BackgroundProperty,
                "CardBackgroundBrush");

            ramCard.SetResourceReference(
                Border.BorderBrushProperty,
                "BorderBrush");
        }

        StackPanel cardPanel =
            new();

        TextBlock ramTitle =
            CreateGuidanceText(
                SettingsService.L(
                    "ОПЕРАТИВНАЯ ПАМЯТЬ",
                    "SYSTEM MEMORY"),
                15,
                FontWeights.Bold,
                "PrimaryTextBrush");

        cardPanel.Children.Add(
            ramTitle);

        _ramGuidanceStatusText =
            CreateGuidanceText(
                SettingsService.L(
                    "Анализ данных...",
                    "Analyzing data..."),
                20,
                FontWeights.Bold,
                "AccentBrush");

        _ramGuidanceStatusText.Margin =
            new Thickness(
                0,
                10,
                0,
                0);

        cardPanel.Children.Add(
            _ramGuidanceStatusText);

        _ramGuidanceCurrentText =
            CreateGuidanceValueText();

        _ramGuidanceCurrentText.Margin =
            new Thickness(
                0,
                10,
                0,
                0);

        cardPanel.Children.Add(
            _ramGuidanceCurrentText);

        _ramGuidanceHistoryText =
            CreateGuidanceValueText();

        _ramGuidanceHistoryText.Margin =
            new Thickness(
                0,
                7,
                0,
                0);

        cardPanel.Children.Add(
            _ramGuidanceHistoryText);

        _ramGuidanceConfigurationText =
            CreateGuidanceText(
                string.Empty,
                11,
                FontWeights.Normal,
                "SecondaryTextBrush");

        _ramGuidanceConfigurationText.Margin =
            new Thickness(
                0,
                7,
                0,
                0);

        cardPanel.Children.Add(
            _ramGuidanceConfigurationText);

        _ramGuidancePlacementWarningText =
            CreateGuidanceText(
                string.Empty,
                12,
                FontWeights.SemiBold,
                "AccentBrush");

        _ramGuidancePlacementWarningText.Margin =
            new Thickness(
                0,
                12,
                0,
                0);

        _ramGuidancePlacementWarningText.Visibility =
            Visibility.Collapsed;

        cardPanel.Children.Add(
            _ramGuidancePlacementWarningText);

        Border separator =
            new()
            {
                Height = 1,
                Margin =
                    new Thickness(
                        0,
                        16,
                        0,
                        14)
            };

        separator.SetResourceReference(
            Border.BackgroundProperty,
            "BorderBrush");

        cardPanel.Children.Add(
            separator);

        TextBlock meaningLabel =
            CreateGuidanceLabelText(
                SettingsService.L(
                    "ЧТО ЭТО ОЗНАЧАЕТ",
                    "WHAT THIS MEANS"));

        cardPanel.Children.Add(
            meaningLabel);

        _ramGuidanceMeaningText =
            CreateGuidanceValueText();

        _ramGuidanceMeaningText.Margin =
            new Thickness(
                0,
                5,
                0,
                0);

        cardPanel.Children.Add(
            _ramGuidanceMeaningText);

        TextBlock actionLabel =
            CreateGuidanceLabelText(
                SettingsService.L(
                    "ЧТО ИМЕЕТ СМЫСЛ СДЕЛАТЬ",
                    "REASONABLE NEXT STEP"));

        actionLabel.Margin =
            new Thickness(
                0,
                16,
                0,
                0);

        cardPanel.Children.Add(
            actionLabel);

        _ramGuidanceActionText =
            CreateGuidanceValueText();

        _ramGuidanceActionText.Margin =
            new Thickness(
                0,
                5,
                0,
                0);

        cardPanel.Children.Add(
            _ramGuidanceActionText);

        _ramGuidanceNoteText =
            CreateGuidanceText(
                SettingsService.L(
                    "Вывод основан на текущей загрузке и локальной истории. " +
                    "Один высокий пик сам по себе не считается признаком нехватки памяти.",
                    "The conclusion is based on current load and local history. " +
                    "A single high peak by itself is not treated as evidence of insufficient memory."),
                10,
                FontWeights.Normal,
                "SecondaryTextBrush");

        _ramGuidanceNoteText.Margin =
            new Thickness(
                0,
                16,
                0,
                0);

        cardPanel.Children.Add(
            _ramGuidanceNoteText);

        ramCard.Child =
            cardPanel;

        rootPanel.Children.Add(
            ramCard);

        Border storageCard =
            new();

        if (TryFindResource(
                "InfoCardStyle")
            is Style storageInfoCardStyle)
        {
            storageCard.Style =
                storageInfoCardStyle;
        }
        else
        {
            storageCard.Padding =
                new Thickness(18);

            storageCard.Margin =
                new Thickness(
                    0,
                    0,
                    0,
                    12);

            storageCard.BorderThickness =
                new Thickness(1);

            storageCard.SetResourceReference(
                Border.BackgroundProperty,
                "CardBackgroundBrush");

            storageCard.SetResourceReference(
                Border.BorderBrushProperty,
                "BorderBrush");
        }

        StackPanel storageCardPanel =
            new();

        TextBlock storageTitle =
            CreateGuidanceText(
                SettingsService.L(
                    "НАКОПИТЕЛИ",
                    "STORAGE"),
                15,
                FontWeights.Bold,
                "PrimaryTextBrush");

        storageCardPanel.Children.Add(
            storageTitle);

        _storageGuidanceStatusText =
            CreateGuidanceText(
                SettingsService.L(
                    "Анализ данных...",
                    "Analyzing data..."),
                20,
                FontWeights.Bold,
                "AccentBrush");

        _storageGuidanceStatusText.Margin =
            new Thickness(
                0,
                10,
                0,
                0);

        storageCardPanel.Children.Add(
            _storageGuidanceStatusText);

        _storageGuidanceDetailsPanel =
            new StackPanel
            {
                Margin =
                    new Thickness(
                        0,
                        10,
                        0,
                        0)
            };

        storageCardPanel.Children.Add(
            _storageGuidanceDetailsPanel);

        Border storageSeparator =
            new()
            {
                Height = 1,
                Margin =
                    new Thickness(
                        0,
                        16,
                        0,
                        14)
            };

        storageSeparator.SetResourceReference(
            Border.BackgroundProperty,
            "BorderBrush");

        storageCardPanel.Children.Add(
            storageSeparator);

        TextBlock storageMeaningLabel =
            CreateGuidanceLabelText(
                SettingsService.L(
                    "ЧТО ЭТО ОЗНАЧАЕТ",
                    "WHAT THIS MEANS"));

        storageCardPanel.Children.Add(
            storageMeaningLabel);

        _storageGuidanceMeaningText =
            CreateGuidanceValueText();

        _storageGuidanceMeaningText.Margin =
            new Thickness(
                0,
                5,
                0,
                0);

        storageCardPanel.Children.Add(
            _storageGuidanceMeaningText);

        TextBlock storageActionLabel =
            CreateGuidanceLabelText(
                SettingsService.L(
                    "ЧТО ИМЕЕТ СМЫСЛ СДЕЛАТЬ",
                    "REASONABLE NEXT STEP"));

        storageActionLabel.Margin =
            new Thickness(
                0,
                16,
                0,
                0);

        storageCardPanel.Children.Add(
            storageActionLabel);

        _storageGuidanceActionText =
            CreateGuidanceValueText();

        _storageGuidanceActionText.Margin =
            new Thickness(
                0,
                5,
                0,
                0);

        storageCardPanel.Children.Add(
            _storageGuidanceActionText);

        storageCard.Child =
            storageCardPanel;

        rootPanel.Children.Add(
            storageCard);

        Border upgradeCard =
            new();

        if (TryFindResource(
                "InfoCardStyle")
            is Style upgradeInfoCardStyle)
        {
            upgradeCard.Style =
                upgradeInfoCardStyle;
        }
        else
        {
            upgradeCard.Padding =
                new Thickness(18);

            upgradeCard.Margin =
                new Thickness(
                    0,
                    0,
                    0,
                    12);

            upgradeCard.BorderThickness =
                new Thickness(1);

            upgradeCard.SetResourceReference(
                Border.BackgroundProperty,
                "CardBackgroundBrush");

            upgradeCard.SetResourceReference(
                Border.BorderBrushProperty,
                "BorderBrush");
        }

        StackPanel upgradeCardPanel =
            new();

        TextBlock upgradeTitle =
            CreateGuidanceText(
                SettingsService.L(
                    "ЧТО МОЖНО УЛУЧШИТЬ",
                    "WHAT COULD BE IMPROVED"),
                15,
                FontWeights.Bold,
                "PrimaryTextBrush");

        upgradeCardPanel.Children.Add(
            upgradeTitle);


        _upgradeGuidanceStatusText =
            CreateGuidanceText(
                SettingsService.L(
                    "Анализ данных...",
                    "Analyzing data..."),
                20,
                FontWeights.Bold,
                "AccentBrush");

        _upgradeGuidanceStatusText.Margin =
            new Thickness(
                0,
                10,
                0,
                0);

        upgradeCardPanel.Children.Add(
            _upgradeGuidanceStatusText);

        _upgradeGuidanceDetailsPanel =
            new StackPanel
            {
                Margin =
                    new Thickness(
                        0,
                        12,
                        0,
                        0)
            };

        upgradeCardPanel.Children.Add(
            _upgradeGuidanceDetailsPanel);

        _upgradeGuidanceNoteText =
            CreateGuidanceText(
                string.Empty,
                11,
                FontWeights.Normal,
                "SecondaryTextBrush");

        _upgradeGuidanceNoteText.Margin =
            new Thickness(
                0,
                14,
                0,
                0);

        _upgradeGuidanceNoteText.Visibility =
            Visibility.Collapsed;

        upgradeCardPanel.Children.Add(
            _upgradeGuidanceNoteText);

        upgradeCard.Child =
            upgradeCardPanel;

        rootPanel.Children.Add(
            upgradeCard);

        scrollViewer.Content =
            rootPanel;

        analysisTab.Content =
            scrollViewer;

        // Технические разделы идут единым блоком:
        // ОБЗОР -> ПРОЦЕССОР -> ПЛАТА -> ПАМЯТЬ -> ВИДЕОКАРТА
        // -> НАКОПИТЕЛИ -> WINDOWS.
        // АНАЛИЗ завершает окно и интерпретирует уже показанные данные.
        tabControl.Items.Add(
            analysisTab);

        _smartGuidanceInitialized =
            true;
    }


    private TextBlock CreateGuidanceValueText()
    {
        TextBlock textBlock =
            new();

        if (TryFindResource(
                "InfoValueStyle")
            is Style infoValueStyle)
        {
            textBlock.Style =
                infoValueStyle;
        }
        else
        {
            textBlock.FontSize =
                13;

            textBlock.TextWrapping =
                TextWrapping.Wrap;

            textBlock.SetResourceReference(
                TextBlock.ForegroundProperty,
                "PrimaryTextBrush");
        }

        return textBlock;
    }


    private TextBlock CreateGuidanceLabelText(
        string text)
    {
        TextBlock textBlock =
            new()
            {
                Text = text
            };

        if (TryFindResource(
                "InfoLabelStyle")
            is Style infoLabelStyle)
        {
            textBlock.Style =
                infoLabelStyle;
        }
        else
        {
            textBlock.FontSize =
                11;

            textBlock.FontWeight =
                FontWeights.SemiBold;

            textBlock.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");
        }

        return textBlock;
    }


    private static TextBlock CreateGuidanceText(
        string text,
        double fontSize,
        FontWeight fontWeight,
        string foregroundResource)
    {
        TextBlock textBlock =
            new()
            {
                Text = text,
                FontSize = fontSize,
                FontWeight = fontWeight,
                TextWrapping =
                    TextWrapping.Wrap
            };

        textBlock.SetResourceReference(
            TextBlock.ForegroundProperty,
            foregroundResource);

        return textBlock;
    }


    private static T? FindVisualChild<T>(
        DependencyObject parent)
        where T : DependencyObject
    {
        int childCount =
            VisualTreeHelper.GetChildrenCount(
                parent);

        for (int i = 0;
             i < childCount;
             i++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(
                    parent,
                    i);

            if (child is T result)
                return result;

            T? nested =
                FindVisualChild<T>(
                    child);

            if (nested != null)
                return nested;
        }

        return null;
    }



}
