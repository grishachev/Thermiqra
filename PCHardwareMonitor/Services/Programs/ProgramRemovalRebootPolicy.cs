using System;
using System.Management;

namespace PCHardwareMonitor;

internal static class ProgramRemovalRebootPolicy
{
    internal static DateTime? ReadCurrentBootUtc()
    {
        try
        {
            using var query = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem");
            query.Options.Timeout = TimeSpan.FromSeconds(10);
            using var rows = query.Get();
            foreach (ManagementObject row in rows)
                using (row)
                    if (row["LastBootUpTime"] is string raw)
                        return ManagementDateTimeConverter.ToDateTime(raw).ToUniversalTime();
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or
            System.Runtime.InteropServices.COMException or ArgumentException or InvalidOperationException)
        { DiagnosticLog.Failure("Uninstall.BootVerification", ex); }
        return null;
    }

    internal static InstalledProgramRemovalResult Refresh(InstalledProgramRemovalResult result, DateTime baselineUtc, DateTime bootUtc)
    {
        DateTime attempt = result.AttemptedUtc ?? baselineUtc;
        if ((result.State != InstalledProgramRemovalState.RebootRequired && !result.HelperObservationIncomplete) || !result.RegistrationAbsent ||
            attempt.Year < 2000 || bootUtc <= attempt.AddSeconds(1)) return result;
        // Native completion still needs live registration and leftover checks.
        // A new boot only releases the reboot-specific hold; it is not cleanup.
        return result with { State = result.State == InstalledProgramRemovalState.RebootRequired ? InstalledProgramRemovalState.Unconfirmed : result.State,
            HelperObservationIncomplete = false, Detail = result.Detail + "\n" + SettingsService.L(
            "Перезагрузка после удаления подтверждена. Результат проверяется заново.",
            "A restart after uninstall is confirmed. The outcome is being checked again.") };
    }
}
