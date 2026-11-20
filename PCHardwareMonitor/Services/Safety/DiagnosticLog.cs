using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace PCHardwareMonitor;

// Bounded local diagnostics. Never records command lines, registry payloads,
// authentication material or user-file contents. Logging must not break work.
internal static class DiagnosticLog
{
    private static readonly object Sync = new();
    internal static void Failure(string area, Exception exception)
    {
        Trace.TraceWarning("Thermiqra {0}: {1} (0x{2:X8})", area, exception.GetType().Name, exception.HResult);
        try
        {
            lock (Sync)
            {
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Thermiqra", "Diagnostics");
                if (InstalledProgramLocationService.HasReparseAncestor(directory)) return;
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "failures.jsonl");
                if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return;
                if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024)
                {
                    string previous = path + ".previous";
                    if (File.Exists(previous) && (File.GetAttributes(previous) & FileAttributes.ReparsePoint) != 0) return;
                    File.Move(path, previous, true);
                }
                // Exception messages can contain sensitive paths or data; retain
                // type/code only. The UI still shows the actionable error.
                File.AppendAllText(path, JsonSerializer.Serialize(new { Utc = DateTime.UtcNow,
                    Area = area.Length > 120 ? area[..120] : area, Error = exception.GetType().FullName,
                    exception.HResult }) + Environment.NewLine);
            }
        }
        catch (Exception logError) when (logError is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or ArgumentException or InvalidOperationException)
        { Trace.TraceWarning("Thermiqra diagnostics unavailable: {0}", logError.GetType().Name); }
    }
}
