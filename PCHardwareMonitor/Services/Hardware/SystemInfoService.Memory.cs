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
    // ОПЕРАТИВНАЯ ПАМЯТЬ
    // ============================================================

    private static MemoryDetails ReadMemoryInfo()
    {
        ulong? totalBytes =
            ReadTotalPhysicalMemory();

        uint? physicalSlotCount =
            ReadPhysicalMemorySlotCount();

        List<MemoryModuleDetails> modules =
            new();

        try
        {
            using ManagementObjectSearcher searcher =
                new(
                    "SELECT " +
                    "BankLabel, " +
                    "DeviceLocator, " +
                    "Capacity, " +
                    "Manufacturer, " +
                    "PartNumber, " +
                    "SerialNumber, " +
                    "Speed, " +
                    "ConfiguredClockSpeed, " +
                    "SMBIOSMemoryType, " +
                    "ConfiguredVoltage, " +
                    "FormFactor, " +
                    "DataWidth, " +
                    "TotalWidth " +
                    "FROM Win32_PhysicalMemory");

            using ManagementObjectCollection results =
                searcher.Get();

            foreach (ManagementObject item in results)
            {
                string? slot =
                    ReadString(
                        item,
                        "DeviceLocator");

                if (string.IsNullOrWhiteSpace(slot))
                {
                    slot =
                        ReadString(
                            item,
                            "BankLabel");
                }

                uint? configuredSpeed =
                    ReadPositiveUInt32(
                        item,
                        "ConfiguredClockSpeed");

                uint? reportedSpeed =
                    ReadPositiveUInt32(
                        item,
                        "Speed");

                modules.Add(
                    new MemoryModuleDetails
                    {
                        Slot = slot,

                        CapacityBytes =
                            ReadUInt64(
                                item,
                                "Capacity"),

                        Manufacturer =
                            ReadString(
                                item,
                                "Manufacturer"),

                        PartNumber =
                            ReadString(
                                item,
                                "PartNumber"),

                        SerialNumber =
                            ReadString(
                                item,
                                "SerialNumber"),

                        FrequencyMhz =
                            configuredSpeed ??
                            reportedSpeed,

                        ReportedSpeedMhz =
                            reportedSpeed,

                        ConfiguredSpeedMhz =
                            configuredSpeed,

                        SmbiosMemoryType =
                            ReadUInt16(
                                item,
                                "SMBIOSMemoryType"),

                        ConfiguredVoltageMv =
                            ReadPositiveUInt32(
                                item,
                                "ConfiguredVoltage"),

                        FormFactor =
                            ReadUInt16(
                                item,
                                "FormFactor"),

                        DataWidthBits =
                            ReadUInt16(
                                item,
                                "DataWidth"),

                        TotalWidthBits =
                            ReadUInt16(
                                item,
                                "TotalWidth")
                    });
            }
        }
        catch
        {
            // Если WMI не отдаст модули,
            // список останется пустым.
        }

        if (!totalBytes.HasValue &&
            modules.Count > 0)
        {
            ulong sum = 0;

            foreach (MemoryModuleDetails module in modules)
            {
                if (module.CapacityBytes.HasValue)
                {
                    sum +=
                        module.CapacityBytes.Value;
                }
            }

            if (sum > 0)
                totalBytes = sum;
        }

        return new MemoryDetails
        {
            TotalBytes = totalBytes,
            PhysicalSlotCount = physicalSlotCount,
            Modules = modules
        };
    }


    private static uint? ReadPhysicalMemorySlotCount()
    {
        try
        {
            using ManagementObjectSearcher searcher =
                new(
                    "SELECT " +
                    "MemoryDevices " +
                    "FROM Win32_PhysicalMemoryArray");

            using ManagementObjectCollection results =
                searcher.Get();

            uint totalSlots = 0;
            bool found = false;

            foreach (ManagementObject item in results)
            {
                uint? slots =
                    ReadPositiveUInt32(
                        item,
                        "MemoryDevices");

                if (!slots.HasValue)
                    continue;

                if (uint.MaxValue - totalSlots <
                    slots.Value)
                {
                    return null;
                }

                totalSlots +=
                    slots.Value;

                found = true;
            }

            return found &&
                   totalSlots > 0
                ? totalSlots
                : null;
        }
        catch
        {
            return null;
        }
    }


    private static ulong? ReadTotalPhysicalMemory()
    {
        try
        {
            using ManagementObjectSearcher searcher =
                new(
                    "SELECT " +
                    "TotalPhysicalMemory " +
                    "FROM Win32_ComputerSystem");

            using ManagementObjectCollection results =
                searcher.Get();

            foreach (ManagementObject item in results)
            {
                return ReadUInt64(
                    item,
                    "TotalPhysicalMemory");
            }
        }
        catch
        {
        }

        return null;
    }

}
