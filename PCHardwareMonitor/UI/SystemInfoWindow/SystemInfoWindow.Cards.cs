using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Controls;

namespace PCHardwareMonitor;

public partial class SystemInfoWindow
{
    // ============================================================
    // ДИНАМИЧЕСКИЕ КАРТОЧКИ
    // ============================================================

    private Border CreateInformationCard(
        string title,
        IReadOnlyList<InformationRow> rows)
    {
        Border card =
            new()
            {
                BorderThickness =
                    new Thickness(1),

                Padding =
                    new Thickness(18),

                Margin =
                    new Thickness(
                        0, 0, 0, 12)
            };

        card.SetResourceReference(
            Border.BackgroundProperty,
            "CardBackgroundBrush");

        card.SetResourceReference(
            Border.BorderBrushProperty,
            "BorderBrush");

        card.SetResourceReference(
            Border.CornerRadiusProperty,
            "PanelCornerRadius");

        StackPanel body =
            new();

        TextBlock heading =
            new()
            {
                Text =
                    SettingsService.IsRussian
                        ? title
                        : SettingsService.TranslateText(
                            title),
                FontSize = 16,
                FontWeight =
                    FontWeights.Bold,
                Margin =
                    new Thickness(
                        0, 0, 0, 12)
            };

        heading.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        body.Children.Add(
            heading);

        foreach (InformationRow row in rows)
        {
            Grid line =
                new()
                {
                    Margin =
                        new Thickness(
                            0, 4, 0, 4)
                };

            line.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(210)
                });

            line.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(
                            1,
                            GridUnitType.Star)
                });

            TextBlock label =
                new()
                {
                    Text =
                        SettingsService.IsRussian
                            ? row.Label
                            : SettingsService.TranslateText(
                                row.Label),

                    FontSize = 11,

                    FontWeight =
                        FontWeights.SemiBold,

                    VerticalAlignment =
                        VerticalAlignment.Center
                };

            label.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            TextBlock value =
                new()
                {
                    Text =
                        SettingsService.IsRussian
                            ? row.Value
                            : SettingsService.TranslateText(
                                row.Value),

                    FontSize = 13,

                    TextWrapping =
                        TextWrapping.Wrap,

                    VerticalAlignment =
                        VerticalAlignment.Center
                };

            value.SetResourceReference(
                TextBlock.ForegroundProperty,
                "PrimaryTextBrush");

            Grid.SetColumn(
                label,
                0);

            Grid.SetColumn(
                value,
                1);

            line.Children.Add(
                label);

            line.Children.Add(
                value);

            body.Children.Add(
                line);
        }

        card.Child =
            body;

        return card;
    }


    private Border CreateEmptyMessage(
        string text)
    {
        Border card =
            new()
            {
                BorderThickness =
                    new Thickness(1),

                Padding =
                    new Thickness(18),

                Margin =
                    new Thickness(
                        0, 0, 0, 12)
            };

        card.SetResourceReference(
            Border.BackgroundProperty,
            "CardBackgroundBrush");

        card.SetResourceReference(
            Border.BorderBrushProperty,
            "BorderBrush");

        card.SetResourceReference(
            Border.CornerRadiusProperty,
            "PanelCornerRadius");

        TextBlock message =
            new()
            {
                Text =
                    SettingsService.IsRussian
                        ? text
                        : SettingsService.TranslateText(
                            text),
                FontSize = 13
            };

        message.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        card.Child =
            message;

        return card;
    }



}
