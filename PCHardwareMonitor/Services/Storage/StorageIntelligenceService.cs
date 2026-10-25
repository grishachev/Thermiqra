using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PCHardwareMonitor;

// Storage Intelligence intentionally only explains existing scan results.
// It does not delete files, access the network, or decide Safe Cleanup eligibility.
public enum StorageInsightConfidence
{
    Identified,
    Likely,
    Unknown
}

public sealed record StorageInsightEntry(
    string Key,
    string TitleRu,
    string TitleEn,
    string DescriptionRu,
    string DescriptionEn,
    string ActionRu,
    string ActionEn,
    StorageInsightConfidence Confidence,
    long AllocatedBytes,
    long RepresentativeNodeId);

public static class StorageIntelligenceService
{
    private const string SteamGames = "steam-games";
    private const string SteamWorkshop = "steam-workshop";
    private const string WindowsFiles = "windows-files";
    private const string NvidiaCache = "nvidia-cache";
    private const string AmdCache = "amd-cache";
    private const string IntelCache = "intel-cache";
    private const string OtherData = "other-data";

    public static IReadOnlyList<StorageInsightEntry> Explain(
        StorageAnalysisTree tree,
        long windowsUsedBytes)
    {
        ArgumentNullException.ThrowIfNull(tree);

        // No file IO: inspect only the already-collected tree. Candidate name
        // filtering keeps expensive GetPath calls to a small minority of nodes.
        Dictionary<string, InsightAccumulator> found =
            new(StringComparer.Ordinal);
        List<(StorageAnalysisTreeNode Node, string Key,
            StorageInsightConfidence Confidence)> candidates = new();

        string localAppData = Normalized(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData));
        string windowsPath = Normalized(Environment.GetFolderPath(
            Environment.SpecialFolder.Windows));

        foreach (StorageAnalysisTreeNode node in tree.Nodes)
        {
            if (!node.IsDirectory || node.Id == tree.RootNodeId ||
                node.AllocatedBytes <= 0 || !IsCandidateName(node.Name))
            {
                continue;
            }

            string path = Normalized(tree.GetPath(node.Id));
            string? key = null;
            StorageInsightConfidence confidence = StorageInsightConfidence.Likely;

            if (NameEquals(node.Name, "Windows") &&
                !string.IsNullOrEmpty(windowsPath) && SamePath(path, windowsPath))
            {
                key = WindowsFiles;
                confidence = StorageInsightConfidence.Identified;
            }
            else if (NameEquals(node.Name, "common") &&
                     HasEnding(path, @"\steamapps\common") &&
                     TryGetSteamAppsNode(tree, node, out StorageAnalysisTreeNode? steamApps))
            {
                key = SteamGames;
                confidence = IsSteamLibraryConfirmed(tree, steamApps!)
                    ? StorageInsightConfidence.Identified
                    : StorageInsightConfidence.Likely;
            }
            else if (NameEquals(node.Name, "content") &&
                     HasEnding(path, @"\steamapps\workshop\content") &&
                     TryGetSteamWorkshopParents(tree, node,
                         out StorageAnalysisTreeNode? workshop,
                         out StorageAnalysisTreeNode? workshopSteamApps))
            {
                key = SteamWorkshop;
                confidence = IsSteamWorkshopConfirmed(tree, workshop!, workshopSteamApps!)
                    ? StorageInsightConfidence.Identified
                    : StorageInsightConfidence.Likely;
            }
            else if (!string.IsNullOrEmpty(localAppData))
            {
                if (MatchesUnder(localAppData, path,
                    @"NVIDIA\DXCache", @"NVIDIA\GLCache",
                    @"NVIDIA Corporation\NV_Cache"))
                {
                    key = NvidiaCache;
                    confidence = StorageInsightConfidence.Identified;
                }
                else if (MatchesUnder(localAppData, path,
                    @"AMD\DxCache", @"AMD\GLCache", @"AMD\DxcCache"))
                {
                    key = AmdCache;
                    // The path alone does not prove that every item is disposable.
                    confidence = StorageInsightConfidence.Likely;
                }
                else if (MatchesUnder(localAppData, path,
                    @"Intel\ShaderCache"))
                {
                    key = IntelCache;
                    confidence = StorageInsightConfidence.Likely;
                }
            }

            if (key != null)
                candidates.Add((node, key, confidence));
        }

