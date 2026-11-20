using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace PCHardwareMonitor;

internal sealed record ProgramObservedApp(string Identity, string Name, string Version, string Publisher, string InstallRoot);
internal sealed record ProgramObservedChange(DateTime FromUtc, DateTime DetectedUtc, string Type,
    ProgramObservedApp Before, ProgramObservedApp After, bool AfterPause);
internal sealed record ProgramObservationDocument(DateTime ScannedUtc, List<ProgramObservedApp> Apps, List<ProgramObservedChange> Changes);

// Registration snapshots only. Neither a file/registry installation trace nor
// evidence that an application exclusively owns a file or registry key.
internal static class ProgramObservationService
{
    internal static string SessionId { get; } = Guid.NewGuid().ToString("N");
    private static CancellationTokenSource? _stop;
    private static readonly object Sync = new();
    private static readonly SemaphoreSlim Requests = new(0, 1);
    private static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Thermiqra", "ProgramObservations");
    private static string PathName => Path.Combine(Folder, "catalog.json");
    internal static string LastError { get; private set; } = "";
    internal static long Revision
    {
        get
        {
            try { return File.Exists(PathName) ? File.GetLastWriteTimeUtc(PathName).Ticks : 0; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return 0; }
        }
    }

    internal static void Start()
    {
        if (_stop != null) return;
        _stop = new(); var token = _stop.Token;
        _ = Task.Run(() => ObserveAsync(token));
    }
    internal static void Stop() => _stop?.Cancel();
    internal static void RequestScan()
    {
        if (_stop == null || _stop.IsCancellationRequested) return;
        try { Requests.Release(); } catch (SemaphoreFullException) { }
    }

    internal static ProgramObservationDocument? Read()
    {
        lock (Sync)
        {
            try
            {
                if (!File.Exists(PathName) || new FileInfo(PathName).Length > 8 * 1024 * 1024 ||
                    (File.GetAttributes(PathName) & FileAttributes.ReparsePoint) != 0 ||
                    InstalledProgramLocationService.HasReparseAncestor(Folder)) return null;
                var doc = JsonSerializer.Deserialize<ProgramObservationDocument>(File.ReadAllText(PathName));
                return doc != null && doc.ScannedUtc.Year >= 2000 && doc.ScannedUtc <= DateTime.UtcNow.AddMinutes(1) &&
                    doc.Apps != null && doc.Changes != null && doc.Apps.Count <= 5000 &&
                    doc.Changes.Count <= 2000 && !doc.Apps.Any(a => a?.Identity == null || a.Name == null || a.Version == null ||
                        a.Publisher == null || a.InstallRoot == null) &&
                    doc.Apps.Select(a => a.Identity).Distinct(StringComparer.OrdinalIgnoreCase).Count() == doc.Apps.Count &&
                    !doc.Changes.Any(c => c?.Before == null || c.After == null)
                    ? doc : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or System.Security.SecurityException) { return null; }
        }
    }

    private static async Task ObserveAsync(CancellationToken token)
    {
        var previous = Read(); bool gap = true;
        while (!token.IsCancellationRequested)
        {
            bool catalogSaved = false;
            try
            {
                if (previous == null && File.Exists(PathName))
                    previous = Read() ?? throw new IOException(SettingsService.L(
                        "Сохранённая история недоступна или повреждена; она не перезаписывается.",
                        "Saved history is unavailable or damaged; it will not be overwritten."));
                var apps = await CaptureAsync(token);
                token.ThrowIfCancellationRequested();
                DateTime now = DateTime.UtcNow;
                List<ProgramObservedChange> changes = previous?.Changes.ToList() ?? new();
                if (previous != null)
                {
                    var old = previous.Apps.ToDictionary(a => a.Identity, StringComparer.OrdinalIgnoreCase);
                    var fresh = apps.ToDictionary(a => a.Identity, StringComparer.OrdinalIgnoreCase);
                    foreach (var app in apps)
                        if (!old.TryGetValue(app.Identity, out var before))
                            changes.Add(new(previous.ScannedUtc, now, "Added", app, app, gap));
                        else if (before != app) changes.Add(new(previous.ScannedUtc, now, "Changed", before, app, gap));
                    foreach (var app in previous.Apps)
                        if (!fresh.ContainsKey(app.Identity)) changes.Add(new(previous.ScannedUtc, now, "Removed", app, app, gap));
                }
                var next = new ProgramObservationDocument(now, apps, changes.TakeLast(2000).ToList());
                Save(next, token); previous = next; gap = false; LastError = "";
                catalogSaved = true;
                await ProgramObservedRemovalService.UpdateAsync(next, token);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or
                InvalidOperationException or System.Security.SecurityException or System.ComponentModel.Win32Exception)
            { LastError = ex.Message; if (!catalogSaved) gap = true; }
            try { await Requests.WaitAsync(TimeSpan.FromMinutes(1), token); }
            catch (OperationCanceledException) { return; }
        }
    }

