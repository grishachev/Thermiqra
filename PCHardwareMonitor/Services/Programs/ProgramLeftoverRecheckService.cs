using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PCHardwareMonitor;

internal static class ProgramLeftoverRecheckService
{
    internal static async Task<ProgramLeftoverReport> RecheckAsync(ProgramRemovalBaseline baseline)
    {
        InstalledProgramsReadStatus status = new();
        var desktop = await Task.Run(() => InstalledProgramsDiscoveryService.ReadRegistryPrograms(default, status));
        var packages = await InstalledProgramsDiscoveryService.ReadStorePackagesAsync(CancellationToken.None);
        if (status.HasFailures || !packages.Available) throw new IOException(SettingsService.L(
            "Не все источники Windows доступны. Прежние сведения сохранены.", "Some Windows sources are unavailable. Previous information was retained."));
        var inventory = desktop.Concat(packages.Apps).ToList();
        bool present = inventory.Any(a => a.Identity.Equals(baseline.Identity, StringComparison.OrdinalIgnoreCase));
        if (baseline.Identity.StartsWith("registry:", StringComparison.Ordinal))
        {
            using var registration = InstalledProgramUninstallPlanReader.OpenRegistration(baseline.Identity);
            present |= registration != null;
        }
        // A failed native attempt remains inspectable even if a partial
        // registration survives. Cleanup stays blocked by NativeResult.
        if (present && (baseline.NativeResult == null || baseline.NativeResult.State == InstalledProgramRemovalState.Removed))
            throw new IOException(SettingsService.L("Программа снова зарегистрирована. Прежнее удаление не анализируется.",
                "The application is registered again. Its previous removal is not analyzed."));
        if (baseline.NativeResult is { } native)
            baseline = baseline with { NativeResult = native with { RegistrationAbsent = !present } };
        string root = !string.IsNullOrWhiteSpace(baseline.InstallRoot) ? baseline.InstallRoot :
            baseline.Probes.FirstOrDefault(p => p.Kind == "Directory" && p.Group == ProgramLeftoverGroup.Related)?.Path ?? "";
        string kind = baseline.Identity.StartsWith("msix:", StringComparison.Ordinal) ? "MSIX" : "Win32";
        InstalledProgramInfo app = new(baseline.Name, baseline.Publisher ?? "", baseline.Version,
            kind, "—", root, "", Identity: baseline.Identity);
        InstalledProgramUninstallPlan plan = new(baseline.Identity, baseline.Name, baseline.Version, kind, "", "", false);
        var refreshed = await Task.Run(() => ProgramLeftoverDiscoveryService.Capture(app, plan, inventory));
        string Key(ProgramLeftoverProbe p) => ProgramRegistryIdentity.Key(p);
        var probes = baseline.Probes.Select(ProgramRegistryIdentity.Canonical).GroupBy(Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in refreshed.Probes)
        {
            string key = Key(candidate);
            if (probes.TryGetValue(key, out var original))
                probes[key] = candidate.Group == ProgramLeftoverGroup.Protected ? candidate : original;
            else probes[key] = candidate.Kind is "Registry" or "Shortcut" && candidate.Group == ProgramLeftoverGroup.Related
                ? candidate with { Group = ProgramLeftoverGroup.NeedsReview,
                    ReasonRu = "Объект найден после удаления. Его связь с программой до удаления не наблюдалась; очистка не разрешена.",
                    ReasonEn = "Object was found after uninstall. Its association was not observed before removal; cleanup is not permitted." }
                : candidate;
        }
        if (refreshed.Probes.Any(p => p.Kind == "Directory" && p.Path.Equals(root, StringComparison.OrdinalIgnoreCase) &&
            p.Group == ProgramLeftoverGroup.Protected))
        {
            foreach (string key in probes.Keys.ToList())
            {
                var probe = probes[key];
                if (probe.Group == ProgramLeftoverGroup.Related && probe.EvidenceRoot.Equals(root, StringComparison.OrdinalIgnoreCase))
                    probes[key] = probe with { Group = ProgramLeftoverGroup.Protected,
                        ReasonRu = "Исходный каталог теперь общий или защищённый; очистка связанных записей исключена.",
                        ReasonEn = "The original directory is now shared or protected; associated registrations are excluded from cleanup." };
            }
        }
        List<ProgramReviewLimitation> limitations = baseline.Limitations.Concat(refreshed.Limitations).Distinct().ToList();
        if (probes.Count > 5000) limitations.Add(new("Число проверяемых объектов ограничено", "Checked object count was bounded"));
        var updated = baseline with { Probes = probes.Values.Take(5000).ToList(), Limitations = limitations,
            InstallRoot = root, Publisher = app.Publisher };
        var report = await Task.Run(() => ProgramLeftoverDiscoveryService.Inspect(updated));
        if (updated.ObservationOnly)
        {
            await Task.Run(() =>
            {
            for (int i = 0; i < report.Found.Count; i++)
            {
                var probe = report.Found[i];
                if (probe.Kind == "Directory" && probe.Group == ProgramLeftoverGroup.Related &&
                    !ProgramLeftoverCleanupPolicy.IsEmptyDirectoryTree(probe.Path))
                    report.Found[i] = probe with { Group = ProgramLeftoverGroup.NeedsReview,
                        ReasonRu = "В каталоге есть файлы или его содержимое не удалось проверить. Наблюдалось только исчезновение записи программы; очистка файлов не разрешена.",
                        ReasonEn = "The directory contains files or could not be inspected. Only registration disappearance was observed; file cleanup is not permitted." };
            }
            });
        }
        await Task.Run(() => ProgramRemovalReviewStore.SaveReport(report));
        return report;
    }
}
