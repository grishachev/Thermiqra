using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;

namespace PCHardwareMonitor;

internal static partial class InstalledProgramIconService
{
    internal static List<string> EnumerateStartMenuShortcuts()
    {
        List<string> result = new();
        string[] roots = {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "Windows", "Start Menu", "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Microsoft", "Windows", "Start Menu", "Programs")
        };
        foreach (string root in roots)
        {
            try
            {
                if (Directory.Exists(root))
                    result.AddRange(Directory.EnumerateFiles(root, "*.lnk",
                        root.Equals(roots[0], StringComparison.OrdinalIgnoreCase) ||
                        root.Equals(roots[1], StringComparison.OrdinalIgnoreCase)
                            ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories)
                        .Take(3000));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
        return result;
    }

    private static string NormalizeAppName(string input)
    {
        return new string(input.Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant).ToArray());
    }

    internal static bool IsUsableProgramExecutable(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        string name = Path.GetFileNameWithoutExtension(path);
        if (name.Length == 0 ||
            name.StartsWith("unins", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("uninstall", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("setup", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("update", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("crash", StringComparison.OrdinalIgnoreCase)) return false;
        return string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase);
    }

    internal static string? ExtractExecutablePath(string raw)
    {
        string value = Environment.ExpandEnvironmentVariables(raw.Trim());
        if (value.StartsWith('"'))
        {
            int end = value.IndexOf('"', 1);
            return end > 1 ? value[1..end] : null;
        }
        int position = value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return position > 0 ? value[..(position + 4)].Trim() : null;
    }

    internal static bool ShortcutMatchesProgram(string displayName, string shortcut)
    {
        string wanted = NormalizeAppName(displayName);
        string candidate = NormalizeAppName(Path.GetFileNameWithoutExtension(shortcut));
        // Short names such as MAX are reliable only when matched exactly.
        if (wanted.Length < 3 || candidate.Length < 3) return false;
        if (string.Equals(wanted, candidate, StringComparison.Ordinal))
            return true;
        // MAX Launcher and MAX (Desktop) should count as MAX shortcuts,
        // but Maxon / 3dsMax must not match merely because of the prefix.
        string rawName = Path.GetFileNameWithoutExtension(shortcut);
        if (wanted.Length == 3 && rawName.StartsWith(displayName,
            StringComparison.CurrentCultureIgnoreCase) &&
            rawName.Length > displayName.Length &&
            !char.IsLetterOrDigit(rawName[displayName.Length]))
            return true;
        return (wanted.Length >= 6 && candidate.Length >= 5 &&
            (wanted.Contains(candidate, StringComparison.Ordinal) ||
             candidate.Contains(wanted, StringComparison.Ordinal))) ||
            InstalledProgramLocationService.NamesRelated(displayName, rawName);
    }

    private static readonly object ShortcutLock = new();
    private static readonly Dictionary<string, (DateTime Modified, DateTime Read,
        string Target, string Icon)> ShortcutCache = new(StringComparer.OrdinalIgnoreCase);

    internal static (string Target, string Icon) ReadShortcutMetadata(string shortcut, bool fresh = false)
    {
        DateTime modified;
        try { modified = File.GetLastWriteTimeUtc(shortcut); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or System.Security.SecurityException) { return ("", ""); }
        lock (ShortcutLock)
            if (!fresh && ShortcutCache.TryGetValue(shortcut, out var known) && known.Modified == modified &&
                DateTime.UtcNow - known.Read < TimeSpan.FromMinutes(1))
                return (known.Target, known.Icon);
        var metadata = ReadShortcutMetadataCore(shortcut);
        if (metadata.Target.Length > 0 || metadata.Icon.Length > 0)
        {
            lock (ShortcutLock)
            {
                if (ShortcutCache.Count > 4096) ShortcutCache.Clear();
                ShortcutCache[shortcut] = (modified, DateTime.UtcNow, metadata.Target, metadata.Icon);
            }
        }
        return metadata;
    }

    private static (string Target, string Icon) ReadShortcutMetadataCore(string shortcut)
    {
        // The shell's shortcut COM API reads the real target and icon reference,
        // unlike treating a .lnk itself as an executable. This is optional;
        // locked-down systems can disable WScript.Shell.
        object? shell = null;
        object? link = null;
        try
        {
            Type? type = Type.GetTypeFromProgID("WScript.Shell");
            if (type == null) return ("", "");
            shell = Activator.CreateInstance(type);
            if (shell == null) return ("", "");
            link = type.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod,
                null, shell, new object[] { shortcut });
            if (link == null) return ("", "");
            Type linkType = link.GetType();
            string target = Convert.ToString(linkType.InvokeMember("TargetPath",
                BindingFlags.GetProperty, null, link, null)) ?? "";
            string icon = Convert.ToString(linkType.InvokeMember("IconLocation",
                BindingFlags.GetProperty, null, link, null)) ?? "";
            return (Environment.ExpandEnvironmentVariables(target),
                Environment.ExpandEnvironmentVariables(icon));
        }
        catch (Exception)
        {
            // Failure of an optional icon lookup must not affect the inventory.
            return ("", "");
        }
        finally
        {
            if (link != null && Marshal.IsComObject(link))
                Marshal.ReleaseComObject(link);
            if (shell != null && Marshal.IsComObject(shell))
                Marshal.ReleaseComObject(shell);
        }
    }

    internal static string ResolveProgramLocation(
        string registeredLocation, string registryIcon, string displayName,
        IReadOnlyList<string> shortcuts, string uninstallCommand,
        string msiInstallLocation)
    {
        return InstalledProgramLocationService.ResolveProgramLocation(
            registeredLocation, registryIcon, displayName, shortcuts,
            uninstallCommand, msiInstallLocation);
    }

    internal static bool IsUsableUninstallerFolder(string folder)
    {
        if (IsWindowsSystemDirectory(folder)) return false;
        string normalized = folder.Replace('/', '\\').TrimEnd('\\');
        return !normalized.Contains(@"\Common Files\", StringComparison.OrdinalIgnoreCase) &&
            !normalized.Contains(@"\Package Cache\", StringComparison.OrdinalIgnoreCase) &&
            !normalized.Contains(@"\Temp\", StringComparison.OrdinalIgnoreCase) &&
            !normalized.Contains(@"\Installer\", StringComparison.OrdinalIgnoreCase);
    }

    // MSI stores additional registered metadata in the Windows Installer
    // database. These properties can be absent; do not invent directories.
    [DllImport("msi.dll", EntryPoint = "MsiGetProductInfoW",
        CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint MsiGetProductInfo(
        string productCode, string propertyName, StringBuilder value, ref uint length);

    internal static string ReadMsiProductProperty(string subkeyName, string propertyName)
    {
        if (!Guid.TryParse(subkeyName, out Guid productCode)) return "";
        try
        {
            uint length = 1024;
            StringBuilder value = new((int)length + 1);
            uint result = MsiGetProductInfo(
                productCode.ToString("B"), propertyName, value, ref length);
            if (result == 234 && length < 32768) // ERROR_MORE_DATA
            {
                length++;
                value = new StringBuilder((int)length + 1);
                result = MsiGetProductInfo(
                    productCode.ToString("B"), propertyName, value, ref length);
            }
            return result == 0 ? value.ToString().Trim() : "";
        }
        catch (Exception ex) when (ex is DllNotFoundException or
            EntryPointNotFoundException or ArgumentException)
        {
            return "";
        }
    }

    internal static bool IsWindowsSystemDirectory(string directory)
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return directory.Equals(windows, StringComparison.OrdinalIgnoreCase) ||
            directory.StartsWith(windows.TrimEnd('\\') + "\\",
                StringComparison.OrdinalIgnoreCase);
    }

    internal static string? ExtractIconFilePath(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        string expanded = Environment.ExpandEnvironmentVariables(raw.Trim());
        string path;
        if (expanded.StartsWith('"'))
        {
            int end = expanded.IndexOf('"', 1);
            path = end > 1 ? expanded[1..end] : expanded.Trim('"');
        }
        else
        {
            int comma = expanded.LastIndexOf(',');
            path = comma > 0 && int.TryParse(expanded[(comma + 1)..],
                out _) ? expanded[..comma] : expanded;
        }
        path = path.Trim().Trim('"');
        return !path.StartsWith(@"\\", StringComparison.Ordinal) &&
            Path.IsPathFullyQualified(path) && File.Exists(path) ? path : null;
    }

    internal static ImageSource? TryReadProgramIcon(
        string registryIcon, string installLocation, string uninstallCommand,
        string displayName, IReadOnlyList<string> shortcuts)
    {
        ImageSource? icon = TryReadDisplayIcon(registryIcon);
        if (icon != null) return icon;

        // Shortcuts provide the right icon for applications with no DisplayIcon,
        // notably MSI installers and programs whose executable has another name.
        foreach (string shortcut in shortcuts)
        {
            if (!ShortcutMatchesProgram(displayName, shortcut)) continue;
            (string target, string iconLocation) = ReadShortcutMetadata(shortcut);
            if (!string.IsNullOrWhiteSpace(iconLocation))
            {
                icon = TryReadDisplayIcon(iconLocation);
                if (icon != null) return icon;
            }
            if (IsUsableProgramExecutable(target))
            {
                icon = TryReadDisplayIcon(target);
                if (icon != null) return icon;
            }
            icon = TryReadShortcutIcon(shortcut);
            if (icon != null) return icon;
        }

        icon = TryReadAssociatedShortcutIcon(installLocation, shortcuts);
        if (icon != null) return icon;

        string? uninstallExe = ExtractExecutablePath(uninstallCommand);
        List<string> roots = new();
        if (!string.IsNullOrWhiteSpace(installLocation) &&
            !installLocation.StartsWith(@"\\", StringComparison.Ordinal) &&
            Directory.Exists(installLocation))
            roots.Add(installLocation);
        if (!string.IsNullOrWhiteSpace(uninstallExe) && File.Exists(uninstallExe))
        {
            string? directory = Path.GetDirectoryName(uninstallExe);
            if (directory != null && !directory.StartsWith(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                StringComparison.OrdinalIgnoreCase))
                roots.Add(directory);
        }
        string wanted = NormalizeAppName(displayName);
        foreach (string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                // Bounded enumeration prevents large game and development trees
                // from causing unbounded work during installed-program inventory.
                Queue<(string Path, int Depth)> pending = new();
                pending.Enqueue((root, 0));
                int visited = 0;
                while (pending.Count > 0 && visited < 450)
                {
                    (string dir, int depth) = pending.Dequeue();
                    visited++;
                    foreach (string candidate in Directory.EnumerateFiles(dir, "*.exe").Take(60))
                    {
                        if (!IsUsableProgramExecutable(candidate)) continue;
                        string stem = NormalizeAppName(Path.GetFileNameWithoutExtension(candidate));
                        if (stem.Length >= 4 && wanted.Length >= 4 &&
                            (wanted.Contains(stem, StringComparison.Ordinal) ||
                             stem.Contains(wanted, StringComparison.Ordinal)))
                        {
                            icon = TryReadDisplayIcon(candidate);
                            if (icon != null) return icon;
                        }
                    }
                    if (depth >= 5) continue;
                    foreach (string sub in Directory.EnumerateDirectories(dir).Take(45))
                    {
                        if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) == 0)
                            pending.Enqueue((sub, depth + 1));
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                ArgumentException or System.Security.SecurityException) { }
        }
        if (!string.IsNullOrWhiteSpace(uninstallExe) &&
            IsUsableProgramExecutable(uninstallExe))
        {
            icon = TryReadDisplayIcon(uninstallExe);
            if (icon != null) return icon;
        }
        // A single executable at the top of a verified installation folder
        // is a reasonable final candidate, unlike an arbitrary nested tool.
        foreach (string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                string[] executables = Directory.EnumerateFiles(root, "*.exe")
                    .Where(IsUsableProgramExecutable).Take(2).ToArray();
                if (executables.Length != 1) continue;
                icon = TryReadDisplayIcon(executables[0]);
                if (icon != null) return icon;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                ArgumentException or System.Security.SecurityException) { }
        }
        return null;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public IntPtr Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string TypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint attributes,
        out ShellFileInfo info, uint size, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(
        string file, int iconIndex, IntPtr[]? largeIcons,
        IntPtr[]? smallIcons, uint count);

    private static ImageSource? TryReadShortcutIcon(string path)
    {
        try
        {
            const uint shellIcon = 0x100;
            const uint large = 0;
            if (SHGetFileInfo(path, 0, out ShellFileInfo result,
                (uint)Marshal.SizeOf<ShellFileInfo>(), shellIcon | large) == IntPtr.Zero ||
                result.Icon == IntPtr.Zero) return null;
            try
            {
                BitmapSource bitmap = Imaging.CreateBitmapSourceFromHIcon(result.Icon,
                    Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(20, 20));
                bitmap.Freeze();
                return bitmap;
            }
            finally { DestroyIcon(result.Icon); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or ExternalException) { return null; }
    }

    internal static ImageSource? TryReadMsixIcon(string location)
    {
        if (string.IsNullOrWhiteSpace(location)) return null;
        try
        {
            string manifest = Path.Combine(location, "AppxManifest.xml");
            if (!File.Exists(manifest)) return null;
            XDocument xml = XDocument.Load(manifest);
            string? relative = xml.Descendants()
                .Where(x => x.Name.LocalName == "VisualElements")
                .Select(x => x.Attributes().FirstOrDefault(a =>
                    a.Name.LocalName == "Square44x44Logo" ||
                    a.Name.LocalName == "Square150x150Logo")?.Value)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            relative ??= xml.Descendants()
                .Where(x => x.Name.LocalName == "Logo")
                .Select(x => x.Value).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            if (string.IsNullOrWhiteSpace(relative) || relative.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
                return null;
            string full = Path.GetFullPath(Path.Combine(location,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(Path.GetFullPath(location).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return null;
            List<string> variants = new() { full };
            string dir = Path.GetDirectoryName(full) ?? location;
            string stem = Path.GetFileNameWithoutExtension(full);
            if (Directory.Exists(dir))
                variants.AddRange(Directory.EnumerateFiles(dir, stem + ".*.png")
                    .Where(p => p.Contains("targetsize-", StringComparison.OrdinalIgnoreCase) ||
                        p.Contains("scale-", StringComparison.OrdinalIgnoreCase)).Take(12));
            foreach (string candidate in variants)
            {
                if (!File.Exists(candidate)) continue;
                BitmapImage bitmap = new();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(candidate, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 36;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or System.Xml.XmlException or NotSupportedException) { }
        return null;
    }

    private static ImageSource? TryReadDisplayIcon(string raw)
    {
        string? path;
        try { path = ExtractIconFilePath(raw); }
        catch (Exception) { return null; }
        if (path == null) return null;

        try
        {
            int iconIndex = 0;
            string expanded = Environment.ExpandEnvironmentVariables(raw.Trim());
            int comma = expanded.LastIndexOf(',');
            if (comma > 0 && int.TryParse(expanded[(comma + 1)..].Trim(),
                NumberStyles.Integer, CultureInfo.InvariantCulture,
                out int parsedIndex))
                iconIndex = parsedIndex;

            // Respect the resource index supplied by the application / shortcut.
            // ExtractAssociatedIcon otherwise often returns a generic DLL icon.
            if (!string.Equals(Path.GetExtension(path), ".ico",
                StringComparison.OrdinalIgnoreCase))
            {
                IntPtr[] large = new IntPtr[1];
                IntPtr[] small = new IntPtr[1];
                try
                {
                    if (ExtractIconEx(path, iconIndex, large, small, 1) > 0)
                    {
                        IntPtr handle = large[0] != IntPtr.Zero ? large[0] : small[0];
                        if (handle != IntPtr.Zero)
                        {
                            BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(
                                handle, Int32Rect.Empty,
                                BitmapSizeOptions.FromWidthAndHeight(28, 28));
                            source.Freeze();
                            return source;
                        }
                    }
                }
                finally
                {
                    if (large[0] != IntPtr.Zero) DestroyIcon(large[0]);
                    if (small[0] != IntPtr.Zero) DestroyIcon(small[0]);
                }
            }

            using System.Drawing.Icon? fileIcon =
                string.Equals(Path.GetExtension(path), ".ico",
                    StringComparison.OrdinalIgnoreCase)
                    ? new System.Drawing.Icon(path)
                    : System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (fileIcon == null) return null;
            BitmapSource bitmap = Imaging.CreateBitmapSourceFromHIcon(
                fileIcon.Handle, Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(28, 28));
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or System.ComponentModel.Win32Exception or
            NotSupportedException or System.Runtime.InteropServices.ExternalException)
        {
            return null;
        }
    }

}
