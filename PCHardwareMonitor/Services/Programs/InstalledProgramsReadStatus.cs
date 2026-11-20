using System;
using System.Collections.Generic;
using System.Linq;

namespace PCHardwareMonitor;

/// <summary>Missing evidence must not be interpreted as a confirmed uninstall.</summary>
internal sealed class InstalledProgramsReadStatus
{
    internal sealed record MetadataWarning(string Identity, string Stage, string Exception, string Message);
    internal List<MetadataWarning> MetadataWarnings { get; } = new();
    internal Dictionary<string, string[]> EnumeratedRegistryKeys { get; } = new();
    internal sealed record RegistryDecision(string Identity, string Name, string Reason);
    internal List<RegistryDecision> SkippedRegistryEntries { get; } = new();
    internal string[] DiscoveredRegistryIdentities { get; set; } = Array.Empty<string>();
    internal HashSet<string> UnavailableSources { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal HashSet<string> UnreadableIdentities { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal bool HasFailures => UnavailableSources.Count > 0 || UnreadableIdentities.Count > 0;

    internal List<InstalledProgramInfo> PreserveUnconfirmed(List<InstalledProgramInfo> fresh,
        IReadOnlyList<InstalledProgramInfo> previous, bool storeAvailable)
    {
        HashSet<string> identities = new(fresh.Select(p => p.Identity), StringComparer.OrdinalIgnoreCase);
        foreach (InstalledProgramInfo old in previous)
        {
            bool sourceFailed = old.Kind == "MSIX" ? !storeAvailable :
                UnavailableSources.Contains(old.Source) || UnreadableIdentities.Contains(old.Identity);
            if (!sourceFailed || identities.Contains(old.Identity)) continue;
            // A registration exposed through a second registry view can already
            // be present under a different identity after a view access failure.
            if (fresh.Any(p => p.Name == old.Name && p.Publisher == old.Publisher &&
                p.Version == old.Version && p.Kind == old.Kind &&
                p.RegisteredLocation.Equals(old.RegisteredLocation, StringComparison.OrdinalIgnoreCase))) continue;
            fresh.Add(old with { CachedInventoryUtc = old.CachedInventoryUtc ?? DateTime.UtcNow,
                SizeFromCache = old.SizeMeasuredUtc.HasValue });
            identities.Add(old.Identity);
        }
        return fresh;
    }
}
