using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PCHardwareMonitor;

// Unreal installations use an internal project name for AppData. Derive it
// from recorded package/executable layout, never from a title abbreviation list.
internal static class ProgramGameDataLocations
{
    private static bool Specific(string name) => name.Length is >= 3 and <= 100 &&
        name.All(c => char.IsLetterOrDigit(c) || c is '_' or '-') &&
        !new[] { "Engine", "Windows", "Common", "Programs", "UnrealGame", "Epic" }.Contains(name, StringComparer.OrdinalIgnoreCase);

    internal static List<string> Aliases(ProgramRemovalBaseline baseline)
    {
        var snapshot = ProgramRemovalReviewStore.ReadObjects(baseline.Id);
        var remembered = (baseline.GameProjects ?? new()).Where(n => n != null && Specific(n)).Take(16).ToList();
        if (snapshot == null) return remembered;
        var paths = snapshot.Files.Where(f => !f.Directory && f.Root.Equals(baseline.InstallRoot, StringComparison.OrdinalIgnoreCase))
            .Select(f => f.RelativePath.Replace('/', '\\')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return paths.Where(p => !p.Contains('\\') && p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileNameWithoutExtension).OfType<string>().Where(Specific)
            .Where(name => paths.Any(p => p.StartsWith(name + @"\Binaries\Win64\", StringComparison.OrdinalIgnoreCase) &&
                p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) &&
                paths.Any(p => p.StartsWith(name + @"\Content\Paks\", StringComparison.OrdinalIgnoreCase)))
            .Concat(remembered).Distinct(StringComparer.OrdinalIgnoreCase).Take(16).ToList();
    }

    internal static List<string> LiveAliases(string root)
    {
        List<string> names = new();
        if (root.Length == 0 || !Directory.Exists(root)) return names;
        ProgramLeftoverCleanupPolicy.CheckRoot(root);
        ProgramLeftoverCleanupPolicy.CheckAncestors(root);
        foreach (string executable in Directory.EnumerateFiles(root, "*.exe").Take(64))
        {
            string name = Path.GetFileNameWithoutExtension(executable);
            if (!Specific(name)) continue;
            string binaries = Path.Combine(root, name, "Binaries", "Win64");
            string packages = Path.Combine(root, name, "Content", "Paks");
            if (!Directory.Exists(binaries) || !Directory.Exists(packages)) continue;
            ProgramLeftoverCleanupPolicy.CheckAncestors(binaries);
            ProgramLeftoverCleanupPolicy.CheckAncestors(packages);
            if (Directory.EnumerateFiles(binaries, "*.exe").Take(1).Any() && Directory.EnumerateFiles(packages).Take(1).Any()) names.Add(name);
        }
        return names;
    }

    internal static ProgramRemovalBaseline Extend(ProgramRemovalBaseline baseline)
    {
        var probes = baseline.Probes.ToList();
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var names = Aliases(baseline);
        foreach (string name in names)
        {
            string path = Path.Combine(local, name);
            if (probes.Any(p => p.Kind == "UserData" && p.Path.Equals(path, StringComparison.OrdinalIgnoreCase))) continue;
            probes.Add(new("UserData", path, ProgramLeftoverGroup.NeedsReview,
                "Профиль игрового проекта из сохранённой структуры установки; связь будет проверена по журналу игры.",
                "Game project profile from the recorded installation layout; association will be checked against the game log.", EvidenceRoot: local));
        }
        return baseline with { Probes = probes, GameProjects = names };
    }
}
