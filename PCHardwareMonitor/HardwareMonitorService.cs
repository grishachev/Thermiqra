using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LibreHardwareMonitor.Hardware;
using RAMSPDToolkit.I2CSMBus;
using RAMSPDToolkit.SPD;
using RAMSPDToolkit.SPD.Interop.Shared;

namespace PCHardwareMonitor;

public sealed class HardwareMonitorService : IDisposable
{
    private readonly Computer _computer;

    public static HardwareSnapshot? LastSnapshot { get; private set; }
    private readonly Dictionary<string, MemorySpdXmpCacheEntry> _spdXmpCache =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _spdXmpCacheInitialized;
    public HardwareMonitorService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsStorageEnabled = true,
            IsMotherboardEnabled = true,
            IsControllerEnabled = true,
            IsNetworkEnabled = false
        };

        _computer.Open();
    }

    public HardwareSnapshot GetSnapshot()
    {
        HardwareSnapshot snapshot = new();

        foreach (IHardware hardware in _computer.Hardware)
        {
            hardware.Update();

            foreach (IHardware subHardware in hardware.SubHardware)
            {
                subHardware.Update();
            }

            ReadCpu(hardware, snapshot);
            ReadGpu(hardware, snapshot);
            ReadMemory(hardware, snapshot);
            ReadStorage(hardware, snapshot);
        }

        ReadLogicalDrives(snapshot);

        EnsureSpdXmpCache();
        ApplySpdXmpCache(snapshot);
        LastSnapshot =
            snapshot;

        return snapshot;
    }

    private static void ReadCpu(
        IHardware hardware,
        HardwareSnapshot snapshot)
    {
        if (hardware.HardwareType != HardwareType.Cpu)
            return;

        snapshot.Cpu.Name = hardware.Name;

        snapshot.Cpu.Temperature = FindSensor(
            hardware,
            SensorType.Temperature,
            "Core (Tctl/Tdie)",
            "CPU Package",
            "Core Max"
        );

        snapshot.Cpu.Load = FindSensor(
            hardware,
            SensorType.Load,
            "CPU Total"
        );
    }

    private static void ReadGpu(
        IHardware hardware,
        HardwareSnapshot snapshot)
    {
        bool isGpu =
            hardware.HardwareType == HardwareType.GpuNvidia ||
            hardware.HardwareType == HardwareType.GpuAmd ||
            hardware.HardwareType == HardwareType.GpuIntel;

        if (!isGpu)
            return;

        GpuInfo gpu = new()
        {
            Id = hardware.Identifier.ToString(),
            Name = hardware.Name,

            Temperature = FindSensor(
                hardware,
                SensorType.Temperature,
                "GPU Core"
            ),

            HotSpotTemperature = FindSensor(
                hardware,
                SensorType.Temperature,
                "GPU Hot Spot"
            ),

            MemoryTemperature = FindSensor(
                hardware,
                SensorType.Temperature,
                "GPU Memory Junction",
                "GPU Memory"
            ),

            Load = FindSensor(
                hardware,
                SensorType.Load,
                "GPU Core"
            ),

            MemoryLoad = FindSensor(
                hardware,
                SensorType.Load,
                "GPU Memory"
            )
        };

        snapshot.Gpus.Add(gpu);
    }

    private static void ReadMemory(
        IHardware hardware,
        HardwareSnapshot snapshot)
    {
        if (hardware.HardwareType != HardwareType.Memory)
            return;

        float? load = FindSensor(
            hardware,
            SensorType.Load,
            "Memory"
        );

        float? used = FindSensor(
            hardware,
            SensorType.Data,
            "Memory Used"
        );

        float? available = FindSensor(
            hardware,
            SensorType.Data,
            "Memory Available"
        );

        if (load.HasValue)
            snapshot.Memory.Load = load;

        if (used.HasValue)
            snapshot.Memory.UsedGb = used;

        if (available.HasValue)
            snapshot.Memory.AvailableGb = available;

        List<MemorySpdTimingInfo> timings =
            hardware.Sensors
                .Where(sensor =>
                    sensor.SensorType == SensorType.Timing &&
                    sensor.Value.HasValue)
                .OrderBy(sensor => sensor.Index)
                .Select(sensor => new MemorySpdTimingInfo
                {
                    Name = sensor.Name,
                    ValueNanoseconds = sensor.Value!.Value
                })
                .ToList();

        if (timings.Count == 0)
            return;

        float? capacityGb = FindSensor(
            hardware,
            SensorType.Data,
            "Capacity"
        );

        snapshot.MemorySpdModules.Add(
            new MemorySpdModuleInfo
            {
                Id = hardware.Identifier.ToString(),
                Name = hardware.Name,
                CapacityGb = capacityGb,
                Timings = timings
            }
        );
    }

    private static void ReadStorage(
        IHardware hardware,
        HardwareSnapshot snapshot)
    {
        if (hardware.HardwareType != HardwareType.Storage)
            return;

        float? temperature = FindSensor(
            hardware,
            SensorType.Temperature,
            "Composite Temperature",
            "Temperature"
        );

        if (!temperature.HasValue)
        {
            ISensor? fallback =
                hardware.Sensors.FirstOrDefault(sensor =>
                    sensor.SensorType == SensorType.Temperature &&
                    sensor.Value.HasValue &&
                    !sensor.Name.Contains(
                        "Warning",
                        StringComparison.OrdinalIgnoreCase) &&
                    !sensor.Name.Contains(
                        "Critical",
                        StringComparison.OrdinalIgnoreCase));

            temperature = fallback?.Value;
        }

        snapshot.StorageDevices.Add(
            new StorageDeviceInfo
            {
                Id = hardware.Identifier.ToString(),
                Name = hardware.Name,
                Temperature = temperature
            }
        );
    }

    private static void ReadLogicalDrives(
        HardwareSnapshot snapshot)
    {
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady)
                    continue;

                if (drive.DriveType != DriveType.Fixed)
                    continue;

                snapshot.Drives.Add(
                    new LogicalDriveInfo
                    {
                        Name = drive.Name,
                        VolumeLabel = drive.VolumeLabel,
                        TotalBytes = drive.TotalSize,
                        FreeBytes = drive.AvailableFreeSpace
                    }
                );
            }
            catch
            {
                // Недоступный диск просто пропускаем.
            }
        }
    }

    private static float? FindSensor(
        IHardware hardware,
        SensorType type,
        params string[] names)
    {
        foreach (string name in names)
        {
            foreach (ISensor sensor in hardware.Sensors)
            {
                if (sensor.SensorType != type)
                    continue;

                if (!string.Equals(
                        sensor.Name,
                        name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (sensor.Value.HasValue)
                    return sensor.Value.Value;
            }
        }

        return null;
    }

    private void EnsureSpdXmpCache()
    {
        if (_spdXmpCacheInitialized)
            return;

        if (SMBusManager.RegisteredSMBuses.Count == 0)
            return;

        try
        {
            foreach (SMBusInterface bus in SMBusManager.RegisteredSMBuses)
            {
                for (byte address = SPDConstants.SPD_BEGIN;
                     address <= SPDConstants.SPD_END;
                     address++)
                {
                    SPDDetector detector =
                        new(bus, address);

                    if (!detector.IsValid)
                        continue;

                    if (detector.Accessor is not DDR4Accessor ddr4)
                        continue;

                    byte[] header = new byte[9];

                    for (int i = 0; i < header.Length; i++)
                    {
                        header[i] =
                            ddr4.At((ushort)(384 + i));
                    }

                    bool hasXmp20 =
                        header[0] == 0x0C &&
                        header[1] == 0x4A &&
                        header[3] == 0x20;

                    string hardwareId =
                        $"/memory/dimm/{ddr4.Index}";

                    MemorySpdXmpCacheEntry cacheEntry = new()
                    {
                        HardwareId = hardwareId,
                        HasXmp20 = hasXmp20,
                        Jedec = ReadDdr4JedecInfo(ddr4)
                    };

                    if (hasXmp20)
                    {
                        byte profileEnabled =
                            header[2];

                        if ((profileEnabled & 0x01) != 0)
                        {
                            MemoryXmpProfileInfo? profile1 =
                                ReadDdr4XmpProfile(
                                    ddr4,
                                    profileNumber: 1,
                                    startAddress: 393);

                            if (profile1 != null)
                            {
                                cacheEntry.Profiles.Add(
                                    profile1);
                            }
                        }

                        if ((profileEnabled & 0x02) != 0)
                        {
                            MemoryXmpProfileInfo? profile2 =
                                ReadDdr4XmpProfile(
                                    ddr4,
                                    profileNumber: 2,
                                    startAddress: 440);

                            if (profile2 != null)
                            {
                                cacheEntry.Profiles.Add(
                                    profile2);
                            }
                        }
                    }

                    _spdXmpCache[hardwareId] =
                        cacheEntry;
                }
            }

            _spdXmpCacheInitialized = true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine(
                $"Thermiqra SPD/XMP cache error: " +
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static MemoryJedecInfo ReadDdr4JedecInfo(
        DDR4Accessor ddr4)
    {
        decimal minimumCycleTimeNs =
            ddr4.SDRAMTimings.MinimumCycleTime;

        int maximumDataRateMt =
            minimumCycleTimeNs > 0
                ? RoundDdrDataRate(
                    2000.0 /
                    (double)minimumCycleTimeNs)
                : 0;

        double? nominalVoltageVolts =
            (ddr4.At(0x0B) & 0x01) != 0
                ? 1.20
                : null;

        return new MemoryJedecInfo
        {
            MaximumDataRateMt =
                maximumDataRateMt,

            MinimumCycleTimeNs =
                (double)ddr4.SDRAMTimings.MinimumCycleTime,

            MaximumCycleTimeNs =
                (double)ddr4.SDRAMTimings.MaximumCycleTime,

            MinimumCasLatencyTimeNs =
                (double)ddr4.SDRAMTimings.MinimumCASLatencyTime,

            MinimumRasToCasDelayTimeNs =
                (double)ddr4.SDRAMTimings.MinimumRASToCASDelayTime,

            MinimumRowPrechargeDelayTimeNs =
                (double)ddr4.SDRAMTimings.MinimumRowPrechargeDelayTime,

            MinimumActiveToPrechargeDelayTimeNs =
                (double)ddr4.SDRAMTimings.MinimumActiveToPrechargeDelayTime,

            NominalVoltageVolts =
                nominalVoltageVolts,

            SupportedCasLatencies =
                ddr4.SDRAMTimings.CASLatenciesSupported
                    .ToList()
        };
    }

    private static MemoryXmpProfileInfo? ReadDdr4XmpProfile(
        DDR4Accessor ddr4,
        int profileNumber,
        ushort startAddress)
    {
        const int profileSize = 0x2F;

        byte[] profile =
            new byte[profileSize];

        for (int i = 0; i < profile.Length; i++)
        {
            profile[i] =
                ddr4.At(
                    (ushort)(startAddress + i));
        }

        double tCkNs =
            profile[3] * 0.125 +
            unchecked((sbyte)profile[38]) * 0.001;

        if (tCkNs <= 0)
            return null;

        double rawDataRateMt =
            2000.0 / tCkNs;

        int dataRateMt =
            RoundDdrDataRate(rawDataRateMt);

        double voltage =
            ((profile[0] & 0x80) != 0 ? 1.0 : 0.0) +
            (profile[0] & 0x7F) / 100.0;

        double tAaNs =
            profile[8] * 0.125 +
            unchecked((sbyte)profile[37]) * 0.001;

        double tRcdNs =
            profile[9] * 0.125 +
            unchecked((sbyte)profile[36]) * 0.001;

        double tRpNs =
            profile[10] * 0.125 +
            unchecked((sbyte)profile[35]) * 0.001;

        int tRasTicks =
            ((profile[11] & 0x0F) << 8) |
            profile[12];

        double tRasNs =
            tRasTicks * 0.125;

        int cl =
            RoundTimingCycles(
                tAaNs,
                tCkNs);

        int trcd =
            RoundTimingCycles(
                tRcdNs,
                tCkNs);

        int trp =
            RoundTimingCycles(
                tRpNs,
                tCkNs);

        int tras =
            RoundTimingCycles(
                tRasNs,
                tCkNs);

        return new MemoryXmpProfileInfo
        {
            ProfileNumber = profileNumber,
            DataRateMt = dataRateMt,
            CasLatency = cl,
            RasToCasDelay = trcd,
            RowPrechargeDelay = trp,
            ActiveToPrechargeDelay = tras,
            VoltageVolts = voltage
        };
    }

    private static int RoundTimingCycles(
        double timingNs,
        double tCkNs)
    {
        if (timingNs <= 0 || tCkNs <= 0)
            return 0;

        return (int)Math.Round(
            timingNs / tCkNs,
            MidpointRounding.AwayFromZero);
    }

    private static int RoundDdrDataRate(
        double dataRateMt)
    {
        if (dataRateMt <= 0)
            return 0;

        double roundedHundred =
            Math.Round(
                dataRateMt / 100.0,
                MidpointRounding.AwayFromZero) *
            100.0;

        double difference =
            roundedHundred - dataRateMt;

        if (difference < -16.5)
            roundedHundred += 33.0;
        else if (difference > 16.5)
            roundedHundred -= 34.0;

        return (int)Math.Round(
            roundedHundred,
            MidpointRounding.AwayFromZero);
    }

    private void ApplySpdXmpCache(
        HardwareSnapshot snapshot)
    {
        if (!_spdXmpCacheInitialized)
            return;

        foreach (MemorySpdModuleInfo module
                 in snapshot.MemorySpdModules)
        {
            if (!_spdXmpCache.TryGetValue(
                    module.Id,
                    out MemorySpdXmpCacheEntry? cacheEntry))
            {
                continue;
            }

            module.Jedec =
                cacheEntry.Jedec == null
                    ? null
                    : new MemoryJedecInfo
                    {
                        MaximumDataRateMt =
                            cacheEntry.Jedec.MaximumDataRateMt,

                        MinimumCycleTimeNs =
                            cacheEntry.Jedec.MinimumCycleTimeNs,

                        MaximumCycleTimeNs =
                            cacheEntry.Jedec.MaximumCycleTimeNs,

                        MinimumCasLatencyTimeNs =
                            cacheEntry.Jedec.MinimumCasLatencyTimeNs,

                        MinimumRasToCasDelayTimeNs =
                            cacheEntry.Jedec.MinimumRasToCasDelayTimeNs,

                        MinimumRowPrechargeDelayTimeNs =
                            cacheEntry.Jedec.MinimumRowPrechargeDelayTimeNs,

                        MinimumActiveToPrechargeDelayTimeNs =
                            cacheEntry.Jedec.MinimumActiveToPrechargeDelayTimeNs,

                        NominalVoltageVolts =
                            cacheEntry.Jedec.NominalVoltageVolts,

                        SupportedCasLatencies =
                            cacheEntry.Jedec.SupportedCasLatencies
                                .ToList()
                    };

            module.XmpVersion =
                cacheEntry.HasXmp20
                    ? "XMP 2.0"
                    : null;

            module.XmpProfiles =
                cacheEntry.Profiles
                    .Select(profile =>
                        new MemoryXmpProfileInfo
                        {
                            ProfileNumber =
                                profile.ProfileNumber,

                            DataRateMt =
                                profile.DataRateMt,

                            CasLatency =
                                profile.CasLatency,

                            RasToCasDelay =
                                profile.RasToCasDelay,

                            RowPrechargeDelay =
                                profile.RowPrechargeDelay,

                            ActiveToPrechargeDelay =
                                profile.ActiveToPrechargeDelay,

                            VoltageVolts =
                                profile.VoltageVolts
                        })
                    .ToList();
        }
    }

    public void Dispose()
    {
        _computer.Close();
    }
}


public sealed class HardwareSnapshot
{
    public CpuInfo Cpu { get; } = new();

    public List<GpuInfo> Gpus { get; } = new();

    public MemoryInfo Memory { get; } = new();

    public List<MemorySpdModuleInfo> MemorySpdModules { get; } = new();

    public List<StorageDeviceInfo> StorageDevices { get; } = new();

    public List<LogicalDriveInfo> Drives { get; } = new();
}


public sealed class CpuInfo
{
    public string Name { get; set; } = "CPU";

    public float? Temperature { get; set; }

    public float? Load { get; set; }
}


public sealed class GpuInfo
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "GPU";

    public float? Temperature { get; set; }

    public float? HotSpotTemperature { get; set; }

    public float? MemoryTemperature { get; set; }

    public float? Load { get; set; }

    public float? MemoryLoad { get; set; }
}


public sealed class MemoryInfo
{
    public float? Load { get; set; }

    public float? UsedGb { get; set; }

    public float? AvailableGb { get; set; }

    public float? TotalGb
    {
        get
        {
            if (!UsedGb.HasValue || !AvailableGb.HasValue)
                return null;

            return UsedGb.Value + AvailableGb.Value;
        }
    }
}


public sealed class MemorySpdModuleInfo
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public float? CapacityGb { get; set; }

    public List<MemorySpdTimingInfo> Timings { get; set; } = new();

    public MemoryJedecInfo? Jedec { get; set; }

    public string? XmpVersion { get; set; }

    public List<MemoryXmpProfileInfo> XmpProfiles { get; set; } = new();
}


public sealed class MemoryJedecInfo
{
    public int MaximumDataRateMt { get; set; }

    public double MinimumCycleTimeNs { get; set; }

    public double MaximumCycleTimeNs { get; set; }

    public double MinimumCasLatencyTimeNs { get; set; }

    public double MinimumRasToCasDelayTimeNs { get; set; }

    public double MinimumRowPrechargeDelayTimeNs { get; set; }

    public double MinimumActiveToPrechargeDelayTimeNs { get; set; }

    public double? NominalVoltageVolts { get; set; }

    public List<int> SupportedCasLatencies { get; set; } = new();
}


public sealed class MemoryXmpProfileInfo
{
    public int ProfileNumber { get; set; }

    public int DataRateMt { get; set; }

    public int CasLatency { get; set; }

    public int RasToCasDelay { get; set; }

    public int RowPrechargeDelay { get; set; }

    public int ActiveToPrechargeDelay { get; set; }

    public double VoltageVolts { get; set; }
}


internal sealed class MemorySpdXmpCacheEntry
{
    public string HardwareId { get; set; } = "";

    public bool HasXmp20 { get; set; }

    public MemoryJedecInfo? Jedec { get; set; }

    public List<MemoryXmpProfileInfo> Profiles { get; } = new();
}


public sealed class MemorySpdTimingInfo
{
    public string Name { get; set; } = "";

    public float ValueNanoseconds { get; set; }
}


public sealed class StorageDeviceInfo
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public float? Temperature { get; set; }
}


public sealed class LogicalDriveInfo
{
    public string Name { get; set; } = "";

    public string VolumeLabel { get; set; } = "";

    public long TotalBytes { get; set; }

    public long FreeBytes { get; set; }
}