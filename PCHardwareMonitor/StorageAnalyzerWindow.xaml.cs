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

        // WPF normally focuses a clicked TreeViewItem and scrolls expanded
        // folders to the top. Handle direct header clicks without that jump.
        UsageTree.PreviewMouseLeftButtonDown +=
            UsageTree_PreviewMouseLeftButtonDown;

        SourceInitialized +=
            StorageAnalyzerWindow_SourceInitialized;

        Loaded +=
            StorageAnalyzerWindow_Loaded;

        Closed +=
            StorageAnalyzerWindow_Closed;
    }


    private void StorageAnalyzerWindow_SourceInitialized(
        object? sender,
        EventArgs e)
    {
        // Open at the size of the main Thermiqra window, not the full desktop.
        if (Owner == null)
            return;

        double ownerWidth =
            Owner.ActualWidth > 0
                ? Owner.ActualWidth
                : Owner.Width;

        double ownerHeight =
            Owner.ActualHeight > 0
                ? Owner.ActualHeight
                : Owner.Height;

        if (double.IsNaN(ownerWidth) ||
            double.IsInfinity(ownerWidth) ||
            double.IsNaN(ownerHeight) ||
            double.IsInfinity(ownerHeight))
        {
            return;
        }

        WindowState =
            WindowState.Normal;

        WindowStartupLocation =
            WindowStartupLocation.Manual;

        // Match the visible main window exactly, without maximizing
        // the analyzer to the entire monitor or shrinking its height.
        Width = Math.Max(MinWidth, ownerWidth);
        Height = Math.Max(MinHeight, ownerHeight);
        Left = Owner.Left;
        Top = Owner.Top;
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

        CancellationTokenSource currentAnalysis =
            _analysisCancellation;

        CancellationToken cancellationToken =
            currentAnalysis.Token;

        // Progress<T> marshals callbacks to the WPF UI thread.
        IProgress<StorageAnalysisProgress> progress =
            new Progress<StorageAnalysisProgress>(update =>
            {
                if (ReferenceEquals(_analysisCancellation, currentAnalysis) &&
                    !currentAnalysis.IsCancellationRequested &&
                    LoadingCard.Visibility == Visibility.Visible)
                {
                    UpdateAnalysisProgress(update);
                }
            });

        SetLoadingState();

        try
        {
            StorageAnalysisResult result =
                await _storageAnalyzerService.AnalyzeAsync(
                    _driveName,
                    cancellationToken,
                    progress);

            cancellationToken.ThrowIfCancellationRequested();

            if (!ReferenceEquals(_analysisCancellation, currentAnalysis))
                return;

            PopulateResult(
                result);
        }
        catch (OperationCanceledException)
        {
            if (!IsVisible ||
                !ReferenceEquals(_analysisCancellation, currentAnalysis))
                return;

            SetCancelledState();
        }
        catch (NotSupportedException ex)
        {
            if (!IsVisible ||
                !ReferenceEquals(_analysisCancellation, currentAnalysis))
                return;

            SetErrorState(
                SettingsService.L(
                    "Для этого диска быстрый анализ пока недоступен.",
                    "Fast analysis is not available for this drive yet."),
                ex.Message);
        }
        catch (Exception ex)
        {
            if (!IsVisible ||
                !ReferenceEquals(_analysisCancellation, currentAnalysis))
                return;

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

        LoadingProgressBar.Value = 0;
        LoadingProgressBar.IsIndeterminate = true;
        LoadingPercentText.Visibility = Visibility.Collapsed;
        LoadingStageText.Text = SettingsService.L(
            "Подготовка чтения данных...",
            "Preparing to read data...");
        LoadingCountsText.Text = SettingsService.L(
            "Найдено: 0 файлов, 0 папок",
            "Found: 0 files, 0 folders");

        StatusText.Text =
            SettingsService.L(
                $"Анализ {_driveName} выполняется в фоне...",
                $"Analyzing {_driveName} in the background...");
    }


    private void UpdateAnalysisProgress(StorageAnalysisProgress progress)
    {
        bool hasMeasuredProgress =
            progress.Stage == StorageAnalysisStage.NtfsRecords &&
            progress.TotalUnits is > 0;

        LoadingProgressBar.IsIndeterminate = !hasMeasuredProgress;

        if (hasMeasuredProgress)
        {
            double percent = Math.Clamp(
                progress.ProcessedUnits * 100.0 /
                    progress.TotalUnits!.Value,
                0, 100);

            LoadingProgressBar.Value = percent;
            LoadingPercentText.Visibility = Visibility.Visible;
            LoadingPercentText.Text = $"{percent:0} %";

            LoadingStageText.Text = SettingsService.L(
                $"Чтение NTFS: {progress.ProcessedUnits:N0} из {progress.TotalUnits.Value:N0} записей",
                $"Reading NTFS: {progress.ProcessedUnits:N0} of {progress.TotalUnits.Value:N0} records");
        }
        else
        {
            LoadingPercentText.Visibility = Visibility.Collapsed;

            LoadingStageText.Text = progress.Stage switch
            {
                StorageAnalysisStage.Traversal => SettingsService.L(
                    "Обход файлов и папок...",
                    "Scanning files and folders..."),
                StorageAnalysisStage.ProcessingEntries => SettingsService.L(
                    "Подсчёт размеров папок...",
                    "Calculating folder sizes..."),
                StorageAnalysisStage.BuildingTree => SettingsService.L(
                    "Подготовка дерева данных...",
                    "Building the file tree..."),
                _ => SettingsService.L(
                    "Чтение структуры накопителя...",
                    "Reading storage structure...")
            };
        }

        LoadingCountsText.Text = SettingsService.L(
            $"Найдено: {progress.FilesFound:N0} файлов, {progress.FoldersFound:N0} папок",
            $"Found: {progress.FilesFound:N0} files, {progress.FoldersFound:N0} folders");
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

        _ = PopulateIntelligenceAsync(
            _tree,
            result.WindowsUsedBytes);

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

        LoadingArtworkEyebrowText.Text =
            russian ? "СТРУКТУРА ДИСКА" : "DRIVE STRUCTURE";

        LoadingArtworkTitleText.Text =
            russian
                ? "Скоро покажем, куда ушло место"
                : "You'll soon see where your space went";

        LoadingArtworkDescriptionText.Text =
            russian
                ? "После анализа вы увидите самые большие папки и файлы и сможете перейти прямо к ним."
                : "When the scan finishes, you can see the largest folders and files and jump straight to them.";

        LoadingArtworkSafetyText.Text =
            russian
                ? "Thermiqra только читает сведения о файлах. Ничего не удаляется и не меняется."
                : "Thermiqra is only reading file information. Nothing is deleted or changed.";

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

        // Cancelling ends this one-shot analysis session: there is no retry
        // action on the cancelled screen, so return to the main window.
        Close();
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
