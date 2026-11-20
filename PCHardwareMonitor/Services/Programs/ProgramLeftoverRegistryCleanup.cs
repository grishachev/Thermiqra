using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace PCHardwareMonitor;

// Only leaf App Paths registrations with missing executable targets qualify.
// Name-only matches and uninstall registrations never enter this writer.
internal static class ProgramLeftoverRegistryCleanup
{
    private const string Parent = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    internal static extern int RegCreateKeyEx(SafeRegistryHandle key, string name, int reserved,
        string? keyClass, int options, int access, IntPtr security, out SafeRegistryHandle result, out int disposition);
    [DllImport("advapi32.dll")]
    internal static extern int RegGetKeySecurity(SafeRegistryHandle key, int information, byte[]? buffer, ref int length);
    [DllImport("advapi32.dll")]
    internal static extern int RegSetKeySecurity(SafeRegistryHandle key, int information, byte[] buffer);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegOpenKeyEx(SafeRegistryHandle key, string name, int options,
        int access, out SafeRegistryHandle result);

    internal static RegistryKey OpenWithoutLinks(RegistryKey parent, string name, RegistryView view, bool writable)
    {
        int error = RegOpenKeyEx(parent.Handle, name, 8, (writable ? 0xF003F : 0x20019) |
            (view == RegistryView.Registry32 ? 0x200 : 0x100), out SafeRegistryHandle handle);
        if (error != 0) { handle.Dispose(); throw new System.ComponentModel.Win32Exception(error); }
        RegistryKey key = RegistryKey.FromHandle(handle, view);
        try
        {
            if (key.GetValueNames().Any(n => n.Equals("SymbolicLinkValue", StringComparison.OrdinalIgnoreCase)))
                ProgramLeftoverCleanupPolicy.Deny();
            return key;
        }
        catch { key.Dispose(); throw; }
    }

    private static RegistryKey OpenParent(ProgramLeftoverProbe probe, bool writable)
    {
        string leaf = probe.Path.StartsWith(Parent + "\\", StringComparison.OrdinalIgnoreCase) ? probe.Path[(Parent.Length + 1)..] : "";
        if (probe.Kind != "Registry" || probe.Group != ProgramLeftoverGroup.Related ||
            leaf.Length == 0 || leaf.Contains('\\') || leaf.Contains('/') ||
            !leaf.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            (probe.Hive != "HKLM" && probe.Hive != "HKCU") || (probe.View != "32" && probe.View != "64"))
            ProgramLeftoverCleanupPolicy.Deny();
        ProgramLeftoverCleanupPolicy.CheckRoot(ProgramLeftoverCleanupPolicy.Root(probe));
        RegistryView view = probe.View == "64" ? RegistryView.Registry64 : RegistryView.Registry32;
        RegistryKey current = RegistryKey.OpenBaseKey(probe.Hive == "HKLM" ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, view);
        try
        {
            foreach (string part in Parent.Split('\\'))
            {
                RegistryKey next = OpenWithoutLinks(current, part, view, writable);
                current.Dispose(); current = next;
            }
            return current;
        }
        catch { current.Dispose(); throw; }
    }

