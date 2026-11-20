using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace PCHardwareMonitor;

// Dedicated per-user settings, plus strictly validated machine location leaves.
// Never a whole vendor, system namespace, or referenced file deletion.
internal static partial class ProgramSettingsRegistryCleanup
{
    internal const string Evidence = "user-settings-tree";
    private static string Token(string s) => new(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static string[] Parts(ProgramLeftoverProbe p)
    {
        string[] parts = p.Path.Split('\\');
        if (p.EvidenceTarget == ProgramOrphanRegistration.Evidence)
        {
            string prefix = ProgramOrphanRegistration.Parent + "\\";
            string leaf = p.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? p.Path[prefix.Length..] : "";
            if (p.Kind != "Registry" || p.Group != ProgramLeftoverGroup.UserData ||
                (p.Hive != "HKCU" && p.Hive != "HKLM") || (p.View != "32" && p.View != "64") ||
                leaf.Length is < 1 or > 255 || leaf.Contains('\\') || leaf.Contains('/') || leaf.Contains('\0'))
                ProgramLeftoverCleanupPolicy.Deny();
            return parts;
        }
        string[] blocked = { "microsoft", "windows", "classes", "policies", "wow6432node", "software",
            "programs", "applications", "common", "settings", "data", "clients" };
        if (p.Kind != "Registry" || (p.Hive != "HKCU" && p.Hive != "HKLM") ||
            (p.View != "64" && p.View != "32") || (p.Hive == "HKLM" && parts.Length != 3) ||
            parts.Length is < 2 or > 3 || !parts[0].Equals("SOFTWARE", StringComparison.OrdinalIgnoreCase) ||
            parts.Skip(1).Any(x => x.Length < 3 || x.Contains('/') || x.Contains('\0') || blocked.Contains(Token(x))))
            ProgramLeftoverCleanupPolicy.Deny();
        return parts;
    }

    internal static bool HasApplicationNamespace(ProgramLeftoverProbe p, ProgramRemovalBaseline baseline)
    {
        string[] parts = Parts(p);
        string product = Token(ProgramUserDataLeftoverService.ProductName(baseline.Name));
        var names = ProgramProductIdentity.Names(baseline);
        if (baseline.ObservationOnly || baseline.Identity.StartsWith("msix:", StringComparison.Ordinal) ||
            !names.Contains(Token(parts[^1])) ||
            (parts.Length == 3 && Token(parts[1]) != Token(baseline.Publisher) && !Token(parts[1]).Contains(product)) ||
            (p.Hive == "HKLM" && Token(parts[1]) != Token(baseline.Publisher)) ||
            baseline.InstallRoot.Length == 0)
            return false;
        return true;
    }

    private static void Association(ProgramLeftoverProbe p, ProgramRemovalBaseline baseline)
    {
        if (!HasApplicationNamespace(p, baseline)) ProgramLeftoverCleanupPolicy.Deny();
        string product = Token(ProgramUserDataLeftoverService.ProductName(baseline.Name));
        var names = ProgramProductIdentity.Names(baseline);
        ProgramLeftoverCleanupPolicy.CheckRoot(baseline.InstallRoot);
        // Reject namespaces also named by another currently registered app.
        var inventory = ProgramLeftoverCleanupService.LiveInventory(baseline.Identity);
        if (inventory.Any(a => Token(ProgramUserDataLeftoverService.ProductName(a.Name)) == product ||
            names.Contains(Token(ProgramProductIdentity.RegistrationName(a.Identity)))))
            ProgramLeftoverCleanupPolicy.Deny();
        ProgramLeftoverCleanupPolicy.CheckInventory(baseline.InstallRoot, inventory);
    }

    internal static ProgramLeftoverProbe Classify(ProgramLeftoverProbe p, ProgramRemovalBaseline baseline)
    {
        if (p.Group == ProgramLeftoverGroup.Protected) return p;
        try
        {
            if (HasForeignIntegration(p)) return p with { Group = ProgramLeftoverGroup.Protected,
                ReasonRu = "Раздел содержит регистрации интеграций других приложений. Автоматическая очистка исключена; их файлы и настройки не затрагиваются.",
                ReasonEn = "This key contains integration registrations belonging to other applications. Automatic cleanup is excluded; their files and settings are left intact." };
            Association(p, baseline);
            p = p with { EvidenceRoot = baseline.InstallRoot };
            var snapshot = Capture(p);
            return p with { Group = ProgramLeftoverGroup.UserData, EvidenceRoot = baseline.InstallRoot,
                EvidenceTarget = Evidence, EvidenceFingerprint = Fingerprint(snapshot),
                ReasonRu = "Отдельный раздел программы, связанный с её именем или идентификатором установки. Копия сохраняется в «Восстановлении». Файлы по адресам внутри раздела не удаляются.",
                ReasonEn = "Dedicated application key associated with its name or installation identity. A copy is saved in Recovery. Files referenced by the key are not deleted." };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
            System.Security.SecurityException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return p.EvidenceTarget != Evidence ? p : p with { Group = ProgramLeftoverGroup.NeedsReview,
                ReasonRu = "Настройки изменились или их безопасная очистка не подтверждена. Выполните новый поиск.",
                ReasonEn = "Settings changed or cleanup could not be validated. Run a new search." };
        }
    }

    internal static ProgramRegistrySnapshot Prepare(ProgramLeftoverProbe p, ProgramRemovalBaseline baseline)
    {
        if (p.EvidenceTarget != Evidence || p.Group != ProgramLeftoverGroup.UserData ||
            !p.EvidenceRoot.Equals(baseline.InstallRoot, StringComparison.OrdinalIgnoreCase)) ProgramLeftoverCleanupPolicy.Deny();
        Association(p, baseline);
        var snapshot = Capture(p);
        if (Fingerprint(snapshot) != p.EvidenceFingerprint) Changed();
        return snapshot;
    }

    internal static string Fingerprint(ProgramRegistrySnapshot s) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(s))));
    private static void Changed() => throw new IOException(SettingsService.L(
        "Настройки изменились. Обновите результат поиска перед очисткой.", "Settings changed. Refresh the search before cleanup."));

    private static RegistryKey Open(ProgramLeftoverProbe p, bool writable)
    {
        string[] parts = Parts(p);
        RegistryKey current = Base(p);
        try
        {
            foreach (string part in parts)
            {
                RegistryKey next = ProgramLeftoverRegistryCleanup.OpenWithoutLinks(current, part, View(p), writable);
                current.Dispose(); current = next;
            }
            return current;
        }
        catch { current.Dispose(); throw; }
    }
    private static RegistryView View(ProgramLeftoverProbe p) => p.View == "32" ? RegistryView.Registry32 : RegistryView.Registry64;
    private static RegistryKey Base(ProgramLeftoverProbe p) => RegistryKey.OpenBaseKey(p.Hive == "HKLM" ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, View(p));

    private static bool HasForeignIntegration(ProgramLeftoverProbe p)
    {
        Parts(p);
        using RegistryKey key = Open(p, false);
        string? name = key.GetSubKeyNames().FirstOrDefault(n => n.Equals("NativeMessagingHosts", StringComparison.OrdinalIgnoreCase));
        if (name == null) return false;
        using RegistryKey integrations = ProgramLeftoverRegistryCleanup.OpenWithoutLinks(key, name, View(p), false);
        return integrations.SubKeyCount > 0 || integrations.ValueCount > 0;
    }
}
