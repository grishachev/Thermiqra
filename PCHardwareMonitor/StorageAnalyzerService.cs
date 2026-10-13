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

public sealed class StorageAnalyzerService
{
    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;

    private const uint FsctlGetNtfsVolumeData = 0x00090064;

    private const uint AttributeTypeAttributeList = 0x20;
    private const uint AttributeTypeFileName = 0x30;
    private const uint AttributeTypeData = 0x80;
    private const uint AttributeEnd = 0xFFFFFFFF;

    private const ushort FileRecordInUse = 0x0001;
    private const ushort FileRecordDirectory = 0x0002;

    private const ushort AttributeFlagCompressionMask = 0x00FF;
    private const ushort AttributeFlagEncrypted = 0x4000;
    private const ushort AttributeFlagSparse = 0x8000;

    private const int ReadBufferSize = 8 * 1024 * 1024;

    public Task<StorageAnalysisResult> AnalyzeAsync(
        string driveName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(driveName))
            throw new ArgumentException("Drive name is required.", nameof(driveName));

        return Task.Run(
            () => AnalyzeCore(driveName, cancellationToken),
            cancellationToken);
    }

    private static StorageAnalysisResult AnalyzeCore(
        string driveName,
        CancellationToken cancellationToken)
    {
        string normalizedDrive = NormalizeDriveName(driveName);
        DriveInfo drive = new(normalizedDrive);

        if (!drive.IsReady)
            throw new IOException($"Drive {normalizedDrive} is not ready.");

        string fileSystem = drive.DriveFormat;

        if (!string.Equals(
                fileSystem,
                "NTFS",
                StringComparison.OrdinalIgnoreCase))
        {
            return AnalyzeByTraversal(
                drive,
                normalizedDrive,
                fileSystem,
                cancellationToken);
        }

        char driveLetter = char.ToUpperInvariant(normalizedDrive[0]);

        long totalBytes = drive.TotalSize;
        long freeBytes = drive.AvailableFreeSpace;
        long windowsUsedBytes = checked(totalBytes - freeBytes);

        Stopwatch totalTimer = Stopwatch.StartNew();

        string volumePath = $@"\\.\{driveLetter}:";

        using SafeFileHandle volumeHandle = CreateFileW(
            volumePath,
            GenericRead,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);

        if (volumeHandle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();

            throw new IOException(
                $"Unable to open NTFS volume {driveLetter}: for reading. " +
                $"Win32 error: {error}.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        NtfsVolumeDataBuffer volumeData =
            GetNtfsVolumeData(volumeHandle);

        int bytesPerSector = checked((int)volumeData.BytesPerSector);
        int bytesPerCluster = checked((int)volumeData.BytesPerCluster);
        int bytesPerRecord =
            checked((int)volumeData.BytesPerFileRecordSegment);

        if (bytesPerRecord <= 0 ||
            bytesPerRecord > 1024 * 1024 ||
            ReadBufferSize % bytesPerRecord != 0)
        {
            throw new InvalidDataException(
                "Unsupported NTFS file record size.");
        }

        byte[] mftBaseRecord =
            ReadMftBaseRecord(
                volumeHandle,
                volumeData,
                bytesPerSector,
                bytesPerCluster,
                bytesPerRecord);

        MftDataInfo mftData =
            GetMftRunList(mftBaseRecord);

        long expectedRecordCount =
            mftData.FileSize > 0
                ? mftData.FileSize / bytesPerRecord
                : 0;

        int initialCapacity =
            expectedRecordCount > 0 && expectedRecordCount <= int.MaxValue
                ? checked((int)expectedRecordCount)
                : 0;

        Dictionary<ulong, Entry> entries =
            initialCapacity > 0
                ? new Dictionary<ulong, Entry>(initialCapacity)
                : new Dictionary<ulong, Entry>();

        ParseStats stats = new();
        byte[] readBuffer = new byte[ReadBufferSize];

        foreach (DataRun run in mftData.Runs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (run.IsSparse)
                continue;

            long runBytes =
                checked(run.ClusterCount * (long)bytesPerCluster);

            long physicalStart =
                checked(run.StartLcn * (long)bytesPerCluster);

            long logicalStart =
                checked(run.StartVcn * (long)bytesPerCluster);

            long runOffset = 0;

            while (runOffset < runBytes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int bytesToRead =
                    (int)Math.Min(
                        readBuffer.Length,
                        runBytes - runOffset);

                bytesToRead -= bytesToRead % bytesPerRecord;

                if (bytesToRead <= 0)
                    break;

                int bytesRead =
                    RandomAccess.Read(
                        volumeHandle,
                        readBuffer.AsSpan(0, bytesToRead),
                        physicalStart + runOffset);

                if (bytesRead <= 0)
                    break;

                int completeBytes =
                    bytesRead - bytesRead % bytesPerRecord;

                if (completeBytes <= 0)
                    break;

                for (int bufferOffset = 0;
                     bufferOffset < completeBytes;
                     bufferOffset += bytesPerRecord)
                {
                    long logicalOffset =
                        checked(
                            logicalStart +
                            runOffset +
                            bufferOffset);

                    ulong recordNumber =
                        checked((ulong)(logicalOffset / bytesPerRecord));

                    Span<byte> record =
                        readBuffer.AsSpan(
                            bufferOffset,
                            bytesPerRecord);

                    ParseRecord(
                        record,
                        recordNumber,
                        bytesPerSector,
                        bytesPerCluster,
                        entries,
                        stats);
                }

                runOffset += completeBytes;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        List<Entry> baseEntries =
            entries.Values
                .Where(entry => entry.HasBaseRecord)
                .ToList();

        List<Entry> files =
            baseEntries
                .Where(entry => !entry.IsDirectory)
                .ToList();

        List<Entry> directories =
            baseEntries
                .Where(entry => entry.IsDirectory)
                .ToList();

        int specialFilesCorrected =
            ApplySpecialAllocatedSizeCorrections(
                files,
                entries,
                driveLetter,
                cancellationToken);

        FolderAggregation folderAggregation =
            AggregateFolders(
                files,
                directories,
                entries,
                cancellationToken);

        long logicalFileBytes =
            files
                .Where(file => file.HasUnnamedData)
                .Sum(file => file.LogicalSize);

        long allocatedFileBytes =
            files
                .Where(file => file.HasUnnamedData)
                .Sum(file => file.AllocatedSize);

        List<StorageAnalysisItem> largestFiles =
            files
                .Where(file =>
                    file.HasUnnamedData &&
                    file.AllocatedSize > 0 &&
                    !string.IsNullOrWhiteSpace(file.Name))
                .OrderByDescending(file => file.AllocatedSize)
                .Take(30)
                .Select(file => new StorageAnalysisItem(
                    ResolvePath(file, entries, driveLetter),
                    file.Name,
                    file.LogicalSize,
                    file.AllocatedSize,
                    false))
                .ToList();

        List<StorageAnalysisItem> largestFolders =
            folderAggregation.Stats.Values
                .Where(folder =>
                    folder.Folder.RecordNumber != 5 &&
                    folder.RecursiveAllocatedBytes > 0 &&
                    !string.IsNullOrWhiteSpace(folder.Folder.Name))
                .OrderByDescending(folder => folder.RecursiveAllocatedBytes)
                .Take(30)
                .Select(folder => new StorageAnalysisItem(
                    ResolvePath(folder.Folder, entries, driveLetter),
                    folder.Folder.Name,
                    folder.RecursiveLogicalBytes,
                    folder.RecursiveAllocatedBytes,
                    true))
                .ToList();

        List<StorageAnalysisItem> rootFolders =
            folderAggregation.Stats.Values
                .Where(folder =>
                    folder.Folder.RecordNumber != 5 &&
                    folder.Folder.ParentRecord == 5 &&
                    folder.RecursiveAllocatedBytes > 0 &&
                    !string.IsNullOrWhiteSpace(folder.Folder.Name))
                .OrderByDescending(folder => folder.RecursiveAllocatedBytes)
                .Select(folder => new StorageAnalysisItem(
                    ResolvePath(folder.Folder, entries, driveLetter),
                    folder.Folder.Name,
                    folder.RecursiveLogicalBytes,
                    folder.RecursiveAllocatedBytes,
                    true))
                .ToList();

        long fileSystemOverheadBytes =
            windowsUsedBytes >
            allocatedFileBytes
                ? windowsUsedBytes -
                  allocatedFileBytes
                : 0;

        StorageAnalysisTree tree =
            BuildNtfsTree(
                driveLetter,
                windowsUsedBytes,
                logicalFileBytes,
                fileSystemOverheadBytes,
                files,
                directories,
                folderAggregation);

        totalTimer.Stop();

        return new StorageAnalysisResult(
            $"{driveLetter}:\\",
            fileSystem,
            totalBytes,
            freeBytes,
            windowsUsedBytes,
            logicalFileBytes,
            allocatedFileBytes,
            fileSystemOverheadBytes,
            files.Count,
            directories.Count,
            specialFilesCorrected,
            folderAggregation.UnresolvedNonEmptyFiles,
            totalTimer.Elapsed,
            rootFolders,
            largestFolders,
            largestFiles)
        {
            Tree =
                tree
        };
    }

    private static StorageAnalysisResult AnalyzeByTraversal(
        DriveInfo drive,
        string normalizedDrive,
        string fileSystem,
        CancellationToken cancellationToken)
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


    private static long GetClusterSize(
        string rootPath)
    {
        try
        {
            if (!GetDiskFreeSpaceW(
                    rootPath,
                    out uint sectorsPerCluster,
                    out uint bytesPerSector,
                    out _,
                    out _))
            {
                return 0;
            }

            return checked(
                (long)sectorsPerCluster *
                bytesPerSector);
        }
        catch
        {
            return 0;
        }
    }


    private static long EstimateAllocatedSize(
        long logicalSize,
        long clusterSize)
    {
        if (logicalSize <= 0)
            return 0;

        if (clusterSize <= 0)
            return logicalSize;

        try
        {
            long clusters =
                checked(
                    (logicalSize +
                     clusterSize -
                     1) /
                    clusterSize);

            return checked(
                clusters *
                clusterSize);
        }
        catch (OverflowException)
        {
            return logicalSize;
        }
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


    private static void ParseRecord(
        Span<byte> record,
        ulong recordNumber,
        int bytesPerSector,
        int bytesPerCluster,
        Dictionary<ulong, Entry> entries,
        ParseStats stats)
    {
        if (!HasFileSignature(record))
            return;

        if (!ApplyUpdateSequenceFixup(
                record,
                bytesPerSector))
        {
            stats.FixupErrors++;
            return;
        }

        if (record.Length < 48)
            return;

        ushort flags =
            BinaryPrimitives.ReadUInt16LittleEndian(
                record.Slice(0x16, 2));

        if ((flags & FileRecordInUse) == 0)
            return;

        ulong baseReferenceRaw =
            BinaryPrimitives.ReadUInt64LittleEndian(
                record.Slice(0x20, 8));

        ulong baseRecordNumber =
            GetRecordNumber(baseReferenceRaw);

        bool isExtension =
            baseRecordNumber != 0;

        ulong ownerRecordNumber =
            isExtension
                ? baseRecordNumber
                : recordNumber;

        if (!entries.TryGetValue(
                ownerRecordNumber,
                out Entry? entry))
        {
            entry = new Entry(ownerRecordNumber);
            entries.Add(ownerRecordNumber, entry);
        }

        if (!isExtension)
        {
            entry.HasBaseRecord = true;
            entry.IsDirectory =
                (flags & FileRecordDirectory) != 0;
        }

        ushort firstAttributeOffset =
            BinaryPrimitives.ReadUInt16LittleEndian(
                record.Slice(0x14, 2));

        uint bytesInUseRaw =
            BinaryPrimitives.ReadUInt32LittleEndian(
                record.Slice(0x18, 4));

        int bytesInUse =
            (int)Math.Min(
                bytesInUseRaw,
                (uint)record.Length);

        if (firstAttributeOffset >= bytesInUse)
            return;

        int offset = firstAttributeOffset;

        while (offset + 16 <= bytesInUse)
        {
            uint type =
                BinaryPrimitives.ReadUInt32LittleEndian(
                    record.Slice(offset, 4));

            if (type == AttributeEnd)
                break;

            uint lengthRaw =
                BinaryPrimitives.ReadUInt32LittleEndian(
                    record.Slice(offset + 4, 4));

            if (lengthRaw < 16 ||
                lengthRaw > int.MaxValue)
            {
                break;
            }

            int attributeLength = (int)lengthRaw;

            if (offset + attributeLength > bytesInUse)
                break;

            ReadOnlySpan<byte> attribute =
                record.Slice(
                    offset,
                    attributeLength);

            byte formCode = attribute[8];
            byte nameLength = attribute[9];

            ushort attributeFlags =
                BinaryPrimitives.ReadUInt16LittleEndian(
                    attribute.Slice(12, 2));

            if (type == AttributeTypeAttributeList)
            {
                entry.HasAttributeList = true;
            }
            else if (type == AttributeTypeFileName &&
                     formCode == 0)
            {
                if (TryReadFileName(
                        attribute,
                        out string? name,
                        out ulong parentRecord,
                        out byte nameSpace))
                {
                    int priority =
                        GetNamePriority(nameSpace);

                    if (priority > entry.NamePriority)
                    {
                        entry.Name = name ?? "";
                        entry.ParentRecord = parentRecord;
                        entry.NamePriority = priority;
                    }
                }
            }
            else if (type == AttributeTypeData &&
                     nameLength == 0)
            {
                if (formCode == 0)
                {
                    if (!entry.HasUnnamedData &&
                        TryReadResidentDataSize(
                            attribute,
                            out long logicalSize))
                    {
                        entry.HasUnnamedData = true;
                        entry.IsResidentData = true;
                        entry.LogicalSize = logicalSize;
                        entry.AllocatedSize = 0;
                    }
                }
                else if (formCode == 1 &&
                         TryReadNonResidentDataExtent(
                             attribute,
                             attributeFlags,
                             bytesPerCluster,
                             out long lowestVcn,
                             out long logicalSize,
                             out long extentAllocatedSize,
                             out bool sparse,
                             out bool compressed,
                             out bool encrypted))
                {
                    bool wasResident =
                        entry.HasUnnamedData &&
                        entry.IsResidentData;

                    if (!entry.HasUnnamedData || wasResident)
                    {
                        entry.HasUnnamedData = true;
                        entry.IsResidentData = false;

                        if (wasResident)
                            entry.AllocatedSize = 0;
                    }

                    entry.AllocatedSize =
                        checked(
                            entry.AllocatedSize +
                            extentAllocatedSize);

                    entry.IsSparse |= sparse;
                    entry.IsCompressed |= compressed;
                    entry.IsEncrypted |= encrypted;

                    if (lowestVcn == 0)
                        entry.LogicalSize = logicalSize;
                }
            }

            offset += attributeLength;
        }
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

    private static bool TryReadResidentDataSize(
        ReadOnlySpan<byte> attribute,
        out long logicalSize)
    {
        logicalSize = 0;

        if (attribute.Length < 24)
            return false;

        logicalSize =
            BinaryPrimitives.ReadUInt32LittleEndian(
                attribute.Slice(16, 4));

        return true;
    }

    private static bool TryReadNonResidentDataExtent(
        ReadOnlySpan<byte> attribute,
        ushort attributeFlags,
        int bytesPerCluster,
        out long lowestVcn,
        out long logicalSize,
        out long extentAllocatedSize,
        out bool sparse,
        out bool compressed,
        out bool encrypted)
    {
        lowestVcn = 0;
        logicalSize = 0;
        extentAllocatedSize = 0;
        sparse = false;
        compressed = false;
        encrypted = false;

        if (attribute.Length < 64 ||
            bytesPerCluster <= 0)
        {
            return false;
        }

        lowestVcn =
            BinaryPrimitives.ReadInt64LittleEndian(
                attribute.Slice(16, 8));

        sparse =
            (attributeFlags & AttributeFlagSparse) != 0;

        compressed =
            (attributeFlags & AttributeFlagCompressionMask) != 0;

        encrypted =
            (attributeFlags & AttributeFlagEncrypted) != 0;

        if (lowestVcn == 0)
        {
            logicalSize =
                BinaryPrimitives.ReadInt64LittleEndian(
                    attribute.Slice(48, 8));

            if (logicalSize < 0)
                return false;
        }

        ushort mappingPairsOffset =
            BinaryPrimitives.ReadUInt16LittleEndian(
                attribute.Slice(32, 2));

        if (mappingPairsOffset >= attribute.Length)
            return false;

        if (!TryParseRunList(
                attribute.Slice(mappingPairsOffset),
                lowestVcn,
                out List<DataRun> runs))
        {
            return false;
        }

        long allocatedClusters = 0;

        foreach (DataRun run in runs)
        {
            if (!run.IsSparse)
            {
                allocatedClusters =
                    checked(
                        allocatedClusters +
                        run.ClusterCount);
            }
        }

        extentAllocatedSize =
            checked(
                allocatedClusters *
                (long)bytesPerCluster);

        return true;
    }

    private static bool TryReadFileName(
        ReadOnlySpan<byte> attribute,
        out string? name,
        out ulong parentRecord,
        out byte nameSpace)
    {
        name = null;
        parentRecord = 0;
        nameSpace = 0;

        if (attribute.Length < 24)
            return false;

        uint valueLengthRaw =
            BinaryPrimitives.ReadUInt32LittleEndian(
                attribute.Slice(16, 4));

        ushort valueOffset =
            BinaryPrimitives.ReadUInt16LittleEndian(
                attribute.Slice(20, 2));

        if (valueLengthRaw > int.MaxValue)
            return false;

        int valueLength = (int)valueLengthRaw;

        if (valueOffset + valueLength > attribute.Length ||
            valueLength < 66)
        {
            return false;
        }

        ReadOnlySpan<byte> value =
            attribute.Slice(
                valueOffset,
                valueLength);

        ulong parentReference =
            BinaryPrimitives.ReadUInt64LittleEndian(
                value.Slice(0, 8));

        parentRecord =
            GetRecordNumber(parentReference);

        byte fileNameLength = value[64];
        nameSpace = value[65];

        int nameBytes =
            checked(fileNameLength * 2);

        if (66 + nameBytes > value.Length)
            return false;

        name =
            Encoding.Unicode.GetString(
                value.Slice(66, nameBytes));

        return true;
    }

    private static int GetNamePriority(byte nameSpace)
    {
        return nameSpace switch
        {
            3 => 4,
            1 => 3,
            0 => 2,
            2 => 1,
            _ => 0
        };
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

    private static byte[] ReadMftBaseRecord(
        SafeFileHandle volumeHandle,
        NtfsVolumeDataBuffer volumeData,
        int bytesPerSector,
        int bytesPerCluster,
        int bytesPerRecord)
    {
        byte[] record = new byte[bytesPerRecord];

        long mftStartOffset =
            checked(
                volumeData.MftStartLcn *
                (long)bytesPerCluster);

        int bytesRead =
            RandomAccess.Read(
                volumeHandle,
                record.AsSpan(),
                mftStartOffset);

        if (bytesRead != bytesPerRecord)
        {
            throw new InvalidDataException(
                $"Unable to read the first MFT record. " +
                $"Read {bytesRead} of {bytesPerRecord} bytes.");
        }

        if (!HasFileSignature(record))
        {
            throw new InvalidDataException(
                "The first MFT record has no FILE signature.");
        }

        if (!ApplyUpdateSequenceFixup(
                record,
                bytesPerSector))
        {
            throw new InvalidDataException(
                "Unable to apply NTFS update sequence fixup to the MFT record.");
        }

        return record;
    }

    private static MftDataInfo GetMftRunList(
        ReadOnlySpan<byte> record)
    {
        ushort firstAttributeOffset =
            BinaryPrimitives.ReadUInt16LittleEndian(
                record.Slice(0x14, 2));

        uint bytesInUseRaw =
            BinaryPrimitives.ReadUInt32LittleEndian(
                record.Slice(0x18, 4));

        int bytesInUse =
            (int)Math.Min(
                bytesInUseRaw,
                (uint)record.Length);

        int offset = firstAttributeOffset;

        while (offset + 16 <= bytesInUse)
        {
            uint type =
                BinaryPrimitives.ReadUInt32LittleEndian(
                    record.Slice(offset, 4));

            if (type == AttributeEnd)
                break;

            uint lengthRaw =
                BinaryPrimitives.ReadUInt32LittleEndian(
                    record.Slice(offset + 4, 4));

            if (lengthRaw < 16 ||
                lengthRaw > int.MaxValue)
            {
                break;
            }

            int attributeLength = (int)lengthRaw;

            if (offset + attributeLength > bytesInUse)
                break;

            byte formCode = record[offset + 8];
            byte nameLength = record[offset + 9];

            if (type == AttributeTypeData &&
                formCode == 1 &&
                nameLength == 0)
            {
                if (attributeLength < 64)
                    break;

                long lowestVcn =
                    BinaryPrimitives.ReadInt64LittleEndian(
                        record.Slice(offset + 16, 8));

                long highestVcn =
                    BinaryPrimitives.ReadInt64LittleEndian(
                        record.Slice(offset + 24, 8));

                ushort mappingPairsOffset =
                    BinaryPrimitives.ReadUInt16LittleEndian(
                        record.Slice(offset + 32, 2));

                long fileSize =
                    BinaryPrimitives.ReadInt64LittleEndian(
                        record.Slice(offset + 48, 8));

                if (mappingPairsOffset >= attributeLength)
                    break;

                if (!TryParseRunList(
                        record.Slice(
                            offset + mappingPairsOffset,
                            attributeLength - mappingPairsOffset),
                        lowestVcn,
                        out List<DataRun> runs))
                {
                    break;
                }

                return new MftDataInfo(
                    lowestVcn,
                    highestVcn,
                    fileSize,
                    runs);
            }

            offset += attributeLength;
        }

        throw new InvalidDataException(
            "Unable to locate the unnamed nonresident $DATA attribute of $MFT.");
    }

    private static bool TryParseRunList(
        ReadOnlySpan<byte> mappingPairs,
        long startingVcn,
        out List<DataRun> runs)
    {
        runs = new List<DataRun>();

        int offset = 0;
        long currentVcn = startingVcn;
        long currentLcn = 0;

        while (offset < mappingPairs.Length)
        {
            byte header = mappingPairs[offset++];

            if (header == 0)
                return true;

            int lengthBytes = header & 0x0F;
            int offsetBytes = (header >> 4) & 0x0F;

            if (lengthBytes == 0 ||
                lengthBytes > 8 ||
                offsetBytes > 8 ||
                offset + lengthBytes + offsetBytes > mappingPairs.Length)
            {
                return false;
            }

            long clusterCount =
                ReadUnsignedLittleEndian(
                    mappingPairs.Slice(
                        offset,
                        lengthBytes));

            offset += lengthBytes;

            if (clusterCount <= 0)
                return false;

            bool isSparse = offsetBytes == 0;
            long startLcn = -1;

            if (!isSparse)
            {
                long lcnDelta =
                    ReadSignedLittleEndian(
                        mappingPairs.Slice(
                            offset,
                            offsetBytes));

                currentLcn =
                    checked(
                        currentLcn +
                        lcnDelta);

                startLcn = currentLcn;
            }

            offset += offsetBytes;

            runs.Add(
                new DataRun(
                    currentVcn,
                    clusterCount,
                    startLcn,
                    isSparse));

            currentVcn =
                checked(
                    currentVcn +
                    clusterCount);
        }

        return false;
    }

    private static NtfsVolumeDataBuffer GetNtfsVolumeData(
        SafeFileHandle volumeHandle)
    {
        int size =
            Marshal.SizeOf<NtfsVolumeDataBuffer>();

        IntPtr buffer =
            Marshal.AllocHGlobal(size);

        try
        {
            bool success =
                DeviceIoControl(
                    volumeHandle,
                    FsctlGetNtfsVolumeData,
                    IntPtr.Zero,
                    0,
                    buffer,
                    size,
                    out _,
                    IntPtr.Zero);

            if (!success)
            {
                int error = Marshal.GetLastWin32Error();

                throw new IOException(
                    $"Unable to read NTFS volume information. " +
                    $"Win32 error: {error}.");
            }

            return Marshal.PtrToStructure<NtfsVolumeDataBuffer>(
                buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool HasFileSignature(
        ReadOnlySpan<byte> record)
    {
        return record.Length >= 4 &&
               record[0] == (byte)'F' &&
               record[1] == (byte)'I' &&
               record[2] == (byte)'L' &&
               record[3] == (byte)'E';
    }

    private static bool ApplyUpdateSequenceFixup(
        Span<byte> record,
        int bytesPerSector)
    {
        if (record.Length < 8 ||
            bytesPerSector <= 0)
        {
            return false;
        }

        ushort usaOffset =
            BinaryPrimitives.ReadUInt16LittleEndian(
                record.Slice(4, 2));

        ushort usaCount =
            BinaryPrimitives.ReadUInt16LittleEndian(
                record.Slice(6, 2));

        if (usaCount < 2)
            return false;

        int usaBytes =
            checked(usaCount * 2);

        if (usaOffset + usaBytes > record.Length)
            return false;

        ushort sequenceNumber =
            BinaryPrimitives.ReadUInt16LittleEndian(
                record.Slice(usaOffset, 2));

        int sectorsInRecord =
            record.Length / bytesPerSector;

        if (usaCount != sectorsInRecord + 1)
            return false;

        for (int sectorIndex = 1;
             sectorIndex < usaCount;
             sectorIndex++)
        {
            int sectorEnd =
                checked(
                    sectorIndex *
                    bytesPerSector -
                    2);

            ushort onDiskValue =
                BinaryPrimitives.ReadUInt16LittleEndian(
                    record.Slice(sectorEnd, 2));

            if (onDiskValue != sequenceNumber)
                return false;

            ushort replacement =
                BinaryPrimitives.ReadUInt16LittleEndian(
                    record.Slice(
                        usaOffset +
                        sectorIndex * 2,
                        2));

            BinaryPrimitives.WriteUInt16LittleEndian(
                record.Slice(sectorEnd, 2),
                replacement);
        }

        return true;
    }

    private static string NormalizeDriveName(string driveName)
    {
        string trimmed = driveName.Trim();

        if (trimmed.Length == 1 &&
            char.IsLetter(trimmed[0]))
        {
            return $"{char.ToUpperInvariant(trimmed[0])}:\\";
        }

        if (trimmed.Length >= 2 &&
            char.IsLetter(trimmed[0]) &&
            trimmed[1] == ':')
        {
            return $"{char.ToUpperInvariant(trimmed[0])}:\\";
        }

        throw new ArgumentException(
            $"Unsupported drive name: {driveName}",
            nameof(driveName));
    }

    private static string ToExtendedPath(string path)
    {
        if (path.StartsWith(
                @"\\?\",
                StringComparison.Ordinal))
        {
            return path;
        }

        if (path.StartsWith(
                @"\\",
                StringComparison.Ordinal))
        {
            return @"\\?\UNC\" +
                   path.Substring(2);
        }

        return @"\\?\" + path;
    }

    private static bool TryGetAllocatedSizeFromWindows(
        string path,
        out long allocatedSize)
    {
        allocatedSize = 0;

        uint low = GetCompressedFileSizeW(
            path,
            out uint high);

        int error = Marshal.GetLastWin32Error();

        if (low == uint.MaxValue &&
            error != 0)
        {
            return false;
        }

        allocatedSize =
            checked(
                (long)(((ulong)high << 32) | low));

        return true;
    }

    private static ulong GetRecordNumber(
        ulong fileReference)
    {
        return fileReference &
               0x0000FFFFFFFFFFFFUL;
    }

    private static long ReadUnsignedLittleEndian(
        ReadOnlySpan<byte> bytes)
    {
        ulong value = 0;

        for (int i = 0; i < bytes.Length; i++)
        {
            value |=
                (ulong)bytes[i] <<
                (8 * i);
        }

        if (value > long.MaxValue)
            throw new OverflowException();

        return (long)value;
    }

    private static long ReadSignedLittleEndian(
        ReadOnlySpan<byte> bytes)
    {
        ulong value = 0;

        for (int i = 0; i < bytes.Length; i++)
        {
            value |=
                (ulong)bytes[i] <<
                (8 * i);
        }

        int bits = bytes.Length * 8;

        if (bits < 64 &&
            (bytes[^1] & 0x80) != 0)
        {
            value |=
                ulong.MaxValue << bits;
        }

        return unchecked((long)value);
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

    private sealed class ParseStats
    {
        public long FixupErrors;
    }

    private readonly record struct FolderAggregation(
        Dictionary<ulong, FolderStat> Stats,
        int UnresolvedNonEmptyFiles,
        long UnresolvedLogicalBytes,
        long UnresolvedAllocatedBytes);


    private readonly record struct FallbackFileTreeEntry(
        string ParentPath,
        string Name,
        long LogicalBytes,
        long AllocatedBytes);


    private readonly record struct DataRun(
        long StartVcn,
        long ClusterCount,
        long StartLcn,
        bool IsSparse);

    private readonly record struct MftDataInfo(
        long LowestVcn,
        long HighestVcn,
        long FileSize,
        List<DataRun> Runs);

    [StructLayout(LayoutKind.Sequential)]
    private struct NtfsVolumeDataBuffer
    {
        public long VolumeSerialNumber;
        public long NumberSectors;
        public long TotalClusters;
        public long FreeClusters;
        public long TotalReserved;

        public uint BytesPerSector;
        public uint BytesPerCluster;
        public uint BytesPerFileRecordSegment;
        public uint ClustersPerFileRecordSegment;

        public long MftValidDataLength;
        public long MftStartLcn;
        public long Mft2StartLcn;
        public long MftZoneStart;
        public long MftZoneEnd;
    }

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        int nInBufferSize,
        IntPtr lpOutBuffer,
        int nOutBufferSize,
        out int lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern uint GetCompressedFileSizeW(
        string lpFileName,
        out uint lpFileSizeHigh);


    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceW(
        string lpRootPathName,
        out uint lpSectorsPerCluster,
        out uint lpBytesPerSector,
        out uint lpNumberOfFreeClusters,
        out uint lpTotalNumberOfClusters);
}

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