    internal static ProgramRegistrySnapshot Capture(ProgramLeftoverProbe probe)
    {
        using RegistryKey parent = OpenParent(probe, false);
        using RegistryKey key = OpenWithoutLinks(parent, Path.GetFileName(probe.Path),
            probe.View == "64" ? RegistryView.Registry64 : RegistryView.Registry32, false);
        if (key.SubKeyCount != 0 || key.ValueCount > 2) ProgramLeftoverCleanupPolicy.Deny();
        List<ProgramRegistryText> values = new();
        foreach (string name in key.GetValueNames().OrderBy(n => n, StringComparer.Ordinal))
        {
            RegistryValueKind kind = key.GetValueKind(name);
            if ((name.Length > 0 && name != "Path") || kind is not (RegistryValueKind.String or RegistryValueKind.ExpandString))
                ProgramLeftoverCleanupPolicy.Deny();
            string text = (string)key.GetValue(name, "", RegistryValueOptions.DoNotExpandEnvironmentNames)!;
            if (text.Length > 32768) ProgramLeftoverCleanupPolicy.Deny();
            values.Add(new(name, text, (int)kind));
        }
        string root = ProgramLeftoverCleanupPolicy.Root(probe);
        string executable = ProgramLeftoverPathPolicy.Normalize(values.FirstOrDefault(v => v.Name.Length == 0)?.Text ?? "");
        if (executable.Length == 0 || !ProgramLeftoverPathPolicy.Inside(executable, root))
            ProgramLeftoverCleanupPolicy.Deny();
        ProgramLeftoverCleanupPolicy.CheckAncestors(executable);
        try { File.GetAttributes(executable); ProgramLeftoverCleanupPolicy.Deny(); }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
        foreach (ProgramRegistryText value in values.Where(v => v.Name == "Path"))
            foreach (string part in Environment.ExpandEnvironmentVariables(value.Text).Split(';', StringSplitOptions.RemoveEmptyEntries))
                if (!ProgramLeftoverPathPolicy.Inside(ProgramLeftoverPathPolicy.Normalize(part), root)) ProgramLeftoverCleanupPolicy.Deny();
        int length = 0;
        RegGetKeySecurity(key.Handle, 7, null, ref length);
        if (length <= 0 || length > 65536) throw new IOException(SettingsService.L("Не удалось сохранить права доступа раздела реестра.", "Registry security backup unavailable."));
        byte[] security = new byte[length];
        int error = RegGetKeySecurity(key.Handle, 7, security, ref length);
        if (error != 0) throw new System.ComponentModel.Win32Exception(error);
        return new(values, Convert.ToBase64String(security));
    }

    internal static void Remove(ProgramLeftoverProbe probe, ProgramRegistrySnapshot approved)
    {
        ProgramRegistrySnapshot fresh = Capture(probe);
        if (JsonSerializer.Serialize(fresh) != JsonSerializer.Serialize(approved))
            throw new IOException(SettingsService.L("Запись изменилась после резервирования.", "Registration changed after backup."));
        using RegistryKey parent = OpenParent(probe, true);
        parent.DeleteSubKey(Path.GetFileName(probe.Path), true); // Never recursive.
    }

    internal static void Restore(ProgramLeftoverProbe probe, ProgramRegistrySnapshot snapshot)
    {
        using RegistryKey parent = OpenParent(probe, true);
        byte[] security = Convert.FromBase64String(snapshot.Security);
        if (security.Length == 0 || security.Length > 65536 || snapshot.Values.Count > 2 ||
            snapshot.Values.Any(v => (v.Name.Length > 0 && v.Name != "Path") || v.Text.Length > 32768 ||
                v.Kind is not ((int)RegistryValueKind.String or (int)RegistryValueKind.ExpandString)))
            throw new IOException(SettingsService.L("Некорректная резервная копия реестра.", "Invalid registry backup."));
        int error = RegCreateKeyEx(parent.Handle, Path.GetFileName(probe.Path), 0, null, 0,
            0xF003F, IntPtr.Zero, out SafeRegistryHandle handle, out int disposition);
        using (handle)
        {
            if (error != 0) throw new System.ComponentModel.Win32Exception(error);
            if (disposition != 1) throw new IOException(SettingsService.L("Запись уже существует; перезапись запрещена.", "Registration already exists; overwrite refused."));
            using RegistryKey key = RegistryKey.FromHandle(handle, probe.View == "64" ? RegistryView.Registry64 : RegistryView.Registry32);
            foreach (ProgramRegistryText value in snapshot.Values) key.SetValue(value.Name, value.Text, (RegistryValueKind)value.Kind);
            error = RegSetKeySecurity(key.Handle, 7, security);
            if (error != 0) throw new System.ComponentModel.Win32Exception(error);
            key.Flush();
        }
    }
}
