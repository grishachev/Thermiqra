using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PCHardwareMonitor;

// Read-only, on-demand retrieval of the standard NVMe SMART/Health log (02h).
// The caller verifies MSFT_Disk.BusType == NVMe and identifies the Windows
// disk number before opening \\.\PhysicalDriveN. A failed/unsupported IOCTL
// is unknown data, never a synthetic zero/healthy status.
internal static class StorageNvmeHealthReader
{
    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private const uint StorageDeviceProtocolSpecificProperty = 50;
    private const uint ProtocolTypeNvme = 3;
    private const uint NvmeDataTypeLogPage = 2;
    private const uint NvmeLogPageHealthInfo = 2;

    private const int QueryHeaderBytes = 8;
    private const int ProtocolDataBytes = 40;
    private const int DescriptorBytes = QueryHeaderBytes + ProtocolDataBytes;
    private const int HealthLogBytes = 512;
    private const int BufferBytes = DescriptorBytes + HealthLogBytes;

    internal static StorageNvmeHealthDetails? TryRead(uint physicalDiskNumber)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            // The property query has FILE_ANY_ACCESS; no write access is
            // requested, and this code never issues a device-changing command.
            using SafeFileHandle handle = CreateFile(
                @"\\.\PhysicalDrive" + physicalDiskNumber,
                0, 0x00000001 | 0x00000002,
                IntPtr.Zero, 3, 0, IntPtr.Zero);

            if (handle.IsInvalid)
                return null;

            byte[] request = new byte[BufferBytes];
            WriteUInt32(request, 0, StorageDeviceProtocolSpecificProperty);
            // PropertyStandardQuery = 0 at offset 4.
            WriteUInt32(request, 8, ProtocolTypeNvme);
            WriteUInt32(request, 12, NvmeDataTypeLogPage);
            WriteUInt32(request, 16, NvmeLogPageHealthInfo);
            WriteUInt32(request, 24, ProtocolDataBytes);
            WriteUInt32(request, 28, HealthLogBytes);

            byte[] response = new byte[BufferBytes];
            if (!DeviceIoControl(
                    handle, IoctlStorageQueryProperty,
                    request, (uint)request.Length,
                    response, (uint)response.Length,
                    out uint bytesReturned, IntPtr.Zero))
            {
                return null;
            }

            if (bytesReturned < DescriptorBytes ||
                ReadUInt32(response, 0) != DescriptorBytes ||
                ReadUInt32(response, 4) != DescriptorBytes ||
                ReadUInt32(response, 8) != ProtocolTypeNvme ||
                ReadUInt32(response, 12) != NvmeDataTypeLogPage)
            {
                return null;
            }

            uint relativeOffset = ReadUInt32(response, 24);
            uint dataLength = ReadUInt32(response, 28);
            ulong absoluteOffset = QueryHeaderBytes + (ulong)relativeOffset;
            if (relativeOffset < ProtocolDataBytes ||
                dataLength < HealthLogBytes ||
                absoluteOffset + HealthLogBytes > bytesReturned ||
                absoluteOffset + HealthLogBytes > (ulong)response.Length)
            {
                return null;
            }

            ReadOnlySpan<byte> log = response.AsSpan(
                checked((int)absoluteOffset), HealthLogBytes);

            // Sanity-check the NVMe layout before interpreting any zero count.
            ushort kelvin = BinaryPrimitives.ReadUInt16LittleEndian(
                log.Slice(1, 2));
            if (kelvin is < 200 or > 500 ||
                log[3] > 100 || log[4] > 100)
            {
                return null;
            }

            return new StorageNvmeHealthDetails
            {
                CriticalWarning = log[0],
                // NVMe SMART/Health composite temperature (Kelvin),
                // converted to whole degrees Celsius for the UI.
                CompositeTemperatureCelsius = kelvin - 273,
                // Data Units Read/Written: 1,000 blocks of 512 bytes per unit.
                DataUnitsRead = ReadUInt128Counter(log, 32),
                DataUnitsWritten = ReadUInt128Counter(log, 48),
                AvailableSparePercent = log[3],
                AvailableSpareThresholdPercent = log[4],
                PercentageUsed = log[5],
                MediaErrors = ReadUInt128Counter(log, 160),
                ErrorLogEntries = ReadUInt128Counter(log, 176),
                PowerOnHours = ReadUInt128Counter(log, 128),
                UnsafeShutdowns = ReadUInt128Counter(log, 144),
                PowerCycles = ReadUInt128Counter(log, 112)
            };
        }
        catch (Exception ex) when (
            ex is System.IO.IOException or UnauthorizedAccessException or
            System.ComponentModel.Win32Exception or
            System.ArgumentException or OverflowException or
            DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    private static BigInteger ReadUInt128Counter(
        ReadOnlySpan<byte> log, int offset) =>
        new(log.Slice(offset, 16), isUnsigned: true, isBigEndian: false);

    private static void WriteUInt32(byte[] buffer, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(
            buffer.AsSpan(offset, 4), value);

    private static uint ReadUInt32(byte[] buffer, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(offset, 4));

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName, uint desiredAccess, uint shareMode,
        IntPtr securityAttributes, uint creationDisposition,
        uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle deviceHandle, uint controlCode,
        [In] byte[] inputBuffer, uint inputBufferSize,
        [Out] byte[] outputBuffer, uint outputBufferSize,
        out uint bytesReturned, IntPtr overlapped);
}

public sealed class StorageNvmeHealthDetails
{
    // These are NVMe SMART/Health log fields, not a health score.
    public byte CriticalWarning { get; init; }
    public int CompositeTemperatureCelsius { get; init; }
    // These are raw NVMe data-unit counters; multiply by 512,000 bytes.
    public BigInteger DataUnitsRead { get; init; }
    public BigInteger DataUnitsWritten { get; init; }
    public byte AvailableSparePercent { get; init; }
    public byte AvailableSpareThresholdPercent { get; init; }
    public byte PercentageUsed { get; init; }
    public BigInteger MediaErrors { get; init; }
    public BigInteger ErrorLogEntries { get; init; }
    public BigInteger PowerOnHours { get; init; }
    public BigInteger UnsafeShutdowns { get; init; }
    public BigInteger PowerCycles { get; init; }
}
