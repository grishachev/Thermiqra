using System;
using System.IO;
using System.Linq;

namespace PCHardwareMonitor;

public partial class StorageAnalyzerWindow
{
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
                    "Закройте ненужные программы перед очисткой. Файлы, которые используются сейчас, могут быть пропущены.",
                    "Close unnecessary applications before cleaning. Files currently in use may be skipped."));
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
                StorageSafetyLevel.Review,
                SettingsService.L(
                    "Похоже на кэш приложения, но по названию папки нельзя надёжно определить её содержимое.",
                    "This looks like application cache, but the folder name alone does not reliably identify its contents."),
                SettingsService.L(
                    "Безопасность: сначала проверить",
                    "Safety: review first"),
                SettingsService.L(
                    "Некоторые приложения хранят здесь важные данные. Не удаляйте папку без проверки или штатного способа очистки.",
                    "Some apps keep important data here. Do not delete it without checking or using the app's own cleanup tools."));
        }

        if (path.Contains(
                @"\steamapps\workshop\content",
                StringComparison.OrdinalIgnoreCase))
        {
            return new StorageGuidance(
                StorageSafetyLevel.Review,
                SettingsService.L(
                    "Похоже на дополнения к играм Steam Workshop: моды и другие загруженные материалы.",
                    "This looks like Steam Workshop add-ons: mods and other downloaded content."),
                SettingsService.L(
                    "Безопасность: не удалять папку вручную",
                    "Safety: do not delete this folder manually"),
                SettingsService.L(
                    "Управляйте подписками на дополнения через Steam. Вручную удалённые материалы могут скачаться снова.",
                    "Manage add-on subscriptions through Steam. Manually deleted content may be downloaded again."));
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
                    "Откройте подпапки и проверьте, какие данные занимают место. Для очистки используйте функции самих программ.",
                    "Open the subfolders to see what uses the space. Use each application’s own cleanup options when available."));
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
                "Если назначение папки неизвестно, не удаляйте её, пока не проверите содержимое.",
                "If you do not know what the folder is for, check its contents before deleting it."));
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
}
