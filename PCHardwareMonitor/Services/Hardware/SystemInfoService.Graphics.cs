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
    // ВИДЕОКАРТЫ
    // ============================================================

    private static IReadOnlyList<GraphicsAdapterDetails>
        ReadGraphicsAdapters()
    {
        List<GraphicsAdapterDetails> adapters =
            new();

        try
        {
            using ManagementObjectSearcher searcher =
                new(
                    "SELECT " +
                    "Name, " +
                    "AdapterCompatibility, " +
                    "DriverVersion, " +
                    "DriverDate, " +
                    "PNPDeviceID, " +
                    "VideoProcessor " +
                    "FROM Win32_VideoController");

            using ManagementObjectCollection results =
                searcher.Get();

            foreach (ManagementObject item in results)
            {
                string? name =
                    ReadString(
                        item,
                        "Name");

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                adapters.Add(
                    new GraphicsAdapterDetails
                    {
                        Name = name,

                        Manufacturer =
                            ReadString(
                                item,
                                "AdapterCompatibility"),

                        DriverVersion =
                            ReadString(
                                item,
                                "DriverVersion"),

                        DriverDate =
                            ReadWmiDate(
                                item,
                                "DriverDate"),

                        PnpDeviceId =
                            ReadString(
                                item,
                                "PNPDeviceID"),

                        VideoProcessor =
                            ReadString(
                                item,
                                "VideoProcessor"),

                        // Win32_VideoController.AdapterRAM ненадёжен
                        // для современных видеокарт с большим объёмом VRAM.
                        // Поэтому здесь намеренно не подставляем потенциально
                        // неверное значение. Надёжный источник добавим отдельно.
                        DedicatedMemoryBytes = null
                    });
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Failure("SystemInfo.Graphics", ex);
            // Если WMI не отдаст видеокарты,
            // список останется пустым.
        }

        return adapters;
    }

}
