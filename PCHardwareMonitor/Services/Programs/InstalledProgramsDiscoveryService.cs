using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace PCHardwareMonitor;

internal static class InstalledProgramsDiscoveryService
{
    // Read-only metadata from the uninstall registry key. This timestamp can
    // change after updates or repairs; it is NOT a guaranteed install date.
    [DllImport("advapi32.dll", EntryPoint = "RegQueryInfoKeyW", CharSet = CharSet.Unicode)]
    private static extern int RegQueryInfoKey(
        SafeRegistryHandle hKey, IntPtr lpClass, IntPtr lpcchClass,
        IntPtr lpReserved, IntPtr lpcSubKeys, IntPtr lpcbMaxSubKeyLen,
        IntPtr lpcbMaxClassLen, IntPtr lpcValues, IntPtr lpcbMaxValueNameLen,
        IntPtr lpcbMaxValueLen, IntPtr lpcbSecurityDescriptor,
        out System.Runtime.InteropServices.ComTypes.FILETIME lpftLastWriteTime);

    private static DateTime? TryReadRegistrationModifiedDate(RegistryKey key)
    {
        try
        {
            int result = RegQueryInfoKey(key.Handle, IntPtr.Zero, IntPtr.Zero,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                out System.Runtime.InteropServices.ComTypes.FILETIME lastWrite);
            if (result != 0)
                return null;
            long ticks = ((long)(uint)lastWrite.dwHighDateTime << 32) |
                (uint)lastWrite.dwLowDateTime;
            if (ticks <= 0)
                return null;
            DateTime date = DateTime.FromFileTimeUtc(ticks).ToLocalTime();
            return date.Year >= 2000 && date <= DateTime.Now.AddMinutes(1) ? date : null;
        }
        catch (Exception ex) when (ex is ArgumentException or
            PlatformNotSupportedException or ObjectDisposedException or
            System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static bool GetJsonBoolean(JsonElement element, string key)
    {
        return element.TryGetProperty(key, out JsonElement value) &&
            value.ValueKind == JsonValueKind.True;
    }

    internal static (DateTime? Date, string Text) ReadStoreInstallDate(string raw)
    {
        if (!string.IsNullOrWhiteSpace(raw) &&
            DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out DateTime date) &&
            date.Date <= DateTime.Today && date.Year >= 2000)
            return (date, SettingsService.L(
                date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        return (null, "—");
    }

    private static (DateTime? Date, string Text) ReadInstallDate(string value)
    {
        if (DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out DateTime date) && date.Date <= DateTime.Today)
        {
            return (date.Date, SettingsService.L(
                date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }
        return (null, "—");
    }

    internal static List<InstalledProgramInfo> ReadRegistryPrograms(CancellationToken token = default, InstalledProgramsReadStatus? status = null)
    {
        List<InstalledProgramInfo> result = new();
        HashSet<string> registered = new(StringComparer.OrdinalIgnoreCase);
        List<string> shortcuts = InstalledProgramIconService.EnumerateStartMenuShortcuts();
        const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        foreach (RegistryHive hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using RegistryKey? uninstall = baseKey.OpenSubKey(keyPath, false);
                    if (uninstall == null)
                        continue;
                    string[] subkeyNames = uninstall.GetSubKeyNames();
                    string sourceIdentity = $"{(hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU")}:{(view == RegistryView.Registry64 ? "64" : "32")}";
                    if (status != null) status.EnumeratedRegistryKeys[sourceIdentity] = subkeyNames;
                    foreach (string subkeyName in subkeyNames)
                    {
                        token.ThrowIfCancellationRequested();
                        string identity = $"registry:{sourceIdentity}:{subkeyName}";
                        try
                        {
                            using RegistryKey? item = uninstall.OpenSubKey(subkeyName, false);
                            if (item == null)
                            {
                                status?.SkippedRegistryEntries.Add(new(identity, "", "Key unavailable after enumeration"));
                                continue;
                            }
                            string name = RegString(item, "DisplayName");
                            string component = RegString(item, "SystemComponent");
                            string parent = RegString(item, "ParentKeyName");
                            if (string.IsNullOrWhiteSpace(name) ||
                                component == "1" || !string.IsNullOrEmpty(parent))
                            {
                                status?.SkippedRegistryEntries.Add(new(identity, name,
                                    string.IsNullOrWhiteSpace(name) ? "Empty DisplayName" :
                                    component == "1" ? "SystemComponent=1" : "ParentKeyName=" + parent));
                                continue;
                            }
                            string releaseType = RegString(item, "ReleaseType");
                            if (releaseType is "Hotfix" or "Security Update" or "Update Rollup")
                            {
                                status?.SkippedRegistryEntries.Add(new(identity, name, "ReleaseType=" + releaseType));
                                continue;
                            }

                            bool isMsi = RegString(item, "WindowsInstaller") == "1";
                            string sizeText = "—";
                            if (long.TryParse(RegString(item, "EstimatedSize"),
                                NumberStyles.Integer, CultureInfo.InvariantCulture, out long sizeKb) &&
                                sizeKb > 0)
                            {
                                sizeText = FormatReportedSize(sizeKb * 1024.0);
                            }
                            string publisher = RegString(item, "Publisher");
                            string version = RegString(item, "DisplayVersion");
                            string installLocation = RegString(item, "InstallLocation");
                            if (string.IsNullOrWhiteSpace(installLocation))
                                installLocation = RegString(item, "Inno Setup: App Path");
                            string uninstallCommand = RegString(item, "UninstallString");
                            if (string.IsNullOrWhiteSpace(uninstallCommand))
                                uninstallCommand = RegString(item, "QuietUninstallString");
                            string registeredIcon = RegString(item, "DisplayIcon");
                            // Exact duplicate entries exposed by different registry views
                            // should be shown once. Distinct install locations / uninstall
                            // commands stay distinct even when display names match.
                            string signature = string.Join("\u001f",
                                name.Trim(), publisher, version,
                                installLocation.Trim().TrimEnd('\\'),
                                !string.IsNullOrWhiteSpace(uninstallCommand)
                                    ? uninstallCommand.Trim()
                                    : $"subkey:{subkeyName}");
                            if (registered.Contains(signature))
                            {
                                status?.SkippedRegistryEntries.Add(new(identity, name, "Duplicate registration"));
                                continue;
                            }
                            string msiLocation = isMsi
                                ? InstalledProgramMetadataReader.Read(() => InstalledProgramIconService.ReadMsiProductProperty(subkeyName, "InstallLocation"), "", identity, "MSI location", status) : "";
                            if (string.IsNullOrWhiteSpace(registeredIcon) && isMsi)
                                registeredIcon = InstalledProgramMetadataReader.Read(() => InstalledProgramIconService.ReadMsiProductProperty(subkeyName, "ProductIcon"), "", identity, "MSI icon", status);
                            Stopwatch lookupTime = Stopwatch.StartNew();
                            List<string> locationEvidence = new();
                            string resolvedLocation = InstalledProgramMetadataReader.Read(() => InstalledProgramLocationService.ResolveProgramLocation(
                                installLocation, registeredIcon, name, shortcuts,
                                uninstallCommand, msiLocation, isMsi ? subkeyName : "", token, locationEvidence.Add), "", identity, "Location", status);
                            string scope = hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU";
                            string source = $"{scope} ({(view == RegistryView.Registry64 ? "64" : "32")}-bit)";
                            string rawDate = RegString(item, "InstallDate");
                            if (string.IsNullOrWhiteSpace(rawDate) && isMsi)
                                rawDate = InstalledProgramMetadataReader.Read(() => InstalledProgramIconService.ReadMsiProductProperty(
                                    subkeyName, "InstallDate"), "", identity, "MSI date", status);
                            (DateTime? date, string dateText) = ReadInstallDate(rawDate);
                            DateTime? registrationModified = TryReadRegistrationModifiedDate(item);
                            bool registryModifiedFallback = false;
                            if (!date.HasValue)
                            {
                                date = registrationModified;
                                if (date.HasValue)
                                {
                                    dateText = InstalledProgramDateService.FormatDate(date.Value);
                                    registryModifiedFallback = true;
                                }
                            }
                            string reason = InstalledProgramClassificationService.GetDesktopComponentReason(
                                name, RegString(item, "ParentDisplayName"));
                            result.Add(new InstalledProgramInfo(
                                name.Trim(), publisher, version,
                                isMsi ? "MSI" : "Win32",
                                sizeText, resolvedLocation, source,
                                dateText, date,
                                InstalledProgramMetadataReader.Read(() => InstalledProgramIconService.TryReadProgramIcon(registeredIcon,
                                    resolvedLocation, uninstallCommand, name, shortcuts), null, identity, "Icon", status),
                                !string.IsNullOrWhiteSpace(reason),
                                sizeText != "—" ? SettingsService.L(
                                    "Размер, заявленный установщиком", "Size reported by installer") : "",
                                !date.HasValue ? "" : registryModifiedFallback
                                    ? SettingsService.L(
                                        "Последнее изменение записи реестра Windows; дата установки может быть другой",
                                        "Windows registry entry last modified; installation may have been earlier")
                                    : SettingsService.L(
                                        "Дата из регистрации Windows (MSI: может меняться при обслуживании)",
                                        "Date from Windows registration (MSI: may change on maintenance)"),
                                reason,
                                $"registry:{scope}:{(view == RegistryView.Registry64 ? "64" : "32")}:{subkeyName}",
                                RegisteredLocation: installLocation,
                                LocationEvidence: string.Join("; ", locationEvidence),
                                DiscoveryDurationMs: lookupTime.ElapsedMilliseconds, RegistrationModifiedOn: registrationModified));
                            registered.Add(signature);
                        }
                        catch (Exception ex) when (ex is System.Security.SecurityException or
                            UnauthorizedAccessException or IOException or ArgumentException)
                        {
                            status?.MetadataWarnings.Add(new(
                                $"registry:{(hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU")}:{(view == RegistryView.Registry64 ? "64" : "32")}:{subkeyName}",
                                "Registration", ex.GetType().Name, ex.Message));
                            status?.UnreadableIdentities.Add(
                                $"registry:{(hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU")}:{(view == RegistryView.Registry64 ? "64" : "32")}:{subkeyName}");
                            // An inaccessible registration entry must not interrupt the entire scan.
                        }
                    }
                }
                catch (Exception ex) when (ex is System.Security.SecurityException or
                    UnauthorizedAccessException or IOException or ArgumentException or PlatformNotSupportedException)
                {
                    status?.UnavailableSources.Add(
                        $"{(hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU")} ({(view == RegistryView.Registry64 ? "64" : "32")}-bit)");
                    // One unavailable hive or registry view does not prevent reading the others.
                }
            }
        }
        if (status != null) status.DiscoveredRegistryIdentities = result.Select(p => p.Identity).ToArray();
        return result.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    internal static List<InstalledProgramInfo> EnrichMissingDates(
        IReadOnlyList<InstalledProgramInfo> programs, CancellationToken token)
    {
        // Folder timestamps are only fallback metadata. They never prove when
        // the user first installed an application.
        Dictionary<string, int> folderUses = new(StringComparer.OrdinalIgnoreCase);
        foreach (InstalledProgramInfo app in programs)
        {
            string location = app.InstallLocation.Trim().TrimEnd('\\', '/');
            if (location.Length == 0) continue;
            folderUses[location] = folderUses.GetValueOrDefault(location) + 1;
        }

        List<InstalledProgramInfo> dated = new(programs.Count);
        foreach (InstalledProgramInfo app in programs)
        {
            token.ThrowIfCancellationRequested();
            InstalledProgramInfo current = app;
            string path = app.InstallLocation.Trim().TrimEnd('\\', '/');
            if (!app.CachedInventoryUtc.HasValue && !app.IsAuxiliary && !app.InstalledOn.HasValue &&
                folderUses.GetValueOrDefault(path) == 1 &&
                InstalledProgramSizeService.IsConfirmedApplicationFolder(path, app))
            {
                (DateTime? folderDate, string dateSource) =
                    InstalledProgramDateService.TryReadFolderDate(path, app.Kind);
                if (folderDate.HasValue)
                    current = current with
                    {
                        InstalledOn = folderDate,
                        InstallDateText = InstalledProgramDateService.FormatDate(folderDate.Value),
                        DateSource = dateSource
                    };
            }
            dated.Add(current);
        }
        return dated;
    }

    private static string RegString(RegistryKey key, string name)
    {
        object? value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return value == null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    }

    private static string FormatReportedSize(double bytes)
    {
        string[] units = SettingsService.L("Б,КБ,МБ,ГБ,ТБ", "B,KB,MB,GB,TB")
            .Split(',');
        int unit = 0;
        while (bytes >= 1024 && unit < units.Length - 1)
        {
            bytes /= 1024;
            unit++;
        }
        return $"{bytes:0.#} {units[unit]}";
    }

    internal static async Task<(List<InstalledProgramInfo> Apps, bool Available)> ReadStorePackagesAsync(
        CancellationToken cancellationToken)
    {
        List<InstalledProgramInfo> result = new();
        string ps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        if (!File.Exists(ps))
            return (result, false);

        // Start-menu app names are often localized and much friendlier than package IDs.
        const string command =
            "[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false); " +
            "$start=@(Get-StartApps -ErrorAction SilentlyContinue); " +
            "Get-AppxPackage -PackageTypeFilter Main -ErrorAction Stop | " +
            "Where-Object { -not $_.IsFramework -and -not $_.IsResourcePackage -and -not $_.NonRemovable } | " +
            "ForEach-Object { $p=$_; $matches=@($start | Where-Object { $_.AppID -like ($p.PackageFamilyName + '!*') }); " +
            "$display=if($matches.Count -gt 0){$matches[0].Name}else{$p.Name}; " +
            "$system=($p.IsPartOfSystem -eq $true -or [string]$p.SignatureKind -eq 'System' -or [string]$p.InstallLocation -like '*\\Windows\\SystemApps\\*'); " +
            "$installDate=if($p.PSObject.Properties['InstallDate']){[string]$p.InstallDate}else{''}; " +
            "[pscustomobject]@{Name=$p.Name;FriendlyName=$display;Version=$p.Version.ToString();Publisher=$p.Publisher;" +
            "InstallLocation=$p.InstallLocation;PackageFullName=$p.PackageFullName;PackageFamilyName=$p.PackageFamilyName;" +
            "HasStartApp=($matches.Count -gt 0);System=$system;InstallDate=$installDate} } | " +
            "ConvertTo-Json -Depth 3 -Compress";

        ProcessStartInfo info = new()
        {
            FileName = ps,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false)
        };
        info.ArgumentList.Add("-NoProfile");
        info.ArgumentList.Add("-NonInteractive");
        info.ArgumentList.Add("-Command");
        info.ArgumentList.Add(command);

        try
        {
            using Process process = new() { StartInfo = info };
            if (!process.Start())
                return (result, false);
            using CancellationTokenSource timeout =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            try
            {
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
                Task<string> errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
                await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(timeout.Token));
                if (process.ExitCode != 0)
                    return (result, false);
                string json = outputTask.Result.Trim();
                if (string.IsNullOrEmpty(json))
                    return (result, true);

                using JsonDocument doc = JsonDocument.Parse(json);
                IEnumerable<JsonElement> entries = doc.RootElement.ValueKind switch
                {
                    JsonValueKind.Array => doc.RootElement.EnumerateArray().ToArray(),
                    JsonValueKind.Object => new[] { doc.RootElement },
                    _ => Array.Empty<JsonElement>()
                };
                foreach (JsonElement entry in entries)
                {
                    string name = GetJsonText(entry, "Name");
                    if (string.IsNullOrWhiteSpace(name))
                        continue;
                    string publisher = GetJsonText(entry, "Publisher");
                    string cn = publisher.Split(',', StringSplitOptions.TrimEntries)
                        .FirstOrDefault(part => part.StartsWith("CN=", StringComparison.OrdinalIgnoreCase)) ?? "";
                    if (cn.Length > 3)
                        publisher = cn[3..];
                    // CN=GUID is a certificate identifier, not a useful publisher name.
                    if (Guid.TryParse(publisher, out _))
                        publisher = "—";
                    string displayName = GetJsonText(entry, "FriendlyName");
                    if (string.IsNullOrWhiteSpace(displayName) ||
                        displayName.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
                        displayName = name;
                    string location = GetJsonText(entry, "InstallLocation");
                    (DateTime? date, string dateText) = ReadStoreInstallDate(
                        GetJsonText(entry, "InstallDate"));
                    bool isSystem = GetJsonBoolean(entry, "System");
                    bool hasStartApp = GetJsonBoolean(entry, "HasStartApp");
                    // Background packages and inbox Windows components are hidden by default,
                    // but remain inspectable through the explicit components option.
                    string componentReason = InstalledProgramClassificationService.GetStoreComponentReason(name,
                        isSystem, hasStartApp);
                    result.Add(new InstalledProgramInfo(
                        displayName, publisher, GetJsonText(entry, "Version"), "MSIX",
                        "—", location,
                        SettingsService.L("Пакет Windows (текущий пользователь)", "Windows package (current user)"),
                        dateText, date, InstalledProgramIconService.TryReadMsixIcon(location),
                        !string.IsNullOrEmpty(componentReason), "",
                        date.HasValue ? SettingsService.L(
                            "Дата регистрации/обновления пакета", "Package registration/update date") : "",
                        componentReason,
                        "msix:" + GetJsonText(entry, "PackageFamilyName")));
                }
                return (result, true);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    try { process.Kill(entireProcessTree: true); }
                    catch (InvalidOperationException) { }
                    catch (System.ComponentModel.Win32Exception) { }
                }
                cancellationToken.ThrowIfCancellationRequested();
                return (result, false);
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or
            UnauthorizedAccessException or IOException or JsonException or InvalidOperationException)
        {
            return (result, false);
        }
    }

    private static string GetJsonText(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out JsonElement value))
            return "";
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number => value.ToString(),
            _ => ""
        };
    }
}
