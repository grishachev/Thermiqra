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
