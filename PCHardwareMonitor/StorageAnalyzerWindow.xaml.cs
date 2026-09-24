using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PCHardwareMonitor;

public partial class StorageAnalyzerWindow : Window
{
    private readonly StorageAnalyzerService _storageAnalyzerService =
        new();

    private readonly string _driveName;

    private CancellationTokenSource? _analysisCancellation;

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

        SummaryGrid.Visibility =
            Visibility.Collapsed;

        ResultsPanel.Visibility =
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

        SummaryGrid.Visibility =
            Visibility.Collapsed;

        ResultsPanel.Visibility =
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

        SummaryGrid.Visibility =
            Visibility.Collapsed;

        ResultsPanel.Visibility =
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
        LoadingCard.Visibility =
            Visibility.Collapsed;

        ErrorCard.Visibility =
            Visibility.Collapsed;

        SummaryGrid.Visibility =
            Visibility.Visible;

        ResultsPanel.Visibility =
            Visibility.Visible;

        CancelButton.Visibility =
            Visibility.Collapsed;

        TotalValueText.Text =
            FormatBytes(
                result.TotalBytes);

        UsedValueText.Text =
            FormatBytes(
                result.WindowsUsedBytes);

        FreeValueText.Text =
            FormatBytes(
                result.FreeBytes);

        ScanTimeValueText.Text =
            SettingsService.L(
                $"{result.Elapsed.TotalSeconds:0.0} с",
                $"{result.Elapsed.TotalSeconds:0.0} s");

        PopulateItems(
            RootFoldersPanel,
            result.RootFolders,
            8);

        PopulateRecommendations(
            result);

        PopulateItems(
            LargestFoldersPanel,
            result.LargestFolders,
            12);

        PopulateItems(
            LargestFilesPanel,
            result.LargestFiles,
            12);

        string unresolvedText =
            result.UnresolvedNonEmptyFiles > 0
                ? SettingsService.L(
                    $"   •   не привязано: {result.UnresolvedNonEmptyFiles:N0}",
                    $"   •   unresolved: {result.UnresolvedNonEmptyFiles:N0}")
                : "";

