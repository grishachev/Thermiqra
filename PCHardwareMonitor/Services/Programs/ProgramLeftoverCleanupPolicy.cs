using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PCHardwareMonitor;

internal static class ProgramLeftoverCleanupPolicy
{
    internal static bool SameEvidence(ProgramLeftoverProbe left, ProgramLeftoverProbe right) =>
        (left with { ReasonRu = "", ReasonEn = "" }) == (right with { ReasonRu = "", ReasonEn = "" });

    internal static bool AlreadyAbsent(ProgramLeftoverProbe probe)
    {
        if (probe.Kind is not ("Directory" or "Shortcut" or "UserData" or "RecordedFile")) return false;
        try { File.GetAttributes(probe.Path); return false; }
        catch (FileNotFoundException) { return true; }
        catch (DirectoryNotFoundException) { return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or
            ArgumentException or NotSupportedException) { return false; }
    }
    internal static string Root(ProgramLeftoverProbe probe) => ProgramLeftoverPathPolicy.Normalize(
        probe.Kind is "Directory" or "UserData" ? probe.Path : probe.EvidenceRoot);

    internal static void CheckRoot(string root)
    {
        if (root.Length == 0 || root != ProgramLeftoverPathPolicy.Normalize(root) ||
            InstalledProgramLocationService.IsBroadContainerDirectory(root) ||
            ProgramLeftoverPathPolicy.UserContent(root)) Deny();
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (ProgramLeftoverPathPolicy.Inside(root, windows)) Deny();
        string packages = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
        if (ProgramLeftoverPathPolicy.Inside(root, packages)) Deny();
        foreach (string system in new[] { "WindowsApps", "ModifiableWindowsApps", "$Recycle.Bin", "System Volume Information" })
            if (root.Split('\\').Any(part => part.Equals(system, StringComparison.OrdinalIgnoreCase))) Deny();
        foreach (Environment.SpecialFolder folder in new[] { Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolder.CommonApplicationData })
        {
            string data = Environment.GetFolderPath(folder);
            // The registered application directory is allowed, but shared
            // storage containers and package data are not cleanup roots.
            if (root.Equals(data, StringComparison.OrdinalIgnoreCase)) Deny();
        }
        string own = ProgramLeftoverPathPolicy.Normalize(AppContext.BaseDirectory);
        if (ProgramLeftoverPathPolicy.Inside(own, root) || ProgramLeftoverPathPolicy.Inside(root, own)) Deny();
        string parent = Path.GetDirectoryName(root) ?? "";
        if (parent.Length == 0 || InstalledProgramLocationService.HasReparseAncestor(parent)) Deny();
        if (Directory.Exists(root) && InstalledProgramLocationService.HasReparseAncestor(root)) Deny();
    }

    internal static void CheckInventory(string root, IReadOnlyList<InstalledProgramInfo> inventory)
    {
        CheckRoot(root);
        foreach (InstalledProgramInfo app in inventory)
        {
            string other = ProgramLeftoverPathPolicy.Normalize(app.InstallLocation);
            if (other.Length == 0) continue;
            if (ProgramLeftoverPathPolicy.Inside(other, root) ||
                (!InstalledProgramLocationService.IsBroadContainerDirectory(other) && ProgramLeftoverPathPolicy.Inside(root, other))) Deny();
        }
    }

    internal static void CheckDirectory(string root)
    {
        CheckRoot(root);
        ProgramSharedComponentGuard.Check(root);
        // Unknown formats and settings are preserved. This first cleanup scope
        // accepts empty directories and executable resources only, never saves.
        HashSet<string> binaries = new(StringComparer.OrdinalIgnoreCase) { ".exe", ".dll", ".mui", ".pdb", ".ocx", ".tlb" };
        HashSet<string> content = new(StringComparer.OrdinalIgnoreCase) { "save", "saves", "savedata", "savegames",
            "saved games", "userdata", "profiles", "documents", "downloads", "backups", "screenshots" };
        Stack<string> pending = new(); pending.Push(root);
        int count = 0;
        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) Deny();
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                if (++count > 5000) throw new IOException(SettingsService.L("Каталог слишком велик для этой очистки.", "Directory exceeds this cleanup's review limit."));
                FileAttributes attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) Deny();
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (content.Contains(Path.GetFileName(entry))) Deny();
                    pending.Push(entry);
                }
                else if (!binaries.Contains(Path.GetExtension(entry)))
                    throw new IOException(SettingsService.L(
                        "Каталог содержит настройки, документы или файлы неизвестного назначения; очистка исключена.",
                        "Directory contains settings, documents or files of unknown purpose; cleanup excluded."));
            }
        }
    }

    internal static void CheckAncestors(string path)
    {
        for (DirectoryInfo? current = new(path); current != null; current = current.Parent)
        {
            try
            {
                if ((File.GetAttributes(current.FullName) & FileAttributes.ReparsePoint) != 0) Deny();
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    internal static string BackupPath(string original, string id) => Path.Combine(Path.GetDirectoryName(original)!,
        "." + Path.GetFileName(original) + ".ThermiqraBackup-" + id);

    internal static bool IsEmptyDirectoryTree(string root)
    {
        try
        {
            System.Diagnostics.Stopwatch budget = System.Diagnostics.Stopwatch.StartNew();
            Stack<string> pending = new(); pending.Push(root); int count = 0;
            while (pending.Count > 0)
            {
                string directory = pending.Pop();
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) return false;
                foreach (string path in Directory.EnumerateFileSystemEntries(directory))
                {
                    if (++count > 1000 || budget.ElapsedMilliseconds > 500) return false;
                    FileAttributes attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.Directory) == 0 || (attributes & FileAttributes.ReparsePoint) != 0) return false;
                    pending.Push(path);
                }
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
        { return false; }
    }

    internal static void Deny() => throw new IOException(SettingsService.L(
        "Объект защищён, связан с другой установкой или его принадлежность не подтверждена.",
        "Object is protected, overlaps another installation or its ownership is unconfirmed."));
}
