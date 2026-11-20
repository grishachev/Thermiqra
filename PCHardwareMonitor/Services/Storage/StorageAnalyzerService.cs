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
        CancellationToken cancellationToken = default,
        IProgress<StorageAnalysisProgress>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(driveName))
            throw new ArgumentException(SettingsService.L("Не указано имя диска.", "Drive name is required."), nameof(driveName));

        return Task.Run(
            () => AnalyzeCore(driveName, cancellationToken, progress),
            cancellationToken);
    }

    private static StorageAnalysisResult AnalyzeCore(
        string driveName,
        CancellationToken cancellationToken,
        IProgress<StorageAnalysisProgress>? progress)
    {
        string normalizedDrive = NormalizeDriveName(driveName);
        DriveInfo drive = new(normalizedDrive);

        if (!drive.IsReady)
            throw new IOException(SettingsService.L($"Диск {normalizedDrive} не готов.", $"Drive {normalizedDrive} is not ready."));

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
                cancellationToken,
                progress);
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
                SettingsService.L($"Не удалось открыть том NTFS {driveLetter}: для чтения. Код ошибки Windows: {error}.",
                    $"Unable to open NTFS volume {driveLetter}: for reading. Win32 error: {error}."));
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
                SettingsService.L("Неподдерживаемый размер файловой записи NTFS.", "Unsupported NTFS file record size."));
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

        // The known amount of MFT data to read, not an estimate of
        // the duration of the entire analysis. Sparse runs contain no records.
        long totalRecordsToRead = 0;

        foreach (DataRun run in mftData.Runs)
        {
            if (!run.IsSparse)
            {
                totalRecordsToRead = SafeAdd(
                    totalRecordsToRead,
                    checked(run.ClusterCount * (long)bytesPerCluster) /
                        bytesPerRecord);
            }
        }

        long processedRecords = 0;
        long lastProgressMs = -250;

        progress?.Report(new StorageAnalysisProgress(
            StorageAnalysisStage.NtfsRecords,
            0, 0, 0, totalRecordsToRead));

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
                processedRecords = SafeAdd(
                    processedRecords,
                    completeBytes / bytesPerRecord);

                long elapsedMs = totalTimer.ElapsedMilliseconds;
                if (elapsedMs - lastProgressMs >= 150)
                {
                    progress?.Report(new StorageAnalysisProgress(
                        StorageAnalysisStage.NtfsRecords,
                        stats.FilesFound,
                        stats.FoldersFound,
                        processedRecords,
                        totalRecordsToRead));
                    lastProgressMs = elapsedMs;
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        progress?.Report(new StorageAnalysisProgress(
            StorageAnalysisStage.ProcessingEntries,
            stats.FilesFound,
            stats.FoldersFound,
            processedRecords,
            totalRecordsToRead));

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

        progress?.Report(new StorageAnalysisProgress(
            StorageAnalysisStage.BuildingTree,
            files.Count,
            directories.Count,
            processedRecords,
            totalRecordsToRead));

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
            SettingsService.L($"Неподдерживаемое имя диска: {driveName}", $"Unsupported drive name: {driveName}"),
            nameof(driveName));
    }
}
