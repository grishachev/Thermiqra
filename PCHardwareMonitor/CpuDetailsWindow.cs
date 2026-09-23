using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PCHardwareMonitor;

public sealed class CpuDetailsWindow : Window
{
    private readonly TextBlock _titleText;
    private readonly TextBlock _subtitleText;
    private readonly TextBlock _updatedText;
    private readonly TextBlock _emptyText;
    private readonly StackPanel _threadsPanel;

    private readonly Dictionary<int, CpuThreadRow>
        _threadRows =
            new();

    private HardwareSnapshot? _lastSnapshot;

    public CpuDetailsWindow()
    {
        Width =
            560;

        Height =
            620;

        MinWidth =
            480;

        MinHeight =
            420;

        WindowStartupLocation =
            WindowStartupLocation.CenterOwner;

        ResizeMode =
            ResizeMode.CanResize;

        ShowInTaskbar =
            false;

        SetResourceReference(
            BackgroundProperty,
            "WindowBackgroundBrush");

        Grid root =
            new()
            {
                Margin =
                    new Thickness(
                        16)
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

        Border header =
            CreateCard();

        header.Margin =
            new Thickness(
                0,
                0,
                0,
                12);

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
            new();

        _titleText =
            new TextBlock
            {
                FontSize = 19,
                FontWeight =
                    FontWeights.Bold
            };

        _titleText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        _subtitleText =
            new TextBlock
            {
                Margin =
                    new Thickness(
                        0,
                        4,
                        0,
                        0),
                FontSize = 10,
                FontWeight =
                    FontWeights.SemiBold
            };

        _subtitleText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        headerText.Children.Add(
            _titleText);

        headerText.Children.Add(
            _subtitleText);

        _updatedText =
            new TextBlock
            {
                HorizontalAlignment =
                    HorizontalAlignment.Right,
                VerticalAlignment =
                    VerticalAlignment.Center,
                Margin =
                    new Thickness(
                        16,
                        0,
                        0,
                        0),
                FontSize = 10
            };

        _updatedText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        Grid.SetColumn(
            headerText,
            0);

        Grid.SetColumn(
            _updatedText,
            1);

        headerGrid.Children.Add(
            headerText);

        headerGrid.Children.Add(
            _updatedText);

        header.Child =
            headerGrid;

        Grid.SetRow(
            header,
            0);

        root.Children.Add(
            header);

        Grid body =
            new();

        ScrollViewer scrollViewer =
            new()
            {
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility =
                    ScrollBarVisibility.Disabled,
                PanningMode =
                    PanningMode.VerticalOnly
            };

        _threadsPanel =
            new StackPanel();

        scrollViewer.Content =
            _threadsPanel;

        _emptyText =
            new TextBlock
            {
                Visibility =
                    Visibility.Collapsed,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                VerticalAlignment =
                    VerticalAlignment.Center,
                TextAlignment =
                    TextAlignment.Center,
                TextWrapping =
                    TextWrapping.Wrap,
                FontSize = 13,
                Margin =
                    new Thickness(
                        24)
            };

        _emptyText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        body.Children.Add(
            scrollViewer);

        body.Children.Add(
            _emptyText);

        Grid.SetRow(
            body,
            1);

        root.Children.Add(
            body);

        Content =
            root;

        PreviewMouseWheel +=
            CpuDetailsWindow_PreviewMouseWheel;

        RefreshLanguage();
    }

    public void RefreshLanguage()
    {
        Title =
            SettingsService.L(
                "Thermiqra — Потоки CPU",
                "Thermiqra — CPU Threads");

        _titleText.Text =
            SettingsService.L(
                "НАГРУЗКА ПО ПОТОКАМ CPU",
                "CPU THREAD LOAD");

        _emptyText.Text =
            SettingsService.L(
                "LibreHardwareMonitor не предоставил отдельные датчики нагрузки логических процессоров.",
                "LibreHardwareMonitor did not expose individual logical processor load sensors.");

        foreach (CpuThreadRow row
                 in _threadRows.Values)
        {
            row.RefreshLanguage();
        }

        if (_lastSnapshot != null)
        {
            UpdateHeader(
                _lastSnapshot);
        }
        else
        {
            _subtitleText.Text =
                SettingsService.L(
                    "ЛОГИЧЕСКИЕ ПРОЦЕССОРЫ // LIVE",
                    "LOGICAL PROCESSORS // LIVE");

            _updatedText.Text =
                "—";
        }
    }

    public void UpdateSnapshot(
        HardwareSnapshot snapshot)
    {
        _lastSnapshot =
            snapshot;

        UpdateHeader(
            snapshot);

        IReadOnlyList<CpuThreadInfo> threads =
            snapshot.Cpu.Threads
                .OrderBy(
                    thread =>
                        thread.Index)
                .ToList();

        bool layoutChanged =
            threads.Count !=
                _threadRows.Count ||
            threads.Any(
                thread =>
                    !_threadRows.ContainsKey(
                        thread.Index));

        if (layoutChanged)
        {
            RebuildThreadRows(
                threads);
        }

        foreach (CpuThreadInfo thread
                 in threads)
        {
            if (_threadRows.TryGetValue(
                    thread.Index,
                    out CpuThreadRow? row))
            {
                row.Update(
                    thread.Load);
            }
        }

        bool hasThreads =
            threads.Count > 0;

        _threadsPanel.Visibility =
            hasThreads
                ? Visibility.Visible
                : Visibility.Collapsed;

        _emptyText.Visibility =
            hasThreads
                ? Visibility.Collapsed
                : Visibility.Visible;
    }

    private void UpdateHeader(
        HardwareSnapshot snapshot)
    {
        int count =
            snapshot.Cpu.Threads.Count;

        _subtitleText.Text =
            SettingsService.L(
                $"ЛОГИЧЕСКИЕ ПРОЦЕССОРЫ: {count} // LIVE",
                $"LOGICAL PROCESSORS: {count} // LIVE");

        _updatedText.Text =
            SettingsService.L(
                "Обновлено ",
                "Updated ") +
            DateTime.Now.ToString(
                "HH:mm:ss");
    }

    private void RebuildThreadRows(
        IReadOnlyList<CpuThreadInfo> threads)
    {
        _threadsPanel.Children.Clear();
        _threadRows.Clear();

        foreach (CpuThreadInfo thread
                 in threads)
        {
            CpuThreadRow row =
                new(
                    thread.Index);

            _threadRows[
                thread.Index] =
                    row;

            _threadsPanel.Children.Add(
                row.Root);
        }
    }

    private void CpuDetailsWindow_PreviewMouseWheel(
        object sender,
        MouseWheelEventArgs e)
    {
        if (e.Handled)
            return;

        DependencyObject? current =
            e.OriginalSource
                as DependencyObject;

        while (current != null)
        {
            if (current is ScrollViewer viewer)
            {
                if (e.Delta > 0)
                {
                    viewer.LineUp();
                    viewer.LineUp();
                }
                else
                {
                    viewer.LineDown();
                    viewer.LineDown();
                }

                e.Handled =
                    true;

                return;
            }

            current =
                VisualTreeHelper.GetParent(
                    current);
        }
    }

    private static Border CreateCard()
    {
        Border border =
            new()
            {
                Padding =
                    new Thickness(
                        14),
                BorderThickness =
                    new Thickness(
                        1),
                CornerRadius =
                    new CornerRadius(
                        6)
            };

        border.SetResourceReference(
            Border.BackgroundProperty,
            "CardBackgroundBrush");

        border.SetResourceReference(
            Border.BorderBrushProperty,
            "BorderBrush");

        return border;
    }

    private sealed class CpuThreadRow
    {
        private readonly int _index;
        private readonly TextBlock _nameText;
        private readonly TextBlock _loadText;
        private readonly Grid _bar;
        private readonly Border _fill;

        private float? _lastLoad;

        public CpuThreadRow(
            int index)
        {
            _index =
                index;

            Root =
                CreateCard();

            Root.Margin =
                new Thickness(
                    0,
                    0,
                    0,
                    8);

            Grid grid =
                new();

            grid.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Auto
                });

            grid.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Auto
                });

            Grid textRow =
                new();

            textRow.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(
                            1,
                            GridUnitType.Star)
                });

            textRow.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        GridLength.Auto
                });

            _nameText =
                new TextBlock
                {
                    FontSize = 12,
                    FontWeight =
                        FontWeights.SemiBold
                };

            _nameText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "PrimaryTextBrush");

            _loadText =
                new TextBlock
                {
                    FontSize = 12,
                    FontWeight =
                        FontWeights.Bold,
                    Margin =
                        new Thickness(
                            12,
                            0,
                            0,
                            0)
                };

            _loadText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "AccentBrush");

            Grid.SetColumn(
                _nameText,
                0);

            Grid.SetColumn(
                _loadText,
                1);

            textRow.Children.Add(
                _nameText);

            textRow.Children.Add(
                _loadText);

            Grid.SetRow(
                textRow,
                0);

            grid.Children.Add(
                textRow);

            _bar =
                new Grid
                {
                    Height = 7,
                    Margin =
                        new Thickness(
                            0,
                            8,
                            0,
                            0),
                    ClipToBounds =
                        true
                };

            Border track =
                new()
                {
                    CornerRadius =
                        new CornerRadius(
                            3)
                };

            track.SetResourceReference(
                Border.BackgroundProperty,
                "GaugeTrackBrush");

            _fill =
                new Border
                {
                    Width = 0,
                    HorizontalAlignment =
                        HorizontalAlignment.Left,
                    CornerRadius =
                        new CornerRadius(
                            3)
                };

            _fill.SetResourceReference(
                Border.BackgroundProperty,
                "AccentBrush");

            _bar.Children.Add(
                track);

            _bar.Children.Add(
                _fill);

            _bar.SizeChanged +=
                (_, _) =>
                    UpdateBar();

            Grid.SetRow(
                _bar,
                1);

            grid.Children.Add(
                _bar);

            Root.Child =
                grid;

            RefreshLanguage();
        }

        public Border Root { get; }

        public void Update(
            float? load)
        {
            _lastLoad =
                load;

            _loadText.Text =
                FormatPercent(
                    _lastLoad);

            UpdateBar();
        }

        public void RefreshLanguage()
        {
            _nameText.Text =
                SettingsService.L(
                    $"Поток {_index}",
                    $"Thread {_index}");

            _loadText.Text =
                FormatPercent(
                    _lastLoad);
        }

        private void UpdateBar()
        {
            double value =
                Math.Clamp(
                    _lastLoad ?? 0,
                    0,
                    100);

            _fill.Width =
                _bar.ActualWidth *
                value /
                100.0;
        }

        private static string FormatPercent(
            float? value)
        {
            return value.HasValue
                ? $"{value.Value:F0} %"
                : "—";
        }
    }
}