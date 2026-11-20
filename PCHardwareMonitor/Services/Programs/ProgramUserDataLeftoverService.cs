using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace PCHardwareMonitor;

internal static partial class ProgramUserDataLeftoverService
{
    internal static string ProductName(string name) => Regex.Replace(new string(name.Take(512).ToArray()),
        @"\s+(?:version|release|версия)\s+.*$|\s+v?\d+(?:\.\d+)+(?:\s.*)?$", "",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100)).Trim();
    private static string Key(string name) => new(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static bool Specific(string key) => key.Length >= 3 && !new[] { "microsoft", "windows", "software", "common",
        "data", "application", "applications", "app", "bin", "bin32", "bin64", "x86", "x64", "current", "programs", "packages", "appdata", "local", "locallow", "roaming", "temp", "cache", "settings", "config", "thermiqra" }.Contains(key);
    private static HashSet<string> Names(string name, string installRoot) => new(new[] { Key(ProductName(name)),
        Key(Path.GetFileName(ProgramLeftoverPathPolicy.Normalize(installRoot))) }.Where(Specific), StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> Roots()
    {
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (roaming.Length > 0) yield return ProgramLeftoverPathPolicy.Normalize(roaming);
        if (local.Length > 0)
        {
            yield return ProgramLeftoverPathPolicy.Normalize(local);
            string? parent = Path.GetDirectoryName(local);
            if (parent != null) yield return ProgramLeftoverPathPolicy.Normalize(Path.Combine(parent, "LocalLow"));
        }
    }

    internal static void Capture(InstalledProgramInfo app, IReadOnlyList<InstalledProgramInfo> inventory,
        List<ProgramLeftoverProbe> probes, List<ProgramReviewLimitation> limitations, IReadOnlyList<string> projects)
    {
        ProgramPackageDataLayout.Capture(app.Identity, app.Name, probes, limitations);
        ProgramProductDataLayout.Capture(app.Name, app.InstallLocation, probes);
        var names = Names(app.Name, app.InstallLocation);
        names.UnionWith(projects.Select(Key));
        string publisher = Key(app.Publisher);
        foreach (string root in Roots())
        {
            try
            {
                ProgramLeftoverCleanupPolicy.CheckAncestors(root);
                HashSet<string> candidates = new(StringComparer.OrdinalIgnoreCase);
                // Include expected paths even before they exist: uninstallers
                // sometimes create configuration files while unregistering.
                foreach (string label in new[] { ProductName(app.Name), Path.GetFileName(ProgramLeftoverPathPolicy.Normalize(app.InstallLocation)) }.Concat(projects))
                    if (names.Contains(Key(label)) && label.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
                        candidates.Add(Path.Combine(root, label));
                if (Directory.Exists(root))
                    foreach (string directory in Directory.EnumerateDirectories(root).Take(1000))
                    {
                        string leaf = Key(Path.GetFileName(directory));
                        if (names.Contains(leaf)) candidates.Add(directory);
                        if (publisher.Length < 3 || leaf != publisher || InstalledProgramLocationService.HasReparseAncestor(directory)) continue;
                        foreach (string child in Directory.EnumerateDirectories(directory).Take(200))
                            if (names.Contains(Key(Path.GetFileName(child)))) candidates.Add(child);
                    }
                foreach (string path in candidates)
                {
                    bool shared = Conflicts(path, app.Identity, inventory);
                    probes.Add(new("UserData", path, shared ? ProgramLeftoverGroup.Protected : ProgramLeftoverGroup.UserData,
                        shared ? "Папка может использоваться другой установленной программой: очистка исключена." : "Данные программы; принадлежность будет проверена после удаления.",
                        shared ? "Another installed application may use this folder: cleanup excluded." : "Application data; ownership will be checked after uninstall.", EvidenceRoot: root));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
            { limitations.Add(new("Поиск данных пользователя ограничен", "User-data discovery was limited", root + ": " + ex.Message)); }
        }
    }

    internal static ProgramLeftoverProbe Inspect(ProgramLeftoverProbe probe, ProgramRemovalBaseline baseline,
        List<ProgramReviewLimitation> limitations)
    {
        // Presence is checked by the discovery caller. This method only
        // classifies an observed object; failure to prove ownership cannot
        // remove that object from the report.
        try
        {
            if (!Directory.Exists(probe.Path)) return Review(probe);
            if (probe.Group == ProgramLeftoverGroup.Protected) return probe;
            ValidatePath(probe, baseline);
            if (baseline.ObservationOnly) return Review(probe);
            var inventory = ProgramLeftoverCleanupService.LiveInventory(baseline.Identity);
            if (Conflicts(probe.Path, baseline.Identity, inventory)) return Review(probe);
            if (RegisteredDataLayout(probe, baseline))
            {
                ProgramLeftoverCleanupPolicy.CheckInventory(probe.Path, inventory);
                if (inventory.Any(a => Key(ProductName(a.Name)) == Key(ProductName(baseline.Name)))) return Review(probe);
            }
            (string Path, string Hash)? marker = probe.EvidenceTarget == ProgramDataFileIdentity.Evidence
                ? (ProgramDataFileIdentity.Evidence, ProgramDataFileIdentity.Fingerprint(probe, baseline)) : FindMarker(probe.Path, baseline);
            bool profile = probe.EvidenceTarget == ProgramDataFileIdentity.Evidence;
            if (marker == null && RegisteredDataLayout(probe, baseline))
            { marker = (ProgramPackageDataLayout.Matches(probe, baseline) ? ProgramPackageDataLayout.Evidence : ProgramProductDataLayout.Evidence,
                ProfileFingerprint(probe.Path)); profile = true; }
            if (marker == null) { marker = FindRecordedData(probe, baseline); profile = marker != null; }
            if (marker == null) { marker = FindGameProfile(probe, baseline); profile = marker != null; }
            if (marker == null) { marker = FindProfile(probe, baseline); profile = marker != null; }
            if (marker == null && ProgramDataFileIdentity.Eligible(baseline.Name, baseline.Publisher))
            {
                var attributed = probe with { EvidenceTarget = ProgramDataFileIdentity.Evidence };
                marker = (ProgramDataFileIdentity.Evidence, ProgramDataFileIdentity.Fingerprint(attributed, baseline));
                profile = true;
            }
            if (marker == null) return Review(probe);
            if (marker.Value.Path == ProgramDataFileIdentity.Evidence)
            {
                ProgramLeftoverCleanupPolicy.CheckInventory(probe.Path, inventory);
                if (inventory.Any(app => Key(ProductName(app.Name)) == Key(ProductName(baseline.Name)))) return Review(probe);
            }
            CheckTree(probe.Path);
            return probe with { Group = ProgramLeftoverGroup.UserData, EvidenceTarget = marker.Value.Path,
                EvidenceFingerprint = marker.Value.Hash,
                ReasonRu = marker.Value.Path == ProgramPackageDataLayout.Evidence ? "Отдельная папка продукта подтверждена пространством издателя и продукта из регистрации пакета Windows. Она может содержать настройки и данные прежних версий. Выбранная папка целиком отправляется в Корзину после повторной проверки состава и других установок продукта." :
                    marker.Value.Path == ProgramDataFileIdentity.Evidence ? "Все файлы этой отдельной папки имеют совпадающие сведения о продукте и издателе. Папка будет отправлена в Корзину после повторной проверки состава и использования другими программами." :
                    profile ? "Отдельная папка данных программы: связь проверена по сохранённому снимку или структуре профиля. В Корзину уйдёт вся папка, включая настройки, учётные данные, проекты и сохранения внутри неё. Перед очисткой содержимое и использование другими программами проверяются повторно." :
                    "Папка данных программы подтверждена её конфигурацией. Включает настройки, закладки и другие данные; выбранная папка целиком отправляется в Корзину.",
                ReasonEn = marker.Value.Path == ProgramPackageDataLayout.Evidence ? "Dedicated product folder confirmed by the vendor and product namespace in Windows package registration. It may contain settings and data from earlier versions. The entire selected folder goes to Recycle Bin after rechecking its contents and other product installations." :
                    marker.Value.Path == ProgramDataFileIdentity.Evidence ? "Every file in this dedicated folder has matching product and publisher metadata. The folder goes to Recycle Bin after rechecking its contents and use by other applications." :
                    profile ? "Dedicated application data folder: association checked against the recorded inventory or profile structure. The entire folder goes to Recycle Bin, including settings, credentials, projects and saved games inside it. Contents and use by other applications are checked again before cleanup." :
                    "Application configuration confirms this data folder. It includes settings, bookmarks and other data; the entire selected folder goes to Recycle Bin." };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or System.Text.Json.JsonException or InvalidOperationException)
        {
            limitations.Add(new("Содержимое папки данных не удалось полностью проверить", "Data-folder contents could not be fully checked",
                probe.Path + ": " + ex.Message));
            return Review(probe);
        }
    }

    private static ProgramLeftoverProbe Review(ProgramLeftoverProbe probe) => probe with { Group = ProgramLeftoverGroup.NeedsReview,
        ReasonRu = "Папка похожа на данные программы, но её принадлежность или безопасность очистки не подтверждена. Она не выбрана для удаления.",
        ReasonEn = "This may be application data, but ownership or cleanup safety is unconfirmed. It is not selected for removal." };

    private static bool RegisteredDataLayout(ProgramLeftoverProbe probe, ProgramRemovalBaseline baseline) =>
        ProgramProductDataLayout.Matches(probe, baseline) || ProgramPackageDataLayout.Matches(probe, baseline);

    internal static ProgramRemovalBaseline ExtendPackageData(ProgramRemovalBaseline baseline)
    {
        if (!baseline.Identity.StartsWith("msix:", StringComparison.OrdinalIgnoreCase)) return baseline;
        var probes = baseline.Probes.ToList(); var limits = baseline.Limitations.ToList();
        var app = new InstalledProgramInfo(baseline.Name, baseline.Publisher, baseline.Version, "MSIX", "—",
            baseline.InstallRoot, "", Identity: baseline.Identity);
        Capture(app, Array.Empty<InstalledProgramInfo>(), probes, limits, Array.Empty<string>());
        return baseline with { Probes = probes.DistinctBy(ProgramRegistryIdentity.Key, StringComparer.OrdinalIgnoreCase).ToList(),
            Limitations = limits.Distinct().ToList() };
    }

    private static (string Path, string Hash)? FindMarker(string folder, ProgramRemovalBaseline baseline)
    {
        var names = Names(baseline.Name, baseline.InstallRoot);
        string installRoot = ProgramLeftoverPathPolicy.Normalize(baseline.InstallRoot);
        string[] extensions = { ".ini", ".conf", ".cfg", ".json", ".xml", ".yaml", ".yml", ".toml", ".txt" };
        foreach (string source in new[] { folder, Path.Combine(folder, "config"), Path.Combine(folder, "settings") })
        {
            if (!Directory.Exists(source)) continue;
            ProgramLeftoverCleanupPolicy.CheckAncestors(source);
            foreach (string file in Directory.EnumerateFiles(source).Take(256).Where(p => extensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase)).Take(24))
            {
                if (Path.GetExtension(file).Equals(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    string stem = Path.GetFileNameWithoutExtension(file);
                    if (!new[] { "conf", "config", "settings", "preferences" }.Any(prefix =>
                        stem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) continue;
                }
                ProgramLeftoverCleanupPolicy.CheckAncestors(file);
                using FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (stream.Length > 65536) continue;
                byte[] bytes = new byte[65537]; int count = 0, read;
                while (count < bytes.Length && (read = stream.Read(bytes, count, bytes.Length - count)) > 0) count += read;
                if (count == bytes.Length) continue;
                Encoding encoding = count >= 2 && bytes[0] == 0xff && bytes[1] == 0xfe ? Encoding.Unicode :
                    count >= 2 && bytes[0] == 0xfe && bytes[1] == 0xff ? Encoding.BigEndianUnicode : Encoding.UTF8;
                string text = encoding.GetString(bytes, 0, count).TrimStart('\uFEFF');
                // The format signature may occur later in a bounded config
                // (e.g. in a configuration-check section), not just its header.
                bool header = text.Split('\n').Any(line => names.Contains(Key(line.Trim())));
                if (!header && Path.GetExtension(file).Equals(".json", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using JsonDocument json = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 16 });
                        if (json.RootElement.ValueKind == JsonValueKind.Object)
                            header = json.RootElement.EnumerateObject().Take(128).Any(property =>
                                new[] { "application", "applicationname", "appname", "product", "productname" }.Contains(Key(property.Name)) &&
                                property.Value.ValueKind == JsonValueKind.String && names.Contains(Key(ProductName(property.Value.GetString() ?? ""))));
                    }
                    catch (JsonException) { }
                }
                bool reference = installRoot.Length > 0 && !InstalledProgramLocationService.IsBroadContainerDirectory(installRoot) &&
                    (text.Contains(installRoot + "\\", StringComparison.OrdinalIgnoreCase) || text.Contains(installRoot.Replace("\\", "\\\\") + "\\\\", StringComparison.OrdinalIgnoreCase));
                if (header || reference) return (file, Convert.ToHexString(SHA256.HashData(bytes.AsSpan(0, count))));
            }
        }
        return null;
    }
}
