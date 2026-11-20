using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace PCHardwareMonitor;

/// <summary>
/// Read-only program inventory with a small local observation history.
/// This is not an installation tracing service and must never be used as
/// proof of ownership of files or registry keys during uninstall.
/// </summary>
internal static class InstalledProgramsHistoryService
{
    private sealed class HistoryEntry
    {
        public HistoryEntry() { }
        public DateTime FirstSeenUtc { get; set; }
        public DateTime LastSeenUtc { get; set; }
        public DateTime? VersionChangedUtc { get; set; }
        public string Version { get; set; } = "";
    }

    private static readonly string HistoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Thermiqra", "installed-programs-history.json");

    private static readonly SemaphoreSlim WriteGate = new(1, 1);

    internal static List<InstalledProgramInfo> RecordObservations(
        IReadOnlyList<InstalledProgramInfo> programs, CancellationToken token)
    {
        WriteGate.Wait(token);
        try { return RecordObservationsCore(programs, token); }
        finally { WriteGate.Release(); }
    }

    private static List<InstalledProgramInfo> RecordObservationsCore(
        IReadOnlyList<InstalledProgramInfo> programs, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Dictionary<string, HistoryEntry> entries;
        try
        {
            entries = File.Exists(HistoryPath)
                ? JsonSerializer.Deserialize<Dictionary<string, HistoryEntry>>(
                    File.ReadAllText(HistoryPath, Encoding.UTF8)) ??
                    new Dictionary<string, HistoryEntry>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, HistoryEntry>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            JsonException or ArgumentException or System.Security.SecurityException)
        {
            // Do not overwrite an unreadable/corrupted history with a new baseline.
            return programs.ToList();
        }

        if (entries.Any(pair => pair.Value == null)) return programs.ToList();
        Dictionary<string, HistoryEntry> updated =
            new(entries, StringComparer.OrdinalIgnoreCase);
        DateTime now = DateTime.UtcNow;
        List<InstalledProgramInfo> output = new(programs.Count);

        foreach (InstalledProgramInfo program in programs)
        {
            token.ThrowIfCancellationRequested();
            if (program.CachedInventoryUtc.HasValue || string.IsNullOrWhiteSpace(program.Identity))
            {
                output.Add(program);
                continue;
            }

            if (!updated.TryGetValue(program.Identity, out HistoryEntry? entry))
            {
                entry = new HistoryEntry
                {
                    FirstSeenUtc = now,
                    LastSeenUtc = now,
                    Version = program.Version
                };
                updated[program.Identity] = entry;
            }
            else
            {
                // A version change is only known if two distinct observations exist.
                if (!string.Equals(entry.Version, program.Version,
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(entry.Version) &&
                    !string.IsNullOrWhiteSpace(program.Version))
                    entry.VersionChangedUtc = now;
                entry.Version = program.Version;
                if (entry.FirstSeenUtc == default || entry.FirstSeenUtc > now)
                    entry.FirstSeenUtc = now;
                entry.LastSeenUtc = now;
            }

            output.Add(program with
            {
                FirstObservedOn = entry.FirstSeenUtc.ToLocalTime().Date,
                LastVersionChangeOn = entry.VersionChangedUtc?.ToLocalTime().Date
            });
        }

        // No registry writes, deletions, or file-system monitoring.
        // Write the JSON atomically so interruption cannot truncate history.
        string temp = HistoryPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath)!);
            string json = JsonSerializer.Serialize(updated,
                new JsonSerializerOptions { WriteIndented = false });
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            token.ThrowIfCancellationRequested();
            File.Move(temp, HistoryPath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or System.Security.SecurityException)
        {
            // Inventory remains usable when history storage is unavailable.
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return output;
    }
}
