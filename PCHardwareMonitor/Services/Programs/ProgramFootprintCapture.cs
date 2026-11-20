using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace PCHardwareMonitor;

internal static partial class ProgramFootprintCapture
{
    internal static ProgramFootprint Capture(ProgramRemovalBaseline baseline, TimeSpan budget, CancellationToken token)
    {
        List<ProgramFootprintFile> files = new();
        List<ProgramFootprintRegistry> registry = new();
        List<ProgramReviewLimitation> limitations = new();
        Stopwatch timer = Stopwatch.StartNew();
        bool complete = true;
        var roots = baseline.Probes.Where(p =>
                     (p.Kind == "Directory" && p.Group == ProgramLeftoverGroup.Related) ||
                     (p.Kind == "UserData" && p.Group != ProgramLeftoverGroup.Protected))
                 .Select(p => p.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (string root in roots)
        {
            token.ThrowIfCancellationRequested();
            // Give each known location a share. A large installation must not
            // consume every entry and leave its AppData profiles unobserved.
            int rootCount = 0;
            int rootLimit = Math.Max(1, 4000 / Math.Max(1, roots.Count));
            TimeSpan rootBudget = budget * (0.7 / Math.Max(1, roots.Count));
            Stopwatch rootTimer = Stopwatch.StartNew();
            bool rootLimited = false;
            try
            {
                ProgramLeftoverCleanupPolicy.CheckRoot(root);
                ProgramLeftoverCleanupPolicy.CheckAncestors(root);
                if (!Directory.Exists(root)) continue;
                Stack<string> pending = new(); pending.Push(root);
                while (pending.TryPop(out string? directory))
                {
                    token.ThrowIfCancellationRequested();
                    if (timer.Elapsed > budget * 0.7 || rootTimer.Elapsed > rootBudget)
                    { complete = false; rootLimited = true; break; }
                    ProgramLeftoverCleanupPolicy.CheckAncestors(directory);
                    foreach (FileSystemInfo entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
                    {
                        token.ThrowIfCancellationRequested();
                        if (files.Count >= 4000 || rootCount >= rootLimit || timer.Elapsed > budget * 0.7 || rootTimer.Elapsed > rootBudget)
                        { complete = false; rootLimited = true; pending.Clear(); break; }
                        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                        { complete = false; continue; }
                        bool isDirectory = (entry.Attributes & FileAttributes.Directory) != 0;
                        files.Add(new(root, Path.GetRelativePath(root, entry.FullName), isDirectory,
                            entry is FileInfo file ? file.Length : 0, entry.LastWriteTimeUtc.Ticks));
                        rootCount++;
                        if (isDirectory) pending.Push(entry.FullName);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                System.Security.SecurityException or ArgumentException)
            {
                complete = false;
                limitations.Add(new("Снимок файлов ограничен", "File snapshot was limited", root + ": " + ex.Message));
            }
            if (rootLimited) limitations.Add(new("Снимок этой папки ограничен по числу объектов или времени",
                "This folder snapshot was bounded by entry count or time", root));
            if (files.Count >= 4000 || timer.Elapsed > budget * 0.7) { complete = false; break; }
        }
        CaptureRegistry(baseline, registry, limitations, timer, budget, token, ref complete);
        var registryRoots = baseline.Probes.Where(p => p.Kind == "Registry" && p.Group != ProgramLeftoverGroup.Protected)
            .Select(p => p.Hive + "|" + p.View + "|" + p.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return new(DateTime.UtcNow, files, registry, complete, limitations, roots, registryRoots);
    }
}
