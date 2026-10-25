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

public sealed partial class IntroductionWindow : Window
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
