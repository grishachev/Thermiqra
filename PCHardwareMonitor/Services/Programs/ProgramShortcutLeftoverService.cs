using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace PCHardwareMonitor;

internal static class ProgramShortcutLeftoverService
{
    private static IEnumerable<(string Path, bool Recursive)> Roots()
    {
        foreach (var item in new[] { (Environment.SpecialFolder.DesktopDirectory, false),
            (Environment.SpecialFolder.CommonDesktopDirectory, false), (Environment.SpecialFolder.Programs, true),
            (Environment.SpecialFolder.CommonPrograms, true) })
        {
            string root = ProgramLeftoverPathPolicy.Normalize(Environment.GetFolderPath(item.Item1));
            if (root.Length > 0) yield return (root, item.Item2);
        }
    }

    private static void CheckSource(string path)
    {
        if (path != ProgramLeftoverPathPolicy.Normalize(path) || !Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
            !Roots().Any(r => r.Recursive ? ProgramLeftoverPathPolicy.Inside(path, r.Path) :
                string.Equals(Path.GetDirectoryName(path), r.Path, StringComparison.OrdinalIgnoreCase))) ProgramLeftoverCleanupPolicy.Deny();
        ProgramLeftoverCleanupPolicy.CheckAncestors(path);
        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) ProgramLeftoverCleanupPolicy.Deny();
    }

    private static string Fingerprint(string path)
    {
        CheckSource(path);
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 1024 * 1024) throw new IOException(SettingsService.L("Ярлык превышает допустимый размер.", "Shortcut exceeds the supported size."));
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string Target(string path, bool fresh = false) =>
        ProgramLeftoverPathPolicy.Normalize(InstalledProgramIconService.ReadShortcutMetadata(path, fresh).Target);

    internal static void Capture(string root, List<ProgramLeftoverProbe> probes, List<ProgramReviewLimitation> limitations)
    {
        Stopwatch budget = Stopwatch.StartNew(); int visited = 0;
        foreach (var source in Roots())
        {
            if (!Directory.Exists(source.Path)) continue;
            Stack<(string Path, int Depth)> pending = new(); pending.Push((source.Path, 0));
            while (pending.Count > 0)
            {
                var directory = pending.Pop();
                try
                {
                    ProgramLeftoverCleanupPolicy.CheckAncestors(directory.Path);
                    foreach (string path in Directory.EnumerateFileSystemEntries(directory.Path))
                    {
                        if (++visited > 3000 || budget.ElapsedMilliseconds > 1500)
                        { limitations.Add(new("Поиск ярлыков ограничен", "Shortcut search was bounded")); return; }
                        FileAttributes attributes = File.GetAttributes(path);
                        if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                        if ((attributes & FileAttributes.Directory) != 0)
                        { if (source.Recursive && directory.Depth < 8) pending.Push((path, directory.Depth + 1)); continue; }
                        if (!Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase)) continue;
                        string target = Target(path);
                        if (!Path.GetExtension(target).Equals(".exe", StringComparison.OrdinalIgnoreCase) || !ProgramLeftoverPathPolicy.Inside(target, root)) continue;
                        string hash = Fingerprint(path); target = Target(path, true);
                        if (hash != Fingerprint(path) || !ProgramLeftoverPathPolicy.Inside(target, root) ||
                            !Path.GetExtension(target).Equals(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                        probes.Add(new("Shortcut", path, ProgramLeftoverGroup.Related,
                            "Цель ярлыка наблюдалась внутри каталога программы. Очистка возможна только после исчезновения цели и при неизменившемся ярлыке.",
                            "Shortcut target was observed inside the application directory. Cleanup requires a missing target and an unchanged shortcut.",
                            EvidenceRoot: root, EvidenceTarget: target, EvidenceFingerprint: hash));
                    }
                }
                catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
                { limitations.Add(new("Источник ярлыков проверен не полностью", "Shortcut source was only partially checked", directory.Path + ": " + ex.Message)); }
            }
        }
    }

    internal static void Validate(ProgramLeftoverProbe probe)
    {
        CheckSource(probe.Path);
        if (probe.Kind != "Shortcut" || probe.Group != ProgramLeftoverGroup.Related || probe.EvidenceFingerprint.Length != 64 ||
            probe.EvidenceTarget.Length == 0 || !Path.GetExtension(probe.EvidenceTarget).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            ProgramLeftoverCleanupPolicy.Deny();
        string root = ProgramLeftoverCleanupPolicy.Root(probe);
        ProgramLeftoverCleanupPolicy.CheckRoot(root);
        if (!ProgramLeftoverPathPolicy.Inside(probe.EvidenceTarget, root) ||
            Fingerprint(probe.Path) != probe.EvidenceFingerprint || Target(probe.Path, true) != probe.EvidenceTarget ||
            Fingerprint(probe.Path) != probe.EvidenceFingerprint)
            throw new IOException(SettingsService.L("Ярлык изменился после сохранения связи; очистка исключена.", "Shortcut changed after association capture; cleanup excluded."));
        ProgramLeftoverCleanupPolicy.CheckAncestors(probe.EvidenceTarget);
        try { File.GetAttributes(probe.EvidenceTarget); }
        catch (FileNotFoundException) { return; }
        catch (DirectoryNotFoundException) { return; }
        throw new IOException(SettingsService.L("Файл цели всё ещё существует; ярлык сохраняется.", "The target file still exists; the shortcut is retained."));
    }

    internal static ProgramLeftoverProbe Inspect(ProgramLeftoverProbe probe)
    {
        File.GetAttributes(probe.Path); // Missing links are omitted by the caller.
        if (probe.Group != ProgramLeftoverGroup.Related) return probe;
        try { Validate(probe); return probe; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
        {
            return probe with { Group = ProgramLeftoverGroup.NeedsReview,
                ReasonRu = "Очистка ярлыка исключена: " + ex.Message,
                ReasonEn = "Shortcut cleanup excluded: " + ex.Message };
        }
    }
}
