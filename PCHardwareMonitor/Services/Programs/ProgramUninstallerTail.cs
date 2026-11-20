using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;

namespace PCHardwareMonitor;

// Tracks helper processes started during a native uninstall, including wrappers
// which exit before their temporary copy finishes cleaning up the install folder.
internal sealed class ProgramUninstallerTail : IDisposable
{
    private sealed record Started(int Parent, string Name);
    private readonly object _sync = new();
    private readonly Dictionary<int, Started> _started = new();
    private readonly DateTime _since = DateTime.UtcNow;
    private readonly string _root, _executable;
    private ManagementEventWatcher? _watcher;
    private int _parent;
    private volatile bool _observationHealthy = true;
    internal bool ObservationAvailable => _watcher != null && _observationHealthy;

    internal ProgramUninstallerTail(string root, string executable)
    {
        _root = ProgramLeftoverPathPolicy.Normalize(root); _executable = executable;
        try
        {
            _watcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
            _watcher.Stopped += (_, _) => _observationHealthy = false;
            _watcher.EventArrived += (_, args) =>
            {
                using ManagementBaseObject item = args.NewEvent;
                int id = Convert.ToInt32(item["ProcessID"]);
                Started process = new(Convert.ToInt32(item["ParentProcessID"]), Convert.ToString(item["ProcessName"]) ?? "");
                lock (_sync)
                {
                    if (_started.Count < 4096) _started[id] = process;
                    else _observationHealthy = false;
                }
            };
            _watcher.Start();
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or
            System.Runtime.InteropServices.COMException or InvalidOperationException or System.Security.SecurityException)
        { DiagnosticLog.Failure("Uninstall.ProcessObserver", ex); _watcher?.Dispose(); _watcher = null; }
    }

    internal void SetParent(int id) => _parent = id;

    internal List<ProgramPendingProcess> PendingProcesses()
    {
        List<ProgramPendingProcess> pending = new();
        KeyValuePair<int, Started>[] entries;
        lock (_sync) entries = _started.ToArray();
        HashSet<int> family = new() { _parent };
        for (int pass = 0; pass < entries.Length; pass++)
        {
            bool added = false;
            foreach (var entry in entries)
                if (family.Contains(entry.Value.Parent)) added |= family.Add(entry.Key);
            if (!added) break;
        }
        string temporary = ProgramLeftoverPathPolicy.Normalize(Path.GetTempPath());
        foreach (var entry in entries.Where(e => e.Key != _parent && family.Contains(e.Key)))
        {
            try
            {
                using Process process = Process.GetProcessById(entry.Key);
                if (process.HasExited || process.StartTime.ToUniversalTime() < _since.AddSeconds(-1)) continue;
                string path = process.MainModule?.FileName ?? "";
                bool systemHelper = new[] { "msiexec.exe", "cmd.exe", "rundll32.exe", "regsvr32.exe" }
                    .Contains(entry.Value.Name, StringComparer.OrdinalIgnoreCase) &&
                    ProgramLeftoverPathPolicy.Inside(path, Environment.GetFolderPath(Environment.SpecialFolder.Windows));
                if (path.Equals(_executable, StringComparison.OrdinalIgnoreCase) || systemHelper ||
                    (_root.Length > 0 && !InstalledProgramLocationService.IsBroadContainerDirectory(_root) &&
                     ProgramLeftoverPathPolicy.Inside(path, _root)) || ProgramLeftoverPathPolicy.Inside(path, temporary)) pending.Add(new(entry.Key, process.StartTime.ToUniversalTime()));
            }
            catch (ArgumentException) { } // The process exited between the event and this read.
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { pending.Add(new(entry.Key, DateTime.MinValue)); } // Cannot prove that this helper finished.
        }
        return pending;
    }

    internal async Task<bool> WaitAsync()
    {
        if (!ObservationAvailable) return false;
        Stopwatch budget = Stopwatch.StartNew();
        int quiet = 0;
        while (budget.Elapsed < TimeSpan.FromSeconds(30))
        {
            if (!ObservationAvailable) return false;
            // Allow late process-start events to arrive after the launcher exits.
            await Task.Delay(500);
            quiet = PendingProcesses().Count > 0 ? 0 : quiet + 1;
            if (quiet >= 4) return true;
        }
        return false;
    }

    public void Dispose()
    {
        try { _watcher?.Stop(); }
        catch (Exception ex) when (ex is ManagementException or System.Runtime.InteropServices.COMException or InvalidOperationException) { DiagnosticLog.Failure("Uninstall.ProcessObserverStop", ex); }
        _watcher?.Dispose();
    }
}
