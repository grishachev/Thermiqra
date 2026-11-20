using System.Management;
using System.Security.Principal;

namespace Thermiqra.RemovalLab;

internal static class GuestSafety
{
    internal static bool IsAllowed(bool optedIn, bool admin, string model) => optedIn && admin &&
        new[] { "virtual machine", "virtualbox", "vmware", "kvm", "qemu", "parallels" }
            .Any(part => model.Contains(part, StringComparison.OrdinalIgnoreCase));

    internal static string Inspect()
    {
        using var identity = WindowsIdentity.GetCurrent();
        bool admin = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        string model = "";
        using var query = new ManagementObjectSearcher("SELECT Model FROM Win32_ComputerSystem");
        using var rows = query.Get();
        foreach (ManagementObject row in rows) using (row) model = Convert.ToString(row["Model"]) ?? "";
        return IsAllowed(Environment.GetEnvironmentVariable("THERMIQRA_DISPOSABLE_LAB") == "1", admin, model)
            ? "" : "Blocked: requires explicit disposable-guest flag, administrator token and a recognized virtual Windows. No installation or cleanup was started.";
    }
}
