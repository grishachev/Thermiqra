using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace PCHardwareMonitor;

/// <summary>
/// Read-only supplementary size estimates for programs that did not register
/// EstimatedSize. Never treats a shared folder as exclusive ownership.
/// </summary>
internal static class InstalledProgramSizeService
{
    private static readonly StringComparer PathComparer =
        StringComparer.OrdinalIgnoreCase;

    // A short-lived, in-memory cache avoids scanning the same application again
    // when the user reopens the list. The root directory timestamp is only a
    // cache invalidation hint; the TTL prevents keeping old values indefinitely.
    private sealed record CachedMeasurement(long Bytes, DateTime RootModifiedUtc,
        DateTime MeasuredUtc);

    private static readonly object CacheLock = new();
    private static readonly Dictionary<string, CachedMeasurement> RecentMeasurements =
        new(PathComparer);
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan FolderBudget = TimeSpan.FromMinutes(4);

    internal static List<InstalledProgramInfo> EnrichMissingSizes(
        IReadOnlyList<InstalledProgramInfo> programs, CancellationToken token,
        IProgress<(InstalledProgramInfo Program, int Completed, int Total)>? progress = null)
    {
        Dictionary<string, int> registrationsPerFolder = new(PathComparer);
        Dictionary<InstalledProgramInfo, string> normalized = new();
        foreach (InstalledProgramInfo app in programs)
        {
            string path = NormalizeFolder(app.InstallLocation);
            normalized[app] = path;
            if (path.Length > 0)
                registrationsPerFolder[path] = registrationsPerFolder.GetValueOrDefault(path) + 1;
        }

        // Generic containers cannot be attributed to one registration.
        // Being inside another app's root does not prove shared ownership.
        List<InstalledProgramInfo> candidates = programs.Where(app =>
        {
            if (app.CachedInventoryUtc.HasValue || app.IsAuxiliary || app.SizeText != "—") return false;
            string path = normalized[app];
            if (!IsConfirmedApplicationFolder(path, app)) return false;
            return true;
        }).ToList();

        HashSet<InstalledProgramInfo> candidateSet = new(candidates);
        Dictionary<string, (long Bytes, bool Complete)> measuredThisRun = new(PathComparer);
        Dictionary<string, DateTime> measuredAt = new(PathComparer);
        Dictionary<string, bool> reusedMeasurement = new(PathComparer);
        List<InstalledProgramInfo> output = new(programs.Count);
        int completed = 0;
        foreach (InstalledProgramInfo app in programs)
        {
            token.ThrowIfCancellationRequested();
            if (!candidateSet.Contains(app))
            {
                string reason = app.InstallLocation.Length == 0
                    ? SettingsService.L("Установщик не сообщил размер; каталог программы не подтверждён",
                        "Installer did not report a size; application directory is unconfirmed")
                    : InstalledProgramLocationService.IsBroadContainerDirectory(app.InstallLocation)
                        ? SettingsService.L("Программа находится в общем каталоге; её отдельный размер не установлен",
                            "Application is in a shared directory; its individual size is unknown")
                        : SettingsService.L("Установщик не сообщил размер; каталог недоступен или не подходит для отдельного подсчёта",
                            "Installer did not report a size; directory is inaccessible or unsuitable for an individual scan");
                output.Add(app.SizeText == "—" && !app.CachedInventoryUtc.HasValue ? app with
                {
                    SizeText = SettingsService.L("Не определён", "Unavailable"),
                    SizeSource = reason
                } : app);
                continue;
            }

            string path = normalized[app];
            (long Bytes, bool Complete) measurement;
            if (!measuredThisRun.TryGetValue(path, out measurement))
            {
                bool reused = TryGetCachedMeasurement(path, out measurement, out DateTime measuredUtc);
                if (!reused)
                {
                    measurement = MeasureFolder(path, token);
                    measuredUtc = DateTime.UtcNow;
                    if (measurement.Complete)
                        StoreCachedMeasurement(path, measurement.Bytes);
                }
                measuredThisRun[path] = measurement;
                measuredAt[path] = measuredUtc;
                reusedMeasurement[path] = reused;
            }

            // Equal roots are known to be used by multiple registrations.
            // A parent/root overlap alone does not make the child's files shared.
            bool shared = registrationsPerFolder.GetValueOrDefault(path) > 1;
            bool includesOtherRoots = programs.Any(other => !ReferenceEquals(app, other) &&
                normalized[other].StartsWith(path + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase));
            string source = shared
                ? SettingsService.L(
                    "Размер общего каталога нескольких записей Windows. Не суммировать как отдельные копии программы; объём освобождения при удалении одной записи неизвестен",
                    "Directory size shared by multiple Windows registrations. Do not add it as separate application copies; space reclaimed by removing one entry is unknown")
                : includesOtherRoots
                    ? SettingsService.L(
                        "Размер каталога, включающего вложенные установки других программ; объём освобождения при удалении неизвестен",
                        "Directory size includes nested installations of other applications; space reclaimed by uninstalling is unknown")
                    : SettingsService.L(
                        "Размер файлов каталога программы; не гарантированный объём освобождения при удалении",
                        "Application directory file size; not guaranteed space reclaimed by uninstalling");
            source += SettingsService.L(
                ". Ссылки и их целевые файлы не включены; общие файлы за пределами каталога не подсчитываются",
                ". Links and their targets are excluded; shared files outside this directory are not counted");
            InstalledProgramInfo updated = app with
            {
                SizeMeasuredUtc = measurement.Complete ? measuredAt[path] : null,
                SizeFromCache = reusedMeasurement[path],
                SizeText = measurement.Complete ? FormatBytes(measurement.Bytes)
                    : SettingsService.L("Не определён", "Unavailable"),
                SizeSource = measurement.Complete ? source : source + SettingsService.L(
                    $". Подсчёт неполный: прочитано {FormatBytes(measurement.Bytes)}; часть файлов недоступна или достигнут лимит",
                    $". Incomplete scan: {FormatBytes(measurement.Bytes)} read; some files unavailable or scan limit reached")
            };
            output.Add(updated);
            completed++;
            progress?.Report((updated, completed, candidates.Count));
        }
        return output;
    }

