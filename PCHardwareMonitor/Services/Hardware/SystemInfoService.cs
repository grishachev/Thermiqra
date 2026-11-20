using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PCHardwareMonitor;

public sealed partial class SystemInfoService
{
    public SystemInformationSnapshot GetSnapshot()
    {
        return new SystemInformationSnapshot
        {
            Windows = ReadWindowsInfo(),
            Processor = ReadProcessorInfo(),
            Motherboard = ReadMotherboardInfo(),
            Memory = ReadMemoryInfo(),
            GraphicsAdapters = ReadGraphicsAdapters(),
            StorageDevices = ReadStorageDevices()
        };
    }


    private static string? NormalizeOsArchitecture(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        string text =
            value.Trim();

        if (text.Contains(
                "64",
                StringComparison.OrdinalIgnoreCase))
        {
            return SettingsService.L(
                "64-разрядная",
                "64-bit");
        }

        if (text.Contains(
                "32",
                StringComparison.OrdinalIgnoreCase) ||
            text.Contains(
                "x86",
                StringComparison.OrdinalIgnoreCase))
        {
            return SettingsService.L(
                "32-разрядная",
                "32-bit");
        }

        return text;
    }


    // ============================================================
    // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ WMI
    // ============================================================

    private static string? ReadString(
        ManagementBaseObject item,
        string propertyName)
    {
        try
        {
            object? value =
                item[propertyName];

            if (value == null)
                return null;

            string? text =
                Convert.ToString(
                    value,
                    CultureInfo.InvariantCulture);

            return CleanText(text);
        }
        catch
        {
            return null;
        }
    }


    private static ushort? ReadUInt16(
        ManagementBaseObject item,
        string propertyName)
    {
        try
        {
            object? value =
                item[propertyName];

            if (value == null)
                return null;

            return Convert.ToUInt16(
                value,
                CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }


    private static uint? ReadUInt32(
        ManagementBaseObject item,
        string propertyName)
    {
        try
        {
            object? value =
                item[propertyName];

            if (value == null)
                return null;

            return Convert.ToUInt32(
                value,
                CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }


    private static uint? ReadPositiveUInt32(
        ManagementBaseObject item,
        string propertyName)
    {
        uint? value =
            ReadUInt32(
                item,
                propertyName);

        if (!value.HasValue ||
            value.Value == 0)
        {
            return null;
        }

        return value;
    }


    private static ulong? ReadUInt64(
        ManagementBaseObject item,
        string propertyName)
    {
        try
        {
            object? value =
                item[propertyName];

            if (value == null)
                return null;

            return Convert.ToUInt64(
                value,
                CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }


    private static DateTime? ReadWmiDate(
        ManagementBaseObject item,
        string propertyName)
    {
        string? value =
            ReadString(
                item,
                propertyName);

        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            return ManagementDateTimeConverter
                .ToDateTime(value);
        }
        catch
        {
            return null;
        }
    }


    private static string? CleanText(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string result =
            value.Trim();

        if (result.Equals(
                "To Be Filled By O.E.M.",
                StringComparison.OrdinalIgnoreCase) ||
            result.Equals(
                "Default string",
                StringComparison.OrdinalIgnoreCase) ||
            result.Equals(
                "Unknown",
                StringComparison.OrdinalIgnoreCase) ||
            result.Equals(
                "Not Specified",
                StringComparison.OrdinalIgnoreCase) ||
            result.Equals(
                "None",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return result;
    }


    // ============================================================
    // WINDOWS API
    // ============================================================

    private enum NativeFirmwareType : uint
    {
        Unknown = 0,
        Bios = 1,
        Uefi = 2,
        Maximum = 3
    }


    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFirmwareType(
        out NativeFirmwareType firmwareType);
}
