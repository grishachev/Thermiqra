using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Win32;

namespace PCHardwareMonitor;

/// <summary>Cheap registration checks before showing a saved inventory.
/// Does not resolve paths, read icons, measure folders, or infer file ownership.</summary>
internal static class InstalledProgramsCacheVerifier
{
    internal static List<InstalledProgramInfo> VerifyRegistryEntries(
        IReadOnlyList<InstalledProgramInfo> saved, CancellationToken token)
    {
        List<InstalledProgramInfo> verified = new();
        foreach (InstalledProgramInfo app in saved)
        {
            token.ThrowIfCancellationRequested();
            // Package presence is confirmed by the independent package query,
            // never by an old install directory that may survive an uninstall.
            if (app.Kind == "MSIX") continue;
            string[] identity = app.Identity.Split(':', 4);
            if (identity.Length != 4 || identity[0] != "registry" ||
                (identity[1] != "HKLM" && identity[1] != "HKCU") ||
                (identity[2] != "64" && identity[2] != "32")) continue;
            try
            {
                using RegistryKey root = RegistryKey.OpenBaseKey(
                    identity[1] == "HKLM" ? RegistryHive.LocalMachine : RegistryHive.CurrentUser,
                    identity[2] == "64" ? RegistryView.Registry64 : RegistryView.Registry32);
                using RegistryKey? entry = root.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + identity[3], false);
                if (entry == null) continue;
                string name = Convert.ToString(entry.GetValue("DisplayName")) ?? "";
                string version = Convert.ToString(entry.GetValue("DisplayVersion")) ?? "";
                string registered = Convert.ToString(entry.GetValue("InstallLocation")) ?? "";
                if (string.IsNullOrWhiteSpace(registered))
                    registered = Convert.ToString(entry.GetValue("Inno Setup: App Path")) ?? "";
                // Do not flash a previous version/location while its fresh
                // metadata is still being discovered by the full inventory.
                if (!string.Equals(name.Trim(), app.Name, StringComparison.Ordinal) ||
                    !string.Equals(version, app.Version, StringComparison.Ordinal) ||
                    !string.Equals(registered, app.RegisteredLocation, StringComparison.OrdinalIgnoreCase)) continue;
                if (Convert.ToString(entry.GetValue("SystemComponent")) == "1" ||
                    !string.IsNullOrEmpty(Convert.ToString(entry.GetValue("ParentKeyName")))) continue;
                verified.Add(app with { CachedInventoryUtc = null });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                System.Security.SecurityException or ArgumentException or PlatformNotSupportedException)
            {
                // Unreadable is not absent. Keep the explicitly unconfirmed row.
                verified.Add(app with { CachedInventoryUtc = app.CachedInventoryUtc ?? DateTime.UtcNow });
            }
        }
        return verified;
    }
}
