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


// The percentage applies only to reading the known NTFS MFT records.
// Traversal and post-processing have no reliable total and remain indeterminate.
public enum StorageAnalysisStage
{
    NtfsRecords,
    Traversal,
    ProcessingEntries,
    BuildingTree
}

public sealed record StorageAnalysisProgress(
    StorageAnalysisStage Stage,
    long FilesFound,
    long FoldersFound,
    long ProcessedUnits,
    long? TotalUnits);

public sealed record StorageAnalysisResult(
    string DriveName,
    string FileSystem,
    long TotalBytes,
    long FreeBytes,
    long WindowsUsedBytes,
    long LogicalFileBytes,
    long AllocatedFileBytes,
    long FileSystemOverheadBytes,
    int FileCount,
    int DirectoryCount,
    int SpecialFilesCorrected,
    int UnresolvedNonEmptyFiles,
    TimeSpan Elapsed,
    IReadOnlyList<StorageAnalysisItem> RootFolders,
    IReadOnlyList<StorageAnalysisItem> LargestFolders,
    IReadOnlyList<StorageAnalysisItem> LargestFiles)
{
    public StorageAnalysisTree? Tree { get; init; }
}


public enum StorageAnalysisTreeNodeKind
{
    Drive,
    Directory,
    File,
    FileSystemData,
    UnresolvedData
}


public sealed record StorageAnalysisTreeNode(
    long Id,
    long ParentId,
    string Name,
    long LogicalBytes,
    long AllocatedBytes,
    StorageAnalysisTreeNodeKind Kind)
{
    public bool IsDirectory =>
        Kind == StorageAnalysisTreeNodeKind.Drive ||
        Kind == StorageAnalysisTreeNodeKind.Directory;
}


public sealed class StorageAnalysisTree
{
    private readonly Dictionary<long, StorageAnalysisTreeNode> _nodes;

    private readonly Dictionary<long, long[]> _children;

    public StorageAnalysisTree(
        string driveName,
        long rootNodeId,
        IReadOnlyDictionary<long, StorageAnalysisTreeNode> nodes)
    {
        DriveName =
            driveName;

        RootNodeId =
            rootNodeId;

        _nodes =
            new Dictionary<long, StorageAnalysisTreeNode>(
                nodes);

        Dictionary<long, List<long>> children =
            new();

        foreach (StorageAnalysisTreeNode node
                 in _nodes.Values)
        {
            if (node.Id == RootNodeId ||
                !_nodes.ContainsKey(
                    node.ParentId))
            {
                continue;
            }

            if (!children.TryGetValue(
                    node.ParentId,
                    out List<long>? childIds))
            {
                childIds =
                    new List<long>();

                children[node.ParentId] =
                    childIds;
            }

            childIds.Add(
                node.Id);
        }

        _children =
            new Dictionary<long, long[]>(
                children.Count);

        foreach (
            KeyValuePair<long, List<long>> pair
            in children)
        {
            pair.Value.Sort(
                (leftId, rightId) =>
                {
                    StorageAnalysisTreeNode left =
                        _nodes[leftId];

                    StorageAnalysisTreeNode right =
                        _nodes[rightId];

                    // Внутри каждой папки сначала показываем папки,
                    // затем файлы. В каждой группе — от большего к меньшему.
                    if (left.IsDirectory !=
                        right.IsDirectory)
                    {
                        return left.IsDirectory
                            ? -1
                            : 1;
                    }

                    int sizeComparison =
                        right.AllocatedBytes.CompareTo(
                            left.AllocatedBytes);

                    if (sizeComparison != 0)
                        return sizeComparison;

                    return string.Compare(
                        left.Name,
                        right.Name,
                        StringComparison.CurrentCultureIgnoreCase);
                });

            _children[pair.Key] =
                pair.Value.ToArray();
        }
    }


    public string DriveName { get; }

    public long RootNodeId { get; }

    public int NodeCount =>
        _nodes.Count;

    public IEnumerable<StorageAnalysisTreeNode> Nodes =>
        _nodes.Values;

    public StorageAnalysisTreeNode RootNode =>
        _nodes[RootNodeId];


    public bool HasChildren(
        long nodeId)
    {
        return
            _children.TryGetValue(
                nodeId,
                out long[]? children) &&
            children.Length > 0;
    }


    public IReadOnlyList<StorageAnalysisTreeNode> GetChildren(
        long nodeId)
    {
        if (!_children.TryGetValue(
                nodeId,
                out long[]? childIds) ||
            childIds.Length == 0)
        {
            return Array.Empty<StorageAnalysisTreeNode>();
        }

        StorageAnalysisTreeNode[] result =
            new StorageAnalysisTreeNode[
                childIds.Length];

        for (int i = 0;
             i < childIds.Length;
             i++)
        {
            result[i] =
                _nodes[
                    childIds[i]];
        }

        return result;
    }


    public bool TryGetNode(
        long nodeId,
        out StorageAnalysisTreeNode? node)
    {
        return _nodes.TryGetValue(
            nodeId,
            out node);
    }


    public string GetPath(
        long nodeId)
    {
        if (!_nodes.TryGetValue(
                nodeId,
                out StorageAnalysisTreeNode? node))
        {
            return DriveName;
        }

        if (node.Id == RootNodeId)
            return DriveName;

        if (node.Kind ==
                StorageAnalysisTreeNodeKind.FileSystemData ||
            node.Kind ==
                StorageAnalysisTreeNodeKind.UnresolvedData)
        {
            return DriveName;
        }

        List<string> parts =
            new();

        HashSet<long> visited =
            new();

        StorageAnalysisTreeNode current =
            node;

        while (current.Id !=
               RootNodeId)
        {
            if (!visited.Add(
                    current.Id))
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(
                    current.Name))
            {
                parts.Add(
                    current.Name);
            }

            if (!_nodes.TryGetValue(
                    current.ParentId,
                    out StorageAnalysisTreeNode? parent))
            {
                break;
            }

            current =
                parent;
        }

        parts.Reverse();

        if (parts.Count == 0)
            return DriveName;

        return DriveName.TrimEnd(
                   Path.DirectorySeparatorChar,
                   Path.AltDirectorySeparatorChar) +
               Path.DirectorySeparatorChar +
               string.Join(
                   Path.DirectorySeparatorChar.ToString(),
                   parts);
    }


    public IReadOnlyList<StorageAnalysisTreeNode> GetLargestBranch(
        int maxDepth = 8)
    {
        if (maxDepth < 1)
            maxDepth = 1;

        List<StorageAnalysisTreeNode> branch =
            new()
            {
                RootNode
            };

        StorageAnalysisTreeNode current =
            RootNode;

        for (int depth = 0;
             depth < maxDepth;
             depth++)
        {
            IReadOnlyList<StorageAnalysisTreeNode> children =
                GetChildren(
                    current.Id);

            StorageAnalysisTreeNode? largest =
                children
                    .FirstOrDefault(
                        child =>
                            child.Kind ==
                                StorageAnalysisTreeNodeKind.Directory ||
                            child.Kind ==
                                StorageAnalysisTreeNodeKind.File);

            if (largest == null)
                break;

            branch.Add(
                largest);

            if (!largest.IsDirectory)
                break;

            current =
                largest;
        }

        return branch;
    }
}


public sealed record StorageAnalysisItem(
    string Path,
    string Name,
    long LogicalBytes,
    long AllocatedBytes,
    bool IsDirectory);
