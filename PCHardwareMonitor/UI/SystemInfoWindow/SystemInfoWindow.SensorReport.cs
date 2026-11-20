using System;
using System.Globalization;
using System.Text;

namespace PCHardwareMonitor;

public partial class SystemInfoWindow
{
    private static void AppendSensorReport(StringBuilder text)
    {
        text.AppendLine(SettingsService.L("[ИСТОЧНИКИ И ПОКАЗАНИЯ ДАТЧИКОВ]", "[SENSOR SOURCES AND READINGS]"));
        text.AppendLine("LibreHardwareMonitor: " + typeof(LibreHardwareMonitor.Hardware.Computer).Assembly.GetName().Version);
        text.AppendLine($".NET: {Environment.Version}; Process: {IntPtr.Size * 8}-bit");
        if (HardwareMonitorService.LastSnapshot is not { } snapshot) return;
        text.AppendLine("CPU: " + snapshot.Cpu.Name);
        foreach (string sensor in snapshot.Cpu.LoadSensorDiagnostics) text.AppendLine("  " + sensor);
        foreach (var gpu in snapshot.Gpus)
        {
            text.AppendLine();
            text.AppendLine("GPU: " + gpu.Name + " [" + gpu.Id + "]");
            if (gpu.SensorDiagnostics.Count == 0)
                text.AppendLine(SettingsService.L("  Библиотека не предоставила датчиков для этого GPU.",
                    "  The library did not expose sensors for this GPU."));
            foreach (var sensor in gpu.SensorDiagnostics)
                text.AppendLine($"  {sensor.Type}: {sensor.Name} [{sensor.Identifier}] = " +
                    (sensor.Value?.ToString(CultureInfo.InvariantCulture) ?? "unavailable"));
        }
    }
}
