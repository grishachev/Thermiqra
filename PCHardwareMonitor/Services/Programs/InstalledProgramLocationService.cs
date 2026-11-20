using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using System.Threading;

namespace PCHardwareMonitor;

/// <summary>
/// Read-only location discovery. An uninstall registration is a hint, not
/// proof that a directory is owned by the registered application.
/// No location returned here may be used as an automatic deletion target.
/// </summary>
internal static class InstalledProgramLocationService
{
    private static readonly StringComparison IgnoreCase =
        StringComparison.OrdinalIgnoreCase;

    internal static string ResolveProgramLocation(
        string registeredLocation, string registryIcon, string displayName,
        IReadOnlyList<string> shortcuts, string uninstallCommand,
        string msiInstallLocation, string msiProductCode = "", CancellationToken token = default, Action<string>? trace = null)
    {
        string registered = ExistingDirectory(registeredLocation);
        string msi = ExistingDirectory(msiInstallLocation);
        trace?.Invoke("InstallLocation: " + (string.IsNullOrWhiteSpace(registeredLocation)
            ? "not registered" : registered.Length > 0 ? "available" : "missing, inaccessible or rejected"));
        trace?.Invoke("MSI location: " + (string.IsNullOrWhiteSpace(msiInstallLocation)
            ? "not registered" : msi.Length > 0 ? "available" : "missing, inaccessible or rejected"));
        string Found(string source, string path)
        {
            trace?.Invoke(path.Length > 0 ? "Selected source: " + source : "No confirmed directory after all available sources");
            return path;
        }
        string iconFile = InstalledProgramIconService.ExtractIconFilePath(registryIcon) ?? "";
        string iconFolder = VerifiedExecutableFolder(iconFile, displayName, true);

        // A real launcher is a stronger source than InstallLocation: the latter
        // is often a parent container or a data directory. However, retain a
        // verified registered root if the launcher lives inside that root.
        if (iconFolder.Length > 0)
            return Found("DisplayIcon", PreferRegisteredRoot(iconFolder, registered, msi));
        if (IsSpecificFolder(registered) && HasDirectAppExecutable(registered, displayName))
            return Found("InstallLocation", registered);
        if (IsSpecificFolder(msi) && HasDirectAppExecutable(msi, displayName))
            return Found("MSI InstallLocation", msi);

        string shortcutFolder = FindShortcutFolder(displayName, shortcuts);
        if (shortcutFolder.Length > 0)
            return Found("shortcut target", PreferRegisteredRoot(shortcutFolder, registered, msi));

        string appPathsFolder = FindAppPathsFolder(displayName);
        if (appPathsFolder.Length > 0)
            return Found("App Paths", PreferRegisteredRoot(appPathsFolder, registered, msi));

        string runFolder = FindStartupExecutableFolder(displayName);
        if (runFolder.Length > 0)
            return Found("startup executable", PreferRegisteredRoot(runFolder, registered, msi));

        string vendorFolder = FindRecognizedVendorLocation(displayName);
        if (vendorFolder.Length > 0)
            return Found("vendor registration", PreferRegisteredRoot(vendorFolder, registered, msi));

        // A confirmed per-program uninstaller is not the same as MSI's shared
        // msiexec.exe. 'uninstall.exe' was accidentally omitted previously.
        string uninstallerFolder = FindUninstallerFolder(uninstallCommand);
        if (uninstallerFolder.Length > 0)
            return Found("uninstall executable", PreferRegisteredRoot(uninstallerFolder, registered, msi));

        if (registered.Length == 0 && msi.Length == 0 && msiProductCode.Length > 0)
        {
            var componentFolders = InstalledProgramMsiService.ReadExecutablePaths(msiProductCode, token)
                .Select(path => VerifiedExecutableFolder(path, displayName, true))
                .Where(folder => folder.Length > 0 && IsSpecificFolder(folder))
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToList();
            // Multiple unrelated component directories do not define one root.
            if (componentFolders.Count == 1) return Found("MSI component key path", componentFolders[0]);
        }

        // Do not erase valid, specific installer locations just because their
        // executable happens to use a different name (often vendor-specific).
        if (IsSpecificFolder(registered)) return Found("InstallLocation", registered);
        if (IsSpecificFolder(msi)) return Found("MSI InstallLocation", msi);

        string child = FindUnambiguousChild(registered, displayName);
        if (child.Length == 0) child = FindUnambiguousChild(msi, displayName);
        return Found("verified child directory", child);
    }

