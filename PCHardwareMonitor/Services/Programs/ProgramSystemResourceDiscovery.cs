using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PCHardwareMonitor;

// Inspection only. These resource types have NO cleanup writer and cannot pass
// ProgramRemovalDecisions. Each source is bounded and failures remain visible.
internal static partial class ProgramSystemResourceDiscovery
{
    internal static void Inspect(ProgramRemovalBaseline baseline, List<ProgramLeftoverProbe> found,
        List<ProgramReviewLimitation> limitations)
    {
        string root = WindowsPathRules.Normalize(baseline.InstallRoot);
        if (root.Length == 0 || InstalledProgramLocationService.IsBroadContainerDirectory(root) ||
            ProgramLeftoverPathPolicy.UserContent(root) || baseline.Identity.StartsWith("msix:", StringComparison.Ordinal)) return;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string kind, string id, string command)
        {
            if (!ProgramResourceAssociation.TargetsScope(Environment.ExpandEnvironmentVariables(command), root) ||
                !seen.Add(kind + ":" + id)) return;
            found.Add(new(kind, id, ProgramLeftoverGroup.Protected,
                "Регистрация ссылается на файл в сохранённой папке программы. Доступна проверка; автоматическое изменение этой регистрации не выполняется.",
                "This registration targets a file in the recorded application folder. Inspection is available; this registration is not changed automatically.",
                EvidenceRoot: root, EvidenceTarget: command));
        }
        void Source(string name, Action action)
        {
            try { action(); }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or
                System.Security.SecurityException or System.ComponentModel.Win32Exception or COMException or
                InvalidOperationException or ArgumentException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
            {
                DiagnosticLog.Failure("Resources." + name, ex);
                limitations.Add(new("Проверка системных регистраций выполнена не полностью",
                    "System registration inspection is incomplete", name));
            }
        }
        Source("Startup", () => ReadStartup(Add));
        Source("Services", () => ReadServices(Add));
        Source("Shell extensions", () => ReadShellExtensions(Add));
        Source("Scheduled tasks", () => ReadTasks(Add));
        Source("Firewall", () => ReadFirewall(Add));
    }

    private static void Limit(Stopwatch budget, int count, int maximum)
    {
        if (count > maximum || budget.Elapsed > TimeSpan.FromSeconds(3))
            throw new System.IO.IOException(SettingsService.L("Достигнут предел проверки системных объектов.", "Resource inspection limit reached."));
    }

    private static void ReadStartup(Action<string, string, string> add)
    {
        var budget = Stopwatch.StartNew(); int count = 0;
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        foreach (string leaf in new[] { "Run", "RunOnce" })
        {
            using var registry = RegistryKey.OpenBaseKey(hive, view);
            string path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\" + leaf;
            using var key = registry.OpenSubKey(path);
            if (key == null) continue;
            foreach (string name in key.GetValueNames())
            {
                Limit(budget, ++count, 2000);
                if (key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is string command)
                    add("Startup", hive + " (" + view + @")\" + path + " · " + name, command);
            }
        }
    }

    private static void ReadServices(Action<string, string, string> add)
    {
        var budget = Stopwatch.StartNew(); int count = 0;
        using var registry = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var services = registry.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
        if (services == null) throw new System.IO.IOException(SettingsService.L("Раздел реестра служб недоступен.", "Services registry unavailable."));
        foreach (string name in services.GetSubKeyNames())
        {
            Limit(budget, ++count, 10000);
            using var key = services.OpenSubKey(name);
            if (key?.GetValue("ImagePath", null, RegistryValueOptions.DoNotExpandEnvironmentNames) is string command)
                add("Service", "Service · " + name, command);
            using var parameters = key?.OpenSubKey("Parameters");
            if (parameters?.GetValue("ServiceDll", null, RegistryValueOptions.DoNotExpandEnvironmentNames) is string library)
                add("Service", "Service DLL · " + name, '"' + library + '"');
        }
    }

    private static void ReadShellExtensions(Action<string, string, string> add)
    {
        var budget = Stopwatch.StartNew(); int count = 0;
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var registry = RegistryKey.OpenBaseKey(hive, view);
            using var approved = registry.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved");
            if (approved == null) continue;
            foreach (string id in approved.GetValueNames().Where(x => Guid.TryParse(x, out _)))
            {
                Limit(budget, ++count, 4000);
                using var server = registry.OpenSubKey(@"SOFTWARE\Classes\CLSID\" + id + @"\InprocServer32");
                if (server?.GetValue("", null, RegistryValueOptions.DoNotExpandEnvironmentNames) is string command)
                    add("ShellExtension", hive + " (" + view + ") · " + id, '"' + command.Trim('"') + '"');
            }
        }
    }

    private static void Release(object? value)
    {
        if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
    }
}