        StatusText.Text =
            SettingsService.L(
                $"Готово: {result.FileCount:N0} файлов, " +
                $"{result.DirectoryCount:N0} папок" +
                $"{unresolvedText}",
                $"Done: {result.FileCount:N0} files, " +
                $"{result.DirectoryCount:N0} folders" +
                $"{unresolvedText}");
    }


    private void PopulateRecommendations(
        StorageAnalysisResult result)
    {
        RecommendationsPanel.Children.Clear();

        List<StorageRecommendation> recommendations =
            BuildRecommendations(
                result);

        if (recommendations.Count == 0)
        {
            RecommendationsPanel.Children.Add(
                CreateRecommendationEmptyMessage());

            return;
        }

        foreach (StorageRecommendation recommendation
                 in recommendations)
        {
            RecommendationsPanel.Children.Add(
                CreateRecommendationCard(
                    recommendation));
        }
    }


    private static List<StorageRecommendation>
        BuildRecommendations(
            StorageAnalysisResult result)
    {
        List<StorageRecommendation> recommendations =
            new();

        List<StorageAnalysisItem> folders =
            new();

        HashSet<string> seenPaths =
            new(
                StringComparer.OrdinalIgnoreCase);

        foreach (StorageAnalysisItem item
                 in result.RootFolders)
        {
            if (seenPaths.Add(
                    NormalizeComparablePath(
                        item.Path)))
            {
                folders.Add(
                    item);
            }
        }

        foreach (StorageAnalysisItem item
                 in result.LargestFolders)
        {
            if (seenPaths.Add(
                    NormalizeComparablePath(
                        item.Path)))
            {
                folders.Add(
                    item);
            }
        }

        StorageAnalysisItem? recycleBin =
            FindFolderByEnding(
                folders,
                @"\$RECYCLE.BIN");

        if (recycleBin != null &&
            recycleBin.AllocatedBytes >=
                256L * 1024 * 1024)
        {
            recommendations.Add(
                new StorageRecommendation(
                    RecommendationLevel.Safe,
                    "Корзина занимает заметное место",
                    "Recycle Bin is using noticeable space",
                    "Удалённые файлы этого диска всё ещё занимают место. " +
                    "Если они больше не нужны, корзину можно очистить " +
                    "штатными средствами Windows.",
                    "Deleted files from this drive are still using space. " +
                    "If they are no longer needed, you can empty the " +
                    "Recycle Bin using Windows.",
                    recycleBin.Path,
                    recycleBin.AllocatedBytes,
                    RecommendationAction.OpenRecycleBin));
        }

        StorageAnalysisItem? downloads =
            FindFolderBySegment(
                folders,
                "Downloads");

        if (downloads != null &&
            downloads.AllocatedBytes >=
                1024L * 1024 * 1024)
        {
            recommendations.Add(
                new StorageRecommendation(
                    RecommendationLevel.Review,
                    "Проверьте папку загрузок",
                    "Review the Downloads folder",
                    "Это пользовательские файлы. " +
                    "Откройте папку и удаляйте только то, " +
                    "что вам действительно больше не нужно.",
                    "These are user files. Open the folder and remove " +
                    "only files you are sure you no longer need.",
                    downloads.Path,
                    downloads.AllocatedBytes,
                    RecommendationAction.OpenFolder));
        }

        StorageAnalysisItem? temp =
            FindKnownTempFolder(
                folders);

        if (temp != null &&
            temp.AllocatedBytes >=
                512L * 1024 * 1024)
        {
            recommendations.Add(
                new StorageRecommendation(
                    RecommendationLevel.Safe,
                    "Временные файлы занимают место",
                    "Temporary files are using space",
                    "Предпочтительно очищать временные файлы через " +
                    "«Память» / «Временные файлы» Windows или через " +
                    "само приложение, которое их создало.",
                    "Prefer cleaning temporary files through Windows " +
                    "Storage / Temporary files or through the app " +
                    "that created them.",
                    temp.Path,
                    temp.AllocatedBytes,
                    RecommendationAction.OpenFolder));
        }

        StorageAnalysisItem? users =
            FindRootFolder(
                folders,
                result.DriveName,
                "Users");

        if (users != null &&
            users.AllocatedBytes >=
                2L * 1024 * 1024 * 1024)
        {
            recommendations.Add(
                new StorageRecommendation(
                    RecommendationLevel.Review,
                    "Проверьте пользовательские файлы",
                    "Review user files",
                    "Профили пользователей могут содержать документы, " +
                    "загрузки, видео, рабочий стол и данные приложений. " +
                    "Проверяйте конкретные личные папки, но не удаляйте " +
                    "профиль пользователя целиком.",
                    "User profiles can contain documents, downloads, videos, " +
                    "desktop files, and application data. Review specific " +
                    "personal folders, but do not delete an entire user profile.",
                    users.Path,
                    users.AllocatedBytes,
                    RecommendationAction.OpenFolder));
        }

        StorageAnalysisItem? appData =
            FindFolderBySegment(
                folders,
                "AppData");

        if (appData != null &&
            appData.AllocatedBytes >=
                1024L * 1024 * 1024)
        {
            recommendations.Add(
                new StorageRecommendation(
                    RecommendationLevel.Review,
                    "AppData занимает заметное место",
                    "AppData is using noticeable space",
                    "Здесь могут находиться кэши, профили, настройки, " +
                    "сохранения и базы данных приложений. " +
                    "Не очищайте AppData целиком. Лучше определить " +
                    "конкретное приложение и использовать его настройки очистки.",
                    "This area can contain caches, profiles, settings, " +
                    "saves, and application databases. Do not clean " +
                    "AppData as a whole. Identify the specific app and " +
                    "use its own cleanup options when possible.",
                    appData.Path,
                    appData.AllocatedBytes,
                    RecommendationAction.OpenFolder));
        }

        StorageAnalysisItem? windows =
            FindRootFolder(
                folders,
                result.DriveName,
                "Windows");

        if (windows != null &&
            windows.AllocatedBytes >=
                1024L * 1024 * 1024)
        {
            recommendations.Add(
                new StorageRecommendation(
                    RecommendationLevel.Protected,
                    "Системные файлы Windows",
                    "Windows system files",
                    "Не удаляйте содержимое папки Windows вручную. " +
                    "Для освобождения системного места используйте " +
                    "штатную очистку Windows.",
                    "Do not delete files from the Windows folder manually. " +
                    "Use Windows built-in storage cleanup for system files.",
                    windows.Path,
                    windows.AllocatedBytes,
                    RecommendationAction.None));
        }

        StorageAnalysisItem? programFiles =
            FindRootFolder(
                folders,
                result.DriveName,
                "Program Files");

        StorageAnalysisItem? programFilesX86 =
            FindRootFolder(
                folders,
                result.DriveName,
                "Program Files (x86)");

        long programBytes =
            (programFiles?.AllocatedBytes ?? 0) +
            (programFilesX86?.AllocatedBytes ?? 0);

        if (programBytes >=
            2L * 1024 * 1024 * 1024)
        {
            string? programPath =
                programFiles?.Path ??
                programFilesX86?.Path;

            recommendations.Add(
                new StorageRecommendation(
                    RecommendationLevel.Protected,
                    "Установленные программы занимают много места",
                    "Installed applications are using significant space",
                    "Не удаляйте файлы программ вручную. " +
                    "Если нужно освободить место, удаляйте ненужные " +
                    "программы через Windows или их штатный деинсталлятор.",
                    "Do not delete application files manually. " +
                    "To free space, uninstall software through Windows " +
                    "or the application's own uninstaller.",
                    programPath,
                    programBytes,
                    RecommendationAction.None));
        }

        StorageAnalysisItem? programData =
            FindRootFolder(
                folders,
                result.DriveName,
                "ProgramData");

        if (programData != null &&
            programData.AllocatedBytes >=
                1024L * 1024 * 1024)
        {
            recommendations.Add(
                new StorageRecommendation(
                    RecommendationLevel.Review,
                    "ProgramData требует осторожности",
                    "ProgramData should be handled carefully",
                    "В этой папке находятся данные разных приложений. " +
                    "Не удаляйте неизвестные папки только из-за их размера. " +
                    "Сначала определите программу-владельца.",
                    "This folder contains data from different applications. " +
                    "Do not delete unknown folders just because they are large. " +
                    "Identify the owning application first.",
                    programData.Path,
                    programData.AllocatedBytes,
                    RecommendationAction.OpenFolder));
        }

        StorageAnalysisItem? systemVolumeInformation =
            FindFolderByEnding(
                folders,
                @"\System Volume Information");

        if (systemVolumeInformation != null &&
            systemVolumeInformation.AllocatedBytes >=
                512L * 1024 * 1024)
        {
            recommendations.Add(
                new StorageRecommendation(
                    RecommendationLevel.Protected,
                    "Системная область Windows",
                    "Windows-managed system area",
                    "Этой областью управляет Windows. " +
                    "Не рекомендуется удалять её содержимое вручную.",
                    "This area is managed by Windows. " +
                    "Do not delete its contents manually.",
                    systemVolumeInformation.Path,
                    systemVolumeInformation.AllocatedBytes,
                    RecommendationAction.None));
        }

        if (recommendations.Count < 4)
        {
            StorageAnalysisItem? unknownLargeFolder =
                FindLargeUnclassifiedFolder(
                    folders,
                    result.DriveName);

            if (unknownLargeFolder != null)
            {
                recommendations.Add(
                    new StorageRecommendation(
                        RecommendationLevel.Review,
                        $"Крупная папка: {unknownLargeFolder.Name}",
                        $"Large folder: {unknownLargeFolder.Name}",
                        "Thermiqra не определяет назначение этой папки " +
                        "достаточно надёжно. Откройте её и проверьте " +
                        "содержимое перед любыми действиями.",
                        "Thermiqra cannot determine the purpose of this " +
                        "folder reliably enough. Open it and review its " +
                        "contents before taking any action.",
                        unknownLargeFolder.Path,
                        unknownLargeFolder.AllocatedBytes,
                        RecommendationAction.OpenFolder));
            }
        }

        if (recommendations.Count > 6)
        {
            recommendations.RemoveRange(
                6,
                recommendations.Count - 6);
        }

        return recommendations;
    }


    private Border CreateRecommendationCard(
        StorageRecommendation recommendation)
    {
        Border card =
            new()
            {
                BorderThickness =
                    new Thickness(1),

                Padding =
                    new Thickness(
                        16, 13, 12, 13),

                Margin =
                    new Thickness(
                        0, 0, 0, 8)
            };

        card.SetResourceReference(
            Border.BackgroundProperty,
            "CardBackgroundBrush");

        card.SetResourceReference(
            Border.BorderBrushProperty,
            recommendation.Level ==
            RecommendationLevel.Protected
                ? "BorderBrush"
                : "AccentBrush");

        card.SetResourceReference(
            Border.CornerRadiusProperty,
            "PanelCornerRadius");

        Grid grid =
            new();

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        StackPanel content =
            new();

        TextBlock levelText =
            new()
            {
                Text =
                    GetRecommendationLevelText(
                        recommendation.Level),

                FontSize =
                    9,

                FontWeight =
                    FontWeights.Bold,

                Margin =
                    new Thickness(
                        0, 0, 0, 5)
            };

        levelText.SetResourceReference(
            TextBlock.ForegroundProperty,
            recommendation.Level ==
            RecommendationLevel.Protected
                ? "SecondaryTextBrush"
                : "AccentBrush");

        TextBlock titleText =
            new()
            {
                Text =
                    SettingsService.IsRussian
                        ? recommendation.TitleRu
                        : recommendation.TitleEn,

                FontSize =
                    14,

                FontWeight =
                    FontWeights.Bold,

                TextWrapping =
                    TextWrapping.Wrap
            };

        titleText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        TextBlock descriptionText =
            new()
            {
                Text =
                    SettingsService.IsRussian
                        ? recommendation.DescriptionRu
                        : recommendation.DescriptionEn,

                FontSize =
                    11,

                Margin =
                    new Thickness(
                        0, 5, 0, 0),

                TextWrapping =
                    TextWrapping.Wrap,

                MaxWidth =
                    760
            };

        descriptionText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        content.Children.Add(
            levelText);

        content.Children.Add(
            titleText);

        content.Children.Add(
            descriptionText);

        if (!string.IsNullOrWhiteSpace(
                recommendation.Path))
        {
            string details =
                recommendation.AllocatedBytes.HasValue
                    ? $"{recommendation.Path}   •   " +
                      $"{FormatBytes(recommendation.AllocatedBytes.Value)}"
                    : recommendation.Path;

            TextBlock pathText =
                new()
                {
                    Text =
                        details,

                    FontSize =
                        10,

                    Margin =
                        new Thickness(
                            0, 6, 0, 0),

                    TextTrimming =
                        TextTrimming.CharacterEllipsis
                };

            pathText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            content.Children.Add(
                pathText);
        }

        Grid.SetColumn(
            content,
            0);

        grid.Children.Add(
            content);

        if (recommendation.Action !=
                RecommendationAction.None)
        {
            Button openButton =
                new()
                {
                    Content =
                        recommendation.Action ==
                        RecommendationAction.OpenRecycleBin
                            ? SettingsService.L(
                                "Открыть корзину",
                                "Open Recycle Bin")
                            : SettingsService.L(
                                "Открыть папку",
                                "Open folder"),

                    Tag =
                        recommendation,

                    MinWidth =
                        120,

                    Margin =
                        new Thickness(
                            16, 0, 0, 0),

                    VerticalAlignment =
                        VerticalAlignment.Center
                };

            openButton.Click +=
                OpenRecommendationActionButton_Click;

            Grid.SetColumn(
                openButton,
                1);

            grid.Children.Add(
                openButton);
        }

        card.Child =
            grid;

        return card;
    }


    private Border CreateRecommendationEmptyMessage()
    {
        Border card =
            new()
            {
                BorderThickness =
                    new Thickness(1),

                Padding =
                    new Thickness(16),

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
            "PanelCornerRadius");

        TextBlock text =
            new()
            {
                Text =
                    SettingsService.L(
                        "Явных безопасных рекомендаций не найдено. " +
                        "Крупные папки можно открыть ниже и проверить вручную.",
                        "No clear safe recommendations were found. " +
                        "You can open the large folders below and review them manually."),

                FontSize =
                    12,

                TextWrapping =
                    TextWrapping.Wrap
            };

        text.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        card.Child =
            text;

        return card;
    }


    private void OpenRecommendationActionButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not StorageRecommendation recommendation)
        {
            return;
        }

        try
        {
            if (recommendation.Action ==
                RecommendationAction.OpenRecycleBin)
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

                return;
            }

            string? path =
                recommendation.Path;

            if (recommendation.Action !=
                    RecommendationAction.OpenFolder ||
                string.IsNullOrWhiteSpace(
                    path))
            {
                return;
            }

            if (!Directory.Exists(path))
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
                        $"\"{path}\"",

                    UseShellExecute =
                        true
                });
        }
        catch (Exception ex)
        {
            StatusText.Text =
                SettingsService.L(
                    $"Не удалось открыть: {ex.Message}",
                    $"Unable to open: {ex.Message}");
        }
    }


    private static StorageAnalysisItem? FindRootFolder(
        IReadOnlyList<StorageAnalysisItem> folders,
        string driveName,
        string folderName)
    {
        string expected =
            NormalizeComparablePath(
                Path.Combine(
                    driveName,
                    folderName));

        foreach (StorageAnalysisItem item
                 in folders)
        {
            if (string.Equals(
                    NormalizeComparablePath(
                        item.Path),
                    expected,
                    StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }


    private static StorageAnalysisItem? FindFolderByEnding(
        IReadOnlyList<StorageAnalysisItem> folders,
        string ending)
    {
        string normalizedEnding =
            ending.Replace(
                '/',
                '\\');

        foreach (StorageAnalysisItem item
                 in folders)
        {
            string path =
                NormalizeComparablePath(
                    item.Path);

            if (path.EndsWith(
                    normalizedEnding,
                    StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }


    private static StorageAnalysisItem? FindFolderBySegment(
        IReadOnlyList<StorageAnalysisItem> folders,
        string segment)
    {
        string token =
            $@"\{segment}\";

        StorageAnalysisItem? best =
            null;

        foreach (StorageAnalysisItem item
                 in folders)
        {
            string path =
                NormalizeComparablePath(
                    item.Path) + "\\";

            if (!path.Contains(
                    token,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (best == null ||
                item.AllocatedBytes >
                    best.AllocatedBytes)
            {
                best =
                    item;
            }
        }

        return best;
    }


    private static StorageAnalysisItem? FindKnownTempFolder(
        IReadOnlyList<StorageAnalysisItem> folders)
    {
        StorageAnalysisItem? best =
            null;

        foreach (StorageAnalysisItem item
                 in folders)
        {
            string path =
                NormalizeComparablePath(
                    item.Path);

            bool isKnownTemp =
                path.EndsWith(
                    @"\AppData\Local\Temp",
                    StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(
                    @"\Windows\Temp",
                    StringComparison.OrdinalIgnoreCase);

            if (!isKnownTemp)
                continue;

            if (best == null ||
                item.AllocatedBytes >
                    best.AllocatedBytes)
            {
                best =
                    item;
            }
        }

        return best;
    }


    private static StorageAnalysisItem?
        FindLargeUnclassifiedFolder(
            IReadOnlyList<StorageAnalysisItem> folders,
            string driveName)
    {
        foreach (StorageAnalysisItem item
                 in folders)
        {
            if (item.AllocatedBytes <
                2L * 1024 * 1024 * 1024)
            {
                continue;
            }

            string path =
                NormalizeComparablePath(
                    item.Path);

            if (IsKnownProtectedOrClassifiedPath(
                    path,
                    driveName))
            {
                continue;
            }

            return item;
        }

        return null;
    }


    private static bool IsKnownProtectedOrClassifiedPath(
        string path,
        string driveName)
    {
        string[] knownNames =
        {
            "Windows",
            "Program Files",
            "Program Files (x86)",
            "ProgramData",
            "Users",
            "$RECYCLE.BIN",
            "System Volume Information"
        };

        foreach (string name in knownNames)
        {
            string rootPath =
                NormalizeComparablePath(
                    Path.Combine(
                        driveName,
                        name));

            if (string.Equals(
                    path,
                    rootPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (path.Contains(
                @"\AppData\",
                StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(
                @"\AppData",
                StringComparison.OrdinalIgnoreCase) ||
            path.Contains(
                @"\Downloads\",
                StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(
                @"\Downloads",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
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


    private static string GetRecommendationLevelText(
        RecommendationLevel level)
    {
        return level switch
        {
            RecommendationLevel.Safe =>
                SettingsService.L(
                    "МОЖНО БЕЗОПАСНО ПРОВЕРИТЬ",
                    "SAFE TO REVIEW"),

            RecommendationLevel.Review =>
                SettingsService.L(
                    "ПРОВЕРИТЬ ПЕРЕД ОЧИСТКОЙ",
                    "REVIEW BEFORE CLEANING"),

            RecommendationLevel.Protected =>
                SettingsService.L(
                    "НЕ УДАЛЯТЬ ВРУЧНУЮ",
                    "DO NOT DELETE MANUALLY"),

            _ =>
                SettingsService.L(
                    "РЕКОМЕНДАЦИЯ",
                    "RECOMMENDATION")
        };
    }


    private void PopulateItems(
        StackPanel panel,
        System.Collections.Generic.IReadOnlyList<StorageAnalysisItem> items,
        int maxItems)
    {
        panel.Children.Clear();

        int count =
            Math.Min(
                maxItems,
                items.Count);

        if (count == 0)
        {
            panel.Children.Add(
                CreateEmptyMessage());

            return;
        }

        for (int i = 0; i < count; i++)
        {
            panel.Children.Add(
                CreateStorageItemCard(
                    items[i]));
        }
    }


    private Border CreateStorageItemCard(
        StorageAnalysisItem item)
    {
        Border card =
            new()
            {
                BorderThickness =
                    new Thickness(1),

                Padding =
                    new Thickness(
                        16, 12, 12, 12),

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
            "PanelCornerRadius");

        Grid grid =
            new();

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        StackPanel textPanel =
            new()
            {
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        TextBlock nameText =
            new()
            {
                Text =
                    item.Name,

                FontSize =
                    13,

                FontWeight =
                    FontWeights.SemiBold,

                TextTrimming =
                    TextTrimming.CharacterEllipsis
            };

        nameText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        TextBlock pathText =
            new()
            {
                Text =
                    item.Path,

                FontSize =
                    10,

                Margin =
                    new Thickness(
                        0, 3, 0, 0),

                TextTrimming =
                    TextTrimming.CharacterEllipsis
            };

        pathText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        textPanel.Children.Add(
            nameText);

        textPanel.Children.Add(
            pathText);

        TextBlock sizeText =
            new()
            {
                Text =
                    FormatBytes(
                        item.AllocatedBytes),

                FontSize =
                    14,

                FontWeight =
                    FontWeights.Bold,

                VerticalAlignment =
                    VerticalAlignment.Center,

                Margin =
                    new Thickness(
                        16, 0, 16, 0)
            };

        sizeText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        Button openButton =
            new()
            {
                Content =
                    SettingsService.L(
                        "Открыть папку",
                        "Open folder"),

                Tag =
                    item,

                MinWidth =
                    120,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        openButton.Click +=
            OpenFolderButton_Click;

        Grid.SetColumn(
            textPanel,
            0);

        Grid.SetColumn(
            sizeText,
            1);

        Grid.SetColumn(
            openButton,
            2);

        grid.Children.Add(
            textPanel);

        grid.Children.Add(
            sizeText);

        grid.Children.Add(
            openButton);

        card.Child =
            grid;

        return card;
    }


    private Border CreateEmptyMessage()
    {
        Border card =
            new()
            {
                BorderThickness =
                    new Thickness(1),

                Padding =
                    new Thickness(16),

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
            "PanelCornerRadius");

        TextBlock text =
            new()
            {
                Text =
                    SettingsService.L(
                        "Нет данных.",
                        "No data."),

                FontSize =
                    12
            };

        text.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        card.Child =
            text;

        return card;
    }


    private void OpenFolderButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not StorageAnalysisItem item)
        {
            return;
        }

        string targetPath =
            GetFolderPath(
                item);

        try
        {
            if (!Directory.Exists(targetPath))
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
                        $"\"{targetPath}\"",

                    UseShellExecute =
                        true
                });
        }
        catch (Exception ex)
        {
            StatusText.Text =
                SettingsService.L(
                    $"Не удалось открыть папку: {ex.Message}",
                    $"Unable to open folder: {ex.Message}");
        }
    }


    private static string GetFolderPath(
        StorageAnalysisItem item)
    {
        if (item.IsDirectory)
            return item.Path;

        string? directory =
            Path.GetDirectoryName(
                item.Path);

        return string.IsNullOrWhiteSpace(
            directory)
            ? item.Path
            : directory;
    }


    private void ApplyLanguage()
    {
        bool russian =
            SettingsService.IsRussian;

        Title =
            russian
                ? $"Thermiqra — Анализ диска {_driveName}"
                : $"Thermiqra — Drive analysis {_driveName}";

        CyberTitleText.Text =
            russian
                ? "АНАЛИЗ ДИСКА"
                : "DRIVE ANALYSIS";

        CyberSubtitleText.Text =
            russian
                ? "Быстрый анализ занятого места"
                : "Fast used-space analysis";

        SteamTitleText.Text =
            CyberTitleText.Text;

        SteamSubtitleText.Text =
            russian
                ? "Ведомость распределения занятого места"
                : "Used-space distribution report";

        FrostTitleText.Text =
            CyberTitleText.Text;

        FrostSubtitleText.Text =
            russian
                ? "Криогенная карта занятого пространства"
                : "Cryogenic used-space map";

        MilitaryTitleText.Text =
            CyberTitleText.Text;

        MilitarySubtitleText.Text =
            russian
                ? "Тактическая сводка распределения данных"
                : "Tactical storage distribution summary";

        SteamMetaText.Text =
            russian
                ? "THERMIQRA / МЕХАНИЧЕСКИЙ АНАЛИЗ НАКОПИТЕЛЯ"
                : "THERMIQRA / MECHANICAL STORAGE ANALYSIS";

        CyberMetaText.Text =
            "THERMIQRA / STORAGE DIAGNOSTICS";

        FrostMetaText.Text =
            "THERMIQRA / CRYO STORAGE ANALYSIS";

        MilitaryMetaText.Text =
            "THERMIQRA / TACTICAL STORAGE INTEL";

        CyberDriveCaptionText.Text =
            russian
                ? "ЦЕЛЕВОЙ ДИСК"
                : "TARGET DRIVE";

        SteamDriveCaptionText.Text =
            russian
                ? "НАКОПИТЕЛЬ"
                : "DRIVE";

        FrostDriveCaptionText.Text =
            "STORAGE NODE";

        MilitaryDriveCaptionText.Text =
            "TARGET";

        LoadingTitleText.Text =
            russian
                ? "Анализ диска..."
                : "Analyzing drive...";

        LoadingDescriptionText.Text =
            russian
                ? "Thermiqra читает структуру NTFS. Главное окно продолжает работать."
                : "Thermiqra is reading the NTFS structure. The main window remains responsive.";

        TotalCaptionText.Text =
            russian
                ? "ВСЕГО"
                : "TOTAL";

        UsedCaptionText.Text =
            russian
                ? "ЗАНЯТО"
                : "USED";

        FreeCaptionText.Text =
            russian
                ? "СВОБОДНО"
                : "FREE";

        ScanTimeCaptionText.Text =
            russian
                ? "АНАЛИЗ"
                : "SCAN";

        RootFoldersTitleText.Text =
            russian
                ? "Где занято место"
                : "Where space is used";

        RecommendationsTitleText.Text =
            russian
                ? "Рекомендации"
                : "Recommendations";

        LargestFoldersTitleText.Text =
            russian
                ? "Крупнейшие папки"
                : "Largest folders";

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


    private enum RecommendationLevel
    {
        Safe,
        Review,
        Protected
    }


    private enum RecommendationAction
    {
        None,
        OpenFolder,
        OpenRecycleBin
    }


    private sealed record StorageRecommendation(
        RecommendationLevel Level,
        string TitleRu,
        string TitleEn,
        string DescriptionRu,
        string DescriptionEn,
        string? Path,
        long? AllocatedBytes,
        RecommendationAction Action);
}
