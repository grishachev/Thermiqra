using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCHardwareMonitor;

public partial class MainWindow
{
    private sealed partial class InstalledProgramsWindow : Window
    {
        private readonly DataGrid _programsTable;
        private readonly TextBox _searchBox;
        private readonly TextBlock _countText;
        private readonly TextBlock _statusText;
        private readonly TextBlock _detailsText;
        private readonly Button _refreshButton;
        private readonly List<InstalledProgramInfo> _programs = new();
        private readonly ObservableCollection<InstalledProgramInfo> _visiblePrograms = new();
        private readonly Dictionary<string, Button> _navigationButtons = new();
        private string _currentCategory = "all";
        private string _sortMode = "date";
        private bool _showAuxiliary;
        private readonly Button _sortDateButton;
        private readonly Button _sortNameButton;
        private readonly CheckBox _auxiliaryToggle;
        private readonly Style _filterButtonStyle;
        private CancellationTokenSource? _loadCancellation;

        public InstalledProgramsWindow(MainWindow owner)
        {
            Owner = owner;
            Title = SettingsService.L("Thermiqra — Программы", "Thermiqra — Programs");
            Width = 1180;
            Height = 780;
            MinWidth = 640;
            MinHeight = 520;
            SizeToContent = SizeToContent.Manual;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            // SourceInitialized may run before the host has reached its final layout size.
            // Recheck once the child is loaded; don't persist an unrelated old child size.
            SourceInitialized += (_, _) => MatchOwnerBounds();
            Loaded += (_, _) => Dispatcher.BeginInvoke(
                new Action(MatchOwnerBounds), DispatcherPriority.ContextIdle);
            ResizeMode = ResizeMode.CanResize;
            FontFamily = owner.FontFamily;
            SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
            SetResourceReference(ForegroundProperty, "PrimaryTextBrush");

            if (owner.Resources[typeof(Button)] is Style buttonStyle)
                Resources[typeof(Button)] = buttonStyle;
            if (owner.Resources[typeof(ScrollBar)] is Style scrollbarStyle)
                Resources[typeof(ScrollBar)] = scrollbarStyle;
            if (owner.Resources[typeof(ToolTip)] is Style tooltipStyle)
                Resources[typeof(ToolTip)] = tooltipStyle;

            // Active-state highlighting must live in a style. Local Foreground
            // values would override the main button template's hover contrast.
            _filterButtonStyle = new Style(typeof(Button),
                owner.Resources[typeof(Button)] as Style);
            DataTrigger activeFilter = new()
            {
                Binding = new Binding(nameof(Button.Tag)) { RelativeSource = RelativeSource.Self },
                Value = "active"
            };
            activeFilter.Setters.Add(new Setter(Control.ForegroundProperty,
                new DynamicResourceExtension("AccentBrush")));
            activeFilter.Setters.Add(new Setter(Control.BorderBrushProperty,
                new DynamicResourceExtension("AccentBrush")));
            _filterButtonStyle.Triggers.Add(activeFilter);
            Trigger filterHover = new() { Property = UIElement.IsMouseOverProperty, Value = true };
            filterHover.Setters.Add(new Setter(Control.ForegroundProperty,
                new DynamicResourceExtension("WindowBackgroundBrush")));
            _filterButtonStyle.Triggers.Add(filterHover);

            Grid root = new() { Margin = new Thickness(18, 16, 18, 12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            FrameworkElement header = ProgramWindowChrome.Header(
                SettingsService.L("Установленные программы", "Installed programs"),
                SettingsService.L("Программы Windows и Microsoft Store · штатное удаление",
                    "Windows and Microsoft Store apps · native uninstall"));
            header.Margin = new Thickness(0, 0, 0, 15);
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            Grid toolbar = new() { Margin = new Thickness(0, 0, 0, 12) };
            toolbar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            toolbar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _searchBox = new TextBox
            {
                Height = 42,
                FontSize = 14.5,
                Padding = new Thickness(12, 8, 12, 6),
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = SettingsService.L("Поиск по названию, расположению, версии или типу", "Search by name, location, version or type")
            };
            _searchBox.SetResourceReference(TextBox.BackgroundProperty, "InputBackgroundBrush");
            _searchBox.SetResourceReference(FrameworkElement.StyleProperty, "ProgramSearchBoxStyle");
            _searchBox.SetResourceReference(TextBox.ForegroundProperty, "PrimaryTextBrush");
            _searchBox.SetResourceReference(TextBox.BorderBrushProperty, "BorderBrush");
            _searchBox.TextChanged += (_, _) => ApplyFilter();
            toolbar.Children.Add(_searchBox);

            _countText = MakeText("", 13, FontWeights.SemiBold, "SecondaryTextBrush");
            _countText.Margin = new Thickness(14, 0, 14, 0);
            _countText.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_countText, 1);
            toolbar.Children.Add(_countText);

            _refreshButton = new Button
            {
                Content = SettingsService.L("Обновить", "Refresh"),
                Padding = new Thickness(16, 8, 16, 8),
                MinWidth = 105,
                Cursor = Cursors.Hand
            };
            _refreshButton.Click += async (_, _) => await RefreshAsync();
            Grid.SetColumn(_refreshButton, 4);
            toolbar.Children.Add(_refreshButton);

            _sortDateButton = new Button
            {
                Content = SettingsService.L("По дате ↓", "Date ↓"),
                Padding = new Thickness(12, 8, 12, 8),
                MinWidth = 100,
                Margin = new Thickness(0, 0, 6, 0)
            };
            _sortDateButton.Style = _filterButtonStyle;
            _sortDateButton.Click += (_, _) => SetSortMode("date");
            Grid.SetColumn(_sortDateButton, 2);
            toolbar.Children.Add(_sortDateButton);
            _sortNameButton = new Button
            {
                Content = SettingsService.L("По имени А–Я", "Name A–Z"),
                Padding = new Thickness(12, 8, 12, 8),
                MinWidth = 122,
                Margin = new Thickness(0, 0, 10, 0)
            };
            _sortNameButton.Style = _filterButtonStyle;
            _sortNameButton.Click += (_, _) => SetSortMode("name");
            Grid.SetColumn(_sortNameButton, 3);
            toolbar.Children.Add(_sortNameButton);
            UpdateSortButtons();
            // Keep search usable when the main window is narrow.
            void ArrangeToolbar()
            {
                bool compact = ActualWidth < 950;
                Grid.SetRow(_searchBox, 0);
                Grid.SetRow(_countText, 0);
                Grid.SetRow(_sortDateButton, compact ? 1 : 0);
                Grid.SetRow(_sortNameButton, compact ? 1 : 0);
                Grid.SetRow(_refreshButton, compact ? 1 : 0);
                if (compact)
                {
                    Grid.SetColumnSpan(_searchBox, 3);
                    Grid.SetColumn(_countText, 3);
                    Grid.SetColumn(_sortDateButton, 0);
                    Grid.SetColumn(_sortNameButton, 1);
                    Grid.SetColumn(_refreshButton, 4);
                    _sortDateButton.Margin = new Thickness(0, 7, 6, 0);
                    _sortNameButton.Margin = new Thickness(0, 7, 10, 0);
                    _refreshButton.Margin = new Thickness(0, 7, 0, 0);
                }
                else
                {
                    Grid.SetColumnSpan(_searchBox, 1);
                    Grid.SetColumn(_countText, 1);
                    Grid.SetColumn(_sortDateButton, 2);
                    Grid.SetColumn(_sortNameButton, 3);
                    Grid.SetColumn(_refreshButton, 4);
                    _sortDateButton.Margin = new Thickness(0, 0, 6, 0);
                    _sortNameButton.Margin = new Thickness(0, 0, 10, 0);
                    _refreshButton.Margin = new Thickness(0);
                }
            }
            SizeChanged += (_, _) => ArrangeToolbar();
            Loaded += (_, _) => ArrangeToolbar();
            Grid.SetRow(toolbar, 1);
            root.Children.Add(toolbar);

            Border tableCard = MakePanel();
            tableCard.Margin = new Thickness(0, 0, 0, 12);
            _programsTable = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserResizeRows = false,
                CanUserSortColumns = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.None,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                RowHeight = 44,
                ColumnHeaderHeight = 43,
                AlternationCount = 2,
                BorderThickness = new Thickness(0),
                FontSize = 14.5,
                EnableRowVirtualization = true,
                EnableColumnVirtualization = true
            };
            _programsTable.SetResourceReference(DataGrid.BackgroundProperty, "CardBackgroundBrush");
            _programsTable.SetResourceReference(DataGrid.ForegroundProperty, "PrimaryTextBrush");
            _programsTable.SetResourceReference(DataGrid.RowBackgroundProperty, "CardBackgroundBrush");
            _programsTable.SetResourceReference(DataGrid.AlternatingRowBackgroundProperty, "InputBackgroundBrush");
            _programsTable.SetResourceReference(DataGrid.BorderBrushProperty, "BorderBrush");
            _programsTable.Columns.Add(MakeProgramColumn());
            _programsTable.Columns.Add(MakeColumn(SettingsService.L("Размер", "Size"), "SizeText", 0.8,
                nameof(InstalledProgramInfo.SizeSource)));
            _programsTable.Columns.Add(MakeColumn(SettingsService.L("Версия", "Version"), "Version", 0.9));
            _programsTable.Columns.Add(MakeColumn(SettingsService.L("Тип", "Type"), "Kind", 0.58));
            _programsTable.Columns.Add(MakeColumn(SettingsService.L("Дата", "Date"), "InstallDateText", 1.05,
                nameof(InstalledProgramInfo.DateSource)));
            // Paths can be long; ellipsize in the list and show the complete value on hover.
            _programsTable.Columns.Add(MakeColumn(
                SettingsService.L("Расположение", "Location"), "LocationText", 2.1,
                nameof(InstalledProgramInfo.LocationExplanation)));
            Style headerStyle = new(typeof(DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(Control.BackgroundProperty,
                new DynamicResourceExtension("InputBackgroundBrush")));
            headerStyle.Setters.Add(new Setter(Control.ForegroundProperty,
                new DynamicResourceExtension("PrimaryTextBrush")));
            headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty,
                new DynamicResourceExtension("BorderBrush")));
            headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            headerStyle.Setters.Add(new Setter(Control.FontSizeProperty, 14.0));
            headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(7, 5, 7, 5)));
            _programsTable.ColumnHeaderStyle = headerStyle;

            // Full-row selection: an obvious accent background across every cell.
            // Foreground follows the active theme, including SteamPunk/Frost/Military.
            Style rowStyle = new(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(Control.BorderThicknessProperty,
                new Thickness(2, 0, 0, 0)));
            rowStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
            rowStyle.Setters.Add(new Setter(Control.ForegroundProperty,
                new DynamicResourceExtension("PrimaryTextBrush")));
            Trigger rowHover = new() { Property = UIElement.IsMouseOverProperty, Value = true };
            rowHover.Setters.Add(new Setter(Control.BackgroundProperty,
                new DynamicResourceExtension("InputBackgroundBrush")));
            rowStyle.Triggers.Add(rowHover);
            Trigger selected = new() { Property = DataGridRow.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Control.BackgroundProperty,
                new DynamicResourceExtension("AccentBrush")));
            selected.Setters.Add(new Setter(Control.ForegroundProperty,
                new DynamicResourceExtension("WindowBackgroundBrush")));
            selected.Setters.Add(new Setter(Control.BorderBrushProperty,
                new DynamicResourceExtension("AccentBrush")));
            rowStyle.Triggers.Add(selected);
            _programsTable.RowStyle = rowStyle;

            // A transparent cell lets the selected row paint the *entire* width.
            Style cellStyle = new(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            cellStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            cellStyle.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
            // WPF's DataGridCell template can cover the selected row background.
            // Paint every selected cell explicitly so both fill and text remain
            // readable even when focus moves out of the table.
            DataTrigger rowSelectedCell = new()
            {
                Binding = new Binding(nameof(DataGridRow.IsSelected))
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor,
                        typeof(DataGridRow), 1)
                },
                Value = true
            };
            rowSelectedCell.Setters.Add(new Setter(Control.BackgroundProperty,
                new DynamicResourceExtension("AccentBrush")));
            rowSelectedCell.Setters.Add(new Setter(Control.ForegroundProperty,
                new DynamicResourceExtension("WindowBackgroundBrush")));
            cellStyle.Triggers.Add(rowSelectedCell);
            Trigger cellSelected = new()
            {
                Property = DataGridCell.IsSelectedProperty, Value = true
            };
            cellSelected.Setters.Add(new Setter(Control.BackgroundProperty,
                new DynamicResourceExtension("AccentBrush")));
            cellSelected.Setters.Add(new Setter(Control.ForegroundProperty,
                new DynamicResourceExtension("WindowBackgroundBrush")));
            cellStyle.Triggers.Add(cellSelected);
            _programsTable.CellStyle = cellStyle;
            _programsTable.ItemsSource = _visiblePrograms;
            _programsTable.SelectionChanged += (_, _) => { ShowSelectedDetails(); UpdateUninstallButton(); };
            tableCard.Child = _programsTable;

            Grid workspace = new();
            workspace.Margin = new Thickness(0, 0, 0, 12);
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(235) });
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border sidePanel = MakePanel();
            sidePanel.Padding = new Thickness(10, 12, 10, 12);
            StackPanel navigation = new();
            TextBlock navTitle = MakeText(
                SettingsService.L("КАТЕГОРИИ", "CATEGORIES"),
                12, FontWeights.Bold, "SecondaryTextBrush");
            navTitle.Margin = new Thickness(10, 2, 0, 12);
            navigation.Children.Add(navTitle);
            navigation.Children.Add(MakeNavigationButton("all"));
            navigation.Children.Add(MakeNavigationButton("recent"));
            navigation.Children.Add(MakeNavigationButton("desktop"));
            navigation.Children.Add(MakeNavigationButton("store"));
            _auxiliaryToggle = new CheckBox
            {
                Content = new TextBlock
                {
                    Text = SettingsService.L("Показать встроенные и служебные", "Show built-in and supporting"),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12.5
                },
                IsChecked = false,
                FontSize = 12.5,
                Margin = new Thickness(9, 10, 3, 0),
                Cursor = Cursors.Hand
            };
            ProgramWindowChrome.CheckBox(_auxiliaryToggle);
            _auxiliaryToggle.SetResourceReference(Control.ForegroundProperty, "SecondaryTextBrush");
            if (_auxiliaryToggle.Content is TextBlock auxiliaryLabel)
                auxiliaryLabel.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
            _auxiliaryToggle.Checked += (_, _) => { _showAuxiliary = true; UpdateNavigationButtons(); ApplyFilter(); };
            _auxiliaryToggle.Unchecked += (_, _) => { _showAuxiliary = false; UpdateNavigationButtons(); ApplyFilter(); };
            navigation.Children.Add(_auxiliaryToggle);

            Border navigationSeparator = new() { Height = 1, Margin = new Thickness(5, 14, 5, 14) };
            navigationSeparator.SetResourceReference(Border.BackgroundProperty, "BorderBrush");
            navigation.Children.Add(navigationSeparator);
            navigation.Children.Add(CreateUninstallButton());
            navigation.Children.Add(CreateReviewButton());
            navigation.Children.Add(CreateRecoveryButton());
            TextBlock safetyNote = MakeText(
                SettingsService.L("Штатное удаление выбранной программы. Очистка остатков — отдельно, по вашему выбору: файлы в Корзину, реестр с копией.",
                    "Native uninstall of the selected application. Optional leftover cleanup is separate: files to Recycle Bin, registry entries backed up."),
                12.5, FontWeights.Normal, "SecondaryTextBrush");
            safetyNote.TextWrapping = TextWrapping.Wrap;
            safetyNote.Margin = new Thickness(10, 0, 7, 0);
            navigation.Children.Add(safetyNote);
            sidePanel.Child = navigation;
            Grid.SetColumn(sidePanel, 0);
            workspace.Children.Add(sidePanel);

            tableCard.Margin = new Thickness(0);
            Grid.SetColumn(tableCard, 2);
            workspace.Children.Add(tableCard);
            Grid.SetRow(workspace, 2);
            root.Children.Add(workspace);
            UpdateNavigationButtons();

            Border detailsCard = MakePanel();
            detailsCard.Padding = new Thickness(14, 12, 14, 12);
            detailsCard.Margin = new Thickness(0, 0, 0, 12);
            _detailsText = MakeText(
                SettingsService.L("Выберите программу для просмотра сведений.", "Select an application to see details."),
                13.5, FontWeights.Normal, "SecondaryTextBrush");
            _detailsText.TextWrapping = TextWrapping.Wrap;
            detailsCard.Child = _detailsText;
            Grid.SetRow(detailsCard, 3);
            root.Children.Add(detailsCard);

            _statusText = MakeText("", 12.5, FontWeights.Normal, "SecondaryTextBrush");
            _statusText.TextWrapping = TextWrapping.Wrap;
            Grid.SetRow(_statusText, 4);
            root.Children.Add(_statusText);

            Loaded += async (_, _) => await RefreshAsync();
            Closed += (_, _) =>
            {
                _loadCancellation?.Cancel();
                _loadCancellation = null;
            };
        }

        private void MatchOwnerBounds()
        {
            if (Owner is not Window host || host.WindowState == WindowState.Minimized)
                return;

            double width = host.ActualWidth > 0 ? host.ActualWidth : host.Width;
            double height = host.ActualHeight > 0 ? host.ActualHeight : host.Height;
            // With restored / DPI-adjusted windows Width can be updated ahead of
            // ActualWidth, so use the larger finite outer dimension.
            if (double.IsFinite(host.Width) && host.Width > width)
                width = host.Width;
            if (double.IsFinite(host.Height) && host.Height > height)
                height = host.Height;
            if (!double.IsFinite(width) || !double.IsFinite(height) ||
                width <= 0 || height <= 0)
                return;

            // WPF coordinates are device-independent pixels. Use the same owner
            // bounds rather than a hard-coded window size or a screen resolution.
            WindowState = WindowState.Normal;
            Width = Math.Max(MinWidth, width);
            Height = Math.Max(MinHeight, height);
            Left = host.Left;
            Top = host.Top;
            WindowPlacementService.ConstrainToWorkArea(this);
        }

        private Button MakeNavigationButton(string category)
        {
            Button button = new()
            {
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(10, 10, 7, 10),
                FontSize = 13,
                Cursor = Cursors.Hand,
                Style = _filterButtonStyle
            };
            button.Click += (_, _) =>
            {
                _currentCategory = category;
                UpdateNavigationButtons();
                ApplyFilter();
            };
            _navigationButtons.Add(category, button);
            return button;
        }

        private void SetSortMode(string mode)
        {
            _sortMode = mode;
            UpdateSortButtons();
            ApplyFilter();
        }

        private void UpdateSortButtons()
        {
            _sortDateButton.Tag = _sortMode == "date" ? "active" : null;
            _sortNameButton.Tag = _sortMode == "name" ? "active" : null;
        }

        private void UpdateNavigationButtons()
        {
            var visiblePrograms = _showAuxiliary
                ? _programs
                : _programs.Where(program => !program.IsAuxiliary).ToList();
            foreach ((string category, Button button) in _navigationButtons)
            {
                int count = category switch
                {
                    "recent" => visiblePrograms.Count(program => IsRecentlyInstalled(program)),
                    "desktop" => visiblePrograms.Count(program => program.Kind != "MSIX"),
                    "store" => visiblePrograms.Count(program => program.Kind == "MSIX"),
                    _ => visiblePrograms.Count
                };
                string label = category switch
                {
                    "recent" => SettingsService.L("Недавно установлены", "Recently installed"),
                    "desktop" => SettingsService.L("Обычные программы", "Desktop programs"),
                    "store" => SettingsService.L("MSIX / Store", "MSIX / Store"),
                    _ => SettingsService.L("Все программы", "All programs")
                };
                button.Content = $"{label}  ({count:N0})";
                button.Tag = category == _currentCategory ? "active" : null;
            }
        }

        private static bool IsRecentlyInstalled(InstalledProgramInfo program)
        {
            return program.InstalledOn is DateTime date &&
                date.Date <= DateTime.Today &&
                date.Date >= DateTime.Today.AddDays(-30);
        }

        private static DataGridTemplateColumn MakeProgramColumn()
        {
            FrameworkElementFactory horizontal = new(typeof(StackPanel));
            horizontal.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            horizontal.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

            // An application icon must replace the fallback glyph, not overlap it.
            // Many icons have transparent pixels, so simply drawing on top shows
            // an orange checkerboard through the icon (as with LocalSend).
            FrameworkElementFactory iconSlot = new(typeof(Grid));
            iconSlot.SetValue(FrameworkElement.WidthProperty, 30.0);
            iconSlot.SetValue(FrameworkElement.HeightProperty, 30.0);
            iconSlot.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));

            FrameworkElementFactory symbol = new(typeof(TextBlock));
            symbol.SetValue(TextBlock.TextProperty, "▦");
            symbol.SetValue(TextBlock.FontSizeProperty, 21.0);

            symbol.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            symbol.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            Style fallbackStyle = new(typeof(TextBlock));
            fallbackStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty,
                new DynamicResourceExtension("AccentBrush")));
            DataTrigger fallbackSelected = new()
            {
                Binding = new Binding(nameof(DataGridRow.IsSelected))
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor,
                        typeof(DataGridRow), 1)
                },
                Value = true
            };
            fallbackSelected.Setters.Add(new Setter(TextBlock.ForegroundProperty,
                new DynamicResourceExtension("WindowBackgroundBrush")));
            fallbackStyle.Triggers.Add(fallbackSelected);
            DataTrigger hideFallback = new()
            {
                Binding = new Binding(nameof(InstalledProgramInfo.HasIcon)),
                Value = true
            };
            hideFallback.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed));
            fallbackStyle.Triggers.Add(hideFallback);
            symbol.SetValue(FrameworkElement.StyleProperty, fallbackStyle);
            iconSlot.AppendChild(symbol);

            FrameworkElementFactory icon = new(typeof(Image));
            icon.SetBinding(Image.SourceProperty, new Binding(nameof(InstalledProgramInfo.AppIcon)));
            icon.SetValue(FrameworkElement.WidthProperty, 27.0);
            icon.SetValue(FrameworkElement.HeightProperty, 27.0);
            icon.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            icon.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            icon.SetValue(Image.StretchProperty, Stretch.Uniform);
            Style imageStyle = new(typeof(Image));
            imageStyle.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed));
            DataTrigger showIcon = new()
            {
                Binding = new Binding(nameof(InstalledProgramInfo.HasIcon)),
                Value = true
            };
            showIcon.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible));
            imageStyle.Triggers.Add(showIcon);
            icon.SetValue(FrameworkElement.StyleProperty, imageStyle);
            iconSlot.AppendChild(icon);
            horizontal.AppendChild(iconSlot);

            FrameworkElementFactory name = new(typeof(TextBlock));
            name.SetBinding(TextBlock.TextProperty, new Binding(nameof(InstalledProgramInfo.Name)));
            name.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            name.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            name.SetBinding(FrameworkElement.ToolTipProperty,
                new Binding(nameof(InstalledProgramInfo.Name)));
            Style nameStyle = new(typeof(TextBlock));
            DataTrigger selectedName = new()
            {
                Binding = new Binding(nameof(DataGridRow.IsSelected))
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor,
                        typeof(DataGridRow), 1)
                },
                Value = true
            };
            selectedName.Setters.Add(new Setter(TextBlock.ForegroundProperty,
                new DynamicResourceExtension("WindowBackgroundBrush")));
            nameStyle.Triggers.Add(selectedName);
            name.SetValue(FrameworkElement.StyleProperty, nameStyle);
            horizontal.AppendChild(name);

            return new DataGridTemplateColumn
            {
                Header = SettingsService.L("Программа", "Program"),
                CellTemplate = new DataTemplate { VisualTree = horizontal },
                SortMemberPath = nameof(InstalledProgramInfo.Name),
                Width = new DataGridLength(2.7, DataGridLengthUnitType.Star)
            };
        }

        private static DataGridTextColumn MakeColumn(
            string title, string property, double weight, string? tooltipProperty = null)
        {
            Style textStyle = new(typeof(TextBlock));
            textStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty,
                TextTrimming.CharacterEllipsis));
            textStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty,
                VerticalAlignment.Center));
            textStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty,
                new Thickness(5, 0, 5, 0)));
            if (tooltipProperty != null)
                textStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,
                    new Binding(tooltipProperty)));
            DataTrigger selectedText = new()
            {
                Binding = new Binding(nameof(DataGridRow.IsSelected))
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor,
                        typeof(DataGridRow), 1)
                },
                Value = true
            };
            selectedText.Setters.Add(new Setter(TextBlock.ForegroundProperty,
                new DynamicResourceExtension("WindowBackgroundBrush")));
            textStyle.Triggers.Add(selectedText);
            return new DataGridTextColumn
            {
                Header = title,
                Binding = new Binding(property),
                ElementStyle = textStyle,
                IsReadOnly = true,
                Width = new DataGridLength(weight, DataGridLengthUnitType.Star)
            };
        }

        private static Border MakePanel()
        {
            Border panel = new() { BorderThickness = new Thickness(1) };
            panel.SetResourceReference(Border.BackgroundProperty, "CardBackgroundBrush");
            panel.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            panel.SetResourceReference(Border.CornerRadiusProperty, "PanelCornerRadius");
            return panel;
        }

        private static TextBlock MakeText(string value, double size, FontWeight weight, string color)
        {
            TextBlock block = new() { Text = value, FontSize = size, FontWeight = weight };
            block.SetResourceReference(TextBlock.ForegroundProperty, color);
            return block;
        }

        private void UpdateMeasuredSize(InstalledProgramInfo update)
        {
            int index = _programs.FindIndex(item =>
                string.Equals(item.Identity, update.Identity, StringComparison.OrdinalIgnoreCase) &&
                item.Source == update.Source && item.Name == update.Name &&
                item.InstallLocation == update.InstallLocation);
            if (index < 0) return;
            InstalledProgramInfo old = _programs[index];
            if (old.SizeText == update.SizeText && old.SizeSource == update.SizeSource &&
                old.SizeMeasuredUtc == update.SizeMeasuredUtc && old.SizeFromCache == update.SizeFromCache) return;
            InstalledProgramInfo revised = old with
            {
                SizeText = update.SizeText,
                SizeSource = update.SizeSource,
                SizeMeasuredUtc = update.SizeMeasuredUtc,
                SizeFromCache = update.SizeFromCache
            };
            _programs[index] = revised;
            int visibleIndex = _visiblePrograms.IndexOf(old);
            if (visibleIndex >= 0)
            {
                bool wasSelected = ReferenceEquals(_programsTable.SelectedItem, old);
                _visiblePrograms[visibleIndex] = revised;
                if (wasSelected)
                {
                    _programsTable.SelectedItem = revised;
                    ShowSelectedDetails();
                }
            }
        }

        private void ApplyFilter()
        {
            string query = _searchBox.Text.Trim();
            IEnumerable<InstalledProgramInfo> source = _showAuxiliary
                ? _programs
                : _programs.Where(program => !program.IsAuxiliary);
            IEnumerable<InstalledProgramInfo> byCategory = _currentCategory switch
            {
                "recent" => source.Where(IsRecentlyInstalled),
                "desktop" => source.Where(item => item.Kind != "MSIX"),
                "store" => source.Where(item => item.Kind == "MSIX"),
                _ => source
            };
            if (!string.IsNullOrWhiteSpace(query))
                byCategory = byCategory.Where(item =>
                    item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                    item.Publisher.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                    item.InstallLocation.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                    item.Version.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                    item.Kind.Contains(query, StringComparison.CurrentCultureIgnoreCase));

            // Keep available intra-day timestamps; alphabetical order is only a tie breaker.
            List<InstalledProgramInfo> sorted = InstalledProgramChronology.Sort(byCategory, _sortMode == "name");
            _visiblePrograms.Clear();
            foreach (InstalledProgramInfo item in sorted)
                _visiblePrograms.Add(item);
            _countText.Text = SettingsService.L(
                $"Показано: {sorted.Count:N0}",
                $"Showing: {sorted.Count:N0}");
        }

        private void ShowSelectedDetails()
        {
            if (_programsTable.SelectedItem is not InstalledProgramInfo app)
            {
                _detailsText.Text = SettingsService.L(
                    "Выберите программу для просмотра сведений.",
                    "Select an application to see details.");
                return;
            }

            string location = string.IsNullOrWhiteSpace(app.InstallLocation)
                ? app.LocationExplanation : app.LocationText;
            string sizeOrigin = string.IsNullOrWhiteSpace(app.SizeSource)
                ? SettingsService.L("нет достоверных данных", "no reliable data") : app.SizeSource;
            if (app.SizeMeasuredUtc.HasValue)
                sizeOrigin += SettingsService.L(
                    $"; измерено {app.SizeMeasuredUtc.Value.ToLocalTime():dd.MM.yyyy HH:mm}" + (app.SizeFromCache ? "; сохранённое значение" : ""),
                    $"; measured {app.SizeMeasuredUtc.Value.ToLocalTime():yyyy-MM-dd HH:mm}" + (app.SizeFromCache ? "; saved value" : ""));
            string dateOrigin = string.IsNullOrWhiteSpace(app.DateSource)
                ? SettingsService.L("нет достоверных данных", "no reliable data") : app.DateSource;
            if (InstalledProgramChronology.Timestamp(app) is DateTime orderTime && orderTime.TimeOfDay != TimeSpan.Zero)
                dateOrigin += app.InstalledOn?.TimeOfDay == TimeSpan.Zero
                    ? SettingsService.L($"; порядок внутри дня — по времени записи Windows: {orderTime:HH:mm:ss}",
                        $"; same-day order uses the Windows registration timestamp: {orderTime:HH:mm:ss}")
                    : SettingsService.L($"; время источника: {orderTime:HH:mm:ss}", $"; source time: {orderTime:HH:mm:ss}");
            string component = app.IsAuxiliary
                ? SettingsService.L($"\nКлассификация: {app.ComponentReason}",
                    $"\nClassification: {app.ComponentReason}") : "";
            string historyRu = app.FirstObservedOn.HasValue
                ? $"\nВпервые обнаружена Thermiqra: {app.FirstObservedOn.Value:dd.MM.yyyy}" : "";
            string historyEn = app.FirstObservedOn.HasValue
                ? $"\nFirst observed by Thermiqra: {app.FirstObservedOn.Value:yyyy-MM-dd}" : "";
            if (app.CachedInventoryUtc.HasValue)
            {
                historyRu += "\nСохранённая запись: её наличие пока не подтверждено текущим чтением Windows.";
                historyEn += "\nSaved entry: its presence has not yet been confirmed by the current Windows scan.";
            }
            if (app.LastVersionChangeOn.HasValue)
            {
                historyRu += $"  •  Изменение версии замечено: {app.LastVersionChangeOn.Value:dd.MM.yyyy}";
                historyEn += $"  •  Version change observed: {app.LastVersionChangeOn.Value:yyyy-MM-dd}";
            }
            _detailsText.Text = SettingsService.L(
                $"{app.Name}  •  {app.Kind}  •  {app.InstallDateText}\nИсточник: {app.Source}  •  Каталог: {location}\nРазмер: {app.SizeText} ({sizeOrigin})  •  Дата: {dateOrigin}{historyRu}{component}",
                $"{app.Name}  •  {app.Kind}  •  {app.InstallDateText}\nSource: {app.Source}  •  Folder: {location}\nSize: {app.SizeText} ({sizeOrigin})  •  Date: {dateOrigin}{historyEn}{component}");
        }

    }
}
