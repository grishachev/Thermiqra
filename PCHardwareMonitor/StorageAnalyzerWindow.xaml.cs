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

public partial class StorageAnalyzerWindow : Window
{
    private const int MaxVisibleChildrenPerFolder = 1200;

    private readonly StorageAnalyzerService _storageAnalyzerService =
        new();

    private readonly string _driveName;

    private readonly HashSet<long> _loadedTreeNodeIds =
        new();

    private readonly Dictionary<long, TreeViewItem> _treeItemsByNodeId =
        new();

    private CancellationTokenSource? _analysisCancellation;

    private StorageAnalysisResult? _currentResult;

    private StorageAnalysisTree? _tree;

    private SelectedStorageEntry? _selectedEntry;

    private bool _showingSafeCandidates;

    private StorageAnalysisTreeNode? _downloadsNode;

    private string? _downloadsPath;

    private const long DownloadsCardMinimumBytes =
        512L * 1024 * 1024;

    private const long RecycleCardMinimumBytes =
        256L * 1024 * 1024;

    private static readonly Guid DownloadsKnownFolderId =
        new(
            "374DE290-123F-4565-9164-39C4925E467B");

    private const uint ShErbNoConfirmation = 0x00000001;
    private const uint ShErbNoProgressUi = 0x00000002;
    private const uint ShErbNoSound = 0x00000004;

    private const uint FoDelete = 0x0003;
    private const ushort FofSilent = 0x0004;
    private const ushort FofNoConfirmation = 0x0010;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofNoErrorUi = 0x0400;


    public StorageAnalyzerWindow(
        string driveName)
    {
        _driveName =
            NormalizeDisplayDriveName(
                driveName);

        InitializeComponent();

        SettingsService.ApplyLanguageToWindow(
            this);

        ApplyLanguage();
        ApplyDriveName();

        Loaded +=
            StorageAnalyzerWindow_Loaded;

        Closed +=
            StorageAnalyzerWindow_Closed;
    }


    private async void StorageAnalyzerWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        Loaded -=
            StorageAnalyzerWindow_Loaded;

