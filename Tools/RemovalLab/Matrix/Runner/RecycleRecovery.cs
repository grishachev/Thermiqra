using System.Runtime.InteropServices;
using PCHardwareMonitor;

namespace Thermiqra.RemovalLab;

internal static class RecycleRecovery
{
    internal static async Task RestoreFixtureFolders(Fixture fixture, ProgramCleanupBackup backup, CaseResult test)
    {
        foreach (string original in backup.Entries.Where(e => e.State == "Recycled" && fixture.Roots.Contains(e.Probe.Path)).Select(e => e.Probe.Path).Distinct())
        {
            test.Require(!Directory.Exists(original) && !File.Exists(original), "File restore target unoccupied");
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Thread thread = new(() =>
            {
                object? shell = null, folder = null, items = null;
                try
                {
                    shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
                    dynamic api = shell!; folder = api.NameSpace(10); dynamic bin = folder!; items = bin.Items(); dynamic list = items!;
                    bool found = false;
                    for (int i = 0; i < (int)list.Count; i++)
                    {
                        object? item = null;
                        try
                        {
                            item = list.Item(i); dynamic entry = item;
                            string parent = Convert.ToString(entry.ExtendedProperty("System.Recycle.DeletedFrom")) ?? "";
                            string name = Convert.ToString(entry.Name) ?? "";
                            if (!Path.Combine(parent, name).Equals(original, StringComparison.OrdinalIgnoreCase)) continue;
                            entry.InvokeVerb("undelete"); found = true; break;
                        }
                        finally { Release(item); }
                    }
                    if (!found) throw new IOException("Recorded fixture folder not found in Recycle Bin");
                    completion.SetResult();
                }
                catch (Exception ex) { completion.SetException(ex); }
                finally { Release(items); Release(folder); Release(shell); }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA); thread.Start(); await completion.Task;
            for (int i = 0; i < 100 && !Directory.Exists(original); i++) await Task.Delay(100);
            test.Require(Directory.Exists(original), "Recorded folder restored from Recycle Bin");
        }
    }
    private static void Release(object? value) { if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
}
