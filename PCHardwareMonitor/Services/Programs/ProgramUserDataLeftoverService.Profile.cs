using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PCHardwareMonitor;

internal static partial class ProgramUserDataLeftoverService
{
    private const string ChromiumProfileEvidence = "application-profile:chromium";

    // A common Chromium/Electron profile layout, independent of product names.
    // Empty or arbitrary name-matching directories do not qualify here.
    private static (string Path, string Hash)? FindProfile(ProgramLeftoverProbe probe, ProgramRemovalBaseline baseline)
    {
        string product = Key(ProductName(baseline.Name));
        if (product.Length < 4 || !Specific(product) || Key(Path.GetFileName(probe.Path)) != product) return null;
        string state = Path.Combine(probe.Path, "Local State");
        string preferences = Path.Combine(probe.Path, "Preferences");
        if (!JsonObject(state) || !JsonObject(preferences)) return null;
        string[] required = { "Network", "Local Storage", "Session Storage" };
        foreach (string part in required)
        {
            string path = Path.Combine(probe.Path, part);
            if (!Directory.Exists(path)) return null;
            ProgramLeftoverCleanupPolicy.CheckAncestors(path);
        }
        if (!Directory.Exists(Path.Combine(probe.Path, "Cache")) &&
            !Directory.Exists(Path.Combine(probe.Path, "Code Cache"))) return null;
        return (ChromiumProfileEvidence, ProfileFingerprint(probe.Path));
    }

    private static bool JsonObject(string path)
    {
        if (!File.Exists(path)) return false;
        ProgramLeftoverCleanupPolicy.CheckAncestors(path);
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is < 2 or > 65536) return false;
        try
        {
            using JsonDocument document = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 32 });
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException) { return false; }
    }

    private static string ProfileFingerprint(string root)
    {
        // Bound inspection, reject all links, and record every entry rather
        // than treating one stable config file as evidence for a changing tree.
        Stopwatch timer = Stopwatch.StartNew();
        List<string> entries = new();
        Stack<string> pending = new(); pending.Push(root);
        while (pending.TryPop(out string? directory))
        {
            ProgramLeftoverCleanupPolicy.CheckAncestors(directory);
            foreach (FileSystemInfo entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                if (entries.Count >= 10000 || timer.Elapsed > TimeSpan.FromSeconds(5))
                    throw new IOException(SettingsService.L("Профиль слишком велик для полной проверки.", "Profile exceeds the full inspection limit."));
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) ProgramLeftoverCleanupPolicy.Deny();
                bool folder = (entry.Attributes & FileAttributes.Directory) != 0;
                entries.Add(JsonSerializer.Serialize(new { Path = Path.GetRelativePath(root, entry.FullName),
                    Directory = folder, Length = entry is FileInfo file ? file.Length : 0,
                    // NTFS directory timestamps can settle after file creation,
                    // even though the enumerated tree and files are unchanged.
                    // Names capture directory changes; file metadata captures payload changes.
                    Modified = folder ? 0 : entry.LastWriteTimeUtc.Ticks }));
                if (folder) pending.Push(entry.FullName);
            }
        }
        entries.Sort(StringComparer.Ordinal);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", entries))));
    }
}
