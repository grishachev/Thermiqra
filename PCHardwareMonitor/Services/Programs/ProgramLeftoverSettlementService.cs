using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCHardwareMonitor;

internal static class ProgramLeftoverSettlementService
{
    // Examine only associations captured before uninstall. No whole-disk search.
    internal static async Task<ProgramLeftoverReport> InspectAfterUninstallAsync(ProgramRemovalBaseline baseline)
    {
        Stopwatch budget = Stopwatch.StartNew();
        string previous = ""; int quiet = 0;
        while (budget.Elapsed < TimeSpan.FromSeconds(12))
        {
            string current = await Task.Run(() => Fingerprint(baseline));
            quiet = current == previous ? quiet + 1 : 0; previous = current;
            if (quiet >= 6) break;
            await Task.Delay(500);
        }
        var report = await Task.Run(() => ProgramLeftoverDiscoveryService.Inspect(baseline));
        if (quiet < 6)
            report.Limitations.Add(new("Расположения ещё меняются после удаления; повторите поиск позже",
                "Locations are still changing after uninstall; search again later"));
        return report;
    }

    private static string Fingerprint(ProgramRemovalBaseline baseline)
    {
        StringBuilder state = new();
        foreach (var probe in baseline.Probes.Where(p => p.Kind is "Directory" or "Shortcut" or "UserData").Take(200))
        {
            state.Append(probe.Path).Append('|');
            try
            {
                ProgramLeftoverCleanupPolicy.CheckAncestors(probe.Path);
                FileAttributes attributes = File.GetAttributes(probe.Path);
                state.Append((int)attributes).Append(':').Append(File.GetLastWriteTimeUtc(probe.Path).Ticks);
                if ((attributes & FileAttributes.Directory) != 0)
                    foreach (var entry in new DirectoryInfo(probe.Path).EnumerateFileSystemInfos()
                        .Take(128).OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
                        state.Append(';').Append(entry.Name).Append(':').Append(entry.LastWriteTimeUtc.Ticks);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { state.Append("absent"); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            { state.Append("unavailable"); }
            state.AppendLine();
        }
        return state.ToString();
    }
}
