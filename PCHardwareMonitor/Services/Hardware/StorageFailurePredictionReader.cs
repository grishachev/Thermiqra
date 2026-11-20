using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace PCHardwareMonitor;

internal sealed record StorageFailurePredictionResult(bool? PredictFailure, byte[]? AtaData, string Diagnostic,
    ushort? ConfirmedBus = null);

// On-demand read-only alternative to MSStorageDriver WMI. No SMART enable,
// self-test, raw ATA command or device-changing IOCTL is issued here.
internal static class StorageFailurePredictionReader
{
    private const uint QueryProperty = 0x002D1400;
    private const uint PredictFailure = 0x002D1100;

    internal static StorageFailurePredictionResult Read(StorageDeviceDetails device)
    {
        StorageFailurePredictionResult Unavailable(string detail) => new(null, null, detail);
        if (string.IsNullOrWhiteSpace(device.DeviceId) || device.PhysicalDiskIndex is not uint number ||
            !string.Equals(device.DeviceId, @"\\.\PHYSICALDRIVE" + number, StringComparison.OrdinalIgnoreCase))
            return Unavailable("Device identity is not a confirmed PhysicalDrive number.");
        try
        {
            using SafeFileHandle handle = CreateFile(device.DeviceId, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (handle.IsInvalid) return Unavailable("Open: Win32 " + Marshal.GetLastWin32Error());
            byte[] descriptor = new byte[4096];
            if (!DeviceIoControl(handle, QueryProperty, new byte[12], 12, descriptor,
                    (uint)descriptor.Length, out uint descriptorLength, IntPtr.Zero))
                return Unavailable("StorageDeviceProperty: Win32 " + Marshal.GetLastWin32Error());
            if (descriptorLength < 36 || descriptorLength > descriptor.Length ||
                Read32(descriptor, 0) < 36 || Read32(descriptor, 4) > descriptorLength)
                return Unavailable("StorageDeviceProperty: incomplete descriptor.");
            uint bus = Read32(descriptor, 28);
            // The vendor buffer is decoded as ATA only for native ATA/SATA.
            // A legacy Win32 InterfaceType=SCSI alone is not a bus identity.
            if (bus is not (3 or 11)) return Unavailable("Native bus " + bus + ": ATA decoding excluded.");
            string? serial = ReadText(descriptor, descriptorLength, Read32(descriptor, 24));
            string? vendor = ReadText(descriptor, descriptorLength, Read32(descriptor, 12));
            string? product = ReadText(descriptor, descriptorLength, Read32(descriptor, 16));
            if (!string.IsNullOrWhiteSpace(device.SerialNumber))
            {
                if (!string.Equals(device.SerialNumber.Trim(), serial?.Trim(), StringComparison.OrdinalIgnoreCase))
                    return Unavailable("StorageDeviceProperty: serial identity could not be confirmed.");
            }
            else
            {
                string model = (device.Model ?? "").Trim();
                if (model.Length == 0 || (!model.Equals(product?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    !model.Equals((vendor + " " + product).Trim(), StringComparison.OrdinalIgnoreCase)))
                    return Unavailable("StorageDeviceProperty: model identity could not be confirmed.");
            }
            byte[] output = new byte[516];
            if (!DeviceIoControl(handle, PredictFailure, Array.Empty<byte>(), 0, output,
                    (uint)output.Length, out uint returned, IntPtr.Zero))
                return Unavailable("IOCTL_STORAGE_PREDICT_FAILURE: Win32 " + Marshal.GetLastWin32Error());
            if (returned != output.Length)
                return Unavailable("IOCTL_STORAGE_PREDICT_FAILURE: incomplete response.");
            bool failure = Read32(output, 0) != 0;
            byte[] sector = output.AsSpan(4, 512).ToArray();
            int checksum = 0;
            foreach (byte value in sector) checksum = (checksum + value) & 0xFF;
            ushort revision = BinaryPrimitives.ReadUInt16LittleEndian(sector);
            bool valid = revision is not (0 or ushort.MaxValue) && checksum == 0;
            return new(failure, valid ? sector : null, valid
                ? "IOCTL_STORAGE_PREDICT_FAILURE: success; ATA sector checksum valid."
                : "IOCTL_STORAGE_PREDICT_FAILURE: status read; ATA sector format/checksum unconfirmed.", (ushort)bus);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
            System.ComponentModel.Win32Exception or DllNotFoundException or EntryPointNotFoundException)
        { return Unavailable(ex.GetType().Name + ": " + ex.Message); }
    }

    private static uint Read32(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));

    private static string? ReadText(byte[] data, uint length, uint offset)
    {
        if (offset < 36 || offset >= length) return null;
        int start = (int)offset, end = start;
        while (end < length && data[end] != 0) end++;
        return end < length ? Encoding.ASCII.GetString(data, start, end - start).Trim() : null;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint sharing,
        IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint code,
        [In] byte[] input, uint inputLength, [Out] byte[] output, uint outputLength,
        out uint returned, IntPtr overlapped);
}
