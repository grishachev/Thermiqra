using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PCHardwareMonitor;

// A Store application can keep ordinary desktop data outside its package.
// Derive vendor/product scopes from the recorded package identity, not a list
// of application names. Never claim the vendor parent or Windows package store.
internal static class ProgramPackageDataLayout
{
    internal const string Evidence = "registered-package-product-data";
    private static string Token(string text) => ProgramProductIdentity.Token(text);
    private static bool Specific(string text) => text.Length >= 3 && !new[] {
        "windows", "microsoft", "packages", "windowsapps", "programs", "common", "data", "appdata",
        "application", "applications", "settings", "config", "cache", "temp", "thermiqra" }.Contains(text);

    private static (string Vendor, HashSet<string> Products)? Namespace(string identity, string name)
    {
        if (!identity.StartsWith("msix:", StringComparison.OrdinalIgnoreCase) || identity.Length > 512) return null;
        string family = identity[5..];
        int suffix = family.LastIndexOf('_');
        if (suffix <= 0 || suffix == family.Length - 1) return null;
        string package = family[..suffix];
        if (package.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-')) || package.Contains("..")) return null;
        int dot = package.LastIndexOf('.');
        if (dot <= 0 || dot == package.Length - 1) return null;
        string vendor = Token(package[..dot]);
        var products = new HashSet<string>(new[] { Token(package[(dot + 1)..]),
            Token(ProgramUserDataLeftoverService.ProductName(name)) }.Where(Specific), StringComparer.OrdinalIgnoreCase);
        return vendor.Length >= 3 && products.Count > 0 ? (vendor, products) : null;
    }

    internal static bool MatchesNamespace(string path, string root, string identity, string name)
    {
        var scope = Namespace(identity, name);
        path = ProgramLeftoverPathPolicy.Normalize(path); root = ProgramLeftoverPathPolicy.Normalize(root);
        if (scope == null || root.Length == 0 || !ProgramLeftoverPathPolicy.Inside(path, root)) return false;
        string[] parts = Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar);
        return parts.Length == 2 && Token(parts[0]) == scope.Value.Vendor && scope.Value.Products.Contains(Token(parts[1]));
    }

    internal static bool Matches(ProgramLeftoverProbe probe, ProgramRemovalBaseline baseline) =>
        probe.Kind == "UserData" && ProgramDataFileIdentity.Locations().Contains(probe.EvidenceRoot, StringComparer.OrdinalIgnoreCase) &&
        MatchesNamespace(probe.Path, probe.EvidenceRoot, baseline.Identity, baseline.Name);

    internal static bool ExternalDataScope(ProgramLeftoverProbe probe, ProgramRemovalBaseline? baseline = null)
    {
        if (probe.Kind != "UserData" || probe.EvidenceFingerprint.Length == 0 || probe.EvidenceTarget.Length == 0 ||
            !ProgramDataFileIdentity.Locations().Contains(probe.EvidenceRoot, StringComparer.OrdinalIgnoreCase)) return false;
        if (baseline != null && probe.EvidenceTarget == Evidence && !Matches(probe, baseline)) return false;
        string path = ProgramLeftoverPathPolicy.Normalize(probe.Path), root = ProgramLeftoverPathPolicy.Normalize(probe.EvidenceRoot);
        if (path == root || !ProgramLeftoverPathPolicy.Inside(path, root)) return false;
        string[] parts = Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar);
        return parts.Length is 1 or 2 && !parts.Any(p => new[] { "packages", "windowsapps", "modifiablewindowsapps", "programs", "thermiqra", "temp" }.Contains(Token(p)));
    }

    internal static void Capture(string identity, string name, List<ProgramLeftoverProbe> probes,
        List<ProgramReviewLimitation> limits, IReadOnlyList<string>? roots = null)
    {
        var scope = Namespace(identity, name);
        if (scope == null) return;
        var known = probes.Where(p => p.Kind == "UserData").Select(p => p.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var timer = Stopwatch.StartNew();
        foreach (string root in roots ?? ProgramDataFileIdentity.Locations())
        {
            try
            {
                ProgramLeftoverCleanupPolicy.CheckAncestors(root);
                if (!Directory.Exists(root)) continue;
                int count = 0;
                foreach (string vendor in Directory.EnumerateDirectories(root))
                {
                    if (++count > 2000 || timer.ElapsedMilliseconds > 3000)
                    { limits.Add(new("Поиск внешних данных пакета ограничен", "External package data search was bounded", root)); break; }
                    if (Token(Path.GetFileName(vendor)) != scope.Value.Vendor) continue;
                    ProgramLeftoverCleanupPolicy.CheckAncestors(vendor);
                    int children = 0;
                    foreach (string product in Directory.EnumerateDirectories(vendor))
                    {
                        if (++children > 500 || timer.ElapsedMilliseconds > 3000)
                        { limits.Add(new("Поиск внешних данных пакета ограничен", "External package data search was bounded", vendor)); break; }
                        if (!scope.Value.Products.Contains(Token(Path.GetFileName(product))) || !known.Add(product)) continue;
                        probes.Add(new("UserData", product, ProgramLeftoverGroup.NeedsReview,
                            "Отдельная папка продукта в пространстве издателя из регистрации пакета. Перед очисткой проверяются состав и другие установки продукта.",
                            "Dedicated product folder in the vendor namespace recorded by the package. Contents and other product installations are checked before cleanup.",
                            EvidenceRoot: root, EvidenceTarget: Evidence));
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
            { limits.Add(new("Внешние данные пакета не удалось полностью проверить", "External package data could not be fully checked", root + ": " + ex.Message)); }
        }
    }
}