        // Check ancestors before adding a category. This also handles unusual
        // installations (for example, a game library nested in Windows) without
        // ever adding the same allocated bytes twice.
        HashSet<long> acceptedRoots = new();
        foreach (var candidate in candidates.OrderBy(item =>
                     tree.GetPath(item.Node.Id).Length))
        {
            if (HasAcceptedAncestor(tree, candidate.Node, acceptedRoots))
                continue;

            acceptedRoots.Add(candidate.Node.Id);
            if (!found.TryGetValue(candidate.Key, out InsightAccumulator? accumulator))
            {
                accumulator = new InsightAccumulator(candidate.Key,
                    candidate.Confidence,
                    candidate.Node.Id, candidate.Node.AllocatedBytes);
                found.Add(candidate.Key, accumulator);
            }
            else
            {
                accumulator.Add(candidate.Node, candidate.Confidence);
            }
        }

        List<StorageInsightEntry> known = found.Values
            .Select(CreateEntry)
            .OrderByDescending(item => item.AllocatedBytes)
            .ToList();

        // Categories represent disjoint areas (Windows, Steam game files,
        // Steam Workshop and vendor caches). All other space remains unclaimed.
        // It can contain personal files, system metadata, and unknown files;
        // it is NEVER automatically classified as removable junk.
        // Break down the unrecognized part by its actual top-level folders.
        // Subtract recognized descendants so that Steam and its parent folder,
        // for example, are never counted twice.
        Dictionary<long, long> excludedByTopLevel = new();
        foreach (long rootId in acceptedRoots)
        {
            if (!tree.TryGetNode(rootId, out StorageAnalysisTreeNode? node) ||
                node == null)
                continue;

            long topId = GetTopLevelId(tree, node);
            excludedByTopLevel.TryGetValue(topId, out long excluded);
            excludedByTopLevel[topId] = SaturatingAdd(excluded, node.AllocatedBytes);
        }

        long rootChildrenBytes = 0;
        foreach (StorageAnalysisTreeNode child in tree.GetChildren(tree.RootNodeId))
        {
            rootChildrenBytes = SaturatingAdd(rootChildrenBytes, child.AllocatedBytes);
            excludedByTopLevel.TryGetValue(child.Id, out long excluded);
            long unclassified = Math.Max(0, child.AllocatedBytes - excluded);
            if (unclassified == 0)
                continue;

            known.Add(CreateUnclassifiedEntry(tree, child, unclassified, excluded > 0));
        }

        // Some disk usage cannot be associated with a path in the scan tree.
        // State that limitation explicitly instead of pretending it is junk.
        long accountedBytes = Math.Max(windowsUsedBytes, tree.RootNode.AllocatedBytes);
        long unrepresentedBytes = Math.Max(0, accountedBytes - rootChildrenBytes);
        if (unrepresentedBytes > 0)
        {
            known.Add(new StorageInsightEntry(
                OtherData,
                "Место без точной папки", "Space without an exact folder",
                "Эту часть занятого места не удалось связать с конкретной папкой при анализе.",
                "This part of the used space could not be linked to an exact folder by the scan.",
                "Не пытайтесь удалять её вручную.",
                "Do not try to delete it manually.",
                StorageInsightConfidence.Unknown,
                unrepresentedBytes,
                tree.RootNodeId));
        }

