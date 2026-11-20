using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PCHardwareMonitor;

public sealed partial class SystemInfoService
{


    // ============================================================
    // МАТЕРИНСКАЯ ПЛАТА / BIOS
    // ============================================================

    private static MotherboardDetails ReadMotherboardInfo()
    {
        string? manufacturer = null;
        string? model = null;
        string? version = null;

        string? biosVersion = null;
        DateTime? biosDate = null;

        try
        {
            using ManagementObjectSearcher boardSearcher =
                new(
                    "SELECT " +
                    "Manufacturer, " +
                    "Product, " +
                    "Version " +
                    "FROM Win32_BaseBoard");

            using ManagementObjectCollection boardResults =
                boardSearcher.Get();

            foreach (ManagementObject item in boardResults)
            {
                manufacturer =
                    ReadString(
                        item,
                        "Manufacturer");

                model =
                    ReadString(
                        item,
                        "Product");

                version =
                    ReadString(
                        item,
                        "Version");

                break;
            }
        }
        catch
        {
            // Оставляем данные платы пустыми.
        }

        try
        {
            using ManagementObjectSearcher biosSearcher =
                new(
                    "SELECT " +
                    "SMBIOSBIOSVersion, " +
                    "Version, " +
                    "ReleaseDate " +
                    "FROM Win32_BIOS");

            using ManagementObjectCollection biosResults =
                biosSearcher.Get();

            foreach (ManagementObject item in biosResults)
            {
                biosVersion =
                    ReadString(
                        item,
                        "SMBIOSBIOSVersion");

                if (string.IsNullOrWhiteSpace(biosVersion))
                {
                    biosVersion =
                        ReadString(
                            item,
                            "Version");
                }

                biosDate =
                    ReadWmiDate(
                        item,
                        "ReleaseDate");

                break;
            }
        }
        catch
        {
            // Оставляем данные BIOS пустыми.
        }

        return new MotherboardDetails
        {
            Manufacturer = manufacturer,
            Model = model,
            Version = version,
            FirmwareType = ReadFirmwareType(),
            BiosVersion = biosVersion,
            BiosDate = biosDate
        };
    }


    private static string? ReadFirmwareType()
    {
        try
        {
            if (!GetFirmwareType(
                    out NativeFirmwareType firmwareType))
            {
                return null;
            }

            return firmwareType switch
            {
                NativeFirmwareType.Bios => "BIOS",
                NativeFirmwareType.Uefi => "UEFI",
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

}
