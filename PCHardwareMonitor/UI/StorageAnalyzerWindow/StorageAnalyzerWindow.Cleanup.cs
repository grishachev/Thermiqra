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

        // Browser cache is opt-in through the SAME Safe Cleanup dialog.
        // Never add a profile root, site storage, cookies or bookmarks.
        AddBrowserCacheCandidates(result, seen, localAppData);

        result.Sort(
            (left, right) =>
                right.AllocatedBytes.CompareTo(
                    left.AllocatedBytes));

        // Preserve the full-width Safe Cleanup card.
        // The lower sections stay unchanged.
        ApplyCleanupSummaryLayout();

        return result;
    }


    private bool _cleanupSummaryLayoutApplied;

    private void ApplyCleanupSummaryLayout()
    {
        if (_cleanupSummaryLayoutApplied)
            return;

        // The main layout is defined by the existing XAML. Do not create a
        // second cleanup entry point, or move the Downloads / Recycle cards.
        if (SafeCandidatesCard.Parent is not Grid summaryGrid)
        {
            return;
        }
        SafeCandidatesCard.Margin = new Thickness(0);
        SafeCandidatesCard.MinHeight = 104;
        SafeCandidatesCard.Cursor = Cursors.Hand;
        Grid.SetColumn(SafeCandidatesCard, 0);
        Grid.SetColumnSpan(SafeCandidatesCard, 2);

        // Intentional right-side call-to-action to balance the wide card.
        // It is display-only: clicking anywhere still uses the existing
        // SafeCandidatesCard_MouseLeftButtonUp handler.
        if (SafeCandidatesCard.Child is Grid content)
        {
            content.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto
            });

            StackPanel details = new()
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(24, 0, 6, 0)
            };

            TextBlock title = new()
            {
                Text = SettingsService.L(
                    "Открыть план очистки  ›",
                    "Open cleanup plan  ›"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Right
            };
            title.SetResourceReference(
                TextBlock.ForegroundProperty, "AccentBrush");

            TextBlock hint = new()
            {
                Text = SettingsService.L(
                    "Вы выбираете, что удалять",
                    "You choose what to remove"),
                FontSize = 10.5,
                Margin = new Thickness(0, 3, 0, 0),
                TextAlignment = TextAlignment.Right
            };
            hint.SetResourceReference(
                TextBlock.ForegroundProperty, "SecondaryTextBrush");

            details.Children.Add(title);
            details.Children.Add(hint);

            Grid.SetColumn(details, 2);
            content.Children.Add(details);

            // Avoid clipping on smaller windows. No resize animations.
            void UpdateDetailsVisibility()
            {
                details.Visibility = summaryGrid.ActualWidth < 680
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            summaryGrid.SizeChanged += (_, _) => UpdateDetailsVisibility();
            UpdateDetailsVisibility();
        }

        _cleanupSummaryLayoutApplied = true;
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


    // BROWSER CACHE: deliberately limited to Cache and Firefox cache2.
    // The directories are discovered by exact vendor/profile locations;
    // arbitrary cache-looking folders are never eligible for cleanup.
    private void AddBrowserCacheCandidates(
        List<CleanupPlanItem> result,
        HashSet<string> seen,
        string localAppData)
    {
        AddChromiumCacheCandidates(result, seen, localAppData,
            Path.Combine("Google", "Chrome", "User Data"),
            "Google Chrome");
        AddChromiumCacheCandidates(result, seen, localAppData,
            Path.Combine("Yandex", "YandexBrowser", "User Data"),
            SettingsService.L("Яндекс Браузер", "Yandex Browser"));
        AddChromiumCacheCandidates(result, seen, localAppData,
            Path.Combine("Microsoft", "Edge", "User Data"),
            "Microsoft Edge");

        AddFirefoxCacheCandidates(result, seen,
            Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Mozilla", "Firefox", "Profiles"));

        // Firefox from Microsoft Store (MSIX) has a separate sandboxed
        // LocalCache\Local vs LocalCache\Roaming profile pair.
        string packages = Path.Combine(localAppData, "Packages");
        foreach (string package in SafeEnumerateDirectories(packages, "Mozilla.Firefox_*"))
        {
            if (!Path.GetFileName(package).StartsWith("Mozilla.Firefox_",
                    StringComparison.OrdinalIgnoreCase))
                continue;
            AddFirefoxCacheCandidates(result, seen,
                Path.Combine(package, "LocalCache", "Local", "Mozilla", "Firefox", "Profiles"),
                Path.Combine(package, "LocalCache", "Roaming", "Mozilla", "Firefox", "Profiles"));
        }
    }

    private void AddChromiumCacheCandidates(
        List<CleanupPlanItem> result,
        HashSet<string> seen,
        string localAppData,
        string vendorSubPath,
        string browserTitle)
    {
        string root = Path.Combine(localAppData, vendorSubPath);
        foreach (string profile in SafeEnumerateDirectories(root))
        {
            string profileName = Path.GetFileName(profile);
            if (!IsStandardChromiumProfileName(profileName))
                continue;

            AddVerifiedBrowserCacheCandidate(result, seen,
                Path.Combine(profile, "Cache"),
                $"{browserTitle} — {profileName}");
        }
    }

    private void AddFirefoxCacheCandidates(
        List<CleanupPlanItem> result,
        HashSet<string> seen,
        string localProfiles,
        string roamingProfiles)
    {
        foreach (string profile in SafeEnumerateDirectories(localProfiles))
        {
            // A matching regular/MSIX Roaming profile provides evidence this
            // is a real Firefox profile, not an arbitrary folder called cache2.
            if (!Directory.Exists(Path.Combine(roamingProfiles, Path.GetFileName(profile))))
                continue;

            AddVerifiedBrowserCacheCandidate(result, seen,
                Path.Combine(profile, "cache2"),
                $"Mozilla Firefox — {Path.GetFileName(profile)}");
        }
    }

    private void AddVerifiedBrowserCacheCandidate(
        List<CleanupPlanItem> result,
        HashSet<string> seen,
        string cachePath,
        string title)
    {
        if (!IsVerifiedBrowserCacheRoot(cachePath))
            return;

        string? processName = GetBrowserProcessForCacheRoot(cachePath);
        bool running = processName != null && IsBrowserProcessRunning(processName);
        string description = SettingsService.L(
            "Только дисковый кэш страниц. Закладки, пароли, история, вход на сайты и сохранённые данные сайтов не затрагиваются. Если браузер открыт, Thermiqra предложит закрыть его с вашего согласия. После очистки сайты могут открываться медленнее при первом посещении.",
            "Disk page cache only. Bookmarks, passwords, history, sign-ins and saved site data are untouched. If the browser is running, Thermiqra will ask for permission to close it. Websites may load more slowly on the first visit afterward.");
        if (running)
            description += SettingsService.L(
                " Браузер запущен. Перед очисткой Thermiqra предложит закрыть его автоматически.",
                " The browser is running. Thermiqra will offer to close it automatically before cleaning.");

        AddCleanupPlanItem(result, seen, cachePath, title, description);
    }

    private static string[] SafeEnumerateDirectories(string root, string pattern = "*")
    {
        try
        {
            return Directory.Exists(root)
                ? Directory.GetDirectories(root, pattern, SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static bool IsStandardChromiumProfileName(string name)
    {
        if (string.Equals(name, "Default", StringComparison.OrdinalIgnoreCase))
            return true;

        return name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(name.AsSpan("Profile ".Length), out int number) &&
            number > 0;
    }

    // Recognize the exact structure (independent of whether its files still
    // exist) so a previously selected browser cache cannot become a general
    // unrestricted cleanup target if the profile is changed mid-operation.
    private static string? GetBrowserProcessForCacheRoot(string path)
    {
        try
        {
            string full = Path.GetFullPath(path).TrimEnd('\\', '/');
            string parent = Path.GetDirectoryName(full) ?? string.Empty;
            string name = Path.GetFileName(full);
            string local = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

            if (string.Equals(name, "Cache", StringComparison.OrdinalIgnoreCase) &&
                IsStandardChromiumProfileName(Path.GetFileName(parent)))
            {
                string userData = Path.GetDirectoryName(parent) ?? string.Empty;
                if (SameBrowserPath(userData,
                        Path.Combine(local, "Google", "Chrome", "User Data")))
                    return "chrome";
                if (SameBrowserPath(userData,
                        Path.Combine(local, "Yandex", "YandexBrowser", "User Data")))
                    return "browser";
                if (SameBrowserPath(userData,
                        Path.Combine(local, "Microsoft", "Edge", "User Data")))
                    return "msedge";
            }

            if (!string.Equals(name, "cache2", StringComparison.OrdinalIgnoreCase))
                return null;

            string profileRoot = Path.GetDirectoryName(parent) ?? string.Empty;
            if (SameBrowserPath(profileRoot,
                    Path.Combine(local, "Mozilla", "Firefox", "Profiles")))
                return "firefox";

            string packagesRoot = Path.Combine(local, "Packages");
            string relative = Path.GetRelativePath(packagesRoot, profileRoot);
            string[] segments = relative.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 6 &&
                segments[0].StartsWith("Mozilla.Firefox_", StringComparison.OrdinalIgnoreCase) &&
                segments[1].Equals("LocalCache", StringComparison.OrdinalIgnoreCase) &&
                segments[2].Equals("Local", StringComparison.OrdinalIgnoreCase) &&
                segments[3].Equals("Mozilla", StringComparison.OrdinalIgnoreCase) &&
                segments[4].Equals("Firefox", StringComparison.OrdinalIgnoreCase) &&
                segments[5].Equals("Profiles", StringComparison.OrdinalIgnoreCase))
                return "firefox";
        }
        catch
        {
            // Unknown/relocated profiles are deliberately not managed.
        }
        return null;
    }

    private static bool SameBrowserPath(string left, string right) =>
        string.Equals(Path.GetFullPath(left).TrimEnd('\\', '/'),
            Path.GetFullPath(right).TrimEnd('\\', '/'),
            StringComparison.OrdinalIgnoreCase);

    private static bool IsVerifiedBrowserCacheRoot(string cachePath)
    {
        string? process = GetBrowserProcessForCacheRoot(cachePath);
        if (process == null || !Directory.Exists(cachePath))
            return false;

        string local = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (!HasNoReparsePoints(local, cachePath))
            return false;

        string profile = Path.GetDirectoryName(cachePath) ?? string.Empty;
        if (process != "firefox")
            return File.Exists(Path.Combine(profile, "Preferences"));

        string localProfiles = Path.GetDirectoryName(profile) ?? string.Empty;
        string roamingProfiles;
        if (SameBrowserPath(localProfiles, Path.Combine(local, "Mozilla", "Firefox", "Profiles")))
            roamingProfiles = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Mozilla", "Firefox", "Profiles");
        else
        {
            // With MSIX, LocalCache\Local\... and LocalCache\Roaming\...
            // live under the same uniquely named package directory.
            string package = localProfiles;
            for (int i = 0; i < 5; i++)
                package = Path.GetDirectoryName(package) ?? string.Empty;
            roamingProfiles = Path.Combine(package,
                "LocalCache", "Roaming", "Mozilla", "Firefox", "Profiles");
        }
        string correspondingProfile = Path.Combine(roamingProfiles, Path.GetFileName(profile));
        string roamingBase = SameBrowserPath(localProfiles,
            Path.Combine(local, "Mozilla", "Firefox", "Profiles"))
            ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            : local;
        return Directory.Exists(correspondingProfile) &&
            HasNoReparsePoints(roamingBase, correspondingProfile);
    }

    // Refuse symbolic links/junctions anywhere between the known user folder
    // and target. In particular, NEVER follow a linked Cache or Profiles folder.
    private static bool HasNoReparsePoints(string basePath, string targetPath)
    {
        try
        {
            string normalizedBase = Path.GetFullPath(basePath);
            string normalizedTarget = Path.GetFullPath(targetPath);
            string relative = Path.GetRelativePath(normalizedBase, normalizedTarget);
            if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal) || Path.IsPathRooted(relative))
                return false;

            string current = normalizedBase;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                return false;
            foreach (string part in relative.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, part);
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    return false;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsBrowserProcessRunning(string processName)
    {
        try
        {
            int currentSession;
            using (Process current = Process.GetCurrentProcess())
                currentSession = current.SessionId;

            Process[] processes = Process.GetProcessesByName(processName);
            bool found = false;
            foreach (Process process in processes)
            {
                using (process)
                {
                    try
                    {
                        if (!process.HasExited && process.SessionId == currentSession)
                            found = true;
                    }
                    catch
                    {
                        // If inspection fails, never assume the browser stopped.
                        found = true;
                    }
                }
            }
            return found;
        }
        catch
        {
            // If process state cannot be determined, fail CLOSED.
            return true;
        }
    }

    // Closing browser processes requires an explicit click in the Safe Cleanup
    // confirmation dialog. Never call this helper from scan or cleanup planning.
    // Close visible windows first; after a short grace period, terminate any
    // verified remaining processes belonging to this Windows session only.
    private static bool CloseSelectedBrowsersAfterConsent(
        IReadOnlyList<string> processNames)
    {
        string[] names = processNames
            .Where(name => name is "chrome" or "msedge" or "browser" or "firefox")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (names.Length == 0)
            return true;

        int currentSession;
        using (Process current = Process.GetCurrentProcess())
            currentSession = current.SessionId;

        void VisitVerifiedProcesses(Action<Process> action)
        {
            foreach (string name in names)
            {
                Process[] processes;
                try
                {
                    processes = Process.GetProcessesByName(name);
                }
                catch
                {
                    continue;
                }

                foreach (Process process in processes)
                {
                    using (process)
                    {
                        try
                        {
                            if (process.HasExited || process.SessionId != currentSession ||
                                !IsVerifiedBrowserExecutable(process, name))
                                continue;

                            action(process);
                        }
                        catch
                        {
                            // An inaccessible/exiting process must never be
                            // treated as successfully closed.
                        }
                    }
                }
            }
        }

        // Give browsers an opportunity to save their own normal shutdown state.
        VisitVerifiedProcesses(process => process.CloseMainWindow());
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Thread.Sleep(400);
            if (names.All(name => !IsBrowserProcessRunning(name)))
                return true;
        }

        // The user explicitly consented to closing the browser even if it
        // cannot exit normally. Do not terminate similarly named unknown EXEs.
        VisitVerifiedProcesses(process => process.Kill(entireProcessTree: true));
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Thread.Sleep(250);
            if (names.All(name => !IsBrowserProcessRunning(name)))
                return true;
        }

        return names.All(name => !IsBrowserProcessRunning(name));
    }

    // A generic browser.exe might belong to an unrelated program. Verify
    // the executable's installation path or product metadata before closing.
    // Failure to identify it means we leave it running and skip its cache.
    private static bool IsVerifiedBrowserExecutable(Process process, string name)
    {
        try
        {
            string? executable = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(executable))
                return false;

            string product = FileVersionInfo.GetVersionInfo(executable).ProductName
                ?? string.Empty;
            return name switch
            {
                "chrome" =>
                    executable.Contains(@"\Google\Chrome\", StringComparison.OrdinalIgnoreCase) ||
                    product.Contains("Google Chrome", StringComparison.OrdinalIgnoreCase),
                "msedge" =>
                    executable.Contains(@"\Microsoft\Edge\", StringComparison.OrdinalIgnoreCase) ||
                    product.Contains("Microsoft Edge", StringComparison.OrdinalIgnoreCase),
                "browser" =>
                    executable.Contains(@"\Yandex\YandexBrowser\", StringComparison.OrdinalIgnoreCase) ||
                    product.Contains("Yandex", StringComparison.OrdinalIgnoreCase),
                "firefox" =>
                    executable.Contains(@"\Mozilla Firefox\", StringComparison.OrdinalIgnoreCase) ||
                    executable.Contains(@"\Mozilla.Firefox_", StringComparison.OrdinalIgnoreCase) ||
                    product.Contains("Firefox", StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
        catch
        {
            return false;
        }
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
                    SettingsService.L($"Не удалось очистить Корзину. Код ошибки Windows: 0x{result:X8}.",
                        $"Could not empty Recycle Bin. Windows error: 0x{result:X8}."));
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
            // The progress dialog owns the worker task. The owner remains
            // blocked until the worker has actually stopped or completed.
            SafeCleanupOutcome outcome =
                ShowSafeCleanupProgressDialog(
                    selected);

            StatusText.Text = outcome.WasStopped
                ? SettingsService.L(
                    "Безопасная очистка остановлена.",
                    "Safe cleanup stopped.")
                : SettingsService.L(
                    "Безопасная очистка завершена.",
                    "Safe cleanup complete.");

            ShowCleanupResultDialog(
                outcome);
        }
        catch (Exception ex)
        {
            StatusText.Text = SettingsService.L(
                "Ошибка очистки. Обновляем данные диска...",
                "Cleanup error. Refreshing drive data...");
            ShowInfoDialog(
                SettingsService.L(
                    "Ошибка безопасной очистки",
                    "Safe cleanup error"),
                SettingsService.L(
                    $"Очистка прервана из-за ошибки. Некоторые файлы могли быть удалены.\n\n{ex.Message}",
                    $"Cleanup stopped due to an error. Some files may already have been deleted.\n\n{ex.Message}"));
        }
        finally
        {
            SafeCandidatesCard.IsEnabled =
                true;
        }

        // Refresh even after a partial cleanup or an unexpected error.
        StatusText.Text = SettingsService.L(
            "Обновляем данные диска после очистки...",
            "Refreshing drive data after cleanup...");
        await StartAnalysisAsync();
    }


    private async void DeleteButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_selectedEntry == null)
            return;

        SelectedStorageEntry entry =
            _selectedEntry;

        // A confirmation dialog must never override structural protection.
        // Enforce the same rule here as in the selected-item UI.
        bool isPseudoOrDrive = entry.Kind is
            StorageAnalysisTreeNodeKind.Drive or
            StorageAnalysisTreeNodeKind.FileSystemData or
            StorageAnalysisTreeNodeKind.UnresolvedData;
        bool isBlockedPath = TryGetDirectDeleteBlockReason(
            entry.Path, out string blockReason);
        if (isPseudoOrDrive || isBlockedPath)
        {
            StatusText.Text = isBlockedPath
                ? blockReason
                : SettingsService.L(
                    "Этот объект нельзя удалить через анализатор.",
                    "This item cannot be deleted through the analyzer.");
            return;
        }

        if (IsDownloadsRootPath(entry.Path))
        {
            ClearDownloadsButton_Click(sender, e);
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


    // General deletion is intentionally more restrictive than a user's
    // explicit Safe Cleanup plan. Never offer an entire OS or profile
    // container for deletion, even after additional confirmation.
    private static bool TryGetDirectDeleteBlockReason(
        string path,
        out string reason)
    {
        reason = SettingsService.L(
            "Это системная или служебная область. Её нельзя удалять через анализатор.",
            "This is a system or service area. It cannot be deleted through the analyzer.");

        if (string.IsNullOrWhiteSpace(path))
            return true;

        string fullPath;
        try
        {
            // Reject drive-relative paths (e.g. C:) and preserve the trailing
            // slash until after the root check: C:\ is not the same as C:.
            if (!Path.IsPathFullyQualified(path))
                return true;

            string absolutePath = Path.GetFullPath(path);
            if (IsDriveRootPath(absolutePath))
                return true;

            fullPath = NormalizeComparablePath(absolutePath);
        }
        catch
        {
            // If Windows cannot interpret a path, fail closed.
            return true;
        }

        if (IsRawRecycleBinPath(fullPath))
            return true;

        string name = Path.GetFileName(fullPath);
        if (IsProtectedSystemPath(fullPath, name) ||
            IsUnderProtectedSystemRoot(fullPath) ||
            (name.StartsWith("NTUSER.DAT", StringComparison.OrdinalIgnoreCase) &&
             fullPath.Contains(@"\Users\", StringComparison.OrdinalIgnoreCase)) ||
            (string.Equals(name, "UsrClass.dat", StringComparison.OrdinalIgnoreCase) &&
             fullPath.Contains(@"\AppData\", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (IsProtectedProfileContainer(fullPath))
        {
            reason = SettingsService.L(
                "Эту папку целиком удалять нельзя: здесь могут быть личные файлы, настройки или данные программ. Откройте её и выберите конкретные ненужные данные.",
                "This entire folder cannot be deleted: it may contain personal files, settings or application data. Open it and select specific items you no longer need.");
            return true;
        }

        reason = string.Empty;
        return false;
    }


    private static bool IsUnderProtectedSystemRoot(string path)
    {
        string root = Path.GetPathRoot(path) ?? string.Empty;
        foreach (string folder in new[]
                 {
                     "Boot", "EFI", "Recovery", "Windows.old",
                     "Documents and Settings"
                 })
        {
            if (IsSameOrChildPath(path, Path.Combine(root, folder)))
                return true;
        }

        // Some installations redirect Windows or program directories.
        foreach (Environment.SpecialFolder folder in new[]
                 {
                     Environment.SpecialFolder.Windows,
                     Environment.SpecialFolder.System,
                     Environment.SpecialFolder.SystemX86,
                     Environment.SpecialFolder.ProgramFiles,
                     Environment.SpecialFolder.ProgramFilesX86,
                     Environment.SpecialFolder.CommonProgramFiles,
                     Environment.SpecialFolder.CommonProgramFilesX86,
                     Environment.SpecialFolder.CommonApplicationData
                 })
        {
            string knownPath = Environment.GetFolderPath(folder);
            if (!string.IsNullOrWhiteSpace(knownPath) &&
                IsSameOrChildPath(path, knownPath))
            {
                return true;
            }
        }

        return false;
    }


    private static bool IsProtectedProfileContainer(string path)
    {
        string profile = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile) &&
            IsSameOrChildPath(profile, path))
        {
            // Protect the profile itself and parent containers of redirected profiles.
            return true;
        }

        string root = Path.GetPathRoot(path) ?? string.Empty;
        string relative = path[root.Length..].Trim('\\');
        string[] parts = relative.Split('\\',
            StringSplitOptions.RemoveEmptyEntries);

        // C:\Users and C:\Users\<any profile>, including Public/Default.
        if (parts.Length >= 1 &&
            string.Equals(parts[0], "Users", StringComparison.OrdinalIgnoreCase) &&
            parts.Length <= 2)
        {
            return true;
        }

        for (int index = 0; index < parts.Length; index++)
        {
            if (!string.Equals(parts[index], "AppData",
                    StringComparison.OrdinalIgnoreCase))
                continue;

            int remaining = parts.Length - index - 1;
            if (remaining == 0)
                return true; // AppData itself.

            if (remaining == 1 &&
                (string.Equals(parts[index + 1], "Local", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(parts[index + 1], "LocalLow", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(parts[index + 1], "Roaming", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // Windows-managed application state is protected below its
            // Microsoft branch, not just at the branch root.
            if (remaining >= 2 &&
                (string.Equals(parts[index + 1], "Local", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(parts[index + 1], "Roaming", StringComparison.OrdinalIgnoreCase)) &&
                string.Equals(parts[index + 2], "Microsoft", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Do not delete whole application infrastructure containers.
            if (remaining == 2 &&
                ((string.Equals(parts[index + 1], "Local", StringComparison.OrdinalIgnoreCase) &&
                  (string.Equals(parts[index + 2], "Microsoft", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(parts[index + 2], "Packages", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(parts[index + 2], "Programs", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(parts[index + 2], "Temp", StringComparison.OrdinalIgnoreCase))) ||
                 (string.Equals(parts[index + 1], "Roaming", StringComparison.OrdinalIgnoreCase) &&
                  string.Equals(parts[index + 2], "Microsoft", StringComparison.OrdinalIgnoreCase))))
            {
                return true;
            }
        }

        return false;
    }


    private static bool IsSameOrChildPath(string path, string ancestor)
    {
        string normalizedAncestor = NormalizeComparablePath(ancestor);
        return string.Equals(path, normalizedAncestor,
                   StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(normalizedAncestor + "\\",
                   StringComparison.OrdinalIgnoreCase);
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


    // Safe cleanup intentionally does not use Directory.Delete(recursive: true):
    // that call can work for a long time without reporting a single file and
    // cannot be cancelled while it recursively traverses a large directory.
    private SafeCleanupOutcome ExecuteSafeCleanup(
        IReadOnlyList<CleanupPlanItem> items,
        IProgress<SafeCleanupProgress> progress,
        CancellationToken cancellationToken)
    {
        long freeBefore = GetAvailableFreeSpace();
        int deletedFiles = 0;
        int deletedFolders = 0;
        int skippedEntries = 0;
        bool wasStopped = false;
        string currentArea = string.Empty;
        string currentPath = string.Empty;
        Stopwatch progressClock = Stopwatch.StartNew();
        long lastReportMilliseconds = -1000;
        HashSet<string> blockedBrowserAreas = new(StringComparer.OrdinalIgnoreCase);

        void Report(bool force = false)
        {
            long now = progressClock.ElapsedMilliseconds;
            if (!force && now - lastReportMilliseconds < 120)
                return;

            lastReportMilliseconds = now;
            progress.Report(new SafeCleanupProgress(
                currentArea,
                currentPath,
                deletedFiles,
                deletedFolders,
                skippedEntries));
        }

        foreach (CleanupPlanItem item in items)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                wasStopped = true;
                break;
            }

            currentArea = item.Title;
            currentPath = item.Path;
            Report(force: true);

            if (!Directory.Exists(item.Path))
                continue;

            string? browserProcess = GetBrowserProcessForCacheRoot(item.Path);
            if (browserProcess != null &&
                (!IsVerifiedBrowserCacheRoot(item.Path) ||
                 IsBrowserProcessRunning(browserProcess)))
            {
                skippedEntries++;
                blockedBrowserAreas.Add(item.Title);
                currentPath = SettingsService.L(
                    "Кэш пропущен: браузер открыт или путь больше не соответствует проверенному каталогу.",
                    "Cache skipped: the browser is running or the path is no longer verified.");
                Report(force: true);
                continue;
            }

            // Do not follow directory junctions/reparse points even if a
            // previously analysed path has since been replaced by a link.
            try
            {
                if ((File.GetAttributes(item.Path) & FileAttributes.ReparsePoint) != 0)
                {
                    skippedEntries++;
                    Report(force: true);
                    continue;
                }
            }
            catch
            {
                skippedEntries++;
                Report(force: true);
                continue;
            }

            string[] rootChildren;
            try
            {
                rootChildren = Directory.GetFileSystemEntries(item.Path);
            }
            catch
            {
                skippedEntries++;
                Report(force: true);
                continue;
            }

            // Post-order traversal: remove a directory only after its children.
            // Root cleanup folders are NEVER put on this stack or deleted.
            long nextBrowserProcessCheck = 0;
            Stack<(string Path, bool RemoveDirectory)> pending = new();
            for (int i = rootChildren.Length - 1; i >= 0; i--)
                pending.Push((rootChildren[i], false));

            while (pending.Count > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    wasStopped = true;
                    break;
                }

                // Browser may reopen during a long cleanup. Stop further
                // changes to its cache without touching any profile files.
                if (browserProcess != null &&
                    progressClock.ElapsedMilliseconds >= nextBrowserProcessCheck)
                {
                    nextBrowserProcessCheck = progressClock.ElapsedMilliseconds + 350;
                    if (IsBrowserProcessRunning(browserProcess) ||
                        !IsVerifiedBrowserCacheRoot(item.Path))
                    {
                        skippedEntries++;
                        blockedBrowserAreas.Add(item.Title);
                        currentPath = SettingsService.L(
                            "Браузер запущен или путь изменился. Оставшаяся очистка пропущена.",
                            "Browser running or path changed. Remaining cleanup skipped.");
                        Report(force: true);
                        break;
                    }
                }

                (string path, bool removeDirectory) = pending.Pop();
                currentPath = path;

                if (browserProcess != null &&
                    !IsSameOrChildPath(Path.GetFullPath(path), Path.GetFullPath(item.Path)))
                {
                    skippedEntries++;
                    Report();
                    continue;
                }

                if (removeDirectory)
                {
                    try
                    {
                        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                            throw new IOException(SettingsService.L("Каталог является ссылкой. Удаление отменено.", "Linked directory; not deleting."));
                        Directory.Delete(path, recursive: false);
                        deletedFolders++;
                    }
                    catch
                    {
                        skippedEntries++;
                    }

                    Report();
                    continue;
                }

                // Retain existing structural protection for each individual
                // entry, including entries added while cleanup is running.
                // The normal general-delete guard intentionally protects
                // everything under Microsoft\AppData. Edge's exact verified
                // Cache root is a much narrower opt-in exception; no other
                // Microsoft profile contents bypass the general protection.
                if (browserProcess == null && TryGetDirectDeleteBlockReason(path, out _))
                {
                    skippedEntries++;
                    Report();
                    continue;
                }

                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(path);
                }
                catch
                {
                    skippedEntries++;
                    Report();
                    continue;
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    // Never traverse symlinks or junctions into unknown data.
                    skippedEntries++;
                    Report();
                    continue;
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    string[] children;
                    try
                    {
                        children = Directory.GetFileSystemEntries(path);
                    }
                    catch
                    {
                        skippedEntries++;
                        Report();
                        continue;
                    }

                    pending.Push((path, true));
                    for (int i = children.Length - 1; i >= 0; i--)
                        pending.Push((children[i], false));
                }
                else
                {
                    try
                    {
                        // Some disposable temporary files are read-only.
                        if ((attributes & FileAttributes.ReadOnly) != 0)
                            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);

                        File.Delete(path);
                        deletedFiles++;
                    }
                    catch
                    {
                        skippedEntries++;
                    }
                }

                Report();
            }

            Report(force: true);
            if (wasStopped)
                break;
        }

        // Free space is measured at the end rather than estimated by summing
        // logical file sizes (which misreports compressed or sparse files).
        long freeAfter = GetAvailableFreeSpace();
        long freedBytes = freeBefore > 0 && freeAfter > 0
            ? Math.Max(0, freeAfter - freeBefore)
            : 0;

        Report(force: true);
        return new SafeCleanupOutcome(
            new CleanupExecutionResult(
                freedBytes,
                deletedFiles + deletedFolders,
                skippedEntries),
            deletedFiles,
            deletedFolders,
            wasStopped || cancellationToken.IsCancellationRequested,
            blockedBrowserAreas.OrderBy(name => name).ToArray());
    }

    private sealed record SafeCleanupProgress(
        string Area,
        string CurrentPath,
        int DeletedFiles,
        int DeletedFolders,
        int SkippedEntries);

    private sealed record SafeCleanupOutcome(
        CleanupExecutionResult Result,
        int DeletedFiles,
        int DeletedFolders,
        bool WasStopped,
        IReadOnlyList<string> BlockedBrowserAreas);


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
        if (TryGetDirectDeleteBlockReason(path, out _))
            return false;

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
        if (TryGetDirectDeleteBlockReason(path, out _))
            return false;

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
        if (TryGetDirectDeleteBlockReason(path, out _))
            return false;

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


}
