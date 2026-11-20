using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;

namespace PCHardwareMonitor;

internal static partial class InstalledProgramIconService
{
    private static ImageSource? TryReadAssociatedShortcutIcon(string installLocation, IReadOnlyList<string> shortcuts)
    {
        string root = ProgramLeftoverPathPolicy.Normalize(installLocation);
        if (root.Length == 0 || !Directory.Exists(root) || IsWindowsSystemDirectory(root) ||
            InstalledProgramLocationService.IsBroadContainerDirectory(root) ||
            InstalledProgramLocationService.HasReparseAncestor(root)) return null;

        string folderName = NormalizeAppName(Path.GetFileName(root));
        if (folderName.Length < 3) return null;
        List<(string Shortcut, string Target, string Icon)> candidates = new();
        foreach (string shortcut in shortcuts)
        {
            // Start-menu groups often have the application folder's name even
            // when the shortcut label contains a role (manager, editor, help).
            // Filter before COM metadata reads; do not inspect every shortcut
            // anew for every application whose installer omitted DisplayIcon.
            string group = NormalizeAppName(Path.GetFileName(Path.GetDirectoryName(shortcut)) ?? "");
            string label = NormalizeAppName(Path.GetFileNameWithoutExtension(shortcut));
            if (group != folderName && label != folderName &&
                !(label.StartsWith(folderName, StringComparison.Ordinal) && label.Length > folderName.Length)) continue;
            var metadata = ReadShortcutMetadata(shortcut);
            string target = ProgramLeftoverPathPolicy.Normalize(metadata.Target);
            if (!IsUsableProgramExecutable(target) || !ProgramLeftoverPathPolicy.Inside(target, root) ||
                !File.Exists(target) || InstalledProgramLocationService.HasReparseAncestor(target)) continue;
            candidates.Add((shortcut, target, metadata.Icon));
        }
        // A suite may expose several unrelated tools. Root association alone
        // must not select the first executable in such a suite arbitrarily.
        if (candidates.Select(c => c.Target).Distinct(StringComparer.OrdinalIgnoreCase).Take(2).Count() != 1) return null;
        var candidate = candidates[0];
        return TryReadDisplayIcon(candidate.Icon) ?? TryReadDisplayIcon(candidate.Target) ?? TryReadShortcutIcon(candidate.Shortcut);
    }
}
