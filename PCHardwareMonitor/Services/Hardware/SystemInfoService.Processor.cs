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
    // ПРОЦЕССОР
    // ============================================================

    private static ProcessorDetails ReadProcessorInfo()
    {
        ProcessorDetails result =
            new();

        try
        {
            using ManagementObjectSearcher searcher =
                new(
                    "SELECT " +
                    "Manufacturer, " +
                    "Name, " +
                    "Architecture, " +
                    "NumberOfCores, " +
                    "NumberOfLogicalProcessors, " +
                    "CurrentClockSpeed, " +
                    "MaxClockSpeed, " +
                    "SocketDesignation, " +
                    "L2CacheSize, " +
                    "L3CacheSize " +
                    "FROM Win32_Processor");

            using ManagementObjectCollection results =
                searcher.Get();

            foreach (ManagementObject item in results)
            {
                ushort? architectureCode =
                    ReadUInt16(
                        item,
                        "Architecture");

                string? architecture =
                    FormatProcessorArchitecture(
                        architectureCode);

                if (string.IsNullOrWhiteSpace(architecture))
                {
                    architecture =
                        RuntimeInformation
                            .ProcessArchitecture
                            .ToString();
                }

                result =
                    new ProcessorDetails
                    {
                        Manufacturer =
                            ReadString(
                                item,
                                "Manufacturer"),

                        Name =
                            ReadString(
                                item,
                                "Name"),

                        Architecture =
                            architecture,

                        PhysicalCores =
                            ReadPositiveUInt32(
                                item,
                                "NumberOfCores"),

                        LogicalProcessors =
                            ReadPositiveUInt32(
                                item,
                                "NumberOfLogicalProcessors"),

                        CurrentClockSpeedMhz =
                            ReadPositiveUInt32(
                                item,
                                "CurrentClockSpeed"),

                        MaxClockSpeedMhz =
                            ReadPositiveUInt32(
                                item,
                                "MaxClockSpeed"),

                        Socket =
                            ReadString(
                                item,
                                "SocketDesignation"),

                        L2CacheKb =
                            ReadPositiveUInt32(
                                item,
                                "L2CacheSize"),

                        L3CacheKb =
                            ReadPositiveUInt32(
                                item,
                                "L3CacheSize")
                    };

                break;
            }
        }
        catch
        {
            // Возвращаем пустой объект.
        }

        return result;
    }


    private static string? FormatProcessorArchitecture(
        ushort? architecture)
    {
        if (!architecture.HasValue)
            return null;

        return architecture.Value switch
        {
            0 => "x86",
            1 => "MIPS",
            2 => "Alpha",
            3 => "PowerPC",
            5 => "ARM",
            6 => "IA64",
            9 => "x64",
            12 => "ARM64",
            _ => $"Код {architecture.Value}"
        };
    }

}
