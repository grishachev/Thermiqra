using System;
using System.Diagnostics;
using System.Linq;

namespace PCHardwareMonitor;

internal sealed record ProgramPendingProcess(int Id, DateTime StartedUtc);

internal static class ProgramPendingProcesses
{
    internal static InstalledProgramRemovalResult Refresh(InstalledProgramRemovalResult result)
    {
        if (result.PendingHelpers == null) return result;
        if (result.PendingHelpers.Count > 4096 || result.PendingHelpers.Any(p => p == null || p.Id <= 0))
            throw new System.IO.IOException(SettingsService.L("Сохранённые сведения о процессах повреждены. Очистка заблокирована.",
                "Saved process information is invalid. Cleanup is blocked."));
        return result with { PendingHelpers = result.PendingHelpers.Where(IsStillRunning).ToList() };
    }

    private static bool IsStillRunning(ProgramPendingProcess item)
    {
        try
        {
            using Process process = Process.GetProcessById(item.Id);
            return !process.HasExited && (item.StartedUtc == DateTime.MinValue ||
                process.StartTime.ToUniversalTime() == item.StartedUtc);
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (System.ComponentModel.Win32Exception) { return true; }
    }
}
