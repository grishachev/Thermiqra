using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PCHardwareMonitor;

public sealed partial class SystemInfoService
{


    // ============================================================
    // WINDOWS
    // ============================================================

    private static OperatingSystemDetails ReadWindowsInfo()
    {
        string? caption = null;
        string? version = null;
        string? buildNumber = null;
        string? architecture = null;
        DateTime? installDate = null;

        try
        {
            using ManagementObjectSearcher searcher =
                new(
                    "SELECT " +
                    "Caption, " +
                    "Version, " +
                    "BuildNumber, " +
                    "OSArchitecture, " +
                    "InstallDate " +
                    "FROM Win32_OperatingSystem");

            using ManagementObjectCollection results =
                searcher.Get();

            foreach (ManagementObject item in results)
            {
                caption =
                    ReadString(
                        item,
                        "Caption");

                version =
                    ReadString(
                        item,
                        "Version");

                buildNumber =
                    ReadString(
                        item,
                        "BuildNumber");

                architecture =
                    ReadString(
                        item,
                        "OSArchitecture");

                installDate =
                    ReadWmiDate(
                        item,
                        "InstallDate");

                break;
            }
        }
        catch
        {
            // Если WMI не отдаст данные,
            // оставляем значения пустыми.
        }

        string? displayVersion =
            ReadWindowsRegistryValue(
                "DisplayVersion");

        if (string.IsNullOrWhiteSpace(displayVersion))
        {
            displayVersion =
                ReadWindowsRegistryValue(
                    "ReleaseId");
        }

        string? windowsName =
            DetectWindowsName(
                caption,
                buildNumber);

        string? edition =
            ExtractWindowsEdition(
                caption);

        if (string.IsNullOrWhiteSpace(edition))
        {
            edition =
                ReadWindowsRegistryValue(
                    "EditionID");
        }

        if (string.IsNullOrWhiteSpace(architecture))
        {
            architecture =
                RuntimeInformation
                    .OSArchitecture
                    .ToString();
        }

        architecture =
            NormalizeOsArchitecture(
                architecture);

        return new OperatingSystemDetails
        {
            Name = windowsName,
            Edition = edition,
            DisplayVersion = displayVersion,
            Version = version,
            BuildNumber = buildNumber,
            Architecture = architecture,
            ComputerName = Environment.MachineName,
            InstallDate = installDate
        };
    }


    private static string? DetectWindowsName(
        string? caption,
        string? buildNumber)
    {
        if (int.TryParse(
                buildNumber,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int build))
        {
            if (build >= 22000)
                return "Windows 11";

            if (build >= 10240)
                return "Windows 10";
        }

        if (!string.IsNullOrWhiteSpace(caption))
        {
            if (caption.Contains(
                    "Windows 11",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Windows 11";
            }

            if (caption.Contains(
                    "Windows 10",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Windows 10";
            }

            return caption.Trim();
        }

        return null;
    }


    private static string? ExtractWindowsEdition(
        string? caption)
    {
        if (string.IsNullOrWhiteSpace(caption))
            return null;

        string edition =
            caption.Trim();

        edition =
            edition.Replace(
                "Microsoft ",
                "",
                StringComparison.OrdinalIgnoreCase);

        edition =
            edition.Replace(
                "Майкрософт ",
                "",
                StringComparison.OrdinalIgnoreCase);

        edition =
            edition.Replace(
                "Windows 11",
                "",
                StringComparison.OrdinalIgnoreCase);

        edition =
            edition.Replace(
                "Windows 10",
                "",
                StringComparison.OrdinalIgnoreCase);

        edition =
            edition.Trim();

        return string.IsNullOrWhiteSpace(edition)
            ? null
            : edition;
    }


    private static string? ReadWindowsRegistryValue(
        string valueName)
    {
        try
        {
            using RegistryKey localMachine =
                RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    RegistryView.Registry64);

            using RegistryKey? key =
                localMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");

            object? value =
                key?.GetValue(
                    valueName);

            return CleanText(
                Convert.ToString(
                    value,
                    CultureInfo.InvariantCulture));
        }
        catch
        {
            return null;
        }
    }

}
