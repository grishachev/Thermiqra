using System;
using System.Globalization;
using System.IO;
using System.Management;
using Microsoft.Win32;

namespace PCHardwareMonitor;

internal static class ProgramRestoreProtectionService
{
    internal static uint Enable(ManagementClass systemRestore)
    {
        using RegistryKey registry = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using RegistryKey? policy = registry.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore");
        if (Convert.ToInt32(policy?.GetValue("DisableSR", 0), CultureInfo.InvariantCulture) != 0)
            throw new InvalidOperationException(SettingsService.L(
                "Восстановление системы запрещено политикой Windows. Измените политику у администратора компьютера.",
                "System Restore is disabled by Windows policy. Contact the computer administrator to change the policy."));
        string drive = Path.GetPathRoot(Environment.SystemDirectory) ?? throw new InvalidOperationException(SettingsService.L("Системный диск недоступен.", "System drive unavailable."));
        using ManagementBaseObject input = systemRestore.GetMethodParameters("Enable"); input["Drive"] = drive;
        using ManagementBaseObject output = systemRestore.InvokeMethod("Enable", input,
            new InvokeMethodOptions { Timeout = TimeSpan.FromMinutes(1) });
        return Convert.ToUInt32(output["ReturnValue"], CultureInfo.InvariantCulture);
    }
}