    private static string PreferRegisteredRoot(string executableFolder,
        string registered, string msi)
    {
        foreach (string root in new[] { registered, msi })
        {
            if (IsSpecificFolder(root) &&
                IsInsideOrSame(executableFolder, root) &&
                !IsBroadContainerDirectory(root))
                return root;
        }
        return executableFolder;
    }

    internal static bool IsBroadContainerDirectory(string raw)
    {
        string path = Normalize(raw);
        if (path.Length == 0) return true;

        string? root = Path.GetPathRoot(path);
        if (root == null || path.TrimEnd('\\').Equals(root.TrimEnd('\\'), IgnoreCase))
            return true;

        string[] commonRoots =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        if (commonRoots.Any(x => x.Length > 0 &&
            path.Equals(x.TrimEnd('\\'), IgnoreCase))) return true;
        if (InstalledProgramIconService.IsWindowsSystemDirectory(path)) return true;

        // Installer caches and transient launchers are not reliable
        // application locations, even if they contain a matching .exe.
        if (path.Contains(@"\Temp\", IgnoreCase) ||
            path.Contains(@"\Package Cache\", IgnoreCase) ||
            path.Contains(@"\Windows\Installer\", IgnoreCase))
            return true;

        string name = Path.GetFileName(path);
        // A drive may contain many separately installed applications.
        // Generic container names cannot identify ownership of its files.
        return new[]
        {
            "Programs", "Program Files", "Program Files (x86)",
            "Applications", "Apps", "Games", "GameLibrary", "Library",
            "SteamLibrary", "steamapps", "Common", "Common Files",
            "WindowsApps", "Package Cache", "Installer", "Temp"
        }.Any(x => x.Equals(name, IgnoreCase));
    }

    private static string ExistingDirectory(string raw)
    {
        string path = Normalize(raw);
        try
        {
            if (path.Length == 0 || !Path.IsPathFullyQualified(path) ||
                path.StartsWith(@"\\", StringComparison.Ordinal) ||
                !Directory.Exists(path) ||
                HasReparseAncestor(path))
                return "";
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            return "";
        }
    }

    internal static bool HasReparseAncestor(string path)
    {
        // A normal leaf can still be reached through a junction higher up.
        try
        {
            for (DirectoryInfo? directory = new(path); directory != null; directory = directory.Parent)
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) return true;
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or System.Security.SecurityException) { return true; }
    }

    private static string Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        try
        {
            string path = Environment.ExpandEnvironmentVariables(raw.Trim().Trim('"'));
            if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal))
                return "";
            return Path.GetFullPath(path).TrimEnd('\\', '/');
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or
            PathTooLongException) { return ""; }
    }

    private static bool IsSpecificFolder(string path) =>
        path.Length > 0 && !IsBroadContainerDirectory(path);

    private static bool IsInsideOrSame(string folder, string parent) =>
        folder.Equals(parent, IgnoreCase) ||
        folder.StartsWith(parent.TrimEnd('\\') + "\\", IgnoreCase);