    private static bool TryGetCachedMeasurement(string path,
        out (long Bytes, bool Complete) measurement, out DateTime measuredUtc)
    {
        measurement = default;
        measuredUtc = default;
        DateTime modified;
        try { modified = Directory.GetLastWriteTimeUtc(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or System.Security.SecurityException) { return false; }
        lock (CacheLock)
        {
            if (!RecentMeasurements.TryGetValue(path, out CachedMeasurement? cached) ||
                cached.RootModifiedUtc != modified ||
                DateTime.UtcNow - cached.MeasuredUtc >= CacheLifetime)
                return false;
            measurement = (cached.Bytes, true);
            measuredUtc = cached.MeasuredUtc;
            return true;
        }
    }

    private static void StoreCachedMeasurement(string path, long bytes)
    {
        try
        {
            DateTime modified = Directory.GetLastWriteTimeUtc(path);
            lock (CacheLock)
            {
                if (RecentMeasurements.Count > 512)
                    RecentMeasurements.Clear();
                RecentMeasurements[path] = new CachedMeasurement(
                    bytes, modified, DateTime.UtcNow);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or System.Security.SecurityException) { }
    }

    private static string NormalizeFolder(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        try
        {
            string expanded = Environment.ExpandEnvironmentVariables(raw.Trim().Trim('"'));
            if (!Path.IsPathFullyQualified(expanded)) return "";
            string path = Path.GetFullPath(expanded);
            return path.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or
            PathTooLongException)
        {
            return "";
        }
    }

    internal static bool IsConfirmedApplicationFolder(string raw, InstalledProgramInfo app)
    {
        string path = NormalizeFolder(raw);
        if (path.Length == 0 || !Path.IsPathFullyQualified(path) ||
            path.StartsWith(@"\\", StringComparison.Ordinal) || !Directory.Exists(path))
            return false;
        try
        {
            DirectoryInfo directory = new(path);
            if (InstalledProgramLocationService.HasReparseAncestor(path) ||
                directory.Parent == null ||
                InstalledProgramIconService.IsWindowsSystemDirectory(path) ||
                InstalledProgramLocationService.IsBroadContainerDirectory(path))
                return false;

            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            foreach (string root in new[] { profile, roaming, local, common,
                programFiles, programFilesX86 })
            {
                if (path.Equals(root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            if (path.StartsWith(common.TrimEnd('\\') + @"\", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(Path.Combine(local, "Packages") + @"\", StringComparison.OrdinalIgnoreCase))
                return false;
            // Known folders can be redirected to another drive; profile-relative
            // English names alone do not protect their actual locations.
            foreach (Environment.SpecialFolder known in new[] {
                Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.DesktopDirectory,
                Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.MyMusic,
                Environment.SpecialFolder.MyVideos })
            {
                string knownPath = Environment.GetFolderPath(known).TrimEnd('\\');
                if (knownPath.Length > 0 && (path.Equals(knownPath, StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith(knownPath + @"\", StringComparison.OrdinalIgnoreCase))) return false;
            }
            foreach (string userFolder in new[] { "Desktop", "Documents", "Downloads",
                "Pictures", "Music", "Videos", "OneDrive" })
            {
                string basePath = Path.Combine(profile, userFolder);
                if (path.Equals(basePath, StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith(basePath + @"\", StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            string tail = directory.Name;
            if (tail.Equals("Common Files", StringComparison.OrdinalIgnoreCase) ||
                tail.Equals("Common7", StringComparison.OrdinalIgnoreCase) ||
                tail.Equals("IDE", StringComparison.OrdinalIgnoreCase) ||
                tail.Equals("Installer", StringComparison.OrdinalIgnoreCase) ||
                tail.Equals("Temp", StringComparison.OrdinalIgnoreCase) ||
                tail.Equals("Package Cache", StringComparison.OrdinalIgnoreCase) ||
                path.Contains(@"\Common Files\", StringComparison.OrdinalIgnoreCase) ||
                path.Contains(@"\Package Cache\", StringComparison.OrdinalIgnoreCase))
                return false;

            // A registration in AppData\Roaming may point only to caches,
            // sessions, cloud-sync files, etc. Count it only when an actual
            // executable lives in that specific app folder (e.g. uTorrent).
            if (path.StartsWith(roaming.TrimEnd('\\') + @"\", StringComparison.OrdinalIgnoreCase))
                return HasApplicationExecutable(path);

            // MSIX registration names the individual package directory.
            // Do not count the WindowsApps parent directory itself.
            if (app.Kind == "MSIX")
            {
                string windowsApps = Path.Combine(programFiles, "WindowsApps");
                if (path.Equals(windowsApps, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or System.Security.SecurityException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool HasApplicationExecutable(string folder)
    {
        try
        {
            // Only check the install folder, not arbitrary profile subtrees.
            foreach (string file in Directory.EnumerateFiles(folder, "*.exe",
                         SearchOption.TopDirectoryOnly).Take(50))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (!name.StartsWith("unins", StringComparison.OrdinalIgnoreCase) &&
                    !name.StartsWith("uninstall", StringComparison.OrdinalIgnoreCase) &&
                    !name.StartsWith("setup", StringComparison.OrdinalIgnoreCase) &&
                    !name.StartsWith("update", StringComparison.OrdinalIgnoreCase) &&
                    !name.StartsWith("crash", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            // Some desktop apps keep the executable in one immediately nested
            // version or bin directory. Do not inspect arbitrary cache trees.
            foreach (string directory in Directory.EnumerateDirectories(folder).Take(40))
            {
                string name = Path.GetFileName(directory);
                if (!name.Equals("bin", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals("Application", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals("current", StringComparison.OrdinalIgnoreCase) &&
                    !name.StartsWith("app-", StringComparison.OrdinalIgnoreCase))
                    continue;
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                    continue;
                if (Directory.EnumerateFiles(directory, "*.exe",
                        SearchOption.TopDirectoryOnly).Take(20).Any(file =>
                        !Path.GetFileNameWithoutExtension(file).StartsWith("unins",
                            StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or ArgumentException) { }
        return false;
    }

    private static (long Bytes, bool Complete) MeasureFolder(
        string root, CancellationToken token)
    {
        long total = 0;
        int entries = 0;
        bool complete = true;
        Stopwatch local = Stopwatch.StartNew();
        Stack<(string Directory, int Depth)> pending = new();
        pending.Push((root, 0));
        try
        {
            while (pending.Count != 0)
            {
                token.ThrowIfCancellationRequested();
                if (local.Elapsed >= FolderBudget ||
                    entries > 400000)
                    return (total, false);
                (string current, int depth) = pending.Pop();
                if (depth > 40) return (total, false);
                try
                {
                    if (InstalledProgramLocationService.HasReparseAncestor(current))
                    {
                        complete = false;
                        continue;
                    }
                    foreach (FileSystemInfo item in new DirectoryInfo(current).EnumerateFileSystemInfos())
                    {
                        token.ThrowIfCancellationRequested();
                        if (++entries % 128 == 0)
                        {
                            if (local.Elapsed >= FolderBudget ||
                                entries > 400000)
                                return (total, false);
                            // One low-impact reader, deliberately yielding to the OS
                            // and other applications. This runs only on a worker thread.
                            Thread.Sleep(8);
                        }
                        if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            // The measurement's scope is this directory's own
                            // files, not targets of symbolic links/junctions.
                            // A deliberately excluded target is not an IO failure.
                            continue;
                        }
                        if (item is DirectoryInfo subfolder)
                            pending.Push((subfolder.FullName, depth + 1));
                        else if (item is FileInfo file)
                            total = checked(total + file.Length);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                    System.Security.SecurityException)
                {
                    complete = false; // Keep readable sibling directories in the queue.
                }
            }
            return (total, complete);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or System.Security.SecurityException or OverflowException or
            PathTooLongException)
        {
            // Never display an incomplete total as a full application size.
            return (total, false);
        }
    }

    private static string FormatBytes(long bytes)
    {
        double size = bytes;
        string[] units = SettingsService.L("Б,КБ,МБ,ГБ,ТБ", "B,KB,MB,GB,TB")
            .Split(',');
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return $"{size:0.#} {units[unit]}";
    }
}
