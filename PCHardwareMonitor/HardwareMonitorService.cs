using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibreHardwareMonitor.Hardware;

namespace PCHardwareMonitor;

public sealed class HardwareMonitorService : IDisposable
{
    private readonly Computer _computer;

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


public sealed class StorageDeviceInfo
{
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
