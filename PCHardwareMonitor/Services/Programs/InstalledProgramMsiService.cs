using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace PCHardwareMonitor;

/// <summary>Read-only MSI metadata fallback. Never configures or repairs products.</summary>
internal static class InstalledProgramMsiService
{
    [DllImport("msi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint MsiOpenDatabaseW(string path, IntPtr mode, out uint database);
    [DllImport("msi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint MsiDatabaseOpenViewW(uint database, string query, out uint view);
    [DllImport("msi.dll", ExactSpelling = true)]
    private static extern uint MsiViewExecute(uint view, uint record);
    [DllImport("msi.dll", ExactSpelling = true)]
    private static extern uint MsiViewFetch(uint view, out uint record);
    [DllImport("msi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint MsiRecordGetStringW(uint record, uint field, StringBuilder value, ref uint length);
    [DllImport("msi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MsiGetComponentPathExW(string product, string component,
        string? sid, uint context, StringBuilder path, ref uint length);
    [DllImport("msi.dll", ExactSpelling = true)]
    private static extern uint MsiCloseHandle(uint handle);

    internal static IReadOnlyList<string> ReadExecutablePaths(string productCode, CancellationToken token)
    {
        List<string> paths = new();
        if (!Guid.TryParse(productCode, out _)) return paths;
        uint database = 0, view = 0;
        bool complete = false;
        try
        {
            string package = InstalledProgramIconService.ReadMsiProductProperty(productCode, "LocalPackage");
            if (!Path.IsPathFullyQualified(package) || !File.Exists(package)) return paths;
            // MSIDBOPEN_READONLY = 0. Do not open a product session, install,
            // trigger advertisement, or query Win32_Product (which can repair).
            if (MsiOpenDatabaseW(package, IntPtr.Zero, out database) != 0 ||
                MsiDatabaseOpenViewW(database,
                    "SELECT `ComponentId` FROM `Component` WHERE `ComponentId` IS NOT NULL", out view) != 0 ||
                MsiViewExecute(view, 0) != 0) return paths;
            Stopwatch budget = Stopwatch.StartNew();
            for (int count = 0; count < 512 && budget.ElapsedMilliseconds < 1500; count++)
            {
                token.ThrowIfCancellationRequested();
                uint fetched = MsiViewFetch(view, out uint record);
                if (fetched == 259) { complete = true; break; } // ERROR_NO_MORE_ITEMS
                if (fetched != 0) break;
                try
                {
                    uint length = 39;
                    StringBuilder component = new(39);
                    if (MsiRecordGetStringW(record, 1, component, ref length) != 0 ||
                        !Guid.TryParse(component.ToString(), out _)) continue;
                    foreach (uint context in new uint[] { 4, 2, 1 })
                    {
                        length = 32767;
                        StringBuilder path = new(32768);
                        // INSTALLSTATE_LOCAL = 3. Registry key paths and missing
                        // files are deliberately excluded from executable evidence.
                        if (MsiGetComponentPathExW(productCode, component.ToString(), null,
                                context, path, ref length) == 3 &&
                            Path.IsPathFullyQualified(path.ToString()) &&
                            Path.GetExtension(path.ToString()).Equals(".exe", StringComparison.OrdinalIgnoreCase) &&
                            File.Exists(path.ToString()))
                        {
                            paths.Add(path.ToString());
                            break;
                        }
                    }
                }
                finally { MsiCloseHandle(record); }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or System.Security.SecurityException or DllNotFoundException or
            EntryPointNotFoundException) { }
        finally
        {
            if (view != 0) MsiCloseHandle(view);
            if (database != 0) MsiCloseHandle(database);
        }
        return complete ? paths : Array.Empty<string>();
    }
}