    private static async Task<List<ProgramObservedApp>> CaptureAsync(CancellationToken token)
    {
        List<ProgramObservedApp> result = new(); HashSet<string> duplicates = new(StringComparer.OrdinalIgnoreCase);
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstall == null) continue;
            foreach (string subkey in uninstall.GetSubKeyNames())
            {
                token.ThrowIfCancellationRequested();
                using var key = uninstall.OpenSubKey(subkey);
                if (key == null) throw new IOException(SettingsService.L("Запись программы изменилась во время наблюдения. Повторите проверку.", "Registration changed during observation; retry required."));
                string Value(string name) => Convert.ToString(key.GetValue(name)) ?? "";
                string name = Value("DisplayName");
                if (string.IsNullOrWhiteSpace(name) || Value("SystemComponent") == "1" || Value("ParentKeyName").Length > 0) continue;
                string publisher = Value("Publisher"), version = Value("DisplayVersion"), location = Value("InstallLocation");
                string scope = hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU";
                if (!duplicates.Add(string.Join("|", scope, subkey, name, publisher, version, location))) continue;
                result.Add(new($"registry:{scope}:{(view == RegistryView.Registry64 ? "64" : "32")}:{subkey}", name, version, publisher, location));
            }
        }
        token.ThrowIfCancellationRequested();
        string json = await InstalledProgramPackageOperations.QueryAsync(
            "[pscustomobject]@{Apps=@(Get-AppxPackage -ErrorAction Stop | Where-Object {-not $_.IsFramework -and -not $_.IsResourcePackage} | " +
            "Select-Object Name,PackageFamilyName,@{Name='Version';Expression={$_.Version.ToString()}},Publisher,InstallLocation)} | ConvertTo-Json -Depth 3 -Compress");
        if (string.IsNullOrWhiteSpace(json)) throw new IOException(SettingsService.L("Наблюдение за пакетами Windows не вернуло результат.", "Windows package observation returned no result."));
        using JsonDocument packages = JsonDocument.Parse(json);
        IEnumerable<JsonElement> entries = packages.RootElement.GetProperty("Apps").EnumerateArray();
        foreach (var package in entries)
        {
            string Value(string name) => package.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";
            string identity = "msix:" + Value("PackageFamilyName");
            if (identity.Length > 5 && !result.Any(a => a.Identity == identity))
                result.Add(new(identity, Value("Name"), Value("Version"), Value("Publisher"), Value("InstallLocation")));
        }
        if (result.Count > 5000) throw new IOException(SettingsService.L("Превышен предел объёма наблюдаемого списка программ.", "Observation inventory exceeds its limit."));
        return result;
    }

    private static void Save(ProgramObservationDocument doc, CancellationToken token)
    {
        lock (Sync)
        {
            ProgramLeftoverCleanupPolicy.CheckAncestors(Folder);
            Directory.CreateDirectory(Folder);
            string temporary = PathName + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(stream, doc);
                    if (stream.Position > 8 * 1024 * 1024) throw new IOException(SettingsService.L("Превышен предел размера истории наблюдений.", "Observation history exceeds its storage limit."));
                    stream.Flush(true);
                }
                token.ThrowIfCancellationRequested(); File.Move(temporary, PathName, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