        return known.OrderByDescending(item => item.AllocatedBytes).ToList();
    }

    private static long GetTopLevelId(StorageAnalysisTree tree, StorageAnalysisTreeNode node)
    {
        StorageAnalysisTreeNode current = node;
        HashSet<long> visited = new() { current.Id };
        while (current.ParentId != tree.RootNodeId &&
               tree.TryGetNode(current.ParentId, out StorageAnalysisTreeNode? parent) &&
               parent != null && visited.Add(parent.Id))
        {
            current = parent;
        }
        return current.Id;
    }

    private static long SaturatingAdd(long left, long right) =>
        right <= 0 ? left : long.MaxValue - left < right ? long.MaxValue : left + right;

    private static StorageInsightEntry CreateUnclassifiedEntry(
        StorageAnalysisTree tree, StorageAnalysisTreeNode node,
        long bytes, bool includesRecognizedAreas)
    {
        string name = node.Name;
        string titleRu;
        string titleEn;
        string descriptionRu;
        string descriptionEn;
        string actionRu;
        string actionEn;
        StorageInsightConfidence confidence = StorageInsightConfidence.Unknown;

        // A conventional root path is a clue, not proof that its contents are safe.
        if (node.Kind == StorageAnalysisTreeNodeKind.FileSystemData)
        {
            titleRu = "Служебные данные диска";
            titleEn = "Disk service data";
            descriptionRu = "Служебная информация, которая помогает диску хранить и находить файлы. Это не обычная папка для очистки.";
            descriptionEn = "Internal information the file system uses to store and find files. This is not a regular folder to clean up.";
            actionRu = "Не удаляйте вручную.";
            actionEn = "Do not delete manually.";
            confidence = StorageInsightConfidence.Identified;
        }
        else if (node.Kind == StorageAnalysisTreeNodeKind.UnresolvedData)
        {
            titleRu = "Данные без полного пути";
            titleEn = "Data without a full path";
            descriptionRu = "Анализ обнаружил эти данные, но не смог определить их точное расположение.";
            descriptionEn = "The scan found these data but could not determine their exact location.";
            actionRu = "Не удаляйте ничего наугад.";
            actionEn = "Do not delete anything by guesswork.";
        }
        else if (node.IsDirectory && NameEquals(name, "Users"))
        {
            titleRu = "Пользовательские данные (Users)";
            titleEn = "User data (Users)";
            descriptionRu = "Обычно здесь хранятся файлы пользователей (например, Документы и Загрузки), настройки и данные программ.";
            descriptionEn = "Usually contains personal files (such as Documents and Downloads), settings and application data.";
            actionRu = "Проверяйте отдельные папки, но не удаляйте Users целиком.";
            actionEn = "Review individual folders, but do not delete the entire Users folder.";
            confidence = StorageInsightConfidence.Likely;
        }
        else if (node.IsDirectory &&
                 (NameEquals(name, "Program Files") || NameEquals(name, "Program Files (x86)")))
        {
            titleRu = "Установленные программы";
            titleEn = "Installed programs";
            descriptionRu = "Обычно здесь находятся установленные программы, игры и файлы, необходимые для их работы.";
            descriptionEn = "Usually contains installed applications, games and files they need to run.";
            actionRu = "Удаляйте ненужные программы через настройки Windows или их деинсталлятор, а не вручную из папки.";
            actionEn = "Uninstall unused applications through Windows Settings or their uninstaller, not by deleting their folders.";
            confidence = StorageInsightConfidence.Likely;
        }
        else if (node.IsDirectory && NameEquals(name, "AppData"))
        {
            titleRu = "Данные приложений (AppData)";
            titleEn = "Application data (AppData)";
            descriptionRu = "Программы могут хранить здесь настройки, сохранения и кэш. Не все эти данные временные.";
            descriptionEn = "Applications may keep settings, saved data and caches here. Not all of these files are temporary.";
            actionRu = "Не удаляйте AppData целиком. Для очистки используйте настройки самой программы.";
            actionEn = "Do not delete all of AppData. Use the application's own cleanup options.";
            confidence = StorageInsightConfidence.Likely;
        }
        else if (node.IsDirectory && NameEquals(name, "ProgramData"))
        {
            titleRu = "Данные программ (ProgramData)";
            titleEn = "Program data (ProgramData)";
            descriptionRu = "Здесь программы могут хранить общие настройки, базы данных, кэш и другие рабочие файлы.";
            descriptionEn = "Applications may store shared settings, databases, caches and other working files here.";
            actionRu = "Не удаляйте ProgramData целиком. Убирайте данные только через программу или её деинсталлятор.";
            actionEn = "Do not delete all of ProgramData. Remove data through the application or its uninstaller.";
            confidence = StorageInsightConfidence.Likely;
        }
        else if (node.IsDirectory &&
                 (NameEquals(name, "SteamLibrary") || NameEquals(name, "Steam")) &&
                 tree.GetChildren(node.Id).Any(child => NameEquals(child.Name, "steamapps")))
        {
            titleRu = "Библиотека Steam";
            titleEn = "Steam library";
            descriptionRu = "Здесь обнаружена структура библиотеки Steam. Она может содержать игры, дополнения и другие загруженные файлы.";
            descriptionEn = "A Steam library structure was found here. It may contain games, add-ons and other downloaded files.";
            actionRu = "Управляйте играми и дополнениями через Steam, а не удаляйте папку целиком.";
            actionEn = "Manage games and add-ons through Steam rather than deleting the folder.";
            confidence = StorageInsightConfidence.Likely;
        }
        else if (node.IsDirectory && NameEquals(name, "System Volume Information"))
        {
            titleRu = "Служебные данные Windows";
            titleEn = "Windows service data";
            descriptionRu = "Windows может хранить здесь точки восстановления и служебные сведения о диске.";
            descriptionEn = "Windows may keep restore points and other disk-related system data here.";
            actionRu = "Не удаляйте вручную. Для управления восстановлением используйте средства защиты системы Windows.";
            actionEn = "Do not delete manually. Manage restore points through Windows System Protection.";
            confidence = StorageInsightConfidence.Likely;
        }
        else if (node.Kind == StorageAnalysisTreeNodeKind.File &&
                 NameEquals(name, "hiberfil.sys"))
        {
            titleRu = "Файл гибернации и быстрого запуска (hiberfil.sys)";
            titleEn = "Hibernation and Fast Startup file (hiberfil.sys)";
            descriptionRu = "Этот файл связан с гибернацией (сохранением сеанса при выключении) и быстрым запуском Windows.";
            descriptionEn = "Windows uses this file for hibernation (saving a session before shutdown) and Fast Startup.";
            actionRu = "Не удаляйте вручную. Если эти функции не нужны, изменяйте их только штатными средствами Windows.";
            actionEn = "Do not delete it manually. If you do not need these features, change them using Windows tools.";
            confidence = StorageInsightConfidence.Likely;
        }
        else if (node.Kind == StorageAnalysisTreeNodeKind.File &&
                 NameEquals(name, "pagefile.sys"))
        {
            titleRu = "Файл подкачки Windows (pagefile.sys)";
            titleEn = "Windows paging file (pagefile.sys)";
            descriptionRu = "Windows использует этот файл для управления памятью, в том числе когда оперативной памяти не хватает.";
            descriptionEn = "Windows uses this file to manage memory, including when there is not enough RAM.";
            actionRu = "Не удаляйте вручную. Настройки файла подкачки меняются средствами Windows; без необходимости лучше их не трогать.";
            actionEn = "Do not delete it manually. Use Windows settings to manage paging; leave them unchanged unless necessary.";
            confidence = StorageInsightConfidence.Likely;
        }
        else if (node.Kind == StorageAnalysisTreeNodeKind.File &&
                 NameEquals(name, "swapfile.sys"))
        {
            titleRu = "Дополнительный файл памяти Windows (swapfile.sys)";
            titleEn = "Additional Windows memory file (swapfile.sys)";
            descriptionRu = "Служебный файл виртуальной памяти, который Windows использует для работы некоторых приложений.";
            descriptionEn = "A virtual-memory file Windows uses when managing some applications.";
            actionRu = "Не удаляйте вручную: Windows обычно управляет этим файлом автоматически.";
            actionEn = "Do not delete it manually; Windows normally manages this file automatically.";
            confidence = StorageInsightConfidence.Likely;
        }
        else
        {
            titleRu = (node.IsDirectory ? "Папка «" : "Файл «") + name + "»";
            titleEn = (node.IsDirectory ? "Folder “" : "File “") + name + "”";
            descriptionRu = "Крупная область диска. Содержимое неизвестно: одного имени недостаточно, чтобы считать данные ненужными.";
            descriptionEn = "A large area of this drive. Its contents are unknown: a name alone does not mean the data are unnecessary.";
            actionRu = "Откройте в дереве и проверьте содержимое перед удалением.";
            actionEn = "Open it in the tree and review its contents before deleting.";
        }

        if (includesRecognizedAreas)
        {
            descriptionRu += " Размер указан без данных, показанных отдельно.";
            descriptionEn += " The size excludes data shown separately.";
        }

        return new StorageInsightEntry(
            "unclassified-" + node.Id, titleRu, titleEn,
            descriptionRu, descriptionEn, actionRu, actionEn,
            confidence, bytes, node.Id);
    }

    private static bool HasAcceptedAncestor(StorageAnalysisTree tree,
        StorageAnalysisTreeNode node, HashSet<long> acceptedRoots)
    {
        long parentId = node.ParentId;
        while (tree.TryGetNode(parentId, out StorageAnalysisTreeNode? parent) &&
               parent != null)
        {
            if (acceptedRoots.Contains(parentId))
                return true;
            if (parentId == tree.RootNodeId || parent.ParentId == parentId)
                break;
            parentId = parent.ParentId;
        }
        return false;
    }

    private static StorageInsightEntry CreateEntry(InsightAccumulator found)
    {
        string key = found.Key;
        return key switch
        {
            SteamWorkshop => new StorageInsightEntry(key,
                "Дополнения к играм Steam", "Steam game add-ons",
                "Дополнительные материалы и модификации, загруженные через Steam Workshop.",
                "Extra content and modifications downloaded via Steam Workshop.",
                "Лучше управлять подписками в Steam, а не удалять эти папки вручную.",
                "Manage subscriptions in Steam rather than manually deleting these folders.",
                found.Confidence, found.Bytes, found.NodeId),
            SteamGames => new StorageInsightEntry(key,
                "Игры Steam", "Steam games",
                "Файлы установленных игр, которые могут занимать много места.",
                "Installed game files that can use a lot of disk space.",
                "Ненужные игры лучше удалять через Steam.",
                "Uninstall games you no longer need through Steam.",
                found.Confidence, found.Bytes, found.NodeId),
            WindowsFiles => new StorageInsightEntry(key,
                "Система Windows", "Windows system",
                "Здесь находятся файлы Windows и её компонентов, необходимые для работы и обновлений системы.",
                "Contains Windows and component files needed to run and update the system.",
                "Не удаляйте папку Windows или файлы внутри неё вручную. Используйте штатную очистку Windows.",
                "Do not manually delete the Windows folder or its contents. Use Windows cleanup tools.",
                found.Confidence, found.Bytes, found.NodeId),
            NvidiaCache => new StorageInsightEntry(key,
                "Временные данные NVIDIA", "NVIDIA temporary data",
                "Кэш драйвера видеокарты, который помогает ускорять работу графики.",
                "Graphics driver cache used to speed up graphics work.",
                "Проверенные области можно очищать через «Безопасную очистку» Thermiqra.",
                "Use Thermiqra Safe Cleanup for verified cache areas.",
                found.Confidence, found.Bytes, found.NodeId),
            AmdCache => new StorageInsightEntry(key,
                "Возможный кэш AMD", "Possible AMD cache",
                "Похоже на временные графические данные AMD. Назначение не подтверждено полностью.",
                "This looks like AMD graphics cache. Its purpose is not fully verified.",
                "Пока не рекомендуется удалять вручную или через «Безопасную очистку».",
                "Manual deletion and Safe Cleanup are not recommended for now.",
                found.Confidence, found.Bytes, found.NodeId),
            IntelCache => new StorageInsightEntry(key,
                "Возможный кэш Intel", "Possible Intel cache",
                "Похоже на временные графические данные Intel. Назначение не подтверждено полностью.",
                "This looks like Intel graphics cache. Its purpose is not fully verified.",
                "Пока не рекомендуется удалять вручную или через «Безопасную очистку».",
                "Manual deletion and Safe Cleanup are not recommended for now.",
                found.Confidence, found.Bytes, found.NodeId),
            _ => throw new InvalidOperationException("Unexpected storage insight key.")
        };
    }

    private static bool IsCandidateName(string name) =>
        NameEquals(name, "Windows") || NameEquals(name, "common") ||
        NameEquals(name, "content") || NameEquals(name, "DXCache") ||
        NameEquals(name, "GLCache") || NameEquals(name, "NV_Cache") ||
        NameEquals(name, "DxCache") || NameEquals(name, "DxcCache") ||
        NameEquals(name, "ShaderCache");

    private static bool TryGetSteamAppsNode(
        StorageAnalysisTree tree, StorageAnalysisTreeNode node,
        out StorageAnalysisTreeNode? steamApps)
    {
        steamApps = null;
        return tree.TryGetNode(node.ParentId, out steamApps) &&
               steamApps != null && NameEquals(steamApps.Name, "steamapps");
    }

    private static bool TryGetSteamWorkshopParents(
        StorageAnalysisTree tree, StorageAnalysisTreeNode node,
        out StorageAnalysisTreeNode? workshop,
        out StorageAnalysisTreeNode? steamApps)
    {
        steamApps = null;
        workshop = null;
        return tree.TryGetNode(node.ParentId, out workshop) &&
               workshop != null && NameEquals(workshop.Name, "workshop") &&
               tree.TryGetNode(workshop.ParentId, out steamApps) &&
               steamApps != null && NameEquals(steamApps.Name, "steamapps");
    }

    private static bool IsSteamLibraryConfirmed(
        StorageAnalysisTree tree, StorageAnalysisTreeNode steamApps) =>
        tree.GetChildren(steamApps.Id).Any(child =>
            child.Name.StartsWith("appmanifest_", StringComparison.OrdinalIgnoreCase) &&
            child.Name.EndsWith(".acf", StringComparison.OrdinalIgnoreCase));

    private static bool IsSteamWorkshopConfirmed(
        StorageAnalysisTree tree,
        StorageAnalysisTreeNode workshop,
        StorageAnalysisTreeNode steamApps) =>
        tree.GetChildren(workshop.Id).Any(child =>
            child.Name.StartsWith("appworkshop_", StringComparison.OrdinalIgnoreCase) &&
            child.Name.EndsWith(".acf", StringComparison.OrdinalIgnoreCase)) ||
        IsSteamLibraryConfirmed(tree, steamApps);

    private static bool MatchesUnder(string root, string path, params string[] relativePaths)
    {
        foreach (string relativePath in relativePaths)
        {
            if (SamePath(path, Normalized(Path.Combine(root, relativePath))))
                return true;
        }
        return false;
    }

    private static string Normalized(string? path) =>
        string.IsNullOrWhiteSpace(path) ? string.Empty : path.TrimEnd('\\', '/');

    private static bool NameEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool SamePath(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool HasEnding(string path, string suffix) =>
        path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);

    private sealed class InsightAccumulator
    {
        public InsightAccumulator(string key,
            StorageInsightConfidence confidence, long nodeId, long bytes)
        {
            Key = key;
            Confidence = confidence;
            NodeId = nodeId;
            Bytes = bytes;
            LargestBytes = bytes;
        }

        public string Key { get; }
        public StorageInsightConfidence Confidence { get; private set; }
        public long NodeId { get; private set; }
        public long Bytes { get; private set; }
        private long LargestBytes { get; set; }

        public void Add(StorageAnalysisTreeNode node, StorageInsightConfidence confidence)
        {
            Bytes = long.MaxValue - Bytes < node.AllocatedBytes
                ? long.MaxValue : Bytes + node.AllocatedBytes;
            if (node.AllocatedBytes > LargestBytes)
            {
                LargestBytes = node.AllocatedBytes;
                NodeId = node.Id;
            }
            if (confidence == StorageInsightConfidence.Likely)
                Confidence = confidence;
        }
    }
}
