// Recovery entry point for a guest whose RunOnce continuation did not start.
// No installation is performed; it can only resume the existing reboot case.
using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Security.Principal;
using Microsoft.Win32;

internal static class MatrixResume
{
    private static int Main()
    {
        const string working = @"C:\ThermiqraDisposableLab";
        const string results = @"C:\LabResults";
        bool safeGuest = false;
        try
        {
            using (var query = new ManagementObjectSearcher("SELECT Model FROM Win32_ComputerSystem"))
            using (var rows = query.Get())
            {
                bool virtualWindows = false;
                foreach (ManagementObject row in rows) using (row)
                {
                    string model = Convert.ToString(row["Model"]).ToLowerInvariant();
                    foreach (string part in new[] { "virtual machine", "virtualbox", "vmware", "kvm", "qemu", "parallels" })
                        virtualWindows |= model.Contains(part);
                }
                if (!virtualWindows) return 10;
            }
            if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) return 11;
            if (File.ReadAllText(Path.Combine(working, "guest-enabled.marker")) != "thermiqra-disposable-guest-v1") return 12;
            safeGuest = true;
            string report = Path.Combine(results, "matrix-results.json");
            string content = File.ReadAllText(report);
            bool retry = content.Contains("\"Status\": \"Failed\"");
            if (!retry && !content.Contains("\"Status\": \"AwaitingReboot\"")) return 13;
            using (var runOnce = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"))
                File.WriteAllText(Path.Combine(results, "resume-diagnostic.txt"),
                    "Manual guest continuation at " + DateTime.UtcNow.ToString("o") + "\r\nRunOnce: " +
                    Convert.ToString(runOnce == null ? null : runOnce.GetValue("!ThermiqraDisposableLabResume")));
            string executable = Path.Combine(@"C:\LabSource", "Thermiqra.RemovalLab.Runner.exe");
            var start = new ProcessStartInfo(executable, (retry ? "--retry-reboot" : "--resume") + " \"" + report + "\"")
                { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = working };
            start.EnvironmentVariables["THERMIQRA_DISPOSABLE_LAB"] = "1";
            using (var process = Process.Start(start)) { process.WaitForExit(); return process.ExitCode; }
        }
        catch (Exception ex)
        {
            // A physical host returns above, before any file is written.
            if (safeGuest && Directory.Exists(results))
                File.WriteAllText(Path.Combine(results, "resume-error.txt"), ex.ToString());
            return 1;
        }
    }
}
