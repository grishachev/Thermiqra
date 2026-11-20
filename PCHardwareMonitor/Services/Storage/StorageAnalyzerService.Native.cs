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
                    SettingsService.L($"Не удалось прочитать сведения о томе NTFS. Код ошибки Windows: {error}.",
                        $"Unable to read NTFS volume information. Win32 error: {error}."));
            }

            return Marshal.PtrToStructure<NtfsVolumeDataBuffer>(
                buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
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
