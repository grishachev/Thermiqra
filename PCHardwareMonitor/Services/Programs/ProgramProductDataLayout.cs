using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PCHardwareMonitor;

// Derive vendor/product namespaces from the registered path and product title.
// No browser or publisher names, and no traversal of a whole vendor directory.
internal static class ProgramProductDataLayout
{
    internal const string Evidence = "registered-product-data-layout";
    private static bool Generic(string name) => new[] { "application", "applications", "app", "bin", "bin32", "bin64", "x86", "x64", "current",
        "programs", "programfiles", "programfilesx86", "windows", "microsoft", "common", "software", "data" }.Contains(ProgramProductIdentity.Token(name));

    internal static (string Vendor, string Product)? Derive(string name, string installRoot)
    {
        string path = ProgramLeftoverPathPolicy.Normalize(installRoot);
        if (path.Length == 0) return null;
        for (int i = 0; i < 3 && Generic(Path.GetFileName(path)); i++) path = Path.GetDirectoryName(path) ?? "";
        string product = Path.GetFileName(path), vendor = Path.GetFileName(Path.GetDirectoryName(path) ?? "");
        string title = ProgramProductIdentity.Token(ProgramUserDataLeftoverService.ProductName(name));
        if (product.Length < 3 || vendor.Length < 3 || Generic(product) || Generic(vendor) ||
            ProgramProductIdentity.Token(vendor + product) != title) return null;
        return (vendor, product);
    }

    internal static bool Matches(ProgramLeftoverProbe probe, ProgramRemovalBaseline baseline)
    {
        var layout = Derive(baseline.Name, baseline.InstallRoot);
        return layout.HasValue && ProgramDataFileIdentity.Locations().Contains(probe.EvidenceRoot, StringComparer.OrdinalIgnoreCase) &&
            probe.Path.Equals(Path.Combine(probe.EvidenceRoot, layout.Value.Vendor, layout.Value.Product), StringComparison.OrdinalIgnoreCase);
    }

    internal static void Capture(string name, string installRoot, List<ProgramLeftoverProbe> probes)
    {
        var layout = Derive(name, installRoot);
        if (!layout.HasValue) return;
        foreach (string root in ProgramDataFileIdentity.Locations())
        {
            string path = Path.Combine(root, layout.Value.Vendor, layout.Value.Product);
            if (probes.Any(p => p.Kind == "UserData" && p.Path.Equals(path, StringComparison.OrdinalIgnoreCase))) continue;
            probes.Add(new("UserData", path, ProgramLeftoverGroup.NeedsReview,
                "Пространство данных издателя и продукта из зарегистрированного пути установки. Состав и использование другими программами будут проверены.",
                "Vendor/product data namespace derived from the registered installation path. Contents and use by other applications will be checked.",
                EvidenceRoot: root, EvidenceTarget: Evidence));
        }
    }

    internal static ProgramRemovalBaseline Extend(ProgramRemovalBaseline baseline)
    {
        // Retire old name-only guesses for generic containers such as AppData\Application.
        var probes = baseline.Probes.Where(p => !(p.Kind == "UserData" && Generic(Path.GetFileName(p.Path)) &&
            p.EvidenceTarget.Length == 0 && p.EvidenceFingerprint.Length == 0)).ToList();
        Capture(baseline.Name, baseline.InstallRoot, probes);
        return baseline with { Probes = probes };
    }
}