        await StartAnalysisAsync();
    }


    private async Task StartAnalysisAsync()
    {
        _analysisCancellation?.Cancel();
        _analysisCancellation?.Dispose();

        _analysisCancellation =
            new CancellationTokenSource();

        CancellationToken cancellationToken =
            _analysisCancellation.Token;

        SetLoadingState();

        try
        {
            StorageAnalysisResult result =
                await _storageAnalyzerService.AnalyzeAsync(
                    _driveName,
                    cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            PopulateResult(
                result);
        }
        catch (OperationCanceledException)
        {
            if (!IsVisible)
                return;

            SetCancelledState();
        }
        catch (NotSupportedException ex)
        {
            SetErrorState(
                SettingsService.L(
                    "Для этого диска быстрый анализ пока недоступен.",
                    "Fast analysis is not available for this drive yet."),
                ex.Message);
        }
        catch (Exception ex)
        {
            SetErrorState(
                SettingsService.L(
                    "Не удалось выполнить анализ диска.",
                    "Unable to analyze the drive."),
                ex.Message);
        }
    }


    private void SetLoadingState()
    {
        LoadingCard.Visibility =
            Visibility.Visible;

        AnalysisContentGrid.Visibility =
            Visibility.Collapsed;

        ErrorCard.Visibility =
            Visibility.Collapsed;

        CancelButton.Visibility =
            Visibility.Visible;

        CancelButton.IsEnabled =
            true;

        StatusText.Text =
            SettingsService.L(
                $"Анализ {_driveName} выполняется в фоне...",
                $"Analyzing {_driveName} in the background...");
    }


    private void SetCancelledState()
    {
        LoadingCard.Visibility =
            Visibility.Collapsed;

        AnalysisContentGrid.Visibility =
            Visibility.Collapsed;

        ErrorCard.Visibility =
            Visibility.Collapsed;

        CancelButton.Visibility =
            Visibility.Collapsed;

        StatusText.Text =
            SettingsService.L(
                "Анализ отменён.",
                "Analysis cancelled.");
    }


    private void SetErrorState(
        string title,
        string description)
    {
        LoadingCard.Visibility =
            Visibility.Collapsed;

        AnalysisContentGrid.Visibility =
            Visibility.Collapsed;

        ErrorCard.Visibility =
            Visibility.Visible;

        CancelButton.Visibility =
            Visibility.Collapsed;

        ErrorTitleText.Text =
            title;

        ErrorDescriptionText.Text =
            description;

        StatusText.Text =
            SettingsService.L(
                "Анализ не завершён.",
                "Analysis did not complete.");
    }


    private void PopulateResult(
        StorageAnalysisResult result)
    {
        _currentResult =
            result;

        _tree =
            result.Tree;

        if (_tree == null)
        {
            SetErrorState(
                SettingsService.L(
                    "Не удалось построить дерево диска.",
                    "Unable to build the drive tree."),
                SettingsService.L(
                    "Анализ завершился, но структура папок недоступна.",
                    "The analysis completed, but the folder structure is unavailable."));

            return;
        }

        LoadingCard.Visibility =
            Visibility.Collapsed;

        ErrorCard.Visibility =
            Visibility.Collapsed;

        AnalysisContentGrid.Visibility =
            Visibility.Visible;

        CancelButton.Visibility =
            Visibility.Collapsed;

        PopulateSummary(
            result);

        PopulateTree(
            result);

        PopulateUserActionCards();

        PopulateLargestFiles(
            result);

        bool isNtfs =
            string.Equals(
                result.FileSystem,
                "NTFS",
                StringComparison.OrdinalIgnoreCase);

        string unresolvedText =
            result.UnresolvedNonEmptyFiles > 0
                ? isNtfs
                    ? SettingsService.L(
                        $"   •   без полного пути: {result.UnresolvedNonEmptyFiles:N0}",
                        $"   •   unresolved paths: {result.UnresolvedNonEmptyFiles:N0}")
                    : SettingsService.L(
                        $"   •   пропущено: {result.UnresolvedNonEmptyFiles:N0}",
                        $"   •   skipped: {result.UnresolvedNonEmptyFiles:N0}")
                : "";

        string modeText =
            isNtfs
                ? ""
                : SettingsService.L(
                    $"   •   {result.FileSystem}, безопасный обход",
                    $"   •   {result.FileSystem}, safe traversal");

        StatusText.Text =
            SettingsService.L(
                $"Готово: {result.FileCount:N0} файлов, " +
                $"{result.DirectoryCount:N0} папок" +
                $"{modeText}" +
                $"{unresolvedText}",
                $"Done: {result.FileCount:N0} files, " +
                $"{result.DirectoryCount:N0} folders" +
                $"{modeText}" +
                $"{unresolvedText}");
    }


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


    private List<CleanupPlanItem> BuildSafeCleanupPlan()
    {
        List<CleanupPlanItem> result =
            new();

        if (_tree == null)
            return result;

        string localAppData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        HashSet<string> seen =
            new(
                StringComparer.OrdinalIgnoreCase);

        AddCleanupPlanItem(
            result,
            seen,
            Path.GetTempPath(),
            SettingsService.L(
                "Временные файлы пользователя",
                "User temporary files"),
            SettingsService.L(
                "Будет очищено только содержимое папки. Занятые и защищённые файлы будут пропущены.",
                "Only the folder contents will be cleaned. Files that are in use or protected will be skipped."));

        AddCleanupPlanItem(
            result,
            seen,
            Path.Combine(
                localAppData,
                "NVIDIA",
                "DXCache"),
            "NVIDIA DXCache",
            SettingsService.L(
                "Кэш шейдеров NVIDIA. Будет очищено только содержимое, сама папка останется. Перед очисткой лучше закрыть игры и 3D-приложения.",
                "NVIDIA shader cache. Only the contents will be cleaned; the folder itself will remain. It is better to close games and 3D applications before cleanup."));

        AddCleanupPlanItem(
            result,
            seen,
            Path.Combine(
                localAppData,
                "NVIDIA",
                "GLCache"),
            "NVIDIA GLCache",
            SettingsService.L(
                "Кэш шейдеров NVIDIA. Будет очищено только содержимое, сама папка останется. Перед очисткой лучше закрыть игры и 3D-приложения.",
                "NVIDIA shader cache. Only the contents will be cleaned; the folder itself will remain. It is better to close games and 3D applications before cleanup."));

        AddCleanupPlanItem(
            result,
            seen,
            Path.Combine(
                localAppData,
                "NVIDIA Corporation",
                "NV_Cache"),
            "NVIDIA NV_Cache",
            SettingsService.L(
                "Кэш NVIDIA. Будет очищено только содержимое, сама папка останется.",
                "NVIDIA cache. Only the contents will be cleaned; the folder itself will remain."));

        result.Sort(
            (left, right) =>
                right.AllocatedBytes.CompareTo(
                    left.AllocatedBytes));

        return result;
    }


    private void AddCleanupPlanItem(
        List<CleanupPlanItem> result,
        HashSet<string> seen,
        string path,
        string title,
        string description)
    {
        if (_tree == null ||
            string.IsNullOrWhiteSpace(
                path))
        {
            return;
        }

        string normalized =
            NormalizeComparablePath(
                path);

        if (!seen.Add(
                normalized))
        {
            return;
        }

        StorageAnalysisTreeNode? node =
            FindTreeNodeByPath(
                normalized);

        if (node == null ||
            !node.IsDirectory ||
            node.AllocatedBytes <= 0)
        {
            return;
        }

        result.Add(
            new CleanupPlanItem(
                title,
                normalized,
                node.AllocatedBytes,
                description));
    }


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
            recycleInfo.ItemCount > 0;

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


    private void ViewDownloadsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_downloadsNode == null ||
            _tree == null)
        {
            return;
        }

        if (SelectPathInTree(
                _tree.GetPath(
                    _downloadsNode.Id),
                expandSelected: true))
        {
            StatusText.Text =
                SettingsService.L(
                    "Папка «Загрузки» показана в дереве.",
                    "The Downloads folder is shown in the tree.");
        }
    }


    private async void ClearDownloadsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_downloadsNode == null ||
            string.IsNullOrWhiteSpace(
                _downloadsPath))
        {
            return;
        }

        FolderContentStats stats =
            GetFolderContentStats(
                _downloadsNode.Id);

        DownloadsClearChoice choice =
            ShowDownloadsClearChoiceDialog(
                _downloadsNode.AllocatedBytes,
                stats);

        if (choice ==
            DownloadsClearChoice.Cancel)
        {
            return;
        }

        ClearDownloadsButton.IsEnabled =
            false;

        ViewDownloadsButton.IsEnabled =
            false;

        try
        {
            DownloadsCleanupResult result;

            if (choice ==
                DownloadsClearChoice.Recycle)
            {
                StatusText.Text =
                    SettingsService.L(
                        "Перемещение содержимого «Загрузок» в корзину...",
                        "Moving Downloads contents to the Recycle Bin...");

                IntPtr ownerHandle =
                    new WindowInteropHelper(
                        this)
                    .Handle;

                result =
                    await Task.Run(
                        () =>
                            MoveDownloadsContentsToRecycleBin(
                                _downloadsPath,
                                ownerHandle));

                if (result.FailedPaths.Count > 0)
                {
                    bool deleteFailedPermanently =
                        ShowConfirmationDialog(
                            SettingsService.L(
                                "Не всё поместилось в корзину",
                                "Some items could not be recycled"),
                            SettingsService.L(
                                $"{result.FailedPaths.Count:N0} объектов не удалось переместить в корзину. Удалить только эти объекты безвозвратно?",
                                $"{result.FailedPaths.Count:N0} items could not be moved to the Recycle Bin. Permanently delete only those items?"),
                            SettingsService.L(
                                "Удалить безвозвратно",
                                "Delete permanently"));

                    if (deleteFailedPermanently)
                    {
                        PermanentDeleteResult permanentResult =
                            await Task.Run(
                                () =>
                                    DeletePathsPermanently(
                                        result.FailedPaths));

                        result =
                            result with
                            {
                                PermanentlyDeletedCount =
                                    permanentResult.DeletedCount,

                                PermanentDeleteFailures =
                                    permanentResult.FailedCount
                            };
                    }
                }

                ShowDownloadsCleanupResultDialog(
                    result,
                    movedToRecycleBin: true);
            }
            else
            {
                bool confirmed =
                    ShowConfirmationDialog(
                        SettingsService.L(
                            "Удалить содержимое «Загрузок» безвозвратно?",
                            "Permanently delete Downloads contents?"),
                        SettingsService.L(
                            "Файлы и папки будут удалены без возможности восстановления через Корзину. Сама папка «Загрузки» останется.",
                            "Files and folders will be deleted without the ability to restore them from the Recycle Bin. The Downloads folder itself will remain."),
                        SettingsService.L(
                            "Удалить безвозвратно",
                            "Delete permanently"));

                if (!confirmed)
                    return;

                StatusText.Text =
                    SettingsService.L(
                        "Безвозвратное удаление содержимого «Загрузок»...",
                        "Permanently deleting Downloads contents...");

                string[] paths =
                    Directory.Exists(
                        _downloadsPath)
                        ? Directory
                            .EnumerateFileSystemEntries(
                                _downloadsPath)
                            .ToArray()
                        : Array.Empty<string>();

                PermanentDeleteResult permanentResult =
                    await Task.Run(
                        () =>
                            DeletePathsPermanently(
                                paths));

                result =
                    new DownloadsCleanupResult(
                        paths.Length -
                            permanentResult.FailedCount,
                        Array.Empty<string>(),
                        permanentResult.DeletedCount,
                        permanentResult.FailedCount);

                ShowDownloadsCleanupResultDialog(
                    result,
                    movedToRecycleBin: false);
            }

            await StartAnalysisAsync();
        }
        catch (Exception ex)
        {
            ShowInfoDialog(
                SettingsService.L(
                    "Не удалось очистить «Загрузки»",
                    "Unable to clean Downloads"),
                ex.Message);
        }
        finally
        {
            ClearDownloadsButton.IsEnabled =
                true;

            ViewDownloadsButton.IsEnabled =
                true;
        }
    }


    private void ViewRecycleButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName =
                        "explorer.exe",

                    Arguments =
                        "shell:RecycleBinFolder",

                    UseShellExecute =
                        true
                });
        }
        catch (Exception ex)
        {
            ShowInfoDialog(
                SettingsService.L(
                    "Не удалось открыть Корзину",
                    "Unable to open Recycle Bin"),
                ex.Message);
        }
    }


    private async void ClearRecycleButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        RecycleBinInfo recycleInfo =
            QueryRecycleBinInfo();

        if (recycleInfo.ItemCount <= 0 ||
            recycleInfo.SizeBytes <= 0)
        {
            ShowInfoDialog(
                SettingsService.L(
                    "Корзина",
                    "Recycle Bin"),
                SettingsService.L(
                    "Корзина уже пуста.",
                    "The Recycle Bin is already empty."));

            return;
        }

        bool confirmed =
            ShowConfirmationDialog(
                SettingsService.L(
                    "Очистить корзину?",
                    "Empty Recycle Bin?"),
                SettingsService.L(
                    $"Будет безвозвратно удалено {recycleInfo.ItemCount:N0} объектов, примерно {FormatBytes(recycleInfo.SizeBytes)}. После очистки восстановить их обычным способом будет нельзя.",
                    $"{recycleInfo.ItemCount:N0} items, approximately {FormatBytes(recycleInfo.SizeBytes)}, will be permanently deleted. They cannot be restored normally after the Recycle Bin is emptied."),
                SettingsService.L(
                    "Очистить корзину",
                    "Empty Recycle Bin"));

        if (!confirmed)
            return;

        ClearRecycleButton.IsEnabled =
            false;

        try
        {
            IntPtr ownerHandle =
                new WindowInteropHelper(
                    this)
                .Handle;

            int result =
                SHEmptyRecycleBin(
                    ownerHandle,
                    null,
                    ShErbNoConfirmation |
                    ShErbNoProgressUi |
                    ShErbNoSound);

            if (result != 0)
            {
                throw new InvalidOperationException(
                    $"SHEmptyRecycleBin returned 0x{result:X8}.");
            }

            ShowInfoDialog(
                SettingsService.L(
                    "Корзина очищена",
                    "Recycle Bin emptied"),
                SettingsService.L(
                    "Корзина очищена. Thermiqra сейчас пересчитает свободное место.",
                    "The Recycle Bin has been emptied. Thermiqra will now recalculate free space."));

            await StartAnalysisAsync();
        }
        catch (Exception ex)
        {
            ShowInfoDialog(
                SettingsService.L(
                    "Не удалось очистить Корзину",
                    "Unable to empty Recycle Bin"),
                ex.Message);
        }
        finally
        {
            ClearRecycleButton.IsEnabled =
                true;
        }
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


    private async void SafeCandidatesCard_MouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        e.Handled =
            true;

        List<CleanupPlanItem> plan =
            BuildSafeCleanupPlan();

        if (plan.Count == 0)
        {
            ShowInfoDialog(
                SettingsService.L(
                    "Безопасная очистка",
                    "Safe cleanup"),
                SettingsService.L(
                    "Thermiqra не нашла на этом диске областей, для которых есть проверенный сценарий автоматической очистки.",
                    "Thermiqra did not find any areas on this drive with a verified automatic cleanup procedure."));

            return;
        }

        List<CleanupPlanItem>? selected =
            ShowSafeCleanupPlanDialog(
                plan);

        if (selected == null ||
            selected.Count == 0)
        {
            return;
        }

        SafeCandidatesCard.IsEnabled =
            false;

        StatusText.Text =
            SettingsService.L(
                "Выполняется безопасная очистка...",
                "Running safe cleanup...");

        try
        {
            CleanupExecutionResult cleanupResult =
                await Task.Run(
                    () =>
                        ExecuteSafeCleanup(
                            selected));

            ShowCleanupResultDialog(
                cleanupResult);

            await StartAnalysisAsync();
        }
        finally
        {
            SafeCandidatesCard.IsEnabled =
                true;
        }
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

        Dispatcher.BeginInvoke(
            new Action(
                () =>
                {
                    currentItem.BringIntoView();

                    ScrollViewer? scrollViewer =
                        FindVisualChild<ScrollViewer>(
                            UsageTree);

                    scrollViewer?.ScrollToLeftEnd();
                }),
            DispatcherPriority.Loaded);

        return true;
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
                    30,

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
                    11,

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
                    11,

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
                    10,

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

        DeleteButton.Content =
            SettingsService.L(
                $"Удалить ({FormatBytes(entry.AllocatedBytes)})",
                $"Delete ({FormatBytes(entry.AllocatedBytes)})");

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

        DeleteButton.IsEnabled =
            hasNormalPath &&
            exists &&
            !isDriveRoot &&
            !isRawRecycleBin;

        DeleteButton.Content =
            isDownloadsRoot
                ? SettingsService.L(
                    $"Очистить ({FormatBytes(entry.AllocatedBytes)})",
                    $"Clear ({FormatBytes(entry.AllocatedBytes)})")
                : SettingsService.L(
                    $"Удалить ({FormatBytes(entry.AllocatedBytes)})",
                    $"Delete ({FormatBytes(entry.AllocatedBytes)})");

        DeleteButton.ToolTip =
            isDownloadsRoot
                ? SettingsService.L(
                    "Очистить содержимое папки «Загрузки», не удаляя саму системную папку.",
                    "Clear the Downloads contents without deleting the system folder itself.")
                : isRawRecycleBin
                    ? SettingsService.L(
                        "Корзина очищается отдельной кнопкой «Очистить корзину».",
                        "Use the separate Empty Recycle Bin action for the Recycle Bin.")
                    : isDriveRoot
                        ? SettingsService.L(
                            "Корень диска целиком удалить нельзя.",
                            "The drive root cannot be deleted.")
                        : SettingsService.L(
                            "Удалить выбранный файл или папку.",
                            "Delete the selected file or folder.");
    }


    private StorageGuidance BuildGuidance(
        SelectedStorageEntry entry)
    {
        string path =
            NormalizeComparablePath(
                entry.Path);

        string name =
            entry.Name;

        if (entry.Kind ==
            StorageAnalysisTreeNodeKind.Drive)
        {
            return new StorageGuidance(
                StorageSafetyLevel.Review,
                SettingsService.L(
                    "Это весь занятый объём выбранного диска. Раскрывайте самые большие ветки слева — они уже отсортированы по размеру.",
                    "This is all used space on the selected drive. Expand the largest branches on the left — they are already sorted by size."),
                SettingsService.L(
                    "Безопасность: сначала найдите конкретную папку или файл",
                    "Safety: first find the specific folder or file"),
                SettingsService.L(
                    "Удалять данные нужно только после того, как понятно, что именно занимает место.",
                    "Delete data only after you know exactly what is using the space."));
        }

        if (entry.Kind ==
            StorageAnalysisTreeNodeKind.FileSystemData)
        {
            return new StorageGuidance(
                StorageSafetyLevel.Protected,
                SettingsService.L(
                    "Это служебное место файловой системы: таблицы, метаданные и другие данные, которыми управляет Windows.",
                    "This is file-system service space: tables, metadata, and other data managed by Windows."),
                SettingsService.L(
                    "Безопасность: не удалять вручную",
                    "Safety: do not delete manually"),
                SettingsService.L(
                    "Thermiqra показывает этот объём для полноты картины. Это не обычная папка.",
                    "Thermiqra shows this space for completeness. It is not a normal folder."));
        }

        if (entry.Kind ==
            StorageAnalysisTreeNodeKind.UnresolvedData)
        {
            return new StorageGuidance(
                StorageSafetyLevel.Protected,
                SettingsService.L(
                    "Часть данных найдена в файловой таблице, но Thermiqra не смогла надёжно восстановить полный путь.",
                    "Some data was found in the file table, but Thermiqra could not reliably reconstruct the full path."),
                SettingsService.L(
                    "Безопасность: ничего не удалять по этой строке",
                    "Safety: do not delete anything from this row"),
                SettingsService.L(
                    "Эта строка нужна только для точного подсчёта занятого места.",
                    "This row is shown only to keep used-space totals accurate."));
        }

        if (IsKnownTempPath(
                path))
        {
            return new StorageGuidance(
                StorageSafetyLevel.Safe,
                SettingsService.L(
                    "Это временные файлы программ. Их часто можно удалить, но часть файлов может использоваться прямо сейчас.",
                    "These are temporary application files. They can often be removed, but some files may be in use right now."),
                SettingsService.L(
                    "Безопасность: обычно безопасно очищать частично",
                    "Safety: usually safe to clean partially"),
                SettingsService.L(
                    "Закройте лишние программы перед очисткой. Занятые файлы Thermiqra не должна считать успешно удалёнными.",
                    "Close unnecessary applications before cleaning. Files that are in use must not be treated as successfully deleted."));
        }

        if (path.EndsWith(
                @"\$RECYCLE.BIN",
                StringComparison.OrdinalIgnoreCase))
        {
            return new StorageGuidance(
                StorageSafetyLevel.Safe,
                SettingsService.L(
                    "Это корзина выбранного диска. Удалённые ранее файлы всё ещё занимают здесь место.",
                    "This is the Recycle Bin for the selected drive. Previously deleted files are still using space here."),
                SettingsService.L(
                    "Безопасность: можно очистить, если содержимое больше не нужно",
                    "Safety: can be cleared if you no longer need its contents"),
                SettingsService.L(
                    "После очистки восстановить эти файлы обычным способом уже не получится.",
                    "After emptying it, these files can no longer be restored normally."));
        }

        if (IsNvidiaShaderCachePath(
                path))
        {
            return new StorageGuidance(
                StorageSafetyLevel.Safe,
                SettingsService.L(
                    "Это кэш шейдеров NVIDIA. Драйвер и игры сохраняют здесь временные данные, чтобы быстрее запускать и компилировать графику.",
                    "This is the NVIDIA shader cache. The driver and games keep temporary data here to speed up startup and shader compilation."),
                SettingsService.L(
                    "Безопасность: обычно можно очистить после закрытия игр",
                    "Safety: usually safe to clear after closing games"),
                SettingsService.L(
                    "После очистки кэш будет создан заново. Первый запуск некоторых игр после этого может быть немного медленнее.",
                    "The cache will be created again. The first launch of some games afterward may be a little slower."));
        }

        if (IsModIoPath(
                path))
        {
            return new StorageGuidance(
                StorageSafetyLevel.Review,
                SettingsService.L(
                    "Это данные модификаций, загруженных через mod.io. Здесь могут находиться сами моды, их кэш и служебные данные конкретной игры.",
                    "These are modification files downloaded through mod.io. This area can contain the mods themselves, cache, and game-specific service data."),
                SettingsService.L(
                    "Безопасность: удаляйте только ненужные моды",
                    "Safety: remove only mods you no longer need"),
                SettingsService.L(
                    "Если игра управляет подписками на моды, удалённые файлы могут быть загружены снова. Лучше сначала определить конкретный мод или игру.",
                    "If the game manages mod subscriptions, deleted files may be downloaded again. First identify the specific mod or game."));
        }

        if (IsApplicationCachePath(
                path))
        {
            return new StorageGuidance(
                StorageSafetyLevel.Safe,
                SettingsService.L(
                    "Похоже на кэш приложения — временные данные, которые программа создаёт для ускорения работы.",
                    "This looks like an application cache — temporary data created to speed up the app."),
                SettingsService.L(
                    "Безопасность: обычно можно очистить после закрытия программы",
                    "Safety: usually safe to clear after closing the app"),
                SettingsService.L(
                    "Программа может создать этот кэш заново при следующем запуске.",
                    "The application may recreate this cache the next time it starts."));
        }

        if (IsSteamGamePath(
                path))
        {
            return new StorageGuidance(
                StorageSafetyLevel.Review,
                SettingsService.L(
                    "Похоже на файлы установленной игры Steam. Они могут занимать десятки или сотни гигабайт.",
                    "This looks like files from an installed Steam game. They can use tens or hundreds of gigabytes."),
                SettingsService.L(
                    "Безопасность: лучше удалять игру через Steam",
                    "Safety: preferably uninstall the game through Steam"),
                SettingsService.L(
                    "Ручное удаление папки может оставить запись игры или дополнительные данные.",
                    "Deleting the folder manually can leave the game entry or additional data behind."));
        }

        if (entry.IsDirectory &&
            TryGetAppDataApplicationName(
                path,
                out string appName))
        {
            return new StorageGuidance(
                StorageSafetyLevel.Review,
                SettingsService.L(
                    $"Это рабочие данные приложения или игры «{appName}». Здесь могут храниться настройки, кэш, журналы, профили, сохранения и данные модификаций.",
                    $"These are working data for the application or game “{appName}”. This area can contain settings, cache, logs, profiles, saves, and modification data."),
                SettingsService.L(
                    "Безопасность: не удалять папку целиком без проверки",
                    "Safety: do not delete the whole folder without reviewing it"),
                SettingsService.L(
                    "Раскройте эту папку дальше. Внутри ищите конкретные кэши, журналы, временные данные или большие файлы, назначение которых понятно.",
                    "Expand this folder further. Look for specific caches, logs, temporary data, or large files whose purpose is clear."));
        }

        if (IsAppDataPath(
                path))
        {
            return new StorageGuidance(
                StorageSafetyLevel.Review,
                SettingsService.L(
                    "Это AppData — рабочие данные программ: кэш, настройки, профили, сохранения и базы данных. Удалять всю папку целиком нельзя.",
                    "This is AppData — application working data such as caches, settings, profiles, saves, and databases. Do not delete the whole folder."),
                SettingsService.L(
                    "Безопасность: разбирать по конкретным подпапкам",
                    "Safety: review specific subfolders"),
                SettingsService.L(
                    "Раскрывайте ветку дальше и ищите конкретную программу или кэш. Thermiqra не должна советовать удалять AppData целиком.",
                    "Expand the branch further and identify a specific application or cache. Thermiqra should never recommend deleting AppData as a whole."));
        }

        if (IsProtectedSystemPath(
                path,
                name))
        {
            return new StorageGuidance(
                StorageSafetyLevel.Protected,
                GetProtectedDescription(
                    path,
                    name),
                SettingsService.L(
                    "Безопасность: не удалять вручную",
                    "Safety: do not delete manually"),
                SettingsService.L(
                    "Для системных файлов и установленных программ нужно использовать штатные средства Windows или самой программы.",
                    "For system files and installed applications, use Windows or the application's own tools."));
        }

        if (IsUserDataPath(
                path))
        {
            return new StorageGuidance(
                StorageSafetyLevel.Review,
                SettingsService.L(
                    "Это пользовательские данные. Здесь могут быть загрузки, документы, видео, рабочие файлы или данные программ.",
                    "This is user data. It can contain downloads, documents, videos, work files, or application data."),
                SettingsService.L(
                    "Безопасность: проверьте содержимое перед удалением",
                    "Safety: review the contents before deleting"),
                SettingsService.L(
                    "Если вы узнаёте конкретный файл или папку и они больше не нужны, их можно будет удалить прямо из Thermiqra.",
                    "If you recognize a specific file or folder and no longer need it, you will be able to delete it directly from Thermiqra."));
        }

        if (!entry.IsDirectory)
        {
            string fileDescription =
                GetKnownFileDescription(
                    path);

            return new StorageGuidance(
                StorageSafetyLevel.Review,
                fileDescription,
                SettingsService.L(
                    "Безопасность: удаляйте только если понимаете назначение файла",
                    "Safety: delete only if you understand what the file is for"),
                SettingsService.L(
                    "Thermiqra показывает реальный размер файла. Перед удалением проверьте имя и путь.",
                    "Thermiqra shows the file's actual size. Check its name and path before deleting."));
        }

        return new StorageGuidance(
            StorageSafetyLevel.Review,
            SettingsService.L(
                "Это папка с данными. Раскройте её слева — внутри элементы уже отсортированы от самых больших к меньшим.",
                "This is a data folder. Expand it on the left — its contents are already sorted from largest to smallest."),
            SettingsService.L(
                "Безопасность: сначала посмотрите, что находится внутри",
                "Safety: first review what is inside"),
            SettingsService.L(
                "Если назначение папки неизвестно, Thermiqra не будет делать вид, что её можно безопасно удалить.",
                "If the folder's purpose is unknown, Thermiqra will not pretend that it is safe to delete."));
    }


    private static bool IsProtectedSystemPath(
        string path,
        string name)
    {
        string normalizedName =
            name.Trim();

        string[] protectedFiles =
        {
            "pagefile.sys",
            "hiberfil.sys",
            "swapfile.sys",
            "bootmgr",
            "ntldr"
        };

        if (protectedFiles.Any(
                file =>
                    string.Equals(
                        normalizedName,
                        file,
                        StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        string root =
            Path.GetPathRoot(
                path) ?? "";

        string[] protectedFolders =
        {
            "Windows",
            "Program Files",
            "Program Files (x86)",
            "ProgramData",
            "System Volume Information",
            "$Windows.~BT",
            "$WinREAgent"
        };

        foreach (string folder
                 in protectedFolders)
        {
            string protectedPath =
                NormalizeComparablePath(
                    Path.Combine(
                        root,
                        folder));

            if (string.Equals(
                    path,
                    protectedPath,
                    StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(
                    protectedPath + "\\",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }


    private static string GetProtectedDescription(
        string path,
        string name)
    {
        if (string.Equals(
                name,
                "pagefile.sys",
                StringComparison.OrdinalIgnoreCase))
        {
            return SettingsService.L(
                "Это файл подкачки Windows. Система использует его как дополнительную виртуальную память.",
                "This is the Windows paging file. The system uses it as additional virtual memory.");
        }

        if (string.Equals(
                name,
                "hiberfil.sys",
                StringComparison.OrdinalIgnoreCase))
        {
            return SettingsService.L(
                "Это системный файл гибернации Windows. Его размер связан с режимами гибернации и быстрого запуска.",
                "This is the Windows hibernation file. Its size is related to hibernation and Fast Startup.");
        }

        if (path.Contains(
                @"\Program Files",
                StringComparison.OrdinalIgnoreCase))
        {
            return SettingsService.L(
                "Здесь находятся установленные программы и их рабочие файлы. Удаление отдельных папок вручную может сломать программы.",
                "Installed applications and their working files are stored here. Deleting folders manually can break applications.");
        }

        if (path.Contains(
                @"\ProgramData",
                StringComparison.OrdinalIgnoreCase))
        {
            return SettingsService.L(
                "Здесь хранятся общие данные установленных программ. По одному имени папки нельзя надёжно решить, что её можно удалить.",
                "Shared data for installed applications is stored here. A folder name alone is not enough to decide that it is safe to delete.");
        }

        return SettingsService.L(
            "Это системная область Windows. Она может содержать компоненты системы, обновления, точки восстановления или служебные данные.",
            "This is a Windows system area. It can contain system components, updates, restore data, or other service files.");
    }


    private static bool IsKnownTempPath(
        string path)
    {
        return
            path.EndsWith(
                @"\AppData\Local\Temp",
                StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(
                @"\Windows\Temp",
                StringComparison.OrdinalIgnoreCase);
    }


    private static bool IsApplicationCachePath(
        string path)
    {
        if (!path.Contains(
                @"\AppData\",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return
            path.EndsWith(
                @"\Cache",
                StringComparison.OrdinalIgnoreCase) ||
            path.Contains(
                @"\Cache\",
                StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(
                @"\Code Cache",
                StringComparison.OrdinalIgnoreCase) ||
            path.Contains(
                @"\Code Cache\",
                StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(
                @"\GPUCache",
                StringComparison.OrdinalIgnoreCase) ||
            path.Contains(
                @"\GPUCache\",
                StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(
                @"\DXCache",
                StringComparison.OrdinalIgnoreCase) ||
            path.Contains(
                @"\DXCache\",
                StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(
                @"\GLCache",
                StringComparison.OrdinalIgnoreCase) ||
            path.Contains(
                @"\GLCache\",
                StringComparison.OrdinalIgnoreCase);
    }


    private static bool IsNvidiaShaderCachePath(
        string path)
    {
        bool nvidiaPath =
            path.Contains(
                @"\NVIDIA\",
                StringComparison.OrdinalIgnoreCase) ||
            path.Contains(
                @"\NVIDIA Corporation\",
                StringComparison.OrdinalIgnoreCase);

        if (!nvidiaPath)
            return false;

        return
            path.EndsWith(
                @"\DXCache",
                StringComparison.OrdinalIgnoreCase) ||
            path.Contains(
                @"\DXCache\",
                StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(
                @"\GLCache",
                StringComparison.OrdinalIgnoreCase) ||
            path.Contains(
                @"\GLCache\",
                StringComparison.OrdinalIgnoreCase);
    }


    private static bool IsModIoPath(
        string path)
    {
        return
            path.EndsWith(
                @"\mod.io",
                StringComparison.OrdinalIgnoreCase) ||
            path.Contains(
                @"\mod.io\",
                StringComparison.OrdinalIgnoreCase);
    }


    private static bool IsSteamGamePath(
        string path)
    {
        return
            path.Contains(
                @"\steamapps\common\",
                StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(
                @"\steamapps\common",
                StringComparison.OrdinalIgnoreCase);
    }


    private static bool TryGetAppDataApplicationName(
        string path,
        out string appName)
    {
        appName =
            string.Empty;

        string normalized =
            NormalizeComparablePath(
                path);

        string[] anchors =
        {
            @"\AppData\Local\",
            @"\AppData\LocalLow\",
            @"\AppData\Roaming\"
        };

        foreach (string anchor
                 in anchors)
        {
            int index =
                normalized.IndexOf(
                    anchor,
                    StringComparison.OrdinalIgnoreCase);

            if (index < 0)
                continue;

            string remainder =
                normalized.Substring(
                    index +
                    anchor.Length)
                    .Trim('\\');

            if (string.IsNullOrWhiteSpace(
                    remainder) ||
                remainder.Contains(
                    '\\'))
            {
                return false;
            }

            appName =
                remainder;

            return true;
        }

        return false;
    }


    private static bool IsAppDataPath(
        string path)
    {
        return
            path.EndsWith(
                @"\AppData",
                StringComparison.OrdinalIgnoreCase) ||
            path.Contains(
                @"\AppData\",
                StringComparison.OrdinalIgnoreCase);
    }


    private static bool IsUserDataPath(
        string path)
    {
        string[] segments =
        {
            "Users",
            "Documents",
            "Downloads",
            "Desktop",
            "Videos",
            "Pictures"
        };

        foreach (string segment
                 in segments)
        {
            if (path.EndsWith(
                    @"\" + segment,
                    StringComparison.OrdinalIgnoreCase) ||
                path.Contains(
                    @"\" + segment + @"\",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }


    private static string GetKnownFileDescription(
        string path)
    {
        string extension =
            Path.GetExtension(
                path)
                .ToLowerInvariant();

        return extension switch
        {
            ".iso" =>
                SettingsService.L(
                    "Это образ диска ISO. Такие файлы часто остаются после установки Windows, игр или программ и могут занимать много места.",
                    "This is an ISO disk image. These files often remain after installing Windows, games, or applications and can use a lot of space."),

            ".zip" or ".rar" or ".7z" =>
                SettingsService.L(
                    "Это архив. Если его содержимое уже распаковано и архив больше не нужен, он может быть кандидатом на удаление.",
                    "This is an archive. If its contents have already been extracted and you no longer need the archive, it may be a candidate for deletion."),

            ".vhd" or ".vhdx" =>
                SettingsService.L(
                    "Это файл виртуального диска. Он может содержать целую виртуальную систему или важные данные.",
                    "This is a virtual disk file. It can contain an entire virtual system or important data."),

            ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" =>
                SettingsService.L(
                    "Это видеофайл. Большие видео часто занимают значительную часть диска.",
                    "This is a video file. Large videos often use a significant amount of disk space."),

            ".exe" or ".msi" =>
                SettingsService.L(
                    "Это исполняемый или установочный файл. Если это старый установщик, он может быть больше не нужен, но сначала проверьте его назначение.",
                    "This is an executable or installer file. If it is an old installer, you may no longer need it, but check what it is first."),

            ".dmp" =>
                SettingsService.L(
                    "Это файл дампа памяти, созданный для диагностики сбоя. Такие файлы иногда бывают очень большими.",
                    "This is a memory dump created for crash diagnostics. These files can sometimes be very large."),

            ".log" =>
                SettingsService.L(
                    "Это журнал работы программы или системы. Большой журнал может указывать на длительное накопление диагностических записей.",
                    "This is an application or system log. A large log can indicate that diagnostic records have accumulated over time."),

            _ =>
                SettingsService.L(
                    "Это файл данных. Thermiqra пока не определяет его назначение достаточно надёжно только по имени и расширению.",
                    "This is a data file. Thermiqra cannot yet determine its purpose reliably enough from the name and extension alone.")
        };
    }


    private static string GetCompactParentPath(
        string path)
    {
        string? parent =
            Path.GetDirectoryName(
                path);

        return string.IsNullOrWhiteSpace(
                parent)
            ? path
            : parent;
    }


    private void PopulateLargestFiles(
        StorageAnalysisResult result)
    {
        LargestFilesPanel.Children.Clear();

        int count =
            Math.Min(
                3,
                result.LargestFiles.Count);

        if (count == 0)
        {
            TextBlock empty =
                new()
                {
                    Text =
                        SettingsService.L(
                            "Крупные файлы не найдены.",
                            "No large files found."),

                    FontSize =
                        10
                };

            empty.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            LargestFilesPanel.Children.Add(
                empty);

            return;
        }

        for (int i = 0;
             i < count;
             i++)
        {
            StorageAnalysisItem file =
                result.LargestFiles[i];

            Button button =
                new()
                {
                    Tag =
                        file,

                    HorizontalContentAlignment =
                        HorizontalAlignment.Stretch,

                    Margin =
                        new Thickness(
                            0, 0, 0,
                            i == count - 1
                                ? 0
                                : 2),

                    Padding =
                        new Thickness(
                            6, 5, 6, 5),

                    BorderThickness =
                        new Thickness(0),

                    Background =
                        Brushes.Transparent
                };

            Grid content =
                new();

            content.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(
                            1,
                            GridUnitType.Star)
                });

            content.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        GridLength.Auto
                });

            StackPanel text =
                new();

            TextBlock name =
                new()
                {
                    Text =
                        file.Name,

                    FontSize =
                        11,

                    FontWeight =
                        FontWeights.SemiBold,

                    TextTrimming =
                        TextTrimming.CharacterEllipsis
                };

            name.SetResourceReference(
                TextBlock.ForegroundProperty,
                "PrimaryTextBrush");

            TextBlock path =
                new()
                {
                    Text =
                        GetCompactParentPath(
                            file.Path),

                    FontSize =
                        9,

                    Margin =
                        new Thickness(
                            0, 2, 0, 0),

                    TextTrimming =
                        TextTrimming.CharacterEllipsis
                };

            path.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            text.Children.Add(
                name);

            text.Children.Add(
                path);

            TextBlock size =
                new()
                {
                    Text =
                        FormatBytes(
                            file.AllocatedBytes),

                    FontSize =
                        11,

                    FontWeight =
                        FontWeights.Bold,

                    VerticalAlignment =
                        VerticalAlignment.Center,

                    Margin =
                        new Thickness(
                            12, 0, 0, 0)
                };

            size.SetResourceReference(
                TextBlock.ForegroundProperty,
                "AccentBrush");

            Grid.SetColumn(
                text,
                0);

            Grid.SetColumn(
                size,
                1);

            content.Children.Add(
                text);

            content.Children.Add(
                size);

            button.Content =
                content;

            button.Click +=
                LargestFileButton_Click;

            LargestFilesPanel.Children.Add(
                button);
        }
    }


    private void LargestFileButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is Button button &&
            button.Tag is StorageAnalysisItem item)
        {
            ShowLegacyItemDetails(
                item);
        }
    }


    private async void DeleteButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_selectedEntry == null)
            return;

        SelectedStorageEntry entry =
            _selectedEntry;

        if (IsDownloadsRootPath(
                entry.Path))
        {
            ClearDownloadsButton_Click(
                sender,
                e);

            return;
        }

        if (entry.Kind ==
                StorageAnalysisTreeNodeKind.Drive ||
            IsDriveRootPath(
                entry.Path) ||
            IsRawRecycleBinPath(
                entry.Path))
        {
            return;
        }

        bool exists =
            entry.IsDirectory
                ? Directory.Exists(
                    entry.Path)
                : File.Exists(
                    entry.Path);

        if (!exists)
        {
            StatusText.Text =
                SettingsService.L(
                    "Файл или папка больше не существует. Анализ будет обновлён.",
                    "The file or folder no longer exists. The analysis will be refreshed.");

            await StartAnalysisAsync();

            return;
        }

        StorageGuidance guidance =
            BuildGuidance(
                entry);

        DeleteChoice choice =
            ShowDeleteChoiceDialog(
                entry,
                guidance);

        if (choice ==
            DeleteChoice.Cancel)
        {
            return;
        }

        if (choice ==
                DeleteChoice.Permanent &&
            guidance.Level ==
                StorageSafetyLevel.Protected)
        {
            bool confirmed =
                ShowConfirmationDialog(
                    SettingsService.L(
                        "Удалить несмотря на предупреждение?",
                        "Delete despite the warning?"),
                    SettingsService.L(
                        $"Thermiqra считает эту область системной или чувствительной.\n\n{entry.Path}\n\nУдаление может повредить Windows, программу или её данные. Продолжить безвозвратное удаление?",
                        $"Thermiqra considers this area system-related or sensitive.\n\n{entry.Path}\n\nDeleting it can damage Windows, an application, or its data. Continue with permanent deletion?"),
                    SettingsService.L(
                        "Удалить безвозвратно",
                        "Delete permanently"));

            if (!confirmed)
                return;
        }

        IntPtr ownerHandle =
            new WindowInteropHelper(
                this)
            .Handle;

        DeleteButton.IsEnabled =
            false;

        OpenButton.IsEnabled =
            false;

        ShowExplorerButton.IsEnabled =
            false;

        try
        {
            if (choice ==
                DeleteChoice.Recycle)
            {
                StatusText.Text =
                    SettingsService.L(
                        $"Перемещаем «{entry.Name}» в Корзину...",
                        $"Moving “{entry.Name}” to the Recycle Bin...");

                bool moved =
                    await Task.Run(
                        () =>
                            TryMovePathToRecycleBin(
                                entry.Path,
                                ownerHandle));

                if (moved)
                {
                    ShowInfoDialog(
                        SettingsService.L(
                            "Перемещено в Корзину",
                            "Moved to Recycle Bin"),
                        SettingsService.L(
                            $"«{entry.Name}» перемещено в Корзину. При необходимости файл или папку ещё можно восстановить. Место на диске окончательно освободится после очистки Корзины.",
                            $"“{entry.Name}” was moved to the Recycle Bin. It can still be restored if needed. Disk space will be fully released after the Recycle Bin is emptied."));
                }
                else
                {
                    bool deletePermanently =
                        ShowConfirmationDialog(
                            SettingsService.L(
                                "Не удалось переместить в Корзину",
                                "Could not move to Recycle Bin"),
                            SettingsService.L(
                                $"Windows не смогла переместить этот объект в Корзину:\n\n{entry.Path}\n\nВозможно, он слишком большой, занят программой или защищён. Попробовать удалить его безвозвратно?",
                                $"Windows could not move this item to the Recycle Bin:\n\n{entry.Path}\n\nIt may be too large, in use, or protected. Try to delete it permanently?"),
                            SettingsService.L(
                                "Удалить безвозвратно",
                                "Delete permanently"));

                    if (deletePermanently)
                    {
                        if (guidance.Level ==
                            StorageSafetyLevel.Protected)
                        {
                            bool protectedConfirmed =
                                ShowConfirmationDialog(
                                    SettingsService.L(
                                        "Последнее подтверждение",
                                        "Final confirmation"),
                                    SettingsService.L(
                                        $"Это системная или чувствительная область:\n\n{entry.Path}\n\nПосле безвозвратного удаления восстановить объект через Корзину будет нельзя.",
                                        $"This is a system-related or sensitive area:\n\n{entry.Path}\n\nAfter permanent deletion, the item cannot be restored through the Recycle Bin."),
                                    SettingsService.L(
                                        "Удалить",
                                        "Delete"));

                            if (!protectedConfirmed)
                            {
                                await StartAnalysisAsync();
                                return;
                            }
                        }

                        StatusText.Text =
                            SettingsService.L(
                                $"Безвозвратно удаляем «{entry.Name}»...",
                                $"Permanently deleting “{entry.Name}”...");

                        bool deleted =
                            await Task.Run(
                                () =>
                                    TryDeletePathPermanentlyWithShell(
                                        entry.Path,
                                        ownerHandle));

                        ShowGeneralDeleteResult(
                            entry,
                            deleted,
                            permanent: true);
                    }
                    else
                    {
                        ShowInfoDialog(
                            SettingsService.L(
                                "Удаление не выполнено",
                                "Deletion not completed"),
                            SettingsService.L(
                                "Объект не был удалён безвозвратно. Thermiqra обновит анализ, потому что Windows могла частично обработать содержимое папки.",
                                "The item was not permanently deleted. Thermiqra will refresh the analysis because Windows may have partially processed the folder contents."));
                    }
                }
            }
            else
            {
                StatusText.Text =
                    SettingsService.L(
                        $"Безвозвратно удаляем «{entry.Name}»...",
                        $"Permanently deleting “{entry.Name}”...");

                bool deleted =
                    await Task.Run(
                        () =>
                            TryDeletePathPermanentlyWithShell(
                                entry.Path,
                                ownerHandle));

                ShowGeneralDeleteResult(
                    entry,
                    deleted,
                    permanent: true);
            }
        }
        catch (Exception ex)
        {
            ShowInfoDialog(
                SettingsService.L(
                    "Не удалось удалить",
                    "Unable to delete"),
                SettingsService.L(
                    $"Windows не смогла выполнить удаление.\n\n{ex.Message}",
                    $"Windows could not complete the deletion.\n\n{ex.Message}"));
        }
        finally
        {
            await StartAnalysisAsync();
        }
    }


    private void OpenButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_selectedEntry == null)
            return;

        SelectedStorageEntry entry =
            _selectedEntry;

        if (entry.IsDirectory)
        {
            if (entry.NodeId.HasValue &&
                _treeItemsByNodeId.TryGetValue(
                    entry.NodeId.Value,
                    out TreeViewItem? item))
            {
                LoadTreeItemChildren(
                    item);

                item.IsExpanded =
                    true;

                StatusText.Text =
                    SettingsService.L(
                        "Папка раскрыта в дереве слева.",
                        "The folder was expanded in the tree on the left.");

                return;
            }

            StatusText.Text =
                SettingsService.L(
                    "Эта папка сейчас не представлена отдельной строкой дерева.",
                    "This folder is not currently represented as a separate tree row.");

            return;
        }

        try
        {
            if (!File.Exists(
                    entry.Path))
            {
                StatusText.Text =
                    SettingsService.L(
                        "Файл недоступен или больше не существует.",
                        "The file is unavailable or no longer exists.");

                return;
            }

            Process.Start(
                new ProcessStartInfo
                {
                    FileName =
                        entry.Path,

                    UseShellExecute =
                        true
                });
        }
        catch (Exception ex)
        {
            StatusText.Text =
                SettingsService.L(
                    $"Не удалось открыть файл: {ex.Message}",
                    $"Unable to open the file: {ex.Message}");
        }
    }


    private void ShowExplorerButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_selectedEntry == null)
            return;

        SelectedStorageEntry entry =
            _selectedEntry;

        try
        {
            if (entry.IsDirectory)
            {
                if (!Directory.Exists(
                        entry.Path))
                {
                    StatusText.Text =
                        SettingsService.L(
                            "Папка недоступна или больше не существует.",
                            "The folder is unavailable or no longer exists.");

                    return;
                }

                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName =
                            "explorer.exe",

                        Arguments =
                            $"\"{entry.Path}\"",

                        UseShellExecute =
                            true
                    });

                return;
            }

            if (!File.Exists(
                    entry.Path))
            {
                StatusText.Text =
                    SettingsService.L(
                        "Файл недоступен или больше не существует.",
                        "The file is unavailable or no longer exists.");

                return;
            }

            Process.Start(
                new ProcessStartInfo
                {
                    FileName =
                        "explorer.exe",

                    Arguments =
                        $"/select,\"{entry.Path}\"",

                    UseShellExecute =
                        true
                });
        }
        catch (Exception ex)
        {
            StatusText.Text =
                SettingsService.L(
                    $"Не удалось открыть Проводник: {ex.Message}",
                    $"Unable to open File Explorer: {ex.Message}");
        }
    }


    private void CopyPathButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_selectedEntry == null ||
            string.IsNullOrWhiteSpace(
                _selectedEntry.Path))
        {
            return;
        }

        try
        {
            Clipboard.SetText(
                _selectedEntry.Path);

            StatusText.Text =
                SettingsService.L(
                    "Путь скопирован.",
                    "Path copied.");
        }
        catch (Exception ex)
        {
            StatusText.Text =
                SettingsService.L(
                    $"Не удалось скопировать путь: {ex.Message}",
                    $"Unable to copy the path: {ex.Message}");
        }
    }


    private string GetDisplayPath(
        StorageAnalysisTreeNode node)
    {
        if (_tree == null)
            return _driveName;

        if (node.Kind ==
            StorageAnalysisTreeNodeKind.FileSystemData)
        {
            return SettingsService.L(
                $"{_driveName} · служебные данные файловой системы",
                $"{_driveName} · file-system service data");
        }

        if (node.Kind ==
            StorageAnalysisTreeNodeKind.UnresolvedData)
        {
            return SettingsService.L(
                $"{_driveName} · данные без полного пути",
                $"{_driveName} · data without a complete path");
        }

        return _tree.GetPath(
            node.Id);
    }


    private string GetNodeTooltip(
        StorageAnalysisTreeNode node)
    {
        return
            GetDisplayPath(
                node) +
            Environment.NewLine +
            FormatBytes(
                node.AllocatedBytes);
    }


    private static string GetNodeIcon(
        StorageAnalysisTreeNodeKind kind)
    {
        return kind switch
        {
            StorageAnalysisTreeNodeKind.Drive =>
                "▣",

            StorageAnalysisTreeNodeKind.Directory =>
                "▰",

            StorageAnalysisTreeNodeKind.File =>
                "▤",

            StorageAnalysisTreeNodeKind.FileSystemData =>
                "⚙",

            StorageAnalysisTreeNodeKind.UnresolvedData =>
                "?",

            _ =>
                "•"
        };
    }


    private string GetNodeDisplayName(
        StorageAnalysisTreeNode node)
    {
        if (node.Kind ==
            StorageAnalysisTreeNodeKind.Drive)
        {
            if (_currentResult == null)
                return _driveName;

            return SettingsService.L(
                $"Диск {_driveName.TrimEnd('\\')} — " +
                $"всего {FormatBytes(_currentResult.TotalBytes)}, " +
                $"свободно {FormatBytes(_currentResult.FreeBytes)}",
                $"Drive {_driveName.TrimEnd('\\')} — " +
                $"total {FormatBytes(_currentResult.TotalBytes)}, " +
                $"free {FormatBytes(_currentResult.FreeBytes)}");
        }

        if (node.Kind ==
            StorageAnalysisTreeNodeKind.FileSystemData)
        {
            return SettingsService.L(
                "Служебные данные файловой системы",
                "File-system service data");
        }

        if (node.Kind ==
            StorageAnalysisTreeNodeKind.UnresolvedData)
        {
            return SettingsService.L(
                "Данные без полного пути",
                "Data without a complete path");
        }

        return string.IsNullOrWhiteSpace(
                node.Name)
            ? SettingsService.L(
                "Без имени",
                "Unnamed")
            : node.Name;
    }


    private static string BuildPercentSuffix(
        long bytes,
        long denominator)
    {
        if (denominator <= 0)
            return "";

        double percent =
            Math.Clamp(
                bytes * 100d /
                denominator,
                0,
                100);

        return
            $" ({percent:0} %)";
    }


    private FolderContentStats GetFolderContentStats(
        long nodeId)
    {
        if (_tree == null)
            return new FolderContentStats(0, 0);

        long fileCount =
            0;

        long folderCount =
            0;

        Stack<long> pending =
            new();

        pending.Push(
            nodeId);

        while (pending.Count > 0)
        {
            long currentId =
                pending.Pop();

            foreach (StorageAnalysisTreeNode child
                     in _tree.GetChildren(
                         currentId))
            {
                if (child.Kind ==
                    StorageAnalysisTreeNodeKind.Directory)
                {
                    folderCount++;

                    pending.Push(
                        child.Id);
                }
                else if (child.Kind ==
                         StorageAnalysisTreeNodeKind.File)
                {
                    fileCount++;
                }
            }
        }

        return new FolderContentStats(
            fileCount,
            folderCount);
    }


    private bool IsDownloadsRootPath(
        string path)
    {
        if (string.IsNullOrWhiteSpace(
                path))
        {
            return false;
        }

        string downloadsPath =
            _downloadsPath ??
            GetDownloadsFolderPath();

        return
            !string.IsNullOrWhiteSpace(
                downloadsPath) &&
            string.Equals(
                NormalizeComparablePath(
                    path),
                NormalizeComparablePath(
                    downloadsPath),
                StringComparison.OrdinalIgnoreCase);
    }


    private static bool IsDriveRootPath(
        string path)
    {
        if (string.IsNullOrWhiteSpace(
                path))
        {
            return false;
        }

        try
        {
            string fullPath =
                Path.GetFullPath(
                    path);

            string? root =
                Path.GetPathRoot(
                    fullPath);

            return
                !string.IsNullOrWhiteSpace(
                    root) &&
                string.Equals(
                    fullPath.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar),
                    root.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }


    private static bool IsRawRecycleBinPath(
        string path)
    {
        string normalized =
            NormalizeComparablePath(
                path);

        return
            normalized.EndsWith(
                @"\$RECYCLE.BIN",
                StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(
                @"\$RECYCLE.BIN\",
                StringComparison.OrdinalIgnoreCase);
    }


    private string GetDownloadsFolderPath()
    {
        IntPtr pathPointer =
            IntPtr.Zero;

        try
        {
            Guid folderId =
                DownloadsKnownFolderId;

            int result =
                SHGetKnownFolderPath(
                    ref folderId,
                    0,
                    IntPtr.Zero,
                    out pathPointer);

            if (result == 0 &&
                pathPointer !=
                    IntPtr.Zero)
            {
                string? path =
                    Marshal.PtrToStringUni(
                        pathPointer);

                if (!string.IsNullOrWhiteSpace(
                        path))
                {
                    return path;
                }
            }
        }
        catch
        {
            // Ниже используется безопасный резервный путь.
        }
        finally
        {
            if (pathPointer !=
                IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(
                    pathPointer);
            }
        }

        string profile =
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);

        return Path.Combine(
            profile,
            "Downloads");
    }


    private bool ShouldShowGlobalRecycleCard()
    {
        string? preferredPath =
            _downloadsPath;

        if (string.IsNullOrWhiteSpace(
                preferredPath))
        {
            preferredPath =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile);
        }

        string? preferredDrive =
            Path.GetPathRoot(
                preferredPath);

        if (string.IsNullOrWhiteSpace(
                preferredDrive))
        {
            return false;
        }

        return string.Equals(
            NormalizeDisplayDriveName(
                preferredDrive),
            _driveName,
            StringComparison.OrdinalIgnoreCase);
    }


    private RecycleBinInfo QueryRecycleBinInfo()
    {
        ShQueryRbInfo info =
            new()
            {
                CbSize =
                    Marshal.SizeOf<ShQueryRbInfo>()
            };

        int result =
            SHQueryRecycleBin(
                null,
                ref info);

        if (result != 0)
            return new RecycleBinInfo(0, 0);

        return new RecycleBinInfo(
            Math.Max(
                0,
                info.SizeBytes),
            Math.Max(
                0,
                info.ItemCount));
    }


    private List<CleanupPlanItem>? ShowSafeCleanupPlanDialog(
        IReadOnlyList<CleanupPlanItem> plan)
    {
        Window dialog =
            CreateThemedDialogWindow(
                SettingsService.L(
                    "Thermiqra — Безопасная очистка",
                    "Thermiqra — Safe cleanup"),
                640,
                620);

        Grid root =
            new()
            {
                Margin =
                    new Thickness(
                        20)
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

        StackPanel header =
            new();

        TextBlock title =
            CreateDialogText(
                SettingsService.L(
                    "Что будет очищено",
                    "What will be cleaned"),
                18,
                FontWeights.Bold,
                "PrimaryTextBrush");

        TextBlock description =
            CreateDialogText(
                SettingsService.L(
                    "Thermiqra включает сюда только области с проверенным сценарием очистки. Будет удалено содержимое указанных папок; сами папки останутся. Эти временные данные удаляются напрямую, чтобы действительно освободить место. Занятые и защищённые файлы будут пропущены.",
                    "Thermiqra includes only areas with a verified cleanup procedure. The contents of the listed folders will be deleted; the folders themselves will remain. These temporary data are deleted directly so disk space is actually freed. Files in use or protected will be skipped."),
                11,
                FontWeights.Normal,
                "SecondaryTextBrush");

        description.Margin =
            new Thickness(
                0, 7, 0, 0);

        description.TextWrapping =
            TextWrapping.Wrap;

        header.Children.Add(
            title);

        header.Children.Add(
            description);

        root.Children.Add(
            header);

        ScrollViewer scroll =
            new()
            {
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Auto,

                Margin =
                    new Thickness(
                        0, 14, 0, 14)
            };

        StackPanel itemsPanel =
            new();

        List<(CheckBox CheckBox, CleanupPlanItem Item)> checkBoxes =
            new();

        TextBlock totalText =
            CreateDialogText(
                "",
                13,
                FontWeights.Bold,
                "AccentBrush");

        void UpdateTotal()
        {
            long selectedBytes =
                checkBoxes
                    .Where(
                        pair =>
                            pair.CheckBox.IsChecked ==
                            true)
                    .Sum(
                        pair =>
                            pair.Item.AllocatedBytes);

            totalText.Text =
                SettingsService.L(
                    $"Выбрано: примерно {FormatBytes(selectedBytes)}",
                    $"Selected: approximately {FormatBytes(selectedBytes)}");
        }

        foreach (CleanupPlanItem item
                 in plan)
        {
            Border card =
                new()
                {
                    BorderThickness =
                        new Thickness(1),

                    Padding =
                        new Thickness(
                            12),

                    Margin =
                        new Thickness(
                            0, 0, 0, 8)
                };

            card.SetResourceReference(
                Border.BackgroundProperty,
                "CardBackgroundBrush");

            card.SetResourceReference(
                Border.BorderBrushProperty,
                "BorderBrush");

            card.SetResourceReference(
                Border.CornerRadiusProperty,
                "MiniCardCornerRadius");

            Grid cardGrid =
                new();

            cardGrid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        GridLength.Auto
                });

            cardGrid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(
                            1,
                            GridUnitType.Star)
                });

            CheckBox checkBox =
                new()
                {
                    IsChecked =
                        true,

                    VerticalAlignment =
                        VerticalAlignment.Top,

                    Margin =
                        new Thickness(
                            0, 2, 10, 0),

                    Tag =
                        item
                };

            checkBox.SetResourceReference(
                Control.ForegroundProperty,
                "PrimaryTextBrush");

            StackPanel itemContent =
                new();

            TextBlock itemTitle =
                CreateDialogText(
                    $"{item.Title} — {FormatBytes(item.AllocatedBytes)}",
                    12,
                    FontWeights.Bold,
                    "PrimaryTextBrush");

            TextBlock itemDescription =
                CreateDialogText(
                    item.Description,
                    10,
                    FontWeights.Normal,
                    "SecondaryTextBrush");

            itemDescription.Margin =
                new Thickness(
                    0, 4, 0, 0);

            itemDescription.TextWrapping =
                TextWrapping.Wrap;

            TextBlock itemPath =
                CreateDialogText(
                    item.Path,
                    9,
                    FontWeights.Normal,
                    "SecondaryTextBrush");

            itemPath.Margin =
                new Thickness(
                    0, 4, 0, 0);

            itemPath.TextWrapping =
                TextWrapping.Wrap;

            itemContent.Children.Add(
                itemTitle);

            itemContent.Children.Add(
                itemDescription);

            itemContent.Children.Add(
                itemPath);

            Grid.SetColumn(
                itemContent,
                1);

            cardGrid.Children.Add(
                checkBox);

            cardGrid.Children.Add(
                itemContent);

            card.Child =
                cardGrid;

            itemsPanel.Children.Add(
                card);

            checkBoxes.Add(
                (checkBox, item));

            checkBox.Checked +=
                (_, _) =>
                    UpdateTotal();

            checkBox.Unchecked +=
                (_, _) =>
                    UpdateTotal();
        }

        scroll.Content =
            itemsPanel;

        Grid.SetRow(
            scroll,
            1);

        root.Children.Add(
            scroll);

        Grid footer =
            new();

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
                    GridLength.Auto
            });

        totalText.VerticalAlignment =
            VerticalAlignment.Center;

        footer.Children.Add(
            totalText);

        Button cancelButton =
            CreateDialogButton(
                SettingsService.L(
                    "Отмена",
                    "Cancel"));

        cancelButton.MinWidth =
            100;

        cancelButton.Margin =
            new Thickness(
                10, 0, 8, 0);

        cancelButton.Click +=
            (_, _) =>
            {
                dialog.DialogResult =
                    false;
            };

        Button cleanButton =
            CreateDialogButton(
                SettingsService.L(
                    "Очистить выбранное",
                    "Clean selected"));

        cleanButton.MinWidth =
            150;

        cleanButton.SetResourceReference(
            Control.BorderBrushProperty,
            "AccentBrush");

        cleanButton.SetResourceReference(
            Control.ForegroundProperty,
            "AccentBrush");

        cleanButton.Click +=
            (_, _) =>
            {
                if (!checkBoxes.Any(
                        pair =>
                            pair.CheckBox.IsChecked ==
                            true))
                {
                    return;
                }

                dialog.DialogResult =
                    true;
            };

        Grid.SetColumn(
            cancelButton,
            1);

        Grid.SetColumn(
            cleanButton,
            2);

        footer.Children.Add(
            cancelButton);

        footer.Children.Add(
            cleanButton);

        Grid.SetRow(
            footer,
            2);

        root.Children.Add(
            footer);

        dialog.Content =
            root;

        UpdateTotal();

        bool? result =
            dialog.ShowDialog();

        if (result !=
            true)
        {
            return null;
        }

        return checkBoxes
            .Where(
                pair =>
                    pair.CheckBox.IsChecked ==
                    true)
            .Select(
                pair =>
                    pair.Item)
            .ToList();
    }


    private CleanupExecutionResult ExecuteSafeCleanup(
        IReadOnlyList<CleanupPlanItem> items)
    {
        long freeBefore =
            GetAvailableFreeSpace();

        int deletedEntries =
            0;

        int skippedEntries =
            0;

        foreach (CleanupPlanItem item
                 in items)
        {
            if (!Directory.Exists(
                    item.Path))
            {
                continue;
            }

            foreach (string child
                     in EnumerateTopLevelEntriesSafe(
                         item.Path))
            {
                if (TryDeletePathPermanently(
                        child))
                {
                    deletedEntries++;
                }
                else
                {
                    skippedEntries++;
                }
            }
        }

        long freeAfter =
            GetAvailableFreeSpace();

        long freedBytes =
            Math.Max(
                0,
                freeAfter -
                freeBefore);

        return new CleanupExecutionResult(
            freedBytes,
            deletedEntries,
            skippedEntries);
    }


    private static string[] EnumerateTopLevelEntriesSafe(
        string directory)
    {
        try
        {
            return Directory
                .EnumerateFileSystemEntries(
                    directory)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }


    private DownloadsCleanupResult MoveDownloadsContentsToRecycleBin(
        string downloadsPath,
        IntPtr ownerHandle)
    {
        int movedCount =
            0;

        List<string> failed =
            new();

        string[] entries =
            Directory.Exists(
                downloadsPath)
                ? Directory
                    .EnumerateFileSystemEntries(
                        downloadsPath)
                    .ToArray()
                : Array.Empty<string>();

        foreach (string path
                 in entries)
        {
            if (TryMovePathToRecycleBin(
                    path,
                    ownerHandle))
            {
                movedCount++;
            }
            else
            {
                failed.Add(
                    path);
            }
        }

        return new DownloadsCleanupResult(
            movedCount,
            failed,
            0,
            0);
    }


    private static bool TryMovePathToRecycleBin(
        string path,
        IntPtr ownerHandle)
    {
        ShFileOpStruct operation =
            new()
            {
                WindowHandle =
                    ownerHandle,

                Function =
                    FoDelete,

                From =
                    path +
                    "\0\0",

                Flags =
                    (ushort)(
                        FofAllowUndo |
                        FofNoConfirmation |
                        FofSilent |
                        FofNoErrorUi)
            };

        int result =
            SHFileOperation(
                ref operation);

        return
            result == 0 &&
            !operation.AnyOperationsAborted;
    }


    private static bool TryDeletePathPermanentlyWithShell(
        string path,
        IntPtr ownerHandle)
    {
        ShFileOpStruct operation =
            new()
            {
                WindowHandle =
                    ownerHandle,

                Function =
                    FoDelete,

                From =
                    path +
                    "\0\0",

                Flags =
                    (ushort)(
                        FofNoConfirmation |
                        FofSilent |
                        FofNoErrorUi)
            };

        int result =
            SHFileOperation(
                ref operation);

        return
            result == 0 &&
            !operation.AnyOperationsAborted;
    }


    private static PermanentDeleteResult DeletePathsPermanently(
        IEnumerable<string> paths)
    {
        int deleted =
            0;

        int failed =
            0;

        foreach (string path
                 in paths)
        {
            if (TryDeletePathPermanently(
                    path))
            {
                deleted++;
            }
            else
            {
                failed++;
            }
        }

        return new PermanentDeleteResult(
            deleted,
            failed);
    }


    private static bool TryDeletePathPermanently(
        string path)
    {
        try
        {
            if (Directory.Exists(
                    path))
            {
                Directory.Delete(
                    path,
                    recursive: true);

                return true;
            }

            if (File.Exists(
                    path))
            {
                File.SetAttributes(
                    path,
                    FileAttributes.Normal);

                File.Delete(
                    path);

                return true;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }


    private long GetAvailableFreeSpace()
    {
        try
        {
            DriveInfo drive =
                new(
                    _driveName);

            return drive.AvailableFreeSpace;
        }
        catch
        {
            return 0;
        }
    }


    private DeleteChoice ShowDeleteChoiceDialog(
        SelectedStorageEntry entry,
        StorageGuidance guidance)
    {
        Window dialog =
            CreateThemedDialogWindow(
                SettingsService.L(
                    "Thermiqra — Удаление",
                    "Thermiqra — Delete"),
                650,
                guidance.Level ==
                    StorageSafetyLevel.Protected
                    ? 470
                    : 430);

        Grid root =
            new()
            {
                Margin =
                    new Thickness(
                        20)
            };

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

        StackPanel content =
            new();

        TextBlock title =
            CreateDialogText(
                SettingsService.L(
                    entry.IsDirectory
                        ? "Удалить выбранную папку?"
                        : "Удалить выбранный файл?",
                    entry.IsDirectory
                        ? "Delete the selected folder?"
                        : "Delete the selected file?"),
                18,
                FontWeights.Bold,
                "PrimaryTextBrush");

        TextBlock name =
            CreateDialogText(
                $"{entry.Name} — {FormatBytes(entry.AllocatedBytes)}",
                12,
                FontWeights.Bold,
                "AccentBrush");

        name.Margin =
            new Thickness(
                0, 10, 0, 0);

        TextBlock path =
            CreateDialogText(
                entry.Path,
                9.5,
                FontWeights.Normal,
                "SecondaryTextBrush");

        path.Margin =
            new Thickness(
                0, 4, 0, 0);

        path.TextWrapping =
            TextWrapping.Wrap;

        TextBlock safety =
            CreateDialogText(
                $"{guidance.SafetyTitle}\n{guidance.SafetyDescription}",
                10.5,
                FontWeights.Normal,
                guidance.Level ==
                    StorageSafetyLevel.Protected
                    ? "AccentBrush"
                    : "PrimaryTextBrush");

        safety.Margin =
            new Thickness(
                0, 14, 0, 0);

        safety.TextWrapping =
            TextWrapping.Wrap;

        TextBlock recycleText =
            CreateDialogText(
                SettingsService.L(
                    "В корзину — можно будет восстановить. Место окончательно освободится после очистки Корзины.",
                    "Recycle — the item can still be restored. Disk space is fully released after the Recycle Bin is emptied."),
                10.5,
                FontWeights.Normal,
                "PrimaryTextBrush");

        recycleText.Margin =
            new Thickness(
                0, 14, 0, 0);

        recycleText.TextWrapping =
            TextWrapping.Wrap;

        TextBlock permanentText =
            CreateDialogText(
                SettingsService.L(
                    "Безвозвратно — удаление сразу освобождает место, восстановить объект через Корзину будет нельзя.",
                    "Permanently — disk space is released immediately, and the item cannot be restored through the Recycle Bin."),
                10.5,
                FontWeights.Normal,
                "PrimaryTextBrush");

        permanentText.Margin =
            new Thickness(
                0, 8, 0, 0);

        permanentText.TextWrapping =
            TextWrapping.Wrap;

        content.Children.Add(
            title);

        content.Children.Add(
            name);

        content.Children.Add(
            path);

        content.Children.Add(
            safety);

        content.Children.Add(
            recycleText);

        content.Children.Add(
            permanentText);

        root.Children.Add(
            content);

        StackPanel buttons =
            new()
            {
                Orientation =
                    Orientation.Horizontal,

                HorizontalAlignment =
                    HorizontalAlignment.Right,

                Margin =
                    new Thickness(
                        0, 18, 0, 0)
            };

        DeleteChoice choice =
            DeleteChoice.Cancel;

        Button cancel =
            CreateDialogButton(
                SettingsService.L(
                    "Отмена",
                    "Cancel"));

        cancel.MinWidth =
            95;

        cancel.Click +=
            (_, _) =>
            {
                choice =
                    DeleteChoice.Cancel;

                dialog.DialogResult =
                    false;
            };

        Button recycle =
            CreateDialogButton(
                SettingsService.L(
                    "В корзину",
                    "Recycle"));

        recycle.MinWidth =
            110;

        recycle.Margin =
            new Thickness(
                8, 0, 0, 0);

        recycle.Click +=
            (_, _) =>
            {
                choice =
                    DeleteChoice.Recycle;

                dialog.DialogResult =
                    true;
            };

        Button permanent =
            CreateDialogButton(
                SettingsService.L(
                    "Безвозвратно",
                    "Permanently"));

        permanent.MinWidth =
            125;

        permanent.Margin =
            new Thickness(
                8, 0, 0, 0);

        permanent.SetResourceReference(
            Control.BorderBrushProperty,
            "AccentBrush");

        permanent.SetResourceReference(
            Control.ForegroundProperty,
            "AccentBrush");

        permanent.Click +=
            (_, _) =>
            {
                choice =
                    DeleteChoice.Permanent;

                dialog.DialogResult =
                    true;
            };

        buttons.Children.Add(
            cancel);

        buttons.Children.Add(
            recycle);

        buttons.Children.Add(
            permanent);

        Grid.SetRow(
            buttons,
            1);

        root.Children.Add(
            buttons);

        dialog.Content =
            root;

        dialog.ShowDialog();

        return choice;
    }


    private DownloadsClearChoice ShowDownloadsClearChoiceDialog(
        long bytes,
        FolderContentStats stats)
    {
        Window dialog =
            CreateThemedDialogWindow(
                SettingsService.L(
                    "Thermiqra — Очистить «Загрузки»",
                    "Thermiqra — Clean Downloads"),
                620,
                390);

        Grid root =
            new()
            {
                Margin =
                    new Thickness(
                        20)
            };

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

        StackPanel content =
            new();

        TextBlock title =
            CreateDialogText(
                SettingsService.L(
                    "Как удалить содержимое «Загрузок»?",
                    "How should Downloads contents be removed?"),
                18,
                FontWeights.Bold,
                "PrimaryTextBrush");

        TextBlock summary =
            CreateDialogText(
                SettingsService.L(
                    $"Сейчас здесь примерно {FormatBytes(bytes)}: {stats.FileCount:N0} файлов и {stats.FolderCount:N0} папок. Сама папка «Загрузки» останется.",
                    $"There is currently about {FormatBytes(bytes)} here: {stats.FileCount:N0} files and {stats.FolderCount:N0} folders. The Downloads folder itself will remain."),
                11,
                FontWeights.Normal,
                "SecondaryTextBrush");

        summary.Margin =
            new Thickness(
                0, 8, 0, 0);

        summary.TextWrapping =
            TextWrapping.Wrap;

        TextBlock recycleText =
            CreateDialogText(
                SettingsService.L(
                    "В корзину — безопаснее: файлы можно будет восстановить, но место на диске освободится только после очистки Корзины.",
                    "Recycle — safer: files can be restored, but disk space is freed only after the Recycle Bin is emptied."),
                10.5,
                FontWeights.Normal,
                "PrimaryTextBrush");

        recycleText.Margin =
            new Thickness(
                0, 16, 0, 0);

        recycleText.TextWrapping =
            TextWrapping.Wrap;

        TextBlock permanentText =
            CreateDialogText(
                SettingsService.L(
                    "Безвозвратно — место освобождается сразу, восстановить файлы через Корзину будет нельзя.",
                    "Permanently — space is freed immediately, and files cannot be restored from the Recycle Bin."),
                10.5,
                FontWeights.Normal,
                "PrimaryTextBrush");

        permanentText.Margin =
            new Thickness(
                0, 9, 0, 0);

        permanentText.TextWrapping =
            TextWrapping.Wrap;

        content.Children.Add(
            title);

        content.Children.Add(
            summary);

        content.Children.Add(
            recycleText);

        content.Children.Add(
            permanentText);

        root.Children.Add(
            content);

        StackPanel buttons =
            new()
            {
                Orientation =
                    Orientation.Horizontal,

                HorizontalAlignment =
                    HorizontalAlignment.Right,

                Margin =
                    new Thickness(
                        0, 18, 0, 0)
            };

        DownloadsClearChoice choice =
            DownloadsClearChoice.Cancel;

        Button cancel =
            CreateDialogButton(
                SettingsService.L(
                    "Отмена",
                    "Cancel"));

        cancel.MinWidth =
            95;

        cancel.Click +=
            (_, _) =>
            {
                choice =
                    DownloadsClearChoice.Cancel;

                dialog.DialogResult =
                    false;
            };

        Button recycle =
            CreateDialogButton(
                SettingsService.L(
                    "В корзину",
                    "Recycle"));

        recycle.MinWidth =
            105;

        recycle.Margin =
            new Thickness(
                8, 0, 0, 0);

        recycle.Click +=
            (_, _) =>
            {
                choice =
                    DownloadsClearChoice.Recycle;

                dialog.DialogResult =
                    true;
            };

        Button permanent =
            CreateDialogButton(
                SettingsService.L(
                    "Безвозвратно",
                    "Permanently"));

        permanent.MinWidth =
            120;

        permanent.Margin =
            new Thickness(
                8, 0, 0, 0);

        permanent.SetResourceReference(
            Control.BorderBrushProperty,
            "AccentBrush");

        permanent.SetResourceReference(
            Control.ForegroundProperty,
            "AccentBrush");

        permanent.Click +=
            (_, _) =>
            {
                choice =
                    DownloadsClearChoice.Permanent;

                dialog.DialogResult =
                    true;
            };

        buttons.Children.Add(
            cancel);

        buttons.Children.Add(
            recycle);

        buttons.Children.Add(
            permanent);

        Grid.SetRow(
            buttons,
            1);

        root.Children.Add(
            buttons);

        dialog.Content =
            root;

        dialog.ShowDialog();

        return choice;
    }


    private bool ShowConfirmationDialog(
        string title,
        string description,
        string confirmText)
    {
        Window dialog =
            CreateThemedDialogWindow(
                title,
                560,
                300);

        Grid root =
            new()
            {
                Margin =
                    new Thickness(
                        20)
            };

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

        TextBlock descriptionText =
            CreateDialogText(
                description,
                11,
                FontWeights.Normal,
                "PrimaryTextBrush");

        descriptionText.TextWrapping =
            TextWrapping.Wrap;

        root.Children.Add(
            descriptionText);

        StackPanel buttons =
            new()
            {
                Orientation =
                    Orientation.Horizontal,

                HorizontalAlignment =
                    HorizontalAlignment.Right,

                Margin =
                    new Thickness(
                        0, 16, 0, 0)
            };

        Button cancel =
            CreateDialogButton(
                SettingsService.L(
                    "Отмена",
                    "Cancel"));

        cancel.MinWidth =
            95;

        cancel.Click +=
            (_, _) =>
            {
                dialog.DialogResult =
                    false;
            };

        Button confirm =
            CreateDialogButton(
                confirmText);

        confirm.MinWidth =
            135;

        confirm.Margin =
            new Thickness(
                8, 0, 0, 0);

        confirm.SetResourceReference(
            Control.BorderBrushProperty,
            "AccentBrush");

        confirm.SetResourceReference(
            Control.ForegroundProperty,
            "AccentBrush");

        confirm.Click +=
            (_, _) =>
            {
                dialog.DialogResult =
                    true;
            };

        buttons.Children.Add(
            cancel);

        buttons.Children.Add(
            confirm);

        Grid.SetRow(
            buttons,
            1);

        root.Children.Add(
            buttons);

        dialog.Content =
            root;

        return dialog.ShowDialog() ==
               true;
    }


    private void ShowInfoDialog(
        string title,
        string description)
    {
        Window dialog =
            CreateThemedDialogWindow(
                title,
                540,
                280);

        Grid root =
            new()
            {
                Margin =
                    new Thickness(
                        20)
            };

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

        TextBlock text =
            CreateDialogText(
                description,
                11,
                FontWeights.Normal,
                "PrimaryTextBrush");

        text.TextWrapping =
            TextWrapping.Wrap;

        root.Children.Add(
            text);

        Button close =
            CreateDialogButton(
                SettingsService.L(
                    "Понятно",
                    "OK"));

        close.MinWidth =
            100;

        close.HorizontalAlignment =
            HorizontalAlignment.Right;

        close.Click +=
            (_, _) =>
            {
                dialog.DialogResult =
                    true;
            };

        Grid.SetRow(
            close,
            1);

        root.Children.Add(
            close);

        dialog.Content =
            root;

        dialog.ShowDialog();
    }


    private void ShowCleanupResultDialog(
        CleanupExecutionResult result)
    {
        string message =
            SettingsService.L(
                $"Очистка завершена. Освобождено примерно {FormatBytes(result.FreedBytes)}. Удалено объектов: {result.DeletedEntries:N0}. Пропущено занятых или защищённых объектов: {result.SkippedEntries:N0}.",
                $"Cleanup complete. Approximately {FormatBytes(result.FreedBytes)} was freed. Deleted items: {result.DeletedEntries:N0}. In-use or protected items skipped: {result.SkippedEntries:N0}.");

        ShowInfoDialog(
            SettingsService.L(
                "Безопасная очистка завершена",
                "Safe cleanup complete"),
            message);
    }


    private void ShowGeneralDeleteResult(
        SelectedStorageEntry entry,
        bool success,
        bool permanent)
    {
        if (success)
        {
            ShowInfoDialog(
                permanent
                    ? SettingsService.L(
                        "Удалено",
                        "Deleted")
                    : SettingsService.L(
                        "Перемещено в Корзину",
                        "Moved to Recycle Bin"),
                permanent
                    ? SettingsService.L(
                        $"«{entry.Name}» удалено безвозвратно. Thermiqra сейчас обновит анализ диска.",
                        $"“{entry.Name}” was permanently deleted. Thermiqra will now refresh the drive analysis.")
                    : SettingsService.L(
                        $"«{entry.Name}» перемещено в Корзину. Thermiqra сейчас обновит анализ диска.",
                        $"“{entry.Name}” was moved to the Recycle Bin. Thermiqra will now refresh the drive analysis."));
        }
        else
        {
            ShowInfoDialog(
                SettingsService.L(
                    "Удаление выполнено не полностью",
                    "Deletion was not fully completed"),
                SettingsService.L(
                    $"Windows не смогла полностью удалить:\n\n{entry.Path}\n\nПричиной могут быть занятые, защищённые или системные файлы. Thermiqra обновит анализ, чтобы показать фактический результат.",
                    $"Windows could not fully delete:\n\n{entry.Path}\n\nSome files may be in use, protected, or system-managed. Thermiqra will refresh the analysis to show the actual result."));
        }
    }


    private void ShowDownloadsCleanupResultDialog(
        DownloadsCleanupResult result,
        bool movedToRecycleBin)
    {
        string message;

        if (movedToRecycleBin)
        {
            int remainingFailures =
                Math.Max(
                    0,
                    result.FailedPaths.Count -
                    result.PermanentlyDeletedCount);

            if (result.PermanentDeleteFailures > 0)
            {
                remainingFailures =
                    result.PermanentDeleteFailures;
            }

            message =
                SettingsService.L(
                    $"В корзину перемещено: {result.ProcessedCount:N0}. Безвозвратно удалено после дополнительного подтверждения: {result.PermanentlyDeletedCount:N0}. Не удалось удалить: {remainingFailures:N0}. Чтобы реально освободить место, при необходимости очистите Корзину.",
                    $"Moved to Recycle Bin: {result.ProcessedCount:N0}. Permanently deleted after additional confirmation: {result.PermanentlyDeletedCount:N0}. Could not delete: {remainingFailures:N0}. To actually free disk space, empty the Recycle Bin if needed.");
        }
        else
        {
            message =
                SettingsService.L(
                    $"Безвозвратно удалено: {result.PermanentlyDeletedCount:N0}. Не удалось удалить: {result.PermanentDeleteFailures:N0}.",
                    $"Permanently deleted: {result.PermanentlyDeletedCount:N0}. Could not delete: {result.PermanentDeleteFailures:N0}.");
        }

        ShowInfoDialog(
            SettingsService.L(
                "Очистка «Загрузок» завершена",
                "Downloads cleanup complete"),
            message);
    }


    private Window CreateThemedDialogWindow(
        string title,
        double width,
        double height)
    {
        Window dialog =
            new()
            {
                Owner =
                    this,

                Title =
                    title,

                Width =
                    width,

                Height =
                    height,

                MinWidth =
                    Math.Min(
                        width,
                        480),

                MinHeight =
                    Math.Min(
                        height,
                        240),

                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner,

                ResizeMode =
                    ResizeMode.CanResize,

                ShowInTaskbar =
                    false
            };

        dialog.SetResourceReference(
            Window.BackgroundProperty,
            "WindowBackgroundBrush");

        dialog.SetResourceReference(
            Control.ForegroundProperty,
            "PrimaryTextBrush");

        if (Resources[typeof(Button)] is Style buttonStyle)
        {
            dialog.Resources[typeof(Button)] =
                buttonStyle;
        }

        if (Resources[typeof(ScrollBar)] is Style scrollBarStyle)
        {
            dialog.Resources[typeof(ScrollBar)] =
                scrollBarStyle;
        }

        return dialog;
    }


    private static TextBlock CreateDialogText(
        string text,
        double fontSize,
        FontWeight fontWeight,
        string foregroundResource)
    {
        TextBlock textBlock =
            new()
            {
                Text =
                    text,

                FontSize =
                    fontSize,

                FontWeight =
                    fontWeight
            };

        textBlock.SetResourceReference(
            TextBlock.ForegroundProperty,
            foregroundResource);

        return textBlock;
    }


    private Button CreateDialogButton(
        string content)
    {
        Button button =
            new()
            {
                Content =
                    content,

                Padding =
                    new Thickness(
                        14, 8, 14, 8),

                Cursor =
                    Cursors.Hand
            };

        button.SetResourceReference(
            Control.BackgroundProperty,
            "InputBackgroundBrush");

        button.SetResourceReference(
            Control.ForegroundProperty,
            "PrimaryTextBrush");

        button.SetResourceReference(
            Control.BorderBrushProperty,
            "BorderBrush");

        return button;
    }


    private void ApplyLanguage()
    {
        bool russian =
            SettingsService.IsRussian;

        Title =
            russian
                ? $"Thermiqra — Анализ диска {_driveName}"
                : $"Thermiqra — Drive analysis {_driveName}";

        string mainTitle =
            russian
                ? "КУДА УШЛО МЕСТО"
                : "WHERE THE SPACE WENT";

        CyberTitleText.Text =
            mainTitle;

        SteamTitleText.Text =
            mainTitle;

        FrostTitleText.Text =
            mainTitle;

        MilitaryTitleText.Text =
            mainTitle;

        CyberMetaText.Text =
            "THERMIQRA / STORAGE NAVIGATOR";

        CyberSubtitleText.Text =
            russian
                ? "Быстрый путь к самым тяжёлым папкам и файлам"
                : "A fast path to the folders and files using the most space";

        SteamMetaText.Text =
            russian
                ? "THERMIQRA / МЕХАНИЧЕСКИЙ НАВИГАТОР НАКОПИТЕЛЯ"
                : "THERMIQRA / MECHANICAL STORAGE NAVIGATOR";

        SteamSubtitleText.Text =
            russian
                ? "Thermiqra показывает путь от диска до самых тяжёлых данных"
                : "Thermiqra shows the path from the drive to the heaviest data";

        FrostMetaText.Text =
            "THERMIQRA / CRYO STORAGE NAVIGATOR";

        FrostSubtitleText.Text =
            russian
                ? "Крупные ветки накопителя видны сразу"
                : "The largest storage branches are visible immediately";

        MilitaryMetaText.Text =
            "THERMIQRA / TACTICAL STORAGE NAVIGATOR";

        MilitarySubtitleText.Text =
            russian
                ? "Тактический маршрут к самым тяжёлым данным"
                : "A tactical route to the heaviest data";

        CyberDriveCaptionText.Text =
            russian
                ? "ДИСК"
                : "DRIVE";

        SteamDriveCaptionText.Text =
            russian
                ? "НАКОПИТЕЛЬ"
                : "DRIVE";

        FrostDriveCaptionText.Text =
            russian
                ? "ДИСК"
                : "DRIVE";

        MilitaryDriveCaptionText.Text =
            russian
                ? "ДИСК"
                : "DRIVE";

        LoadingTitleText.Text =
            russian
                ? "Анализ диска..."
                : "Analyzing drive...";

        LoadingDescriptionText.Text =
            russian
                ? "Thermiqra читает структуру накопителя. Главное окно продолжает работать."
                : "Thermiqra is reading the storage structure. The main window remains responsive.";

        LargestBranchCaptionText.Text =
            russian
                ? "САМАЯ БОЛЬШАЯ ВЕТКА"
                : "LARGEST BRANCH";

        LargestFileCaptionText.Text =
            russian
                ? "КРУПНЕЙШИЙ ФАЙЛ"
                : "LARGEST FILE";

        LargestBranchCard.ToolTip =
            russian
                ? "Показать в дереве"
                : "Show in tree";

        LargestFileCard.ToolTip =
            russian
                ? "Показать файл в дереве"
                : "Show file in tree";

        SafeCandidatesCard.ToolTip =
            russian
                ? "Показать план безопасной очистки"
                : "Review the safe cleanup plan";

        PotentialCleanupCaptionText.Text =
            russian
                ? "БЕЗОПАСНАЯ ОЧИСТКА"
                : "SAFE CLEANUP";

        SafeModeTitleText.Text =
            russian
                ? "Можно безопасно проверить"
                : "Safe to review";

        BackToAllButton.Content =
            russian
                ? "← Все данные"
                : "← All data";

        DownloadsCaptionText.Text =
            russian
                ? "ЗАГРУЗКИ"
                : "DOWNLOADS";

        RecycleCaptionText.Text =
            russian
                ? "КОРЗИНА"
                : "RECYCLE BIN";

        ViewDownloadsButton.Content =
            russian
                ? "Посмотреть"
                : "View";

        ClearDownloadsButton.Content =
            russian
                ? "Очистить папку"
                : "Clean folder";

        ViewRecycleButton.Content =
            russian
                ? "Посмотреть"
                : "View";

        ClearRecycleButton.Content =
            russian
                ? "Очистить корзину"
                : "Empty Recycle Bin";

        NameColumnText.Text =
            russian
                ? "Имя"
                : "Name";

        SizeColumnText.Text =
            russian
                ? "Размер"
                : "Size";

        ShareColumnText.Text =
            russian
                ? "Доля"
                : "Share";

        SelectedTitleText.Text =
            russian
                ? "Выберите папку или файл"
                : "Select a folder or file";

        SelectedDescriptionText.Text =
            russian
                ? "Нажмите на строку слева, чтобы Thermiqra объяснила, что это и что с этим можно делать."
                : "Select a row on the left and Thermiqra will explain what it is and what you can do with it.";

        SafetyTitleText.Text =
            russian
                ? "Безопасность"
                : "Safety";

        DeleteButton.Content =
            russian
                ? "Удалить"
                : "Delete";

        OpenButton.Content =
            russian
                ? "Показать содержимое"
                : "Show contents";

        ShowExplorerButton.Content =
            russian
                ? "Показать в Проводнике"
                : "Show in File Explorer";

        CopyPathButton.Content =
            russian
                ? "Скопировать путь"
                : "Copy path";

        DeleteStageHintText.Text =
            russian
                ? "Выбранные файлы и папки можно отправить в Корзину или удалить безвозвратно. Перед удалением Thermiqra покажет точный путь и предупреждение."
                : "Selected files and folders can be sent to the Recycle Bin or deleted permanently. Thermiqra shows the exact path and warning before deletion.";

        DeleteButton.ToolTip =
            russian
                ? "Удалить выбранный файл или папку."
                : "Delete the selected file or folder.";

        LargestFilesTitleText.Text =
            russian
                ? "Крупнейшие файлы"
                : "Largest files";

        ErrorTitleText.Text =
            russian
                ? "Не удалось выполнить анализ"
                : "Unable to analyze the drive";

        CancelButton.Content =
            russian
                ? "Отмена"
                : "Cancel";

        CloseButton.Content =
            russian
                ? "Закрыть"
                : "Close";

        StatusText.Text =
            russian
                ? "Подготовка анализа..."
                : "Preparing analysis...";
    }


    private void ApplyDriveName()
    {
        CyberDriveText.Text =
            _driveName;

        SteamDriveText.Text =
            _driveName;

        FrostDriveText.Text =
            _driveName;

        MilitaryDriveText.Text =
            _driveName;
    }


    private static T? FindVisualChild<T>(
        DependencyObject parent)
        where T : DependencyObject
    {
        int childCount =
            VisualTreeHelper.GetChildrenCount(
                parent);

        for (int index = 0;
             index < childCount;
             index++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(
                    parent,
                    index);

            if (child is T match)
                return match;

            T? nested =
                FindVisualChild<T>(
                    child);

            if (nested != null)
                return nested;
        }

        return null;
    }


    private static string NormalizeDisplayDriveName(
        string driveName)
    {
        if (string.IsNullOrWhiteSpace(
                driveName))
        {
            return "";
        }

        string trimmed =
            driveName.Trim();

        if (trimmed.Length == 1 &&
            char.IsLetter(
                trimmed[0]))
        {
            return
                $"{char.ToUpperInvariant(trimmed[0])}:\\";
        }

        if (trimmed.Length >= 2 &&
            char.IsLetter(
                trimmed[0]) &&
            trimmed[1] == ':')
        {
            return
                $"{char.ToUpperInvariant(trimmed[0])}:\\";
        }

        return trimmed;
    }


    private static string NormalizeComparablePath(
        string path)
    {
        return path
            .Replace(
                '/',
                '\\')
            .TrimEnd('\\');
    }


    private static long SafeAdd(
        long left,
        long right)
    {
        if (right <= 0)
            return left;

        if (left >
            long.MaxValue -
            right)
        {
            return long.MaxValue;
        }

        return left +
               right;
    }


    private static string FormatBytes(
        long bytes)
    {
        if (bytes < 0)
            bytes = 0;

        const double kb =
            1024d;

        const double mb =
            kb * 1024d;

        const double gb =
            mb * 1024d;

        const double tb =
            gb * 1024d;

        if (bytes >= tb)
        {
            return SettingsService.L(
                $"{bytes / tb:0.00} ТБ",
                $"{bytes / tb:0.00} TB");
        }

        if (bytes >= gb)
        {
            return SettingsService.L(
                $"{bytes / gb:0.00} ГБ",
                $"{bytes / gb:0.00} GB");
        }

        if (bytes >= mb)
        {
            return SettingsService.L(
                $"{bytes / mb:0.0} МБ",
                $"{bytes / mb:0.0} MB");
        }

        if (bytes >= kb)
        {
            return SettingsService.L(
                $"{bytes / kb:0.0} КБ",
                $"{bytes / kb:0.0} KB");
        }

        return SettingsService.L(
            $"{bytes} Б",
            $"{bytes} B");
    }


    private void CancelButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        CancelButton.IsEnabled =
            false;

        StatusText.Text =
            SettingsService.L(
                "Отмена анализа...",
                "Cancelling analysis...");

        _analysisCancellation?.Cancel();
    }


    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }


    private void StorageAnalyzerWindow_Closed(
        object? sender,
        EventArgs e)
    {
        _analysisCancellation?.Cancel();
        _analysisCancellation?.Dispose();
        _analysisCancellation = null;
    }


    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct ShFileOpStruct
    {
        public IntPtr WindowHandle;
        public uint Function;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string From;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? To;

        public ushort Flags;

        [MarshalAs(UnmanagedType.Bool)]
        public bool AnyOperationsAborted;

        public IntPtr NameMappings;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? ProgressTitle;
    }


    [StructLayout(LayoutKind.Sequential)]
    private struct ShQueryRbInfo
    {
        public int CbSize;
        public long SizeBytes;
        public long ItemCount;
    }


    [DllImport(
        "shell32.dll",
        CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(
        ref Guid folderId,
        uint flags,
        IntPtr token,
        out IntPtr path);


    [DllImport(
        "shell32.dll",
        CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(
        string? rootPath,
        ref ShQueryRbInfo info);


    [DllImport(
        "shell32.dll",
        CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(
        IntPtr windowHandle,
        string? rootPath,
        uint flags);


    [DllImport(
        "shell32.dll",
        CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(
        ref ShFileOpStruct operation);


    private enum DeleteChoice
    {
        Cancel,
        Recycle,
        Permanent
    }


    private enum DownloadsClearChoice
    {
        Cancel,
        Recycle,
        Permanent
    }


    private sealed record CleanupPlanItem(
        string Title,
        string Path,
        long AllocatedBytes,
        string Description);


    private sealed record CleanupExecutionResult(
        long FreedBytes,
        int DeletedEntries,
        int SkippedEntries);


    private sealed record FolderContentStats(
        long FileCount,
        long FolderCount);


    private sealed record RecycleBinInfo(
        long SizeBytes,
        long ItemCount);


    private sealed record DownloadsCleanupResult(
        int ProcessedCount,
        IReadOnlyList<string> FailedPaths,
        int PermanentlyDeletedCount,
        int PermanentDeleteFailures);


    private sealed record PermanentDeleteResult(
        int DeletedCount,
        int FailedCount);


    private enum StorageSafetyLevel
    {
        Safe,
        Review,
        Protected
    }


    private sealed record StorageGuidance(
        StorageSafetyLevel Level,
        string Description,
        string SafetyTitle,
        string SafetyDescription);


    private sealed record SelectedStorageEntry(
        long? NodeId,
        string Path,
        string Name,
        long AllocatedBytes,
        bool IsDirectory,
        StorageAnalysisTreeNodeKind Kind);
}
