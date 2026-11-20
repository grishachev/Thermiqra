using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace PCHardwareMonitor;

public sealed partial class StorageAnalyzerService
{

    private static StorageAnalysisResult AnalyzeByTraversal(
        DriveInfo drive,
        string normalizedDrive,
        string fileSystem,
        CancellationToken cancellationToken,
        IProgress<StorageAnalysisProgress>? progress)
    {
        long totalBytes =
            drive.TotalSize;

        long freeBytes =
            drive.AvailableFreeSpace;

        long windowsUsedBytes =
            checked(
                totalBytes -
                freeBytes);

        Stopwatch totalTimer =
            Stopwatch.StartNew();

        long clusterSize =
            GetClusterSize(
                normalizedDrive);

        Dictionary<string, FallbackFolderStat> folders =
            new(
                StringComparer.OrdinalIgnoreCase);

        HashSet<string> visitedDirectories =
            new(
                StringComparer.OrdinalIgnoreCase);

        Stack<FallbackWorkItem> work =
            new();

        work.Push(
            new FallbackWorkItem(
                normalizedDrive,
                null,
                false));

        List<StorageAnalysisItem> largestFileCandidates =
            new();

        List<FallbackFileTreeEntry> fallbackTreeFiles =
            new();

        long logicalFileBytes = 0;
        long allocatedFileBytes = 0;

        int fileCount = 0;
        int directoryCount = 0;
        int skippedEntries = 0;
        long lastProgressMs = -250;

        progress?.Report(new StorageAnalysisProgress(
            StorageAnalysisStage.Traversal,
            0, 0, 0, null));

        while (work.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            FallbackWorkItem item =
                work.Pop();

            if (item.IsExit)
            {
                if (!folders.TryGetValue(
                        item.Path,
                        out FallbackFolderStat? completed))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(
                        completed.ParentPath) &&
                    folders.TryGetValue(
                        completed.ParentPath,
                        out FallbackFolderStat? parent))
                {
                    parent.RecursiveLogicalBytes =
                        SafeAdd(
                            parent.RecursiveLogicalBytes,
                            completed.RecursiveLogicalBytes);

                    parent.RecursiveAllocatedBytes =
                        SafeAdd(
                            parent.RecursiveAllocatedBytes,
                            completed.RecursiveAllocatedBytes);
                }

                continue;
            }

            if (!visitedDirectories.Add(
                    item.Path))
            {
                continue;
            }

            FallbackFolderStat folder =
                new(
                    item.Path,
                    item.ParentPath,
                    GetFallbackName(
                        item.Path));

            folders[item.Path] =
                folder;

            directoryCount++;

            long folderReportMs = totalTimer.ElapsedMilliseconds;
            if (folderReportMs - lastProgressMs >= 150)
            {
                progress?.Report(new StorageAnalysisProgress(
                    StorageAnalysisStage.Traversal,
                    fileCount,
                    directoryCount,
                    0, null));
                lastProgressMs = folderReportMs;
            }

            work.Push(
                new FallbackWorkItem(
                    item.Path,
                    item.ParentPath,
                    true));

            try
            {
                foreach (string entryPath in
                         Directory.EnumerateFileSystemEntries(
                             item.Path))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    FileAttributes attributes;

                    try
                    {
                        attributes =
                            File.GetAttributes(
                                entryPath);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        skippedEntries++;
                        continue;
                    }
                    catch (IOException)
                    {
                        skippedEntries++;
                        continue;
                    }

                    if ((attributes &
                         FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    if ((attributes &
                         FileAttributes.Directory) != 0)
                    {
                        work.Push(
                            new FallbackWorkItem(
                                entryPath,
                                item.Path,
                                false));

                        continue;
                    }

                    long logicalSize;

                    try
                    {
                        logicalSize =
                            new FileInfo(
                                entryPath)
                                .Length;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        skippedEntries++;
                        continue;
                    }
                    catch (IOException)
                    {
                        skippedEntries++;
                        continue;
                    }

                    long allocatedSize;

                    if (!TryGetAllocatedSizeFromWindows(
                            ToExtendedPath(
                                entryPath),
                            out allocatedSize))
                    {
                        allocatedSize =
                            EstimateAllocatedSize(
                                logicalSize,
                                clusterSize);
                    }

                    if (allocatedSize < 0)
                    {
                        allocatedSize =
                            logicalSize;
                    }

                    fileCount++;

                    long fileReportMs = totalTimer.ElapsedMilliseconds;
                    if (fileReportMs - lastProgressMs >= 150)
                    {
                        progress?.Report(new StorageAnalysisProgress(
                            StorageAnalysisStage.Traversal,
                            fileCount,
                            directoryCount,
                            0, null));
                        lastProgressMs = fileReportMs;
                    }

                    logicalFileBytes =
                        SafeAdd(
                            logicalFileBytes,
                            logicalSize);

                    allocatedFileBytes =
                        SafeAdd(
                            allocatedFileBytes,
                            allocatedSize);

                    folder.RecursiveLogicalBytes =
                        SafeAdd(
                            folder.RecursiveLogicalBytes,
                            logicalSize);

                    folder.RecursiveAllocatedBytes =
                        SafeAdd(
                            folder.RecursiveAllocatedBytes,
                            allocatedSize);

                    string fileName =
                        Path.GetFileName(
                            entryPath);

                    StorageAnalysisItem fileItem =
                        new(
                            entryPath,
                            fileName,
                            logicalSize,
                            allocatedSize,
                            false);

                    if (allocatedSize > 0 &&
                        !string.IsNullOrWhiteSpace(
                            fileName))
                    {
                        fallbackTreeFiles.Add(
                            new FallbackFileTreeEntry(
                                item.Path,
                                fileName,
                                logicalSize,
                                allocatedSize));
                    }

                    AddTopCandidate(
                        largestFileCandidates,
                        fileItem,
                        30);
                }
            }
            catch (UnauthorizedAccessException)
            {
                skippedEntries++;
            }
            catch (IOException)
            {
                skippedEntries++;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        progress?.Report(new StorageAnalysisProgress(
            StorageAnalysisStage.BuildingTree,
            fileCount,
            directoryCount,
            0, null));

        List<StorageAnalysisItem> rootFolders =
            folders.Values
                .Where(
                    folder =>
                        folder.ParentPath != null &&
                        string.Equals(
                            folder.ParentPath,
                            normalizedDrive,
                            StringComparison.OrdinalIgnoreCase) &&
                        folder.RecursiveAllocatedBytes > 0)
                .OrderByDescending(
                    folder =>
                        folder.RecursiveAllocatedBytes)
                .Select(
                    folder =>
                        new StorageAnalysisItem(
                            folder.Path,
                            folder.Name,
                            folder.RecursiveLogicalBytes,
                            folder.RecursiveAllocatedBytes,
                            true))
                .ToList();

        List<StorageAnalysisItem> largestFolders =
            folders.Values
                .Where(
                    folder =>
                        !string.Equals(
                            folder.Path,
                            normalizedDrive,
                            StringComparison.OrdinalIgnoreCase) &&
                        folder.RecursiveAllocatedBytes > 0)
                .OrderByDescending(
                    folder =>
                        folder.RecursiveAllocatedBytes)
                .Take(30)
                .Select(
                    folder =>
                        new StorageAnalysisItem(
                            folder.Path,
                            folder.Name,
                            folder.RecursiveLogicalBytes,
                            folder.RecursiveAllocatedBytes,
                            true))
                .ToList();

        List<StorageAnalysisItem> largestFiles =
            largestFileCandidates
                .OrderByDescending(
                    file =>
                        file.AllocatedBytes)
                .Take(30)
                .ToList();

        long fileSystemOverheadBytes =
            windowsUsedBytes >
            allocatedFileBytes
                ? windowsUsedBytes -
                  allocatedFileBytes
                : 0;

        StorageAnalysisTree tree =
            BuildTraversalTree(
                normalizedDrive,
                windowsUsedBytes,
                logicalFileBytes,
                fileSystemOverheadBytes,
                folders,
                fallbackTreeFiles);

        totalTimer.Stop();

        return new StorageAnalysisResult(
            normalizedDrive,
            fileSystem,
            totalBytes,
            freeBytes,
            windowsUsedBytes,
            logicalFileBytes,
            allocatedFileBytes,
            fileSystemOverheadBytes,
            fileCount,
            directoryCount,
            0,
            skippedEntries,
            totalTimer.Elapsed,
            rootFolders,
            largestFolders,
            largestFiles)
        {
            Tree =
                tree
        };
    }


    private static StorageAnalysisTree BuildTraversalTree(
        string normalizedDrive,
        long windowsUsedBytes,
        long logicalFileBytes,
        long fileSystemOverheadBytes,
        IReadOnlyDictionary<string, FallbackFolderStat> folders,
        IReadOnlyList<FallbackFileTreeEntry> files)
    {
        const long rootNodeId = 0;
        const long fileSystemNodeId = -1;

        Dictionary<long, StorageAnalysisTreeNode> nodes =
            new();

        Dictionary<string, long> folderIds =
            new(
                StringComparer.OrdinalIgnoreCase)
            {
                [normalizedDrive] =
                    rootNodeId
            };

        long nextNodeId = 1;

        nodes[rootNodeId] =
            new StorageAnalysisTreeNode(
                rootNodeId,
                long.MinValue,
                normalizedDrive,
                logicalFileBytes,
                windowsUsedBytes,
                StorageAnalysisTreeNodeKind.Drive);

        foreach (FallbackFolderStat folder
                 in folders.Values
                     .Where(
                         folder =>
                             !string.Equals(
                                 folder.Path,
                                 normalizedDrive,
                                 StringComparison.OrdinalIgnoreCase) &&
                             folder.RecursiveAllocatedBytes > 0)
                     .OrderBy(
                         folder =>
                             folder.Path.Count(
                                 character =>
                                     character == '\\')))
        {
            if (string.IsNullOrWhiteSpace(
                    folder.ParentPath) ||
                !folderIds.TryGetValue(
                    folder.ParentPath,
                    out long parentNodeId))
            {
                continue;
            }

            long nodeId =
                nextNodeId++;

            folderIds[folder.Path] =
                nodeId;

            nodes[nodeId] =
                new StorageAnalysisTreeNode(
                    nodeId,
                    parentNodeId,
                    folder.Name,
                    folder.RecursiveLogicalBytes,
                    folder.RecursiveAllocatedBytes,
                    StorageAnalysisTreeNodeKind.Directory);
        }

        foreach (FallbackFileTreeEntry file
                 in files)
        {
            if (!folderIds.TryGetValue(
                    file.ParentPath,
                    out long parentNodeId))
            {
                continue;
            }

            long nodeId =
                nextNodeId++;

            nodes[nodeId] =
                new StorageAnalysisTreeNode(
                    nodeId,
                    parentNodeId,
                    file.Name,
                    file.LogicalBytes,
                    file.AllocatedBytes,
                    StorageAnalysisTreeNodeKind.File);
        }

        if (fileSystemOverheadBytes > 0)
        {
            nodes[fileSystemNodeId] =
                new StorageAnalysisTreeNode(
                    fileSystemNodeId,
                    rootNodeId,
                    "",
                    fileSystemOverheadBytes,
                    fileSystemOverheadBytes,
                    StorageAnalysisTreeNodeKind.FileSystemData);
        }

        return new StorageAnalysisTree(
            normalizedDrive,
            rootNodeId,
            nodes);
    }

    private sealed class FallbackFolderStat
    {
        public FallbackFolderStat(
            string path,
            string? parentPath,
            string name)
        {
            Path = path;
            ParentPath = parentPath;
            Name = name;
        }

        public string Path { get; }

        public string? ParentPath { get; }

        public string Name { get; }

        public long RecursiveLogicalBytes { get; set; }

        public long RecursiveAllocatedBytes { get; set; }
    }


    private readonly record struct FallbackWorkItem(
        string Path,
        string? ParentPath,
        bool IsExit);


    private readonly record struct FallbackFileTreeEntry(
        string ParentPath,
        string Name,
        long LogicalBytes,
        long AllocatedBytes);

}
