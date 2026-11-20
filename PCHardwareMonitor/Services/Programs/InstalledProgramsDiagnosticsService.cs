using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace PCHardwareMonitor;

/// <summary>Local audit of discovery decisions. No telemetry or registry writes.</summary>
internal static class InstalledProgramsDiagnosticsService
{
    internal static void Save(IReadOnlyList<InstalledProgramInfo> programs,
        bool storeAvailable, long elapsedMilliseconds, CancellationToken token,
        InstalledProgramsReadStatus? status = null)
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Thermiqra", "installed-programs-diagnostics.json");
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            token.ThrowIfCancellationRequested();
            var report = new
            {
                Schema = 1, CapturedUtc = DateTime.UtcNow, StoreAvailable = storeAvailable,
                ElapsedMilliseconds = elapsedMilliseconds, Total = programs.Count,
                ByKind = programs.GroupBy(p => p.Kind).ToDictionary(g => g.Key, g => g.Count()),
                MissingLocations = programs.Count(p => p.InstallLocation.Length == 0),
                MetadataWarnings = status?.MetadataWarnings,
                UnreadableRegistrations = status?.UnreadableIdentities,
                UnavailableRegistrySources = status?.UnavailableSources,
                EnumeratedRegistryKeys = status?.EnumeratedRegistryKeys,
                SkippedRegistryEntries = status?.SkippedRegistryEntries,
                DiscoveredRegistryIdentities = status?.DiscoveredRegistryIdentities,
                Programs = programs.Select(p => new
                {
                    p.Identity, p.Name, p.Version, p.Kind, p.Source, p.RegisteredLocation,
                    p.InstallLocation, p.LocationEvidence, p.DiscoveryDurationMs,
                    p.SizeText, p.SizeSource, p.SizeMeasuredUtc, p.SizeFromCache,
                    p.CachedInventoryUtc
                })
            };
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(report,
                new JsonSerializerOptions { WriteIndented = true }));
            token.ThrowIfCancellationRequested();
            File.Move(temporary, path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or System.Security.SecurityException) { }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
