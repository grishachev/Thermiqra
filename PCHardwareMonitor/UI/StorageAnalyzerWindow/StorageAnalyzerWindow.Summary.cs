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
    private IReadOnlyList<StorageInsightEntry> _storageInsights =
        Array.Empty<StorageInsightEntry>();
    private StorageAnalysisTree? _storageInsightsTree;
    private bool _storageInsightsExpanded;

    private void PopulateSummary(
        StorageAnalysisResult result)
    {
        if (_tree == null)
            return;

        IReadOnlyList<StorageAnalysisTreeNode> branch =
            _tree.GetLargestBranch(
                8);

        StorageAnalysisTreeNode? largestBranch =
            branch.Count > 1
                ? branch[1]
                : null;

        if (largestBranch != null)
        {
            LargestBranchNameText.Text =
                GetNodeDisplayName(
                    largestBranch);

            LargestBranchSizeText.Text =
                FormatBytes(
                    largestBranch.AllocatedBytes) +
                BuildPercentSuffix(
                    largestBranch.AllocatedBytes,
                    result.WindowsUsedBytes);
        }
        else
        {
            LargestBranchNameText.Text =
                "—";

            LargestBranchSizeText.Text =
                "—";
        }

        StorageAnalysisItem? largestFile =
            result.LargestFiles
                .FirstOrDefault();

        if (largestFile != null)
        {
            LargestFileNameText.Text =
                largestFile.Name;

            LargestFileSizeText.Text =
                FormatBytes(
                    largestFile.AllocatedBytes) +
                BuildPercentSuffix(
                    largestFile.AllocatedBytes,
                    result.WindowsUsedBytes);
        }
        else
        {
            LargestFileNameText.Text =
                "—";

            LargestFileSizeText.Text =
                "—";
        }

        AttentionCaptionText.Text = SettingsService.L(
            "НА ЧТО ОБРАТИТЬ ВНИМАНИЕ", "WORTH A CLOSER LOOK");
        AttentionActionText.Text = SettingsService.L(
            "Посмотреть рекомендации", "Review suggestions");
        AttentionHintText.Text = SettingsService.L(
            "Крупные области, которые стоит проверить самостоятельно",
            "Large areas you may want to review yourself");
        AttentionCard.ToolTip = SettingsService.L(
            "Показать рекомендации по данным текущего анализа",
            "Show suggestions based on this scan");

        List<CleanupPlanItem> cleanupPlan =
            BuildSafeCleanupPlan();

        long cleanupBytes =
            cleanupPlan.Sum(
                item =>
                    item.AllocatedBytes);

        if (cleanupBytes > 0)
        {
            PotentialCleanupValueText.Text =
                "≈ " +
                FormatBytes(
                    cleanupBytes);

            PotentialCleanupHintText.Text =
                SettingsService.L(
                    "Проверенные временные файлы и кэш • нажмите, чтобы увидеть план",
                    "Verified temporary files and cache • click to review the plan");
        }
        else
        {
            PotentialCleanupValueText.Text =
                "—";

            PotentialCleanupHintText.Text =
                SettingsService.L(
                    "Проверенных областей для автоматической очистки не найдено",
                    "No verified areas for automatic cleanup were found");
        }
    }


    private async Task PopulateIntelligenceAsync(
        StorageAnalysisTree treeSnapshot,
        long windowsUsedBytes)
    {
        _storageInsightsExpanded = false;
        _storageInsights = Array.Empty<StorageInsightEntry>();
        _storageInsightsTree = treeSnapshot;
        StorageInsightsMoreButton.Visibility = Visibility.Collapsed;
        StorageInsightsItemsPanel.Children.Clear();
        StorageInsightsTitleText.Text = SettingsService.L(
            "Что занимает место", "What uses disk space");
        StorageInsightsHintText.Text = SettingsService.L(
            "Нажмите на категорию, чтобы посмотреть её папку в дереве.",
            "Select a category to view its folder in the tree.");

        try
        {
            IReadOnlyList<StorageInsightEntry> insights = await Task.Run(
                () => StorageIntelligenceService.Explain(
                    treeSnapshot, windowsUsedBytes));

            if (!IsVisible || !ReferenceEquals(_tree, treeSnapshot))
                return;

            _storageInsights = insights;
            _storageInsightsTree = treeSnapshot;
            RenderStorageInsights();
        }
        catch (Exception)
        {
            if (!ReferenceEquals(_tree, treeSnapshot))
                return;

            StorageInsightsHintText.Text = SettingsService.L(
                "Не удалось подготовить пояснения. Подробный анализ папок доступен ниже.",
                "Could not prepare explanations. Detailed folder analysis is available below.");
        }
    }

    private void StorageInsightsMoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (_storageInsightsTree == null ||
            !ReferenceEquals(_tree, _storageInsightsTree))
            return;

        _storageInsightsExpanded = !_storageInsightsExpanded;
        RenderStorageInsights();
    }

    private void RenderStorageInsights()
    {
        StorageInsightsItemsPanel.Children.Clear();
        if (_storageInsightsTree == null ||
            !ReferenceEquals(_tree, _storageInsightsTree))
            return;

        int count = _storageInsightsExpanded
            ? Math.Min(12, _storageInsights.Count)
            : Math.Min(3, _storageInsights.Count);

        for (int i = 0; i < count; i++)
        {
            StorageInsightsItemsPanel.Children.Add(
                CreateStorageInsightButton(_storageInsights[i], _storageInsightsTree));
        }

        StorageInsightsMoreButton.Visibility = _storageInsights.Count > 3
            ? Visibility.Visible : Visibility.Collapsed;
        StorageInsightsMoreButton.Content = _storageInsightsExpanded
            ? SettingsService.L("Свернуть", "Show less")
            : SettingsService.L(
                $"Ещё {Math.Min(9, _storageInsights.Count - 3)}",
                $"Show {Math.Min(9, _storageInsights.Count - 3)} more");

        StorageInsightsHintText.Text = SettingsService.L(
            _storageInsights.Count > 12 && _storageInsightsExpanded
                ? "Показаны 12 крупнейших областей. Остальные доступны в дереве ниже."
                : "Нажмите на область, чтобы увидеть её папку в дереве.",
            _storageInsights.Count > 12 && _storageInsightsExpanded
                ? "Showing the 12 largest areas. More are available in the tree below."
                : "Select an area to view its folder in the tree.");
    }

    private Button CreateStorageInsightButton(
        StorageInsightEntry insight,
        StorageAnalysisTree treeSnapshot)
    {
        Grid heading = new();
        heading.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });
        heading.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = GridLength.Auto
        });

        TextBlock title = new()
        {
            Text = SettingsService.L(insight.TitleRu, insight.TitleEn),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        title.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryTextBrush");
        heading.Children.Add(title);

        TextBlock size = new()
        {
            Text = FormatBytes(insight.AllocatedBytes),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        size.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        Grid.SetColumn(size, 1);
        heading.Children.Add(size);

        TextBlock description = new()
        {
            Text = SettingsService.L(insight.DescriptionRu, insight.DescriptionEn),
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        };
        description.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");

        TextBlock action = new()
        {
            Text = SettingsService.L(insight.ActionRu, insight.ActionEn),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 0)
        };
        action.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryTextBrush");

        StackPanel content = new();
        content.Children.Add(heading);
        content.Children.Add(description);
        content.Children.Add(action);

        Button button = new()
        {
            Content = content,
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(3, 0, 3, 0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top,
            MinHeight = 80,
            ToolTip = treeSnapshot.GetPath(insight.RepresentativeNodeId)
        };

        button.Click += (_, _) =>
        {
            if (!ReferenceEquals(_tree, treeSnapshot))
                return;

            if (SelectPathInTree(
                treeSnapshot.GetPath(insight.RepresentativeNodeId),
                expandSelected: false))
            {
                StatusText.Text = SettingsService.L(
                    insight.ActionRu, insight.ActionEn);
            }
        };
        return button;
    }


    private void AttentionCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        if (_tree == null || !ReferenceEquals(_tree, _storageInsightsTree))
        {
            ShowInfoDialog(
                SettingsService.L("На что обратить внимание", "Worth a closer look"),
                SettingsService.L(
                    "Подождите, пока Thermiqra подготовит пояснения к результатам анализа.",
                    "Please wait while Thermiqra prepares explanations for the scan results."));
            return;
        }

        StorageAnalysisTree treeSnapshot = _tree;
        Window dialog = CreateThemedDialogWindow(
            SettingsService.L("Thermiqra — На что обратить внимание",
                "Thermiqra — Worth a closer look"), 720, 590);

        Grid layout = new() { Margin = new Thickness(20) };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition
        {
            Height = new GridLength(1, GridUnitType.Star)
        });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        StackPanel header = new();
        header.Children.Add(CreateDialogText(
            SettingsService.L("На что обратить внимание", "Worth a closer look"),
            19, FontWeights.Bold, "PrimaryTextBrush"));

        TextBlock intro = CreateDialogText(
            SettingsService.L(
                "Список составлен из уже найденных данных. Большой размер не означает, что файлы лишние. Ничего не удаляется автоматически.",
                "This list uses data from the completed scan. Large size does not mean the files are unnecessary. Nothing is deleted automatically."),
            12, FontWeights.Normal, "SecondaryTextBrush");
        intro.TextWrapping = TextWrapping.Wrap;
        intro.Margin = new Thickness(0, 8, 0, 0);
        header.Children.Add(intro);
        layout.Children.Add(header);

        ScrollViewer scroll = new()
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(0, 14, 0, 14)
        };
        StackPanel items = new();

        if (PotentialCleanupValueText.Text != "—")
        {
            items.Children.Add(CreateAttentionSectionTitle(
                SettingsService.L("Проверенная безопасная очистка",
                    "Verified safe cleanup")));
            TextBlock cleanupText = CreateDialogText(
                SettingsService.L(
                    $"Для проверенных временных данных доступна очистка примерно {PotentialCleanupValueText.Text}. Используйте отдельную карточку «Безопасная очистка» в анализаторе.",
                    $"Verified temporary data may allow you to reclaim about {PotentialCleanupValueText.Text}. Use the separate Safe Cleanup card in the analyzer."),
                12, FontWeights.Normal, "SecondaryTextBrush");
            cleanupText.TextWrapping = TextWrapping.Wrap;
            cleanupText.Margin = new Thickness(0, 0, 0, 16);
            items.Children.Add(cleanupText);
        }

        IReadOnlyList<StorageInsightEntry> review =
            BuildAttentionReviewItems(treeSnapshot);

        items.Children.Add(CreateAttentionSectionTitle(
            SettingsService.L("Стоит проверить самостоятельно", "Worth reviewing")));
        if (review.Count == 0)
        {
            TextBlock empty = CreateDialogText(
                SettingsService.L(
                    "Пока нет крупных областей, которые можно обоснованно выделить для проверки. Полное дерево папок остаётся доступно в анализаторе.",
                    "No large areas can currently be singled out for review with sufficient confidence. The full folder tree remains available in the analyzer."),
                12, FontWeights.Normal, "SecondaryTextBrush");
            empty.TextWrapping = TextWrapping.Wrap;
            items.Children.Add(empty);
        }
        else
        {
            foreach (StorageInsightEntry insight in review)
                items.Children.Add(CreateAttentionReviewButton(insight, treeSnapshot, dialog));
        }

        TextBlock note = CreateDialogText(
            SettingsService.L(
                "Здесь пока работают только встроенные правила Thermiqra. Локальный ИИ подключим отдельным этапом — без автоматического удаления файлов.",
                "Only Thermiqra's built-in rules are used here for now. Local AI will be added separately, without automatic file deletion."),
            11, FontWeights.Normal, "SecondaryTextBrush");
        note.TextWrapping = TextWrapping.Wrap;
        note.Margin = new Thickness(0, 14, 0, 0);
        items.Children.Add(note);

        scroll.Content = items;
        Grid.SetRow(scroll, 1);
        layout.Children.Add(scroll);

        Button close = CreateDialogButton(SettingsService.L("Закрыть", "Close"));
        close.MinWidth = 110;
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.Click += (_, _) => dialog.Close();
        Grid.SetRow(close, 2);
        layout.Children.Add(close);

        dialog.Content = layout;
        dialog.ShowDialog();
    }

    private TextBlock CreateAttentionSectionTitle(string title)
    {
        TextBlock text = CreateDialogText(title, 14, FontWeights.Bold, "PrimaryTextBrush");
        text.Margin = new Thickness(0, 0, 0, 9);
        return text;
    }

    private IReadOnlyList<StorageInsightEntry> BuildAttentionReviewItems(
        StorageAnalysisTree treeSnapshot)
    {
        const long minimumBytes = 1024L * 1024 * 1024;
        List<StorageInsightEntry> detailed = new();

        // Prefer meaningful application folders over the entire Users directory.
        // Inspect only a few known container levels in the completed scan tree.
        foreach (StorageAnalysisTreeNode users in treeSnapshot.GetChildren(treeSnapshot.RootNodeId))
        {
            if (!users.IsDirectory ||
                !string.Equals(users.Name, "Users", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (StorageAnalysisTreeNode profile in treeSnapshot.GetChildren(users.Id))
            {
                if (!profile.IsDirectory)
                    continue;

                foreach (StorageAnalysisTreeNode profileFolder in treeSnapshot.GetChildren(profile.Id))
                {
                    if (!profileFolder.IsDirectory ||
                        !string.Equals(profileFolder.Name, "AppData", StringComparison.OrdinalIgnoreCase))
                        continue;

                    foreach (StorageAnalysisTreeNode appArea in treeSnapshot.GetChildren(profileFolder.Id))
                    {
                        if (!appArea.IsDirectory ||
                            !(appArea.Name.Equals("Local", StringComparison.OrdinalIgnoreCase) ||
                              appArea.Name.Equals("Roaming", StringComparison.OrdinalIgnoreCase) ||
                              appArea.Name.Equals("LocalLow", StringComparison.OrdinalIgnoreCase)))
                            continue;

                        foreach (StorageAnalysisTreeNode folder in treeSnapshot.GetChildren(appArea.Id))
                        {
                            if (!folder.IsDirectory || folder.AllocatedBytes < minimumBytes ||
                                folder.Name.Equals("Temp", StringComparison.OrdinalIgnoreCase) ||
                                folder.Name.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) ||
                                folder.Name.Equals("Programs", StringComparison.OrdinalIgnoreCase) ||
                                folder.Name.Equals("Packages", StringComparison.OrdinalIgnoreCase))
                                continue;

                            detailed.Add(new StorageInsightEntry(
                                "attention-app-" + folder.Id,
                                $"Данные приложения «{folder.Name}»",
                                $"Application data “{folder.Name}”",
                                "Эта папка занимает много места. В ней могут находиться настройки, сохранения, кэш или другие необходимые программе файлы.",
                                "This folder uses substantial space. It may contain settings, saves, cache, or other files needed by the application.",
                                "Проверьте содержимое; не удаляйте папку целиком без понимания её назначения.",
                                "Review its contents; do not remove the entire folder without understanding what it contains.",
                                StorageInsightConfidence.Unknown,
                                folder.AllocatedBytes, folder.Id));
                        }
                    }
                }
            }
        }

        bool hasUserDetails = detailed.Count > 0;
        IEnumerable<StorageInsightEntry> known = _storageInsights.Where(item =>
        {
            if (item.AllocatedBytes < minimumBytes ||
                !(item.Key is "steam-games" or "steam-workshop" or
                  "amd-cache" or "intel-cache" ||
                  item.Key.StartsWith("unclassified-", StringComparison.Ordinal)))
                return false;

            if (!treeSnapshot.TryGetNode(item.RepresentativeNodeId,
                    out StorageAnalysisTreeNode? node) || node == null)
                return false;

            if (item.Key is not ("steam-games" or "steam-workshop") &&
                IsProtectedSystemPath(treeSnapshot.GetPath(node.Id), node.Name))
                return false;

            return !hasUserDetails ||
                !string.Equals(node.Name, "Users", StringComparison.OrdinalIgnoreCase);
        });

        return known.Concat(detailed)
            .GroupBy(item => item.RepresentativeNodeId)
            .Select(group => group.First())
            .OrderByDescending(item => item.AllocatedBytes)
            .Take(6)
            .ToList();
    }

    private Button CreateAttentionReviewButton(
        StorageInsightEntry insight, StorageAnalysisTree treeSnapshot, Window dialog)
    {
        StackPanel content = new();
        Grid top = new();
        top.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        TextBlock name = CreateDialogText(
            SettingsService.L(insight.TitleRu, insight.TitleEn),
            13, FontWeights.SemiBold, "PrimaryTextBrush");
        name.TextWrapping = TextWrapping.Wrap;
        name.Margin = new Thickness(0, 0, 8, 0);
        top.Children.Add(name);

        TextBlock size = CreateDialogText(
            FormatBytes(insight.AllocatedBytes), 13, FontWeights.Bold, "AccentBrush");
        Grid.SetColumn(size, 1);
        top.Children.Add(size);
        content.Children.Add(top);

        TextBlock description = CreateDialogText(
            SettingsService.L(insight.DescriptionRu, insight.DescriptionEn),
            11, FontWeights.Normal, "SecondaryTextBrush");
        description.TextWrapping = TextWrapping.Wrap;
        description.Margin = new Thickness(0, 5, 0, 0);
        content.Children.Add(description);

        TextBlock action = CreateDialogText(
            SettingsService.L("Показать эту область в дереве →",
                "Show this area in the tree →"),
            11, FontWeights.SemiBold, "AccentBrush");
        action.Margin = new Thickness(0, 7, 0, 0);
        content.Children.Add(action);

        Button button = CreateDialogButton("");
        button.Content = content;
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.Margin = new Thickness(0, 0, 0, 9);
        button.Click += (_, _) =>
        {
            dialog.Close();
            if (ReferenceEquals(_tree, treeSnapshot))
            {
                SelectPathInTree(
                    treeSnapshot.GetPath(insight.RepresentativeNodeId),
                    expandSelected: false);
            }
        };
        return button;
    }

    private void PopulateUserActionCards()
    {
        if (_tree == null)
            return;

        _downloadsPath =
            GetDownloadsFolderPath();

        _downloadsNode =
            string.IsNullOrWhiteSpace(
                _downloadsPath)
                ? null
                : FindTreeNodeByPath(
                    _downloadsPath);

        FolderContentStats downloadsStats =
            _downloadsNode != null
                ? GetFolderContentStats(
                    _downloadsNode.Id)
                : new FolderContentStats(
                    0,
                    0);

        bool showDownloads =
            _downloadsNode != null &&
            _downloadsNode.AllocatedBytes >= DownloadsCardMinimumBytes &&
            (downloadsStats.FileCount > 0 ||
             downloadsStats.FolderCount > 0);

        DownloadsCard.Visibility =
            showDownloads
                ? Visibility.Visible
                : Visibility.Collapsed;

        if (showDownloads &&
            _downloadsNode != null)
        {
            DownloadsValueText.Text =
                FormatBytes(
                    _downloadsNode.AllocatedBytes);

            DownloadsHintText.Text =
                SettingsService.L(
                    $"{downloadsStats.FileCount:N0} файлов • {downloadsStats.FolderCount:N0} папок • личные данные, решение за вами",
                    $"{downloadsStats.FileCount:N0} files • {downloadsStats.FolderCount:N0} folders • personal data, the choice is yours");
        }

        RecycleBinInfo recycleInfo =
            QueryRecycleBinInfo();

        bool showRecycle =
            ShouldShowGlobalRecycleCard() &&
            recycleInfo.ItemCount > 0 &&
            recycleInfo.SizeBytes >= RecycleCardMinimumBytes;

        RecycleCard.Visibility =
            showRecycle
                ? Visibility.Visible
                : Visibility.Collapsed;

        if (showRecycle)
        {
            RecycleValueText.Text =
                FormatBytes(
                    recycleInfo.SizeBytes);

            RecycleHintText.Text =
                SettingsService.L(
                    $"{recycleInfo.ItemCount:N0} объектов • общая Корзина Windows • после очистки восстановить их обычным способом будет нельзя",
                    $"{recycleInfo.ItemCount:N0} items • Windows Recycle Bin across all drives • after emptying, they cannot be restored normally");
        }

        UpdateUserActionCardsLayout(
            showDownloads,
            showRecycle);
    }


    private void UpdateUserActionCardsLayout(
        bool showDownloads,
        bool showRecycle)
    {
        UserActionCardsGrid.Visibility =
            showDownloads ||
            showRecycle
                ? Visibility.Visible
                : Visibility.Collapsed;

        Grid.SetColumnSpan(
            DownloadsCard,
            1);

        Grid.SetColumnSpan(
            RecycleCard,
            1);

        DownloadsCard.Margin =
            new Thickness(
                0);

        RecycleCard.Margin =
            new Thickness(
                0);

        if (showDownloads &&
            !showRecycle)
        {
            Grid.SetColumn(
                DownloadsCard,
                0);

            Grid.SetColumnSpan(
                DownloadsCard,
                3);

            return;
        }

        if (showRecycle &&
            !showDownloads)
        {
            Grid.SetColumn(
                RecycleCard,
                0);

            Grid.SetColumnSpan(
                RecycleCard,
                3);

            return;
        }

        Grid.SetColumn(
            DownloadsCard,
            0);

        Grid.SetColumn(
            RecycleCard,
            2);
    }


    private void LargestBranchCard_MouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (_tree == null)
            return;

        IReadOnlyList<StorageAnalysisTreeNode> branch =
            _tree.GetLargestBranch(
                8);

        if (branch.Count < 2)
            return;

        StorageAnalysisTreeNode node =
            branch[1];

        if (SelectPathInTree(
                _tree.GetPath(
                    node.Id),
                expandSelected: true))
        {
            StatusText.Text =
                SettingsService.L(
                    $"Показана самая большая ветка: {GetNodeDisplayName(node)}.",
                    $"Largest branch shown: {GetNodeDisplayName(node)}.");
        }

        e.Handled =
            true;
    }


    private void LargestFileCard_MouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        StorageAnalysisItem? largestFile =
            _currentResult?
                .LargestFiles
                .FirstOrDefault();

        if (largestFile == null)
            return;

        if (SelectPathInTree(
                largestFile.Path,
                expandSelected: false))
        {
            StatusText.Text =
                SettingsService.L(
                    $"Крупнейший файл показан в дереве: {largestFile.Name}.",
                    $"Largest file shown in the tree: {largestFile.Name}.");
        }
        else
        {
            ShowLegacyItemDetails(
                largestFile);

            StatusText.Text =
                SettingsService.L(
                    "Файл найден, но его строку не удалось раскрыть в дереве.",
                    "The file was found, but its row could not be expanded in the tree.");
        }

        e.Handled =
            true;
    }


    private void PopulateLargestFiles(
        StorageAnalysisResult result)
    {
        LargestFilesPanel.Children.Clear();

        int count = Math.Min(5, result.LargestFiles.Count);

        if (count == 0)
        {
            TextBlock empty = new()
            {
                Text = SettingsService.L(
                    "Крупные файлы не найдены.",
                    "No large files found."),
                FontSize = 12
            };

            empty.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            LargestFilesPanel.Children.Add(empty);
            return;
        }

        for (int i = 0; i < count; i++)
        {
            StorageAnalysisItem file = result.LargestFiles[i];

            // The window button template must respect HorizontalContentAlignment.
            // This makes the two-column layout use the full available width.
            Button button = new()
            {
                Tag = file,
                ToolTip = file.Path,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(5, 8, 5, 8),
                Margin = new Thickness(0),
                BorderThickness = new Thickness(
                    0, 0, 0, i == count - 1 ? 0 : 1),
                Background = Brushes.Transparent
            };

            button.SetResourceReference(
                Control.BorderBrushProperty,
                "BorderBrush");

            Grid content = new()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                SnapsToDevicePixels = true
            };

            content.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star),
                MinWidth = 0
            });

            content.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(103)
            });

            StackPanel text = new()
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            TextBlock name = new()
            {
                Text = file.Name,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Left,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            name.SetResourceReference(
                TextBlock.ForegroundProperty,
                "PrimaryTextBrush");

            TextBlock path = new()
            {
                Text = GetCompactParentPath(file.Path),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0),
                TextAlignment = TextAlignment.Left,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            path.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            text.Children.Add(name);
            text.Children.Add(path);

            TextBlock size = new()
            {
                Text = FormatBytes(file.AllocatedBytes),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Right,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };

            size.SetResourceReference(
                TextBlock.ForegroundProperty,
                "AccentBrush");

            Grid.SetColumn(text, 0);
            Grid.SetColumn(size, 1);
            content.Children.Add(text);
            content.Children.Add(size);

            button.Content = content;
            button.Click += LargestFileButton_Click;
            LargestFilesPanel.Children.Add(button);
        }
    }


    private void LargestFileButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is Button button &&
            button.Tag is StorageAnalysisItem item)
        {
            if (!SelectPathInTree(item.Path, expandSelected: false))
                ShowLegacyItemDetails(item);
        }
    }
}