    private static string VerifiedExecutableFolder(string path, string displayName, bool registeredIcon = false)
    {
        try
        {
            if (!InstalledProgramIconService.IsUsableProgramExecutable(path) ||
                !File.Exists(path)) return "";
            string folder = ExistingDirectory(Path.GetDirectoryName(path) ?? "");
            if (folder.Length == 0 || InstalledProgramIconService.IsWindowsSystemDirectory(folder) ||
                !InstalledProgramIconService.IsUsableUninstallerFolder(folder)) return "";
            // A real app executable can live directly in a shared container.
            // Display its location, but the size service must reject that root.
            if (ExecutableRelatedToApp(displayName, path)) return folder;
            if (!IsSpecificFolder(folder)) return "";
            // The per-program DisplayIcon registry value is itself an
            // association. Generic tools (explorer, msiexec, rundll32) are
            // never accepted; otherwise a vendor may freely name its EXE.
            if (registeredIcon && !IsGenericExecutable(path) &&
                InstalledProgramIconService.IsUsableUninstallerFolder(folder)) return folder;
            return "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or System.Security.SecurityException) { return ""; }
    }

    private static bool ExecutableRelatedToApp(string displayName, string executable)
    {
        string wanted = Identifier(displayName);
        if (wanted.Length < 3) return false;
        string stem = Identifier(Path.GetFileNameWithoutExtension(executable));
        if (SubstantialMatch(wanted, stem)) return true;

        DirectoryInfo? directory = new FileInfo(executable).Directory;
        for (int level = 0; directory != null && level < 3;
             directory = directory.Parent, level++)
        {
            string part = Identifier(directory.Name);
            if (!IsBroadContainerDirectory(directory.FullName) &&
                SubstantialMatch(wanted, part)) return true;
        }

        // Localised names do not necessarily resemble the EXE, e.g.
        // Яндекс.Диск vs YandexDisk2 or Бамбук ТВ vs BambukTV.
        try
        {
            FileVersionInfo version = FileVersionInfo.GetVersionInfo(executable);
            if (SubstantialMatch(wanted, Identifier(version.ProductName ?? "")) ||
                SubstantialMatch(wanted, Identifier(version.FileDescription ?? "")))
                return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or System.Security.SecurityException or
            System.ComponentModel.Win32Exception) { }
        return false;
    }

    internal static bool NamesRelated(string displayName, string candidateName) =>
        SubstantialMatch(Identifier(displayName), Identifier(candidateName));

    private static bool SubstantialMatch(string wanted, string candidate)
    {
        if (wanted.Length >= 3 && wanted.Equals(candidate, StringComparison.Ordinal)) return true;
        if (wanted.Length < 4 || candidate.Length < 4) return false;
        // Executables commonly have a numeric suffix (YandexDisk2.exe).
        string withoutSuffix = candidate.TrimEnd('0', '1', '2', '3', '4',
            '5', '6', '7', '8', '9');
        if (withoutSuffix.Length >= 5 && wanted.Equals(withoutSuffix,
            StringComparison.Ordinal)) return true;
        // Don't confuse unrelated products whose names share a short prefix.
        return (candidate.Length >= 5 && wanted.Contains(candidate, StringComparison.Ordinal)) ||
               (wanted.Length >= 5 && candidate.Contains(wanted, StringComparison.Ordinal));
    }

    private static bool IsGenericExecutable(string path)
    {
        string stem = Path.GetFileNameWithoutExtension(path);
        return new[] { "msiexec", "rundll32", "explorer", "cmd", "powershell",
            "pwsh", "regsvr32", "svchost", "conhost", "setup", "update",
            "uninstall", "unins000", "unins001", "launcher" }
            .Any(x => stem.Equals(x, IgnoreCase));
    }

    private static string Identifier(string name)
    {
        // Consistent Russian/English matching; invariant letters alone cannot
        // match translated app names. Preserve digits but omit punctuation.
        const string ru = "абвгдежзийклмнопрстуфхцчшщъыьэюяё";
        string[] en = { "a", "b", "v", "g", "d", "e", "zh", "z", "i", "y",
            "k", "l", "m", "n", "o", "p", "r", "s", "t", "u", "f", "h",
            "ts", "ch", "sh", "sch", "", "y", "", "e", "yu", "ya", "yo" };
        System.Text.StringBuilder result = new();
        foreach (char original in name)
        {
            char ch = char.ToLowerInvariant(original);
            int index = ru.IndexOf(ch);
            if (index >= 0) result.Append(en[index]);
            else if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9')) result.Append(ch);
        }
        return result.ToString().Replace("ks", "x", StringComparison.Ordinal);
    }

    private static bool ExecutableFolderClearlyMatches(
        string displayName, string folder, string iconFile)
    {
        if (!string.IsNullOrEmpty(iconFile) && File.Exists(iconFile) &&
            Path.GetDirectoryName(iconFile)?.Equals(folder, IgnoreCase) == true &&
            ExecutableRelatedToApp(displayName, iconFile)) return true;
        string name = Identifier(Path.GetFileName(folder));
        string wanted = Identifier(displayName);
        return name.Length >= 4 && wanted.Length >= 4 &&
            (wanted.Contains(name, StringComparison.Ordinal) ||
             name.Contains(wanted, StringComparison.Ordinal));
    }

    private static bool HasDirectAppExecutable(string folder, string displayName)
    {
        try
        {
            return Directory.EnumerateFiles(folder, "*.exe", SearchOption.TopDirectoryOnly)
                .Take(50)
                .Any(file => InstalledProgramIconService.IsUsableProgramExecutable(file) &&
                    ExecutableRelatedToApp(displayName, file));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or System.Security.SecurityException) { return false; }
    }

    private static string FindShortcutFolder(string displayName, IReadOnlyList<string> shortcuts)
    {
        foreach (string shortcut in shortcuts)
        {
            if (!InstalledProgramIconService.ShortcutMatchesProgram(displayName, shortcut))
                continue;
            (string target, _) = InstalledProgramIconService.ReadShortcutMetadata(shortcut);
            string folder = VerifiedExecutableFolder(target, displayName);
            if (folder.Length > 0) return folder;
        }
        return "";
    }

    private static string FindAppPathsFolder(string displayName)
    {
        string wanted = Identifier(displayName);
        if (wanted.Length < 3) return "";
        const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
        foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using RegistryKey parent = RegistryKey.OpenBaseKey(hive, view);
                using RegistryKey? appPaths = parent.OpenSubKey(keyPath, false);
                if (appPaths == null) continue;
                foreach (string name in appPaths.GetSubKeyNames())
                {
                    if (!name.EndsWith(".exe", IgnoreCase)) continue;
                    string stem = Identifier(Path.GetFileNameWithoutExtension(name));
                    if (stem.Length < 3 ||
                        !(wanted.Equals(stem, StringComparison.Ordinal) ||
                          (stem.Length >= 5 && wanted.Contains(stem, StringComparison.Ordinal)) ||
                          NamesRelated(displayName, Path.GetFileNameWithoutExtension(name))))
                        continue;
                    using RegistryKey? item = appPaths.OpenSubKey(name, false);
                    string target = Convert.ToString(item?.GetValue(null)) ?? "";
                    string folder = VerifiedExecutableFolder(
                        Environment.ExpandEnvironmentVariables(target.Trim('"')), displayName);
                    if (folder.Length > 0) return folder;
                    // Some registrations expose only the App Paths search path,
                    // not the default executable value. Verify the named EXE in
                    // each registered directory; never treat Path itself as a root.
                    string searchPath = Convert.ToString(item?.GetValue("Path")) ?? "";
                    foreach (string part in searchPath.Split(';', StringSplitOptions.RemoveEmptyEntries).Take(8))
                    {
                        string directory = ExistingDirectory(part);
                        if (directory.Length == 0) continue;
                        folder = VerifiedExecutableFolder(Path.Combine(directory, name), displayName);
                        if (folder.Length > 0) return folder;
                    }
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or
                System.Security.SecurityException or ArgumentException or
                PlatformNotSupportedException) { }
        }
        return "";
    }

