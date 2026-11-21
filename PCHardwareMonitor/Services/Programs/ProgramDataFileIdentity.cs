using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PCHardwareMonitor;

// Generic resource-identity evidence. No product, framework, directory suffix,
// or installer filename list. Metadata is read without executing any binary.
internal static class ProgramDataFileIdentity
{
    internal const string Evidence = "binary-product-directory";
    private static string Token(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static bool Match(FileVersionInfo version, string name, string publisher) =>
        Token(ProgramUserDataLeftoverService.ProductName(version.ProductName ?? "")) == Token(ProgramUserDataLeftoverService.ProductName(name)) &&
        Token(version.CompanyName ?? "") == Token(publisher);
    internal static bool Eligible(string name, string publisher) => Token(ProgramUserDataLeftoverService.ProductName(name)).Length >= 4 &&
        Token(publisher).Length >= 3 && !new[] { "microsoft", "windows", "electron", "software", "installer", "setup", "update" }
            .Contains(Token(ProgramUserDataLeftoverService.ProductName(name)));

    internal static List<string> Locations() => new[] { Environment.SpecialFolder.LocalApplicationData,
        Environment.SpecialFolder.ApplicationData }.Select(Environment.GetFolderPath)
        .Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "..", "LocalLow"))
        .Select(ProgramLeftoverPathPolicy.Normalize).Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    internal static bool Scoped(string path, string dataRoot)
    {
        if (!Locations().Contains(dataRoot, StringComparer.OrdinalIgnoreCase) ||
            !ProgramLeftoverPathPolicy.Inside(path, dataRoot) || path.Equals(dataRoot, StringComparison.OrdinalIgnoreCase)) return false;
        var parts = Path.GetRelativePath(dataRoot, path).Split('\\');
        return parts.Length is 1 or 2 && !parts.Any(p => new[] { "Microsoft", "Windows", "Packages", "Programs", "Temp", "Thermiqra" }
            .Contains(p, StringComparer.OrdinalIgnoreCase));
    }

    internal static ProgramRemovalBaseline Extend(ProgramRemovalBaseline baseline)
    {
        var probes = baseline.Probes.ToList(); var limits = RefreshSearchLimitations(baseline.Limitations);
        Discover(baseline.Name, baseline.Publisher, probes, limits);
        return baseline with { Probes = probes, Limitations = limits.Distinct().ToList() };
    }

    internal static List<ProgramReviewLimitation> RefreshSearchLimitations(IEnumerable<ProgramReviewLimitation> previous) =>
        previous.Where(l => l.ReasonEn != "File identity association search was bounded by time or folder count").ToList();

    internal static void Discover(string name, string publisher, List<ProgramLeftoverProbe> probes, List<ProgramReviewLimitation> limits,
        Func<string, IEnumerable<string>>? enumerateDirectories = null)
    {
        if (!Eligible(name, publisher)) return;
        enumerateDirectories ??= Directory.EnumerateDirectories;
        var known = probes.Where(p => p.Kind == "UserData").Select(p => p.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Complete this shallow search on the existing background worker. A wall-clock
        // deadline makes identical data look incomplete on slower/antivirus-scanned PCs.
        // Scope stays at two levels under AppData; protected roots and links stay excluded.
        foreach (string root in Locations())
        {
            try
            {
                ProgramLeftoverCleanupPolicy.CheckAncestors(root);
                if (!Directory.Exists(root)) continue;
                foreach (string parent in enumerateDirectories(root))
                {
                    if (!Scoped(parent, root) || InstalledProgramLocationService.HasReparseAncestor(parent)) continue;
                    Inspect(parent, root);
                    try
                    {
                        foreach (string child in enumerateDirectories(parent))
                        {
                            Inspect(child, root);
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            { limits.Add(new("Часть сведений файлов недоступна", "Some file identity metadata was inaccessible", root)); }
        }

        void Inspect(string folder, string root)
        {
            if (known.Contains(folder) || !Scoped(folder, root) || InstalledProgramLocationService.HasReparseAncestor(folder)) return;
            try
            {
                foreach (string file in Directory.EnumerateFiles(folder, "*.exe"))
                {
                    ProgramLeftoverCleanupPolicy.CheckAncestors(file);
                    if (!Match(FileVersionInfo.GetVersionInfo(file), name, publisher)) continue;
                    known.Add(folder);
                    probes.Add(new("UserData", folder, ProgramLeftoverGroup.NeedsReview,
                        "Сведения исполняемого файла совпадают с продуктом и издателем. Состав папки будет проверен отдельно.",
                        "Binary metadata matches the product and publisher. Folder contents will be validated separately.",
                        EvidenceRoot: root, EvidenceTarget: Evidence));
                    break;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                System.ComponentModel.Win32Exception or System.Security.SecurityException) { }
        }
    }

    internal static string Fingerprint(ProgramLeftoverProbe probe, ProgramRemovalBaseline baseline)
    {
        if (probe.EvidenceTarget != Evidence || !Eligible(baseline.Name, baseline.Publisher) ||
            !Scoped(probe.Path, probe.EvidenceRoot)) ProgramLeftoverCleanupPolicy.Deny();
        ProgramLeftoverCleanupPolicy.CheckRoot(probe.Path);
        ProgramLeftoverCleanupPolicy.CheckAncestors(probe.Path);
        // Whole-directory cleanup is permitted only when EVERY file carries
        // the same product AND publisher. Mixed/unknown content stays review-only.
        List<string> entries = new(); int files = 0;
        Stopwatch timer = Stopwatch.StartNew(); Stack<string> pending = new(); pending.Push(probe.Path);
        while (pending.TryPop(out string? folder))
        {
            foreach (FileSystemInfo entry in new DirectoryInfo(folder).EnumerateFileSystemInfos())
            {
                if (entries.Count >= 128 || timer.ElapsedMilliseconds > 2000 || (entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    ProgramLeftoverCleanupPolicy.Deny();
                bool directory = (entry.Attributes & FileAttributes.Directory) != 0;
                if (directory) pending.Push(entry.FullName);
                else
                {
                    if (!new[] { ".exe", ".dll" }.Contains(entry.Extension, StringComparer.OrdinalIgnoreCase)) ProgramLeftoverCleanupPolicy.Deny();
                    if (!Match(FileVersionInfo.GetVersionInfo(entry.FullName), baseline.Name, baseline.Publisher)) ProgramLeftoverCleanupPolicy.Deny();
                    files++;
                }
                entries.Add(JsonSerializer.Serialize(new { Path = Path.GetRelativePath(probe.Path, entry.FullName), Directory = directory,
                    Length = entry is FileInfo file ? file.Length : 0, Modified = entry.LastWriteTimeUtc.Ticks }));
            }
        }
        if (files == 0) ProgramLeftoverCleanupPolicy.Deny();
        entries.Sort(StringComparer.Ordinal);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", entries))));
    }
}
