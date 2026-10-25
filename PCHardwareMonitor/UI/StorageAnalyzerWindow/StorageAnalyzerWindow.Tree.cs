using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCHardwareMonitor;

public partial class StorageAnalyzerWindow
{

    private StorageAnalysisTreeNode? FindTreeNodeByPath(
        string path)
    {
        if (_tree == null)
            return null;

        string normalized =
            NormalizeComparablePath(
                path);

        foreach (StorageAnalysisTreeNode node
                 in _tree.Nodes)
        {
            if (node.Kind !=
                    StorageAnalysisTreeNodeKind.Directory &&
                node.Kind !=
                    StorageAnalysisTreeNodeKind.Drive)
            {
                continue;
            }

            string nodePath =
                NormalizeComparablePath(
                    _tree.GetPath(
                        node.Id));

            if (string.Equals(
                    nodePath,
                    normalized,
                    StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }
        }

        return null;
    }


    private void PopulateTree(
        StorageAnalysisResult result)
    {
        if (_tree == null)
            return;

        _showingSafeCandidates =
            false;

        SafeModeBar.Visibility =
            Visibility.Collapsed;

        ++_treeNavigationSerial;
        UsageTree.Items.Clear();

        _loadedTreeNodeIds.Clear();
        _treeItemsByNodeId.Clear();

        TreeViewItem rootItem =
            CreateTreeItem(
                _tree.RootNode);

        UsageTree.Items.Add(
            rootItem);

        LoadTreeItemChildren(
            rootItem);

        rootItem.IsExpanded =
            true;

        Dispatcher.BeginInvoke(
            new Action(
                ExpandLargestBranch),
            DispatcherPriority.Background);
    }


    private void BackToAllButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_currentResult == null)
            return;

        PopulateTree(
            _currentResult);

