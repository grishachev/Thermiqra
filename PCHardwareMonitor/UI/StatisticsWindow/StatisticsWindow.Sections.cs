using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PCHardwareMonitor;

public partial class StatisticsWindow
{
    private void BuildExtendedSections()
    {
        if (_extendedSectionsBuilt)
            return;

        if (StatisticsScrollViewer.Content
            is not StackPanel root)
        {
            return;
        }

        _extendedSectionsBuilt = true;

        StorageDetailsPanel.Margin =
            new Thickness(
                0, 0, 0, 20);

        TextBlock analyticsTitle =
            CreateDynamicSectionTitle(
                SettingsService.L(
                    "АНАЛИТИКА ИСТОРИИ",
                    "HISTORY ANALYTICS"));

        root.Children.Add(
            analyticsTitle);

        _historyAnalyticsPanel =
            new StackPanel
            {
                Margin =
                    new Thickness(
                        0, 0, 0, 20)
            };

        root.Children.Add(
            _historyAnalyticsPanel);

        TextBlock recordsTitle =
            CreateDynamicSectionTitle(
                SettingsService.L(
                "РЕКОРДЫ ЗА ВСЁ ВРЕМЯ",
                "ALL-TIME RECORDS"));

        root.Children.Add(
            recordsTitle);

        _recordsPanel =
            new Grid
            {
                Margin =
                    new Thickness(
                        0, 0, 0, 20)
            };

        root.Children.Add(
            _recordsPanel);

        TextBlock eventsTitle =
            CreateDynamicSectionTitle(
                SettingsService.L(
                "СОБЫТИЯ WARNING / CRITICAL",
                "WARNING / CRITICAL EVENTS"));

        root.Children.Add(
            eventsTitle);

        Border eventsCard =
            CreateCard();

        eventsCard.Margin =
            new Thickness(
                0, 0, 0, 14);

        StackPanel eventsRoot =
            new();

        _eventCountsText =
            new TextBlock
            {
                Text =
                    "WARNING: 0   •   CRITICAL: 0",
                FontSize = 13,
                FontWeight =
                    FontWeights.Bold,
                Margin =
                    new Thickness(
                        0, 0, 0, 12)
            };

        _eventCountsText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        TextBlock eventsCaption =
            new()
            {
                Text =
                    SettingsService.L(
                    "Последние события за выбранный период",
                    "Latest events for the selected period"),
                FontSize = 10,
                Margin =
                    new Thickness(
                        0, 0, 0, 8)
            };

        eventsCaption.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        _eventsListPanel =
            new StackPanel();

        eventsRoot.Children.Add(
            _eventCountsText);

        eventsRoot.Children.Add(
            eventsCaption);

        eventsRoot.Children.Add(
            _eventsListPanel);

        eventsCard.Child =
            eventsRoot;

        root.Children.Add(
            eventsCard);

        Border clearCard =
            CreateCard();

        clearCard.Margin =
            new Thickness(
                0, 0, 0, 8);

        Grid clearGrid =
            new();

        clearGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        clearGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        StackPanel clearText =
            new();

        TextBlock clearTitle =
            new()
            {
                Text =
                    SettingsService.L(
                    "ОЧИСТКА СТАТИСТИКИ",
                    "CLEAR STATISTICS"),
                FontSize = 12,
                FontWeight =
                    FontWeights.Bold
            };

        clearTitle.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        TextBlock clearDescription =
            new()
            {
                Text =
                    SettingsService.L(
                    "Удаляет историю датчиков, журнал событий и рекорды. Настройки Thermiqra не затрагиваются.",
                    "Deletes sensor history, the event log, and records. Thermiqra settings are not affected."),
                FontSize = 10,
                TextWrapping =
                    TextWrapping.Wrap,
                Margin =
                    new Thickness(
                        0, 4, 18, 0)
            };

        clearDescription.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        clearText.Children.Add(
            clearTitle);

        clearText.Children.Add(
            clearDescription);

        Button clearButton =
            new()
            {
                Content =
                    SettingsService.L(
                    "Очистить статистику",
                    "Clear statistics"),
                MinWidth = 150,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        clearButton.Click +=
            ClearStatisticsButton_Click;

        Grid.SetColumn(
            clearText,
            0);

        Grid.SetColumn(
            clearButton,
            1);

        clearGrid.Children.Add(
            clearText);

        clearGrid.Children.Add(
            clearButton);

        clearCard.Child =
            clearGrid;

        root.Children.Add(
            clearCard);
    }

    private TextBlock CreateDynamicSectionTitle(
        string text)
    {
        TextBlock title =
            new()
            {
                Text = text,
                FontSize = 15,
                FontWeight =
                    FontWeights.Bold,
                Margin =
                    new Thickness(
                        0, 0, 0, 10)
            };

        title.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        object? font =
            TryFindResource(
                "DisplayFontFamily");

        if (font is FontFamily fontFamily)
        {
            title.FontFamily =
                fontFamily;
        }

        return title;
    }

    private void RefreshExtendedSections()
    {
        if (!_extendedSectionsBuilt)
            return;

        RebuildHistoryAnalytics();
        RebuildAllTimeRecords();
        RebuildAlertEvents();
    }


}
