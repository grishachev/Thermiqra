using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PCHardwareMonitor;

internal sealed record ProgramObservedAssociation(ProgramObservedApp App, ProgramRemovalBaseline Baseline, string Session = "",
    ProgramFootprint? Footprint = null, List<ProgramFootprintChange>? ObjectChanges = null,
    DateTime? FirstObservedUtc = null, DateTime? InstallationEpochUtc = null);

internal static class ProgramObservedAssociationStore
{
    private static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Thermiqra", "ProgramObservations", "Associations");
    private static string FileName(string identity) => Path.Combine(Folder,
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToUpperInvariant()))) + ".json");

    internal static bool Exists(string identity) => File.Exists(FileName(identity));

    internal static ProgramObservedAssociation? Read(ProgramObservedApp app, bool exact = true)
    {
        try
        {
            string path = FileName(app.Identity);
            if (!File.Exists(path) || new FileInfo(path).Length > 4 * 1024 * 1024 ||
                InstalledProgramLocationService.HasReparseAncestor(Folder) ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return null;
            var saved = JsonSerializer.Deserialize<ProgramObservedAssociation>(File.ReadAllText(path));
            return (exact ? saved?.App == app : string.Equals(saved?.App?.Identity, app.Identity, StringComparison.OrdinalIgnoreCase)) &&
                saved!.App.Name != null && saved.App.Version != null && saved.App.Publisher != null && saved.App.InstallRoot != null &&
                string.Equals(saved.Baseline?.Identity, app.Identity, StringComparison.OrdinalIgnoreCase) && saved.Baseline != null && saved.Baseline.Probes != null &&
                saved.Baseline.Name != null && saved.Baseline.Version != null &&
                saved.Baseline.CapturedUtc.Year >= 2000 && saved.Baseline.CapturedUtc <= DateTime.UtcNow.AddMinutes(1) &&
                saved.Baseline.Limitations != null && saved.Baseline.Probes.Count <= 5000 &&
                !saved.Baseline.Probes.Exists(p => p == null || p.Path == null || p.EvidenceRoot == null ||
                    p.Kind == null || p.Hive == null || p.View == null || p.ReasonRu == null || p.ReasonEn == null ||
                    p.EvidenceTarget == null || p.EvidenceFingerprint == null) &&
                !saved.Baseline.Limitations.Exists(l => l == null || l.ReasonRu == null || l.ReasonEn == null || l.Detail == null) &&
                ValidFootprint(saved.Footprint) && (saved.ObjectChanges == null ||
                    saved.ObjectChanges.Count <= 1000 && !saved.ObjectChanges.Exists(c => c == null || c.Path == null || c.Kind == null))
                ? saved : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or System.Security.SecurityException) { return null; }
    }

    internal static void Save(ProgramObservedAssociation association)
    {
        ProgramLeftoverCleanupPolicy.CheckAncestors(Folder); Directory.CreateDirectory(Folder);
        string path = FileName(association.App.Identity), temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, association);
                if (stream.Position > 4 * 1024 * 1024) throw new IOException(SettingsService.L("Превышен предел размера снимка связей программы.", "Association snapshot exceeds its limit."));
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    internal static bool ValidFootprint(ProgramFootprint? value) => value == null ||
        value.Files != null && value.Registry != null && value.Limitations != null &&
        value.Files.Count <= 4000 && value.Registry.Count <= 128 &&
        (value.Roots == null || value.Roots.Count <= 5000 && !value.Roots.Exists(r => r == null)) &&
        (value.RegistryRoots == null || value.RegistryRoots.Count <= 5000 && !value.RegistryRoots.Exists(r => r == null)) &&
        !value.Files.Exists(f => f == null || f.Root == null || f.RelativePath == null || f.Length < 0) &&
        !value.Registry.Exists(k => k == null || k.Path == null || k.Hive == null || k.View == null || k.Fingerprint == null) &&
        !value.Limitations.Exists(l => l == null || l.ReasonRu == null || l.ReasonEn == null || l.Detail == null);

    internal static string EventId(ProgramObservedChange change) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        change.Before.Identity + "|" + change.DetectedUtc.ToString("O"))))[..32].ToLowerInvariant();
}
