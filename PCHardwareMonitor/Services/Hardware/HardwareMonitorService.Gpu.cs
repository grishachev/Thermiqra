using System;
using System.Linq;
using LibreHardwareMonitor.Hardware;

namespace PCHardwareMonitor;

public sealed partial class HardwareMonitorService
{
    // IntelIntegratedGpu exposes D3D engine names instead of "GPU Core".
    // Use the busiest reported engine, as Windows Task Manager does. Engine
    // percentages run concurrently and must not be summed or averaged.
    private static float? ReadD3dLoad(IHardware hardware) => hardware.Sensors
        .Where(sensor => sensor.SensorType == SensorType.Load &&
            sensor.Name.StartsWith("D3D ", StringComparison.OrdinalIgnoreCase) &&
            sensor.Value is float value && float.IsFinite(value) && value is >= 0 and <= 100)
        .Select(sensor => sensor.Value).DefaultIfEmpty(null).Max();
}

public sealed record GpuSensorReading(string Name, string Type, string Identifier, float? Value);
