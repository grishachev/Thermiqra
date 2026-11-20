using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;

namespace PCHardwareMonitor;

internal static partial class ProgramSettingsRegistryCleanup
{
    internal static void Remove(ProgramLeftoverProbe p, ProgramRegistrySnapshot saved)
    {
        if ((p.EvidenceTarget != Evidence && p.EvidenceTarget != ProgramOrphanRegistration.Evidence) || p.Group != ProgramLeftoverGroup.UserData || !Valid(saved) ||
            Fingerprint(saved) != p.EvidenceFingerprint) ProgramLeftoverCleanupPolicy.Deny();
        var fresh = Capture(p);
        if (Fingerprint(fresh) != Fingerprint(saved)) Changed();
        foreach (var node in saved.Tree!.OrderByDescending(n => n.Path.Length))
        {
            // Open the known root and walk each child without following links.
            using RegistryKey root = Open(p, true);
            if (node.Path.Length == 0) continue;
            using RegistryKey parent = Descend(root, Parent(node.Path), true, View(p));
            using (RegistryKey child = ProgramLeftoverRegistryCleanup.OpenWithoutLinks(parent, Leaf(node.Path), View(p), true))
            {
                if (child.SubKeyCount != 0 || JsonSerializer.Serialize(Values(child)) != JsonSerializer.Serialize(node.Values) ||
                    Security(child) != node.Security) Changed();
            }
            parent.DeleteSubKey(Leaf(node.Path), true); // Bounded, leaf-only deletion.
        }
        using RegistryKey registry = Base(p);
        using RegistryKey owner = Descend(registry, Parent(p.Path), true, View(p));
        using (RegistryKey root = Open(p, true))
        {
            if (root.SubKeyCount != 0 || JsonSerializer.Serialize(Values(root)) != JsonSerializer.Serialize(saved.Tree![0].Values) ||
                Security(root) != saved.Tree![0].Security) Changed();
        }
        owner.DeleteSubKey(Leaf(p.Path), true);
    }

    private static string Parent(string path) => path.LastIndexOf('\\') is int i && i >= 0 ? path[..i] : "";
    private static string Leaf(string path) => path[(path.LastIndexOf('\\') + 1)..];
    private static RegistryKey Descend(RegistryKey root, string path, bool writable, RegistryView view)
    {
        RegistryKey current = RegistryKey.FromHandle(new Microsoft.Win32.SafeHandles.SafeRegistryHandle(root.Handle.DangerousGetHandle(), false), view);
        try
        {
            foreach (string part in path.Split('\\', StringSplitOptions.RemoveEmptyEntries))
            {
                var next = ProgramLeftoverRegistryCleanup.OpenWithoutLinks(current, part, view, writable);
                current.Dispose(); current = next;
            }
            return current;
        }
        catch { current.Dispose(); throw; }
    }

    internal static void Restore(ProgramLeftoverProbe p, ProgramRegistrySnapshot saved)
    {
        Parts(p);
        CheckMachineReferences(p, saved, requireMissing: false);
        if ((p.EvidenceTarget != Evidence && p.EvidenceTarget != ProgramOrphanRegistration.Evidence) || !Valid(saved) || Fingerprint(saved) != p.EvidenceFingerprint)
            ProgramLeftoverCleanupPolicy.Deny();
        // A partially removed tree may remain after an access error. Accept
        // only identical saved nodes; never overwrite new settings.
        using RegistryKey registry = Base(p);
        using RegistryKey parent = Descend(registry, Parent(p.Path), true, View(p));
        using (RegistryKey? existing = parent.OpenSubKey(Leaf(p.Path), false))
        {
            if (existing != null)
            {
                var remaining = Capture(p, requireMissing: false);
                if (!ProgramRegistryRestorePolicy.IsUnchangedSubset(saved, remaining)) Changed();
            }
        }
        foreach (var node in saved.Tree!)
        {
            string relative = Leaf(p.Path) + (node.Path.Length > 0 ? "\\" + node.Path : "");
            using RegistryKey owner = Descend(parent, Parent(relative), true, View(p));
            int error = ProgramLeftoverRegistryCleanup.RegCreateKeyEx(owner.Handle, Leaf(relative), 0, null, 0,
                0xF003F | (View(p) == RegistryView.Registry32 ? 0x200 : 0x100), IntPtr.Zero, out var handle, out int disposition);
            using (handle)
            {
                if (error != 0) throw new System.ComponentModel.Win32Exception(error);
                using RegistryKey key = RegistryKey.FromHandle(handle, View(p));
                if (disposition == 1)
                {
                    foreach (var value in node.Values) key.SetValue(value.Name, Decode(value), (RegistryValueKind)value.Kind);
                    error = ProgramLeftoverRegistryCleanup.RegSetKeySecurity(key.Handle, 7, Convert.FromBase64String(node.Security));
                    if (error != 0) throw new System.ComponentModel.Win32Exception(error);
                    key.Flush();
                }
                else if (JsonSerializer.Serialize(Values(key)) != JsonSerializer.Serialize(node.Values) || Security(key) != node.Security) Changed();
            }
        }
        if (Fingerprint(Capture(p, requireMissing: false)) != Fingerprint(saved)) Changed();
    }
}
