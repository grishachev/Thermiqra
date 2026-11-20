using System;
using System.IO;
using Microsoft.Win32;

namespace PCHardwareMonitor;

internal static class ProgramSharedComponentGuard
{
    internal static void Check(string path)
    {
        string root = WindowsPathRules.Normalize(path);
        if (root.Length == 0) ProgramLeftoverCleanupPolicy.Deny();
        foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            // Read only. Never decrement reference counts on somebody else's behalf.
            using RegistryKey hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using RegistryKey? shared = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs");
            if (shared == null) continue;
            if (shared.ValueCount > 20000) throw new IOException(SettingsService.L("Список общих компонентов слишком велик для полной проверки.",
                "Shared-component reference list exceeds the full inspection limit."));
            foreach (string name in shared.GetValueNames())
            {
                string target = WindowsPathRules.Normalize(name);
                if (target.Length == 0 || !WindowsPathRules.Inside(target, root)) continue;
                object? value = shared.GetValue(name);
                if (IsReferenced(value)) throw new IOException(SettingsService.L("Объект зарегистрирован как общий компонент. Очистка заблокирована: ",
                    "The item is registered as a shared component. Cleanup blocked: ") + target);
            }
        }
    }
    internal static bool IsReferenced(object? value) => value switch { int count => count != 0, long count => count != 0, _ => true };
}
