using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace PCHardwareMonitor;

// A registered install directory may retain local configuration/resources.
// The isolated registered installation folder is the explicit cleanup scope.
// A snapshot identifies that scope, not ownership of each resource inside it.
// Links, shared/system/personal roots and newly recreated roots stay excluded.
internal static class ProgramRecordedInstallCleanup
{
    internal const string DirectoryEvidence = "recorded-install-directory";
    private static readonly HashSet<string> Binaries = new(StringComparer.OrdinalIgnoreCase)
    { ".exe", ".dll", ".mui", ".pdb", ".ocx", ".tlb" };

    private static Dictionary<string, ProgramFootprintFile> ReadFiles(string root, ProgramRemovalBaseline baseline)
    {
        if (baseline.ObservationOnly || !root.Equals(baseline.InstallRoot, StringComparison.OrdinalIgnoreCase))
            ProgramLeftoverCleanupPolicy.Deny();
        ProgramLeftoverCleanupPolicy.CheckRoot(root);
        ProgramLeftoverCleanupPolicy.CheckAncestors(root);
        ProgramSharedComponentGuard.Check(root);
        var before = ProgramRemovalReviewStore.ReadObjects(baseline.Id);
        if (before == null) throw new IOException(SettingsService.L("Нет снимка файлов перед удалением.", "No pre-uninstall file inventory is available."));
        return before.Files.Where(f => f.Root.Equals(root, StringComparison.OrdinalIgnoreCase))
            .GroupBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    private static void CheckEntry(string root, FileSystemInfo entry, IReadOnlyDictionary<string, ProgramFootprintFile> before)
    {
        string relative = Path.GetRelativePath(root, entry.FullName);
        if (!before.TryGetValue(relative, out var recorded)) throw new IOException(SettingsService.L(
            "Файл или папка отсутствует в снимке перед удалением.", "The file or folder is missing from the pre-uninstall inventory."));
        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
            ProgramLeftoverCleanupPolicy.Deny();
        bool directory = (entry.Attributes & FileAttributes.Directory) != 0;
        if (recorded!.Directory != directory) ProgramLeftoverCleanupPolicy.Deny();
        if (entry is FileInfo file && (file.Length != recorded.Length || file.LastWriteTimeUtc.Ticks != recorded.LastWriteUtcTicks))
            ProgramLeftoverCleanupPolicy.Deny();
    }

    private static string DirectoryFingerprint(string root)
    {
        Stack<string> pending = new(); pending.Push(root);
        DirectoryInfo rootInfo = new(root);
        List<string> entries = new() { "root|" + rootInfo.CreationTimeUtc.Ticks }; Stopwatch timer = Stopwatch.StartNew();
        while (pending.TryPop(out string? directory))
        {
            ProgramLeftoverCleanupPolicy.CheckAncestors(directory);
            foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                if (entries.Count >= 4000 || timer.Elapsed > TimeSpan.FromSeconds(2)) ProgramLeftoverCleanupPolicy.Deny();
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) ProgramLeftoverCleanupPolicy.Deny();
                entries.Add(Path.GetRelativePath(root, entry.FullName) + "|" +
                    (entry is FileInfo file ? file.Length + "|" + file.LastWriteTimeUtc.Ticks : "directory"));
                if (entry is DirectoryInfo) pending.Push(entry.FullName);
            }
        }
        entries.Sort(StringComparer.OrdinalIgnoreCase);
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(entries)));
    }

    internal static ProgramLeftoverProbe ClassifyDirectory(ProgramLeftoverProbe probe, ProgramRemovalBaseline baseline)
    {
        _ = ReadFiles(probe.Path, baseline);
        if (new DirectoryInfo(probe.Path).CreationTimeUtc > baseline.CapturedUtc)
            throw new IOException(SettingsService.L("Папка создана заново после снимка. Очистка прежней установки не применяется.",
                "The folder was recreated after the snapshot. Previous-installation cleanup does not apply."));
        string fingerprint = DirectoryFingerprint(probe.Path);
        return probe with { Group = ProgramLeftoverGroup.UserData, EvidenceRoot = probe.Path,
            EvidenceTarget = DirectoryEvidence, EvidenceFingerprint = fingerprint,
            ReasonRu = "Отдельная папка установки из регистрации Windows. Очистка включает все оставшиеся файлы, профили и настройки внутри неё. Перед действием состав папки и использование другими установленными программами проверяются повторно.",
            ReasonEn = "Dedicated installation folder from Windows registration. Cleanup includes all remaining files, profiles and settings inside it. Contents and use by other installed applications are rechecked before the operation." };
    }

    internal static IEnumerable<ProgramLeftoverProbe> ClassifyFiles(ProgramLeftoverProbe parent, ProgramRemovalBaseline baseline)
    {
        var before = ReadFiles(parent.Path, baseline);
        List<ProgramLeftoverProbe> candidates = new();
        Stopwatch timer = Stopwatch.StartNew();
        foreach (var file in before.Values.Where(f => !f.Directory).Take(4000))
        {
            if (candidates.Count >= 32 || timer.Elapsed > TimeSpan.FromSeconds(2)) break;
            try
            {
                string path = Path.GetFullPath(Path.Combine(file.Root, file.RelativePath));
                if (!ProgramLeftoverPathPolicy.Inside(path, parent.Path)) continue;
                ProgramLeftoverCleanupPolicy.CheckAncestors(path);
                FileInfo current = new(path);
                if (!current.Exists) continue;
                CheckEntry(parent.Path, current, before);
                bool binary = Binaries.Contains(current.Extension);
                candidates.Add(new("RecordedFile", path, binary ? ProgramLeftoverGroup.Related : ProgramLeftoverGroup.UserData,
                    binary ? "Файл программы был учтён перед удалением и не изменился. Будет отправлен в Корзину." :
                        "Локальный файл данных в папке установки был учтён перед удалением и не изменился. Он может содержать настройки или данные программы; выбранный файл отправляется в Корзину.",
                    binary ? "This application file was recorded before uninstall and is unchanged. It goes to Recycle Bin." :
                        "This local data file was recorded in the installation folder before uninstall and is unchanged. It may contain application settings or data; the selected file goes to Recycle Bin.",
                    EvidenceRoot: parent.Path, EvidenceFingerprint: Fingerprint(current)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException) { }
        }
        return candidates;
    }

    internal static void Validate(ProgramLeftoverProbe probe, ProgramRemovalBaseline baseline)
    {
        var before = ReadFiles(ProgramLeftoverCleanupPolicy.Root(probe), baseline);
        if (probe.Kind == "Directory" && probe.EvidenceTarget == DirectoryEvidence)
        {
            if (new DirectoryInfo(probe.Path).CreationTimeUtc > baseline.CapturedUtc) ProgramLeftoverCleanupPolicy.Deny();
            if (DirectoryFingerprint(probe.Path) != probe.EvidenceFingerprint) ProgramLeftoverCleanupPolicy.Deny();
        }
        else if (probe.Kind == "RecordedFile")
        {
            ProgramLeftoverCleanupPolicy.CheckAncestors(probe.Path);
            if (!ProgramLeftoverPathPolicy.Inside(probe.Path, probe.EvidenceRoot)) ProgramLeftoverCleanupPolicy.Deny();
            FileInfo current = new(probe.Path); CheckEntry(probe.EvidenceRoot, current, before);
            if (Fingerprint(current) != probe.EvidenceFingerprint) ProgramLeftoverCleanupPolicy.Deny();
        }
        else ProgramLeftoverCleanupPolicy.Deny();
    }

    private static string Fingerprint(FileInfo file) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new { file.FullName, file.Length, Ticks = file.LastWriteTimeUtc.Ticks })));
}