        StatusText.Text =
            SettingsService.L(
                "Показаны все данные диска.",
                "All drive data is shown.");
    }


    private void ShowFullTreeIfNeeded()
    {
        if (!_showingSafeCandidates ||
            _currentResult == null)
        {
            return;
        }

        PopulateTree(
            _currentResult);
    }


    private void ShowSafeCandidates()
    {
        // Старый режим списка кандидатов больше не используется.
        // Верхняя карточка теперь открывает проверяемый план очистки.
    }


    private bool SelectPathInTree(
        string path,
        bool expandSelected)
    {
        if (_tree == null ||
            string.IsNullOrWhiteSpace(
                path))
        {
            return false;
        }

        string normalizedDrive =
            NormalizeComparablePath(
                _tree.DriveName);

        string normalizedPath =
            NormalizeComparablePath(
                path);

        if (!normalizedPath.StartsWith(
                normalizedDrive,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!_treeItemsByNodeId.TryGetValue(
                _tree.RootNodeId,
                out TreeViewItem? currentItem))
        {
            return false;
        }

        StorageAnalysisTreeNode currentNode =
            _tree.RootNode;

        string relative =
            normalizedPath.Length >
            normalizedDrive.Length
                ? normalizedPath
                    .Substring(
                        normalizedDrive.Length)
                    .TrimStart('\\')
                : string.Empty;

        if (string.IsNullOrWhiteSpace(
                relative))
        {
            currentItem.IsSelected =
                true;

            int rootNavigationSerial = ++_treeNavigationSerial;
            TreeViewItem rootItem = currentItem;
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (rootNavigationSerial == _treeNavigationSerial)
                    {
                        ScrollTreeHeaderIntoView(rootItem);
                    }
                }),
                DispatcherPriority.Loaded);
            return true;
        }

        string[] parts =
            relative.Split(
                '\\',
                StringSplitOptions.RemoveEmptyEntries);

        foreach (string part
                 in parts)
        {
            LoadTreeItemChildren(
                currentItem);

            StorageAnalysisTreeNode? child =
                _tree.GetChildren(
                        currentNode.Id)
                    .FirstOrDefault(
                        candidate =>
                            string.Equals(
                                candidate.Name,
                                part,
                                StringComparison.OrdinalIgnoreCase));

            if (child == null)
                return false;

            if (!_treeItemsByNodeId.TryGetValue(
                    child.Id,
                    out TreeViewItem? childItem))
            {
                childItem =
                    CreateTreeItem(
                        child);

                currentItem.Items.Add(
                    childItem);
            }

            currentItem.IsExpanded =
                true;

            currentNode =
                child;

            currentItem =
                childItem;
        }

        if (expandSelected &&
            currentNode.IsDirectory)
        {
            LoadTreeItemChildren(
                currentItem);

            currentItem.IsExpanded =
                true;
        }

        currentItem.IsSelected =
            true;

        // Do not BringIntoView the TreeViewItem: if it is already expanded,
        // WPF tries to bring its WHOLE subtree into view and can hide the
        // folder header. Always navigate to the header itself.
        int navigationSerial = ++_treeNavigationSerial;
        StorageAnalysisTree treeSnapshot = _tree;
        TreeViewItem targetItem = currentItem;
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                if (navigationSerial != _treeNavigationSerial ||
                    !ReferenceEquals(_tree, treeSnapshot))
                {
                    return;
                }

                ScrollTreeHeaderIntoView(targetItem);
            }),
            DispatcherPriority.Loaded);

        return true;
    }

    private int _treeNavigationSerial;

    // Manual scrolling to the ROW HEADER, never to the expanded container.
    // If the header is visible already, keep the user's current scroll offset.
    private void ScrollTreeHeaderIntoView(TreeViewItem item)
    {
        ScrollViewer? viewer = FindVisualChild<ScrollViewer>(UsageTree);
        if (viewer == null)
            return;

        // If WPF has not realized the row yet, request only the approximate
        // header rectangle, not the whole expanded item's layout bounds.
        if (item.Header is not FrameworkElement header || !header.IsLoaded)
        {
            try
            {
                _allowTreeBringIntoView = true;
                item.BringIntoView(new Rect(0, 0,
                    Math.Max(1, item.ActualWidth), 36));
            }
            finally
            {
                _allowTreeBringIntoView = false;
            }
            return;
        }

        try
        {
            Point headerTop = header.TransformToAncestor(viewer)
                .Transform(new Point(0, 0));
            double top = headerTop.Y;
            double bottom = top + Math.Max(1, header.ActualHeight);
            double viewport = viewer.ActualHeight;
            const double padding = 12;

            if (top < padding)
            {
                viewer.ScrollToVerticalOffset(Math.Clamp(
                    viewer.VerticalOffset + top - padding,
                    0d, viewer.ScrollableHeight));
            }
            else if (bottom > viewport - padding)
            {
                // A previously expanded folder may extend below the view.
                // Scroll the header into sight without moving the whole subtree.
                viewer.ScrollToVerticalOffset(Math.Clamp(
                    viewer.VerticalOffset + bottom - viewport + padding,
                    0d, viewer.ScrollableHeight));
            }

            viewer.ScrollToLeftEnd();
        }
        catch (InvalidOperationException)
        {
            // Virtualization/layout may detach a row while the scan changes.
        }
    }


    private TreeViewItem CreateTreeItem(
        StorageAnalysisTreeNode node)
    {
        TreeViewItem item =
            new()
            {
                Header =
                    CreateTreeHeader(
                        node),

                Tag =
                    node.Id,

                HorizontalContentAlignment =
                    HorizontalAlignment.Stretch
            };

        if (TryFindResource(
                "StorageTreeItemStyle")
            is Style itemStyle)
        {
            item.Style =
                itemStyle;
        }

        item.Expanded +=
            TreeItem_Expanded;

        item.Collapsed +=
            TreeItem_Collapsed;

        if (_tree != null &&
            node.IsDirectory &&
            _tree.HasChildren(
                node.Id))
        {
            TreeViewItem dummy =
                new()
                {
                    Header =
                        new TextBlock
                        {
                            Text = "…",
                            Opacity = 0.3
                        },

                    IsHitTestVisible =
                        false
                };

            if (TryFindResource(
                    "StorageTreeItemStyle")
                is Style dummyStyle)
            {
                dummy.Style =
                    dummyStyle;
            }

            item.Items.Add(
                dummy);
        }

        _treeItemsByNodeId[
            node.Id] =
            item;

        return item;
    }


    private UIElement CreateTreeHeader(
        StorageAnalysisTreeNode node)
    {
        Grid row =
            new()
            {
                MinHeight =
                    36,

                ToolTip =
                    GetNodeTooltip(
                        node)
            };

        row.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        row.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        105)
            });

        row.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        150)
            });

        StackPanel namePanel =
            new()
            {
                Orientation =
                    Orientation.Horizontal,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        TextBlock icon =
            new()
            {
                Text =
                    GetNodeIcon(
                        node.Kind),

                Width =
                    22,

                FontSize =
                    12,

                FontWeight =
                    FontWeights.Bold,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        icon.SetResourceReference(
            TextBlock.ForegroundProperty,
            node.Kind ==
                StorageAnalysisTreeNodeKind.File
                ? "SecondaryTextBrush"
                : "AccentBrush");

        TextBlock name =
            new()
            {
                Text =
                    GetNodeDisplayName(
                        node),

                FontSize =
                    13,

                FontWeight =
                    node.Kind ==
                        StorageAnalysisTreeNodeKind.Drive
                        ? FontWeights.Bold
                        : node.Kind ==
                            StorageAnalysisTreeNodeKind.Directory
                            ? FontWeights.SemiBold
                            : FontWeights.Normal,

                TextTrimming =
                    TextTrimming.CharacterEllipsis,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        name.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        namePanel.Children.Add(
            icon);

        namePanel.Children.Add(
            name);

        TextBlock size =
            new()
            {
                Text =
                    FormatBytes(
                        node.AllocatedBytes),

                FontSize =
                    13,

                FontWeight =
                    FontWeights.SemiBold,

                HorizontalAlignment =
                    HorizontalAlignment.Right,

                VerticalAlignment =
                    VerticalAlignment.Center,

                Margin =
                    new Thickness(
                        8, 0, 10, 0)
            };

        size.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        Grid share =
            CreateShareIndicator(
                node.AllocatedBytes);

        Grid.SetColumn(
            namePanel,
            0);

        Grid.SetColumn(
            size,
            1);

        Grid.SetColumn(
            share,
            2);

        row.Children.Add(
            namePanel);

        row.Children.Add(
            size);

        row.Children.Add(
            share);

        return row;
    }


    private Grid CreateShareIndicator(
        long bytes)
    {
        long denominator =
            Math.Max(
                _currentResult?.WindowsUsedBytes ?? 0,
                1);

        double percent =
            Math.Clamp(
                bytes * 100d /
                denominator,
                0,
                100);

        Grid panel =
            new()
            {
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        panel.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        48)
            });

        panel.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        92)
            });

        string percentLabel =
            bytes > 0 &&
            percent < 1
                ? "<1 %"
                : $"{percent:0} %";

        TextBlock percentText =
            new()
            {
                Text =
                    percentLabel,

                FontSize =
                    12,

                HorizontalAlignment =
                    HorizontalAlignment.Right,

                VerticalAlignment =
                    VerticalAlignment.Center,

                Margin =
                    new Thickness(
                        0, 0, 8, 0)
            };

        percentText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        Grid bar =
            new()
            {
                Width =
                    88,

                Height =
                    8,

                HorizontalAlignment =
                    HorizontalAlignment.Left,

                VerticalAlignment =
                    VerticalAlignment.Center
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

        Border fill =
            new()
            {
                Width =
                    bytes > 0
                        ? Math.Max(
                            2,
                            88d *
                            percent /
                            100d)
                        : 0,

                HorizontalAlignment =
                    HorizontalAlignment.Left,

                CornerRadius =
                    new CornerRadius(
                        3)
            };

        fill.SetResourceReference(
            Border.BackgroundProperty,
            "AccentBrush");

        bar.Children.Add(
            track);

        bar.Children.Add(
            fill);

        Grid.SetColumn(
            percentText,
            0);

        Grid.SetColumn(
            bar,
            1);

        panel.Children.Add(
            percentText);

        panel.Children.Add(
            bar);

        return panel;
    }


    private void TreeItem_Expanded(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not TreeViewItem item)
            return;

        LoadTreeItemChildren(
            item);

        e.Handled =
            true;
    }


    // Collapse the complete already-created subtree. Lazy data stays cached.
    // Nested Collapsed events must not trigger repeated O(n^2) traversal.
    private bool _recursiveCollapseInProgress;

    private void TreeItem_Collapsed(
        object sender,
        RoutedEventArgs e)
    {
        if (_recursiveCollapseInProgress)
        {
            e.Handled = true;
            return;
        }

        if (sender is not TreeViewItem item ||
            !ReferenceEquals(e.OriginalSource, item))
        {
            return;
        }

        try
        {
            _recursiveCollapseInProgress = true;
            CollapseDescendants(item);
        }
        finally
        {
            _recursiveCollapseInProgress = false;
        }

        e.Handled = true;
    }

    private static void CollapseDescendants(TreeViewItem parent)
    {
        foreach (object child in parent.Items)
        {
            if (child is TreeViewItem descendant)
            {
                CollapseDescendants(descendant);
                descendant.IsExpanded = false;
            }
        }
    }

    private void LoadTreeItemChildren(
        TreeViewItem item)
    {
        if (_tree == null ||
            item.Tag is not long nodeId ||
            _loadedTreeNodeIds.Contains(
                nodeId))
        {
            return;
        }

        _loadedTreeNodeIds.Add(
            nodeId);

        item.Items.Clear();

        IReadOnlyList<StorageAnalysisTreeNode> children =
            _tree.GetChildren(
                nodeId);

        int visibleCount =
            Math.Min(
                children.Count,
                MaxVisibleChildrenPerFolder);

        for (int i = 0;
             i < visibleCount;
             i++)
        {
            item.Items.Add(
                CreateTreeItem(
                    children[i]));
        }

        if (children.Count >
            visibleCount)
        {
            TreeViewItem omitted =
                new()
                {
                    Header =
                        CreateOmittedChildrenHeader(
                            children.Count -
                            visibleCount),

                    IsEnabled =
                        false,

                    IsHitTestVisible =
                        false
                };

            if (TryFindResource(
                    "StorageTreeItemStyle")
                is Style omittedStyle)
            {
                omitted.Style =
                    omittedStyle;
            }

            item.Items.Add(
                omitted);
        }
    }


    private UIElement CreateOmittedChildrenHeader(
        int omittedCount)
    {
        TextBlock text =
            new()
            {
                Text =
                    SettingsService.L(
                        $"Ещё {omittedCount:N0} мелких объектов не показано",
                        $"{omittedCount:N0} more small items are not shown"),

                FontSize =
                    10,

                FontStyle =
                    FontStyles.Italic,

                Margin =
                    new Thickness(
                        4, 4, 0, 4)
            };

        text.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        return text;
    }


    private void ExpandLargestBranch()
    {
        if (_tree == null)
            return;

        IReadOnlyList<StorageAnalysisTreeNode> branch =
            _tree.GetLargestBranch(
                5);

        if (branch.Count == 0)
            return;

        TreeViewItem? lastDirectoryItem =
            null;

        for (int index = 0;
             index < branch.Count;
             index++)
        {
            StorageAnalysisTreeNode node =
                branch[index];

            if (!_treeItemsByNodeId.TryGetValue(
                    node.Id,
                    out TreeViewItem? item))
            {
                break;
            }

            if (node.IsDirectory)
            {
                lastDirectoryItem =
                    item;
            }

            bool isLastNode =
                index ==
                branch.Count - 1;

            if (node.IsDirectory &&
                !isLastNode)
            {
                LoadTreeItemChildren(
                    item);

                item.IsExpanded =
                    true;
            }
        }

        if (lastDirectoryItem != null)
        {
            lastDirectoryItem.IsSelected =
                true;
        }

        Dispatcher.BeginInvoke(
            new Action(
                () =>
                {
                    ScrollViewer? scrollViewer =
                        FindVisualChild<ScrollViewer>(
                            UsageTree);

                    if (scrollViewer == null)
                        return;

                    scrollViewer.ScrollToLeftEnd();
                    scrollViewer.ScrollToTop();
                }),
            DispatcherPriority.Loaded);
    }


    // Prevent WPF from shifting a deep tree when the user merely selects
    // an already-visible row. Navigation from the top summary still uses
    // SelectPathInTree and is intentionally allowed to scroll.
    private void UsageTree_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source ||
            FindAncestorInTree<ToggleButton>(source) != null ||
            FindAncestorInTree<ScrollBar>(source) != null)
        {
            return;
        }

        TreeViewItem? item =
            FindAncestorInTree<TreeViewItem>(source);

        if (item == null ||
            item.Tag is not long ||
            !item.IsEnabled)
        {
            return;
        }

        // A TreeViewItem's visual area includes its expanded descendants.
        // Only its own header should select the item; never intercept a
        // click on a child or on blank space beneath the header.
        double headerY = e.GetPosition(item).Y;
        if (headerY < 0 || headerY > 40)
            return;

        ScrollViewer? viewer =
            FindVisualChild<ScrollViewer>(UsageTree);
        double savedOffset = viewer?.VerticalOffset ?? 0d;

        // Handling PreviewMouseLeftButtonDown stops TreeViewItem's built-in
        // mouse-focus path, which otherwise scrolls expanded ancestors.
        e.Handled = true;
        item.IsSelected = true;

        // Preserve the usual double-click expansion behavior.
        if (e.ClickCount == 2 && item.HasItems)
            item.IsExpanded = !item.IsExpanded;

        // WPF can still coerce the ScrollViewer after layout. Restore the
        // previous position once, clamped if the subtree became shorter.
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                if (!item.IsLoaded)
                {
                    return;
                }

                ScrollViewer? current =
                    FindVisualChild<ScrollViewer>(UsageTree);
                if (current == null)
                    return;

                current.ScrollToVerticalOffset(
                    Math.Clamp(savedOffset, 0d, current.ScrollableHeight));
            }),
            DispatcherPriority.ContextIdle);
    }


    private static T? FindAncestorInTree<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source != null)
        {
            if (source is T found)
                return found;

            source = source is Visual
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        return null;
    }


    private bool _allowTreeBringIntoView;

    private void UsageTree_RequestBringIntoView(
        object sender,
        RequestBringIntoViewEventArgs e)
    {
        // A manual click must not reposition the folder tree.
        // Explicit navigation from a summary card can still scroll
        // to its destination via SelectPathInTree.
        if (!_allowTreeBringIntoView)
            e.Handled = true;
    }


    private void UsageTree_SelectedItemChanged(
        object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        if (_tree == null ||
            e.NewValue is not TreeViewItem item ||
            item.Tag is not long nodeId ||
            !_tree.TryGetNode(
                nodeId,
                out StorageAnalysisTreeNode? node) ||
            node == null)
        {
            return;
        }

        ShowNodeDetails(
            node);
    }


    private void ShowNodeDetails(
        StorageAnalysisTreeNode node)
    {
        if (_tree == null)
            return;

        string path =
            GetDisplayPath(
                node);

        SelectedStorageEntry entry =
            new(
                node.Id,
                path,
                GetNodeDisplayName(
                    node),
                node.AllocatedBytes,
                node.IsDirectory,
                node.Kind);

        ShowSelectedEntry(
            entry);
    }


    private void ShowLegacyItemDetails(
        StorageAnalysisItem item)
    {
        SelectedStorageEntry entry =
            new(
                null,
                item.Path,
                item.Name,
                item.AllocatedBytes,
                item.IsDirectory,
                item.IsDirectory
                    ? StorageAnalysisTreeNodeKind.Directory
                    : StorageAnalysisTreeNodeKind.File);

        ShowSelectedEntry(
            entry);
    }


    private void ShowSelectedEntry(
        SelectedStorageEntry entry)
    {
        _selectedEntry =
            entry;

        SelectedTitleText.Text =
            $"{entry.Name} — " +
            $"{FormatBytes(entry.AllocatedBytes)}";

        SelectedPathText.Text =
            entry.Path;

        StorageGuidance guidance =
            BuildGuidance(
                entry);

        SelectedDescriptionText.Text =
            guidance.Description;

        SafetyTitleText.Text =
            guidance.SafetyTitle;

        SafetyDescriptionText.Text =
            guidance.SafetyDescription;

        SafetyBorder.SetResourceReference(
            Border.BorderBrushProperty,
            guidance.Level ==
                StorageSafetyLevel.Protected
                ? "BorderBrush"
                : "AccentBrush");

        SafetyTitleText.SetResourceReference(
            TextBlock.ForegroundProperty,
            guidance.Level ==
                StorageSafetyLevel.Protected
                ? "PrimaryTextBrush"
                : "AccentBrush");

        bool isPseudo =
            entry.Kind ==
                StorageAnalysisTreeNodeKind.FileSystemData ||
            entry.Kind ==
                StorageAnalysisTreeNodeKind.UnresolvedData;

        bool hasNormalPath =
            !isPseudo &&
            !string.IsNullOrWhiteSpace(
                entry.Path);

        OpenButton.IsEnabled =
            hasNormalPath;

        OpenButton.Content =
            entry.IsDirectory
                ? SettingsService.L(
                    "Показать содержимое",
                    "Show contents")
                : SettingsService.L(
                    "Открыть файл",
                    "Open file");

        ShowExplorerButton.IsEnabled =
            hasNormalPath;

        CopyPathButton.IsEnabled =
            !string.IsNullOrWhiteSpace(
                entry.Path);

        bool isDriveRoot =
            entry.Kind ==
                StorageAnalysisTreeNodeKind.Drive ||
            IsDriveRootPath(
                entry.Path);

        bool isRawRecycleBin =
            IsRawRecycleBinPath(
                entry.Path);

        bool exists =
            entry.IsDirectory
                ? Directory.Exists(
                    entry.Path)
                : File.Exists(
                    entry.Path);

        bool isDownloadsRoot =
            IsDownloadsRootPath(
                entry.Path);

        // Structural protection is applied here and again in the deletion
        // handler and native operations; confirmation cannot bypass it.
        bool blockedByPath = TryGetDirectDeleteBlockReason(
            entry.Path, out string blockReason);
        bool cannotDelete = isPseudo || isDriveRoot ||
                            isRawRecycleBin || blockedByPath;
        DeleteButton.IsEnabled = hasNormalPath && exists && !cannotDelete;

        DeleteButton.Content =
            cannotDelete
                ? SettingsService.L("Удаление недоступно", "Deletion unavailable")
                : isDownloadsRoot
                    ? SettingsService.L(
                        $"Очистить ({FormatBytes(entry.AllocatedBytes)})",
                        $"Clear ({FormatBytes(entry.AllocatedBytes)})")
                    : SettingsService.L(
                        $"Удалить ({FormatBytes(entry.AllocatedBytes)})",
                        $"Delete ({FormatBytes(entry.AllocatedBytes)})");

        DeleteStageHintText.Text =
            blockedByPath ? blockReason :
            isDriveRoot || isPseudo || isRawRecycleBin
                ? SettingsService.L(
                    "Для этой служебной области удаление недоступно.",
                    "Deletion is unavailable for this service area.")
                : SettingsService.L(
                    "Выбранные файлы и папки можно отправить в Корзину или удалить безвозвратно. Перед удалением Thermiqra покажет точный путь и предупреждение.",
                    "Selected files and folders can be sent to the Recycle Bin or deleted permanently. Thermiqra shows the exact path and warning before deletion.");

        DeleteButton.ToolTip =
            cannotDelete
                ? DeleteStageHintText.Text
                : isDownloadsRoot
                    ? SettingsService.L(
                        "Очистить содержимое папки «Загрузки», не удаляя саму системную папку.",
                        "Clear the Downloads contents without deleting the system folder itself.")
                    : SettingsService.L(
                        "Удалить выбранный файл или папку.",
                        "Delete the selected file or folder.");
    }


}