    private static string FindUninstallerFolder(string command)
    {
        string? path = InstalledProgramIconService.ExtractExecutablePath(command);
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "";
        string stem = Path.GetFileNameWithoutExtension(path);
        if (!(stem.StartsWith("unins", IgnoreCase) ||
              stem.StartsWith("remove", IgnoreCase) ||
              stem.StartsWith("uninstall", IgnoreCase))) return "";
        string folder = ExistingDirectory(Path.GetDirectoryName(path) ?? "");
        return IsSpecificFolder(folder) &&
            InstalledProgramIconService.IsUsableUninstallerFolder(folder) ? folder : "";
    }

    private static string FindStartupExecutableFolder(string displayName)
    {
        string wanted = Identifier(displayName);
        if (wanted.Length < 3) return "";
        const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using RegistryKey root = RegistryKey.OpenBaseKey(hive, view);
                using RegistryKey? run = root.OpenSubKey(keyPath, false);
                if (run == null) continue;
                foreach (string name in run.GetValueNames())
                {
                    if (!SubstantialMatch(wanted, Identifier(name))) continue;
                    string command = Convert.ToString(run.GetValue(name)) ?? "";
                    string executable = InstalledProgramIconService.ExtractExecutablePath(command) ?? "";
                    string folder = VerifiedExecutableFolder(executable, displayName);
                    if (folder.Length > 0) return folder;
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or
                System.Security.SecurityException or ArgumentException or
                PlatformNotSupportedException) { }
        }
        return "";
    }

    private static string FindRecognizedVendorLocation(string displayName)
    {
        // Steam's uninstall registration can incorrectly be the containing
        // A:\Programs folder; Valve separately records the executable path.
        if (!Identifier(displayName).Equals("steam", StringComparison.Ordinal)) return "";
        foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using RegistryKey root = RegistryKey.OpenBaseKey(hive, view);
                using RegistryKey? key = root.OpenSubKey(@"SOFTWARE\Valve\Steam", false);
                if (key == null) continue;
                string exe = Convert.ToString(key.GetValue("SteamExe")) ?? "";
                string folder = VerifiedExecutableFolder(exe.Trim('"'), displayName);
                if (folder.Length > 0) return folder;
                string install = ExistingDirectory(Convert.ToString(key.GetValue("InstallPath")) ?? "");
                if (IsSpecificFolder(install) &&
                    File.Exists(Path.Combine(install, "steam.exe"))) return install;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or
                System.Security.SecurityException or ArgumentException or
                PlatformNotSupportedException) { }
        }
        return "";
    }

    private static string FindUnambiguousChild(string broadParent, string displayName)
    {
        if (broadParent.Length == 0 || !IsBroadContainerDirectory(broadParent)) return "";
        string wanted = Identifier(displayName);
        if (wanted.Length < 3) return "";
        try
        {
            // Maximum 200 immediate children. If ambiguous, return unknown.
            List<string> matches = new();
            foreach (string sub in Directory.EnumerateDirectories(broadParent).Take(200))
            {
                if (!Identifier(Path.GetFileName(sub)).Equals(wanted, StringComparison.Ordinal))
                    continue;
                string folder = ExistingDirectory(sub);
                if (!IsSpecificFolder(folder)) continue;
                if (!Directory.EnumerateFiles(folder, "*.exe", SearchOption.TopDirectoryOnly)
                    .Take(30).Any(file =>
                        InstalledProgramIconService.IsUsableProgramExecutable(file) &&
                        ExecutableRelatedToApp(displayName, file)))
                    continue;
                matches.Add(folder);
                if (matches.Count > 1) return "";
            }
            return matches.Count == 1 ? matches[0] : "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or System.Security.SecurityException) { return ""; }
    }
}
