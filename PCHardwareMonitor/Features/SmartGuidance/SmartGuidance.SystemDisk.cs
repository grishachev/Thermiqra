using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCHardwareMonitor;


public partial class SystemInfoWindow
{
    private static LogicalDriveInfo?
        FindWindowsLogicalDrive(
            HardwareSnapshot snapshot)
    {
        string windowsPath =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);

        string? root =
            Path.GetPathRoot(
                windowsPath);

        if (string.IsNullOrWhiteSpace(
                root))
        {
            return null;
        }

        string normalizedRoot =
            FormatDriveName(
                root);

        return snapshot.Drives
            .FirstOrDefault(
                drive =>
                    string.Equals(
                        FormatDriveName(
                            drive.Name),
                        normalizedRoot,
                        StringComparison.OrdinalIgnoreCase));
    }


    private static bool ShouldSuggestLargerSystemDrive(
        LogicalDriveInfo drive)
    {
        if (drive.TotalBytes <= 0)
            return false;

        double totalGb =
            drive.TotalBytes /
            1024d /
            1024d /
            1024d;

        double freeGb =
            drive.FreeBytes /
            1024d /
            1024d /
            1024d;

        double freePercent =
            drive.FreeBytes /
            (double)drive.TotalBytes *
            100.0;

        if (freeGb <= 12 ||
            freePercent <= 5)
        {
            return true;
        }

        if (totalGb <= 160 &&
            (freeGb <= 40 ||
             freePercent <= 30))
        {
            return true;
        }

        if (totalGb <= 320 &&
            (freeGb <= 32 ||
             freePercent <= 15))
        {
            return true;
        }

        return false;
    }


    private static SystemDriveHardwareEvidence
        ReadSystemDriveHardwareEvidence(
            HardwareSnapshot snapshot)
    {
        int? systemDiskIndex =
            TryGetWindowsPhysicalDiskIndex();

        if (!systemDiskIndex.HasValue)
        {
            return new SystemDriveHardwareEvidence
            {
                Kind =
                    PhysicalStorageKind.Unknown,

                HasOtherSsd =
                    snapshot.StorageDevices.Any(
                        IsStorageDeviceSsd)
            };
        }

        Dictionary<int, PhysicalStorageKind>
            physicalKinds =
                ReadPhysicalStorageKinds();

        PhysicalStorageKind kind =
            physicalKinds.TryGetValue(
                systemDiskIndex.Value,
                out PhysicalStorageKind detectedKind)
                    ? detectedKind
                    : PhysicalStorageKind.Unknown;

        string? systemModel =
            ReadWin32DiskModel(
                systemDiskIndex.Value);

        if (kind ==
            PhysicalStorageKind.Unknown)
        {
            StorageDeviceInfo? matchingStorage =
                snapshot.StorageDevices
                    .FirstOrDefault(
                        storage =>
                            !string.IsNullOrWhiteSpace(
                                systemModel) &&
                            (
                                storage.Name.Contains(
                                    systemModel,
                                    StringComparison.OrdinalIgnoreCase) ||
                                systemModel.Contains(
                                    storage.Name,
                                    StringComparison.OrdinalIgnoreCase)
                            ));

            if (matchingStorage != null)
            {
                kind =
                    GetPhysicalStorageKind(
                        matchingStorage);
            }
            else if (!string.IsNullOrWhiteSpace(
                         systemModel))
            {
                if (systemModel.Contains(
                        "NVME",
                        StringComparison.OrdinalIgnoreCase) ||
                    systemModel.Contains(
                        "SSD",
                        StringComparison.OrdinalIgnoreCase))
                {
                    kind =
                        PhysicalStorageKind.Ssd;
                }
            }
        }

        bool hasOtherSsd =
            physicalKinds.Any(
                pair =>
                    pair.Key !=
                        systemDiskIndex.Value &&
                    pair.Value ==
                        PhysicalStorageKind.Ssd);

        if (!hasOtherSsd &&
            kind ==
                PhysicalStorageKind.Hdd)
        {
            hasOtherSsd =
                snapshot.StorageDevices.Any(
                    IsStorageDeviceSsd);
        }

        return new SystemDriveHardwareEvidence
        {
            Kind =
                kind,

            HasOtherSsd =
                hasOtherSsd
        };
    }


    private static int?
        TryGetWindowsPhysicalDiskIndex()
    {
        try
        {
            string windowsPath =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.Windows);

            string? root =
                Path.GetPathRoot(
                    windowsPath);

            if (string.IsNullOrWhiteSpace(
                    root))
            {
                return null;
            }

            string logicalId =
                FormatDriveName(
                    root);

            using ManagementObjectSearcher searcher =
                new(
                    "SELECT Antecedent, Dependent " +
                    "FROM Win32_LogicalDiskToPartition");

            using ManagementObjectCollection results =
                searcher.Get();

            foreach (ManagementObject association in
                     results)
            {
                string dependent =
                    association[
                        "Dependent"]?
                    .ToString() ??
                    string.Empty;

                if (!dependent.Contains(
                        $"DeviceID=\"{logicalId}\"",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? antecedent =
                    association[
                        "Antecedent"]?
                    .ToString();

                if (string.IsNullOrWhiteSpace(
                        antecedent))
                {
                    continue;
                }

                using ManagementObject partition =
                    new(
                        new ManagementPath(
                            antecedent));

                partition.Get();

                object? diskIndexValue =
                    partition[
                        "DiskIndex"];

                if (diskIndexValue != null &&
                    int.TryParse(
                        diskIndexValue.ToString(),
                        out int diskIndex))
                {
                    return diskIndex;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Не удалось определить физический диск Windows: {ex}");
        }

        return null;
    }


    private static Dictionary<int, PhysicalStorageKind>
        ReadPhysicalStorageKinds()
    {
        Dictionary<int, PhysicalStorageKind> result =
            new();

        try
        {
            ManagementScope scope =
                new(
                    @"\\.\root\Microsoft\Windows\Storage");

            scope.Connect();

            ObjectQuery query =
                new(
                    "SELECT DeviceId, MediaType " +
                    "FROM MSFT_PhysicalDisk");

            using ManagementObjectSearcher searcher =
                new(
                    scope,
                    query);

            using ManagementObjectCollection items =
                searcher.Get();

            foreach (ManagementObject item in
                     items)
            {
                if (!int.TryParse(
                        item["DeviceId"]?
                            .ToString(),
                        out int deviceId))
                {
                    continue;
                }

                int mediaType =
                    item["MediaType"] == null
                        ? 0
                        : Convert.ToInt32(
                            item["MediaType"]);

                PhysicalStorageKind kind =
                    mediaType switch
                    {
                        3 =>
                            PhysicalStorageKind.Hdd,

                        4 =>
                            PhysicalStorageKind.Ssd,

                        _ =>
                            PhysicalStorageKind.Unknown
                    };

                result[deviceId] =
                    kind;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Не удалось определить типы физических накопителей: {ex}");
        }

        return result;
    }


    private static string?
        ReadWin32DiskModel(
            int diskIndex)
    {
        try
        {
            using ManagementObjectSearcher searcher =
                new(
                    "SELECT Model " +
                    "FROM Win32_DiskDrive " +
                    $"WHERE Index = {diskIndex}");

            using ManagementObjectCollection results =
                searcher.Get();

            foreach (ManagementObject item in
                     results)
            {
                return item["Model"]?
                    .ToString()?
                    .Trim();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Не удалось получить модель системного диска: {ex}");
        }

        return null;
    }


    private static bool IsStorageDeviceSsd(
        StorageDeviceInfo storage)
    {
        return GetPhysicalStorageKind(
                   storage) ==
               PhysicalStorageKind.Ssd;
    }


    private static PhysicalStorageKind
        GetPhysicalStorageKind(
            StorageDeviceInfo storage)
    {
        string identifier =
            storage.Id ??
            string.Empty;

        if (identifier.Contains(
                "nvme",
                StringComparison.OrdinalIgnoreCase) ||
            identifier.Contains(
                "ssd",
                StringComparison.OrdinalIgnoreCase))
        {
            return PhysicalStorageKind.Ssd;
        }

        if (identifier.Contains(
                "hdd",
                StringComparison.OrdinalIgnoreCase))
        {
            return PhysicalStorageKind.Hdd;
        }

        return PhysicalStorageKind.Unknown;
    }


    private sealed class SystemDriveHardwareEvidence
    {
        public PhysicalStorageKind Kind { get; init; }

        public bool HasOtherSsd { get; init; }
    }


    private enum PhysicalStorageKind
    {
        Unknown = 0,
        Hdd = 1,
        Ssd = 2
    }



}
