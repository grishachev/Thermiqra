using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows.Media.Imaging;

namespace PCHardwareMonitor;

/// <summary>A display snapshot only; never authority for uninstall or file ownership.</summary>
internal static class InstalledProgramsCacheService
{
    internal sealed record Snapshot(DateTime SavedUtc, List<InstalledProgramInfo> Programs);
    private sealed record Row(InstalledProgramInfo Data, byte[]? Icon);
    private sealed record Document(int Schema, string Language, DateTime SavedUtc, List<Row> Rows);
    private static readonly object Sync = new();
    private static readonly Dictionary<string, Snapshot> Memory = new();
    private static readonly Dictionary<string, long> SaveGenerations = new();
    private static readonly JsonSerializerOptions Options = new()
    {
        IgnoreReadOnlyProperties = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private static string Language => SettingsService.IsRussian ? "ru" : "en";
    private static string CachePath(string language) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Thermiqra", $"installed-programs-cache-v1-{language}.json");

    internal static Snapshot? Read(CancellationToken token)
    {
        string language = Language;
        lock (Sync)
            if (Memory.TryGetValue(language, out Snapshot? cached)) return AsSaved(cached);
        try
        {
            string path = CachePath(language);
            if (!File.Exists(path) || new FileInfo(path).Length > 32 * 1024 * 1024) return null;
            token.ThrowIfCancellationRequested();
            Document? document = JsonSerializer.Deserialize<Document>(File.ReadAllText(path), Options);
            if (document == null || document.Schema != 1 || document.Language != language ||
                document.SavedUtc == default || document.SavedUtc > DateTime.UtcNow.AddMinutes(5) ||
                document.Rows == null || document.Rows.Count > 10000) return null;
            List<InstalledProgramInfo> programs = new();
            foreach (Row row in document.Rows)
            {
                token.ThrowIfCancellationRequested();
                if (row?.Data == null || string.IsNullOrWhiteSpace(row.Data.Identity) ||
                    typeof(InstalledProgramInfo).GetProperties().Any(property =>
                        property.PropertyType == typeof(string) && property.SetMethod != null &&
                        property.GetValue(row.Data) == null)) return null;
                BitmapImage? icon = null;
                if (row.Icon is { Length: > 0 and < 65536 })
                {
                    try
                    {
                        using MemoryStream stream = new(row.Icon);
                        icon = new BitmapImage();
                        icon.BeginInit();
                        icon.CacheOption = BitmapCacheOption.OnLoad;
                        icon.DecodePixelWidth = 32;
                        icon.StreamSource = stream;
                        icon.EndInit();
                        icon.Freeze();
                    }
                    catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException)
                    { icon = null; }
                }
                programs.Add(row.Data with { AppIcon = icon });
            }
            Snapshot snapshot = new(document.SavedUtc, programs);
            lock (Sync)
            {
                // A background read must not replace a newer completed save.
                if (Memory.TryGetValue(language, out Snapshot? newer)) return AsSaved(newer);
                Memory[language] = snapshot;
            }
            return AsSaved(snapshot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or
            ArgumentException or NotSupportedException or System.Security.SecurityException) { return null; }
    }

    private static Snapshot AsSaved(Snapshot snapshot) => new(snapshot.SavedUtc,
        snapshot.Programs.Select(p => p with
        {
            CachedInventoryUtc = p.CachedInventoryUtc ?? snapshot.SavedUtc,
            SizeFromCache = p.SizeMeasuredUtc.HasValue
        }).ToList());

    internal static void Save(IReadOnlyList<InstalledProgramInfo> programs, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        string language = Language;
        DateTime saved = DateTime.UtcNow;
        List<InstalledProgramInfo> copy = programs.ToList();
        long generation;
        lock (Sync)
        {
            generation = SaveGenerations.GetValueOrDefault(language) + 1;
            SaveGenerations[language] = generation;
        }
        string path = CachePath(language);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            List<Row> rows = new();
            foreach (InstalledProgramInfo program in copy)
            {
                token.ThrowIfCancellationRequested();
                byte[]? icon = null;
                if (program.AppIcon is BitmapSource bitmap)
                {
                    try
                    {
                        using MemoryStream stream = new();
                        PngBitmapEncoder encoder = new();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        encoder.Save(stream);
                        if (stream.Length < 65536) icon = stream.ToArray();
                    }
                    catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException) { }
                }
                rows.Add(new(program with { AppIcon = null }, icon));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(new Document(1, language, saved, rows), Options));
            lock (Sync)
            {
                token.ThrowIfCancellationRequested();
                if (SaveGenerations[language] != generation) return;
                // Commit only the latest completed job. A cancelled job cannot
                // publish its partial snapshot in memory before serialization.
                Memory[language] = new(saved, copy);
                File.Move(temporary, path, true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or
            NotSupportedException or ArgumentException or System.Security.SecurityException) { }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    internal static List<InstalledProgramInfo> KeepDisplaySizes(
        IReadOnlyList<InstalledProgramInfo> fresh, IReadOnlyList<InstalledProgramInfo> previous)
    {
        var byIdentity = previous.Where(p => p.Identity.Length > 0)
            .GroupBy(p => p.Identity, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        return fresh.Select(p => p.SizeText == "—" && byIdentity.TryGetValue(p.Identity, out var old) &&
                old.Version == p.Version && old.Kind == p.Kind && old.SizeMeasuredUtc.HasValue &&
                old.InstallLocation.Equals(p.InstallLocation, StringComparison.OrdinalIgnoreCase)
            ? p with { SizeText = old.SizeText, SizeSource = old.SizeSource,
                SizeMeasuredUtc = old.SizeMeasuredUtc, SizeFromCache = true } : p).ToList();
    }
}
