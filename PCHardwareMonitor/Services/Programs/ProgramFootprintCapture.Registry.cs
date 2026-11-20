using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Win32;

namespace PCHardwareMonitor;

internal static partial class ProgramFootprintCapture
{
    private static void CaptureRegistry(ProgramRemovalBaseline baseline, List<ProgramFootprintRegistry> result,
        List<ProgramReviewLimitation> limitations, Stopwatch timer, TimeSpan budget,
        CancellationToken token, ref bool complete)
    {
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        foreach (var probe in baseline.Probes.Where(p => p.Kind == "Registry" && p.Group != ProgramLeftoverGroup.Protected))
        {
            token.ThrowIfCancellationRequested();
            if (probe.Hive is not ("HKCU" or "HKLM") || probe.View is not ("32" or "64"))
            { complete = false; continue; }
            try
            {
                using var hive = RegistryKey.OpenBaseKey(probe.Hive == "HKLM" ? RegistryHive.LocalMachine : RegistryHive.CurrentUser,
                    probe.View == "64" ? RegistryView.Registry64 : RegistryView.Registry32);
                Stack<(string Path, int Depth)> pending = new(); pending.Push((probe.Path, 0));
                while (pending.TryPop(out var next))
                {
                    token.ThrowIfCancellationRequested();
                    if (result.Count >= 128 || timer.Elapsed > budget) { complete = false; return; }
                    if (!visited.Add(probe.Hive + "|" + probe.View + "|" + next.Path)) continue;
                    using var key = hive.OpenSubKey(next.Path, writable: false);
                    if (key == null) continue;
                    using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    string[] names = key.GetValueNames();
                    if (names.Length > 256) { complete = false; continue; }
                    int count = 0; bool keyComplete = true;
                    foreach (string name in names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                    {
                        token.ThrowIfCancellationRequested();
                        if (timer.Elapsed > budget) { complete = false; return; }
                        object? value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                        // Persist only fingerprints, never passwords, license
                        // values or other registry payloads in the journal.
                        if (value is byte[] { Length: > 65536 } || value is string { Length: > 32768 } ||
                            value is string[] strings && strings.Sum(s => (long)s.Length) > 32768)
                        { complete = false; keyComplete = false; continue; }
                        hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(new { Name = name, Kind = key.GetValueKind(name), Value = value }));
                        count++;
                    }
                    result.Add(new(probe.Hive, probe.View, next.Path, Convert.ToHexString(hash.GetHashAndReset()), count, keyComplete));
                    string[] children = key.GetSubKeyNames();
                    if (next.Depth < 2)
                    {
                        if (children.Length > 128) complete = false;
                        foreach (string child in children.Take(128)) pending.Push((next.Path + "\\" + child, next.Depth + 1));
                    }
                    else if (children.Length > 0) complete = false;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                System.Security.SecurityException or ArgumentException)
            {
                complete = false;
                limitations.Add(new("Снимок реестра ограничен", "Registry snapshot was limited", probe.Path + ": " + ex.Message));
            }
        }
    }
}
