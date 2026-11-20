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


    private static StorageAnalysisTree BuildNtfsTree(
        char driveLetter,
        long windowsUsedBytes,
        long logicalFileBytes,
        long fileSystemOverheadBytes,
        IReadOnlyList<Entry> files,
        IReadOnlyList<Entry> directories,
        FolderAggregation folderAggregation)
    {
        const long rootNodeId = 5;
        const long fileSystemNodeId = -1;
        const long unresolvedNodeId = -2;

        Dictionary<long, StorageAnalysisTreeNode> nodes =
            new();

        nodes[rootNodeId] =
            new StorageAnalysisTreeNode(
                rootNodeId,
                long.MinValue,
                $"{driveLetter}:\\",
                logicalFileBytes,
                windowsUsedBytes,
                StorageAnalysisTreeNodeKind.Drive);

        foreach (Entry directory in directories)
        {
            if (directory.RecordNumber == 5 ||
                string.IsNullOrWhiteSpace(
                    directory.Name) ||
                !folderAggregation.Stats.TryGetValue(
                    directory.RecordNumber,
                    out FolderStat? folder) ||
                folder.RecursiveAllocatedBytes <= 0)
            {
                continue;
            }

            long nodeId =
                checked(
                    (long)directory.RecordNumber);

            long parentNodeId =
                checked(
                    (long)directory.ParentRecord);

            nodes[nodeId] =
                new StorageAnalysisTreeNode(
                    nodeId,
                    parentNodeId,
                    directory.Name,
                    folder.RecursiveLogicalBytes,
                    folder.RecursiveAllocatedBytes,
                    StorageAnalysisTreeNodeKind.Directory);
        }

        foreach (Entry file in files)
        {
            if (!file.HasUnnamedData ||
                file.AllocatedSize <= 0 ||
                string.IsNullOrWhiteSpace(
                    file.Name))
            {
                continue;
            }

            long nodeId =
                checked(
                    (long)file.RecordNumber);

            long parentNodeId =
                checked(
                    (long)file.ParentRecord);

            nodes[nodeId] =
                new StorageAnalysisTreeNode(
                    nodeId,
                    parentNodeId,
                    file.Name,
                    file.LogicalSize,
                    file.AllocatedSize,
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

        if (folderAggregation.UnresolvedAllocatedBytes > 0)
        {
            nodes[unresolvedNodeId] =
                new StorageAnalysisTreeNode(
                    unresolvedNodeId,
                    rootNodeId,
                    "",
                    folderAggregation.UnresolvedLogicalBytes,
                    folderAggregation.UnresolvedAllocatedBytes,
                    StorageAnalysisTreeNodeKind.UnresolvedData);
        }

        return new StorageAnalysisTree(
            $"{driveLetter}:\\",
            rootNodeId,
            nodes);
    }


    private static void AddTopCandidate(
        List<StorageAnalysisItem> items,
        StorageAnalysisItem item,
        int limit)
    {
        items.Add(
            item);

        if (items.Count <=
            limit * 2)
        {
            return;
        }

        items.Sort(
            (left, right) =>
                right.AllocatedBytes.CompareTo(
                    left.AllocatedBytes));

        items.RemoveRange(
            limit,
            items.Count -
            limit);
    }


    private static string GetFallbackName(
        string path)
    {
        string trimmed =
            path.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

        string name =
            Path.GetFileName(
                trimmed);

        return string.IsNullOrWhiteSpace(
                name)
            ? path
            : name;
    }

    private static int ApplySpecialAllocatedSizeCorrections(
        List<Entry> files,
        Dictionary<ulong, Entry> entries,
        char driveLetter,
        CancellationToken cancellationToken)
    {
        int corrected = 0;

        foreach (Entry file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!file.HasUnnamedData ||
                (!file.IsSparse && !file.IsCompressed) ||
                string.IsNullOrWhiteSpace(file.Name))
            {
                continue;
            }

            string path =
                ResolvePath(
                    file,
                    entries,
                    driveLetter);

            if (path.Contains(
                    "[MFT:",
                    StringComparison.Ordinal) ||
                path.Contains(
                    "[cycle]",
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (TryGetAllocatedSizeFromWindows(
                    ToExtendedPath(path),
                    out long allocatedBytes))
            {
                file.AllocatedSize = allocatedBytes;
                corrected++;
            }
        }

        return corrected;
    }

    private static FolderAggregation AggregateFolders(
        List<Entry> files,
        List<Entry> directories,
        Dictionary<ulong, Entry> entries,
        CancellationToken cancellationToken)
    {
        Dictionary<ulong, FolderStat> stats =
            directories.ToDictionary(
                directory => directory.RecordNumber,
                directory => new FolderStat(directory));

        int unresolvedNonEmptyFiles = 0;
        long unresolvedLogicalBytes = 0;
        long unresolvedAllocatedBytes = 0;

        foreach (Entry file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!file.HasUnnamedData)
                continue;

            ulong currentParent = file.ParentRecord;
            HashSet<ulong>? visited = null;
            bool reachedRoot = false;

            for (int depth = 0; depth < 256; depth++)
            {
                if (!stats.TryGetValue(
                        currentParent,
                        out FolderStat? folder))
                {
                    break;
                }

                visited ??= new HashSet<ulong>();

                if (!visited.Add(currentParent))
                    break;

                folder.RecursiveLogicalBytes =
                    checked(
                        folder.RecursiveLogicalBytes +
                        file.LogicalSize);

                folder.RecursiveAllocatedBytes =
                    checked(
                        folder.RecursiveAllocatedBytes +
                        file.AllocatedSize);

                if (currentParent == 5)
                {
                    reachedRoot = true;
                    break;
                }

                currentParent =
                    folder.Folder.ParentRecord;
            }

            if (!reachedRoot &&
                (file.LogicalSize > 0 ||
                 file.AllocatedSize > 0 ||
                 !string.IsNullOrWhiteSpace(file.Name)))
            {
                unresolvedNonEmptyFiles++;

                unresolvedLogicalBytes =
                    SafeAdd(
                        unresolvedLogicalBytes,
                        file.LogicalSize);

                unresolvedAllocatedBytes =
                    SafeAdd(
                        unresolvedAllocatedBytes,
                        file.AllocatedSize);
            }
        }

        return new FolderAggregation(
            stats,
            unresolvedNonEmptyFiles,
            unresolvedLogicalBytes,
            unresolvedAllocatedBytes);
    }

    private static string ResolvePath(
        Entry entry,
        Dictionary<ulong, Entry> entries,
        char driveLetter)
    {
        if (entry.RecordNumber == 5)
            return $"{driveLetter}:\\";

        List<string> parts = new();
        HashSet<ulong> visited = new();

        Entry current = entry;

        for (int depth = 0; depth < 256; depth++)
        {
            if (!visited.Add(current.RecordNumber))
            {
                parts.Add("[cycle]");
                break;
            }

            if (current.RecordNumber == 5)
                break;

            if (!string.IsNullOrWhiteSpace(current.Name) &&
                current.Name != ".")
            {
                parts.Add(current.Name);
            }

            if (current.ParentRecord == 5)
                break;

            if (!entries.TryGetValue(
                    current.ParentRecord,
                    out Entry? parent))
            {
                parts.Add($"[MFT:{current.ParentRecord}]");
                break;
            }

            current = parent;
        }

        parts.Reverse();

        if (parts.Count == 0)
            return $"{driveLetter}:\\";

        return $"{driveLetter}:\\" +
               string.Join("\\", parts);
    }


    private sealed class Entry
    {
        public Entry(ulong recordNumber)
        {
            RecordNumber = recordNumber;
        }

        public ulong RecordNumber { get; }
        public ulong ParentRecord { get; set; }
        public string Name { get; set; } = "";
        public int NamePriority { get; set; } = int.MinValue;

        public bool HasBaseRecord { get; set; }
        public bool IsDirectory { get; set; }

        public bool HasAttributeList { get; set; }

        public bool HasUnnamedData { get; set; }
        public bool IsResidentData { get; set; }
        public long LogicalSize { get; set; }
        public long AllocatedSize { get; set; }

        public bool IsSparse { get; set; }
        public bool IsCompressed { get; set; }
        public bool IsEncrypted { get; set; }
    }

    private sealed class FolderStat
    {
        public FolderStat(Entry folder)
        {
            Folder = folder;
        }

        public Entry Folder { get; }

        public long RecursiveLogicalBytes { get; set; }
        public long RecursiveAllocatedBytes { get; set; }
    }

    private readonly record struct FolderAggregation(
        Dictionary<ulong, FolderStat> Stats,
        int UnresolvedNonEmptyFiles,
        long UnresolvedLogicalBytes,
        long UnresolvedAllocatedBytes);

}
