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
    // ФИЗИЧЕСКИЕ НАКОПИТЕЛИ
    // ============================================================

    private static IReadOnlyList<StorageDeviceDetails>
        ReadStorageDevices()
    {
        List<StorageDeviceDetails> devices =
            new();

        try
        {
            using ManagementObjectSearcher searcher =
                new(
                    "SELECT " +
                    "DeviceID, " +
                    "Index, " +
                    "Model, " +
                    "Manufacturer, " +
                    "SerialNumber, " +
                    "PNPDeviceID, " +
                    "FirmwareRevision, " +
                    "InterfaceType, " +
                    "MediaType, " +
                    "Size " +
                    "FROM Win32_DiskDrive");

            using ManagementObjectCollection results =
                searcher.Get();

            foreach (ManagementObject item in results)
            {
                string? model =
                    ReadString(
                        item,
                        "Model");

                devices.Add(
                    new StorageDeviceDetails
                    {
                        DeviceId =
                            ReadString(
                                item,
                                "DeviceID"),

                        PhysicalDiskIndex =
                            ReadUInt32(
                                item,
                                "Index"),

                        PnpDeviceId =
                            ReadString(item, "PNPDeviceID"),

                        Model = model,

                        Manufacturer =
                            NormalizeStorageManufacturer(
                                ReadString(
                                    item,
                                    "Manufacturer")),

                        SerialNumber =
                            ReadString(
                                item,
                                "SerialNumber"),

                        Firmware =
                            ReadString(
                                item,
                                "FirmwareRevision"),

                        InterfaceType =
                            ReadString(
                                item,
                                "InterfaceType"),

                        MediaType =
                            NormalizeStorageMediaType(
                                ReadString(
                                    item,
                                    "MediaType")),

                        CapacityBytes =
                            ReadUInt64(
                                item,
                                "Size")
                    });
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Failure("SystemInfo.Storage", ex);
            // Если WMI не отдаст накопители,
            // список останется пустым.
        }

        return devices;
    }


    // Read only when System Information is opened. Never query the storage
    // provider from the one-second monitoring cycle.
    //
    // MSFT_Disk.Number equals Win32_DiskDrive.Index. BusType belongs to that
    // numbered disk. MSFT_PhysicalDisk.DeviceId is NOT automatically the disk
    // number, so use its media type only after corroborating the identity.
    public static IReadOnlyDictionary<uint, StorageReliabilityDetails>
        ReadStorageReliabilityCounters(
            IReadOnlyList<StorageDeviceDetails>? knownDevices = null,
            List<string>? diagnostics = null)
    {
        Dictionary<uint, StorageReliabilityDetails> result = new();

        try
        {
            ManagementScope scope = new(
                @"\\.\root\Microsoft\Windows\Storage");

            scope.Connect();

            // Query a second class for the real HDD/SSD distinction. The
            // legacy Win32_DiskDrive.MediaType means fixed/removable, not HDD.
            List<PhysicalDiskDescription> physicalDisks =
                ReadPhysicalDiskDescriptions(scope);

            using ManagementObjectSearcher searcher = new(
                scope,
                new ObjectQuery("SELECT * FROM MSFT_Disk"));

            using ManagementObjectCollection disks = searcher.Get();

            foreach (ManagementObject disk in disks)
            {
                using (disk)
                {
                    uint? number = ReadUInt32(disk, "Number");
                    if (!number.HasValue || result.ContainsKey(number.Value))
                        continue;

                    ushort? busType = ReadUInt16(disk, "BusType");
                    ushort? mediaType = ResolveStorageMediaType(
                        disk, number.Value, busType, physicalDisks,
                        knownDevices);

                    byte? wear = null;
                    byte? temperature = null;
                    ushort? hours = null;
                    ulong? readErrors = null;
                    ulong? writeErrors = null;

                    // Some USB bridges, RAID and virtual devices provide no
                    // counters, but the bus/media classification may remain.
                    try
                    {
                        using ManagementObjectCollection counters =
                            disk.GetRelated(
                                "MSFT_StorageReliabilityCounter",
                                "MSFT_DiskToStorageReliabilityCounter",
                                null!, null!,
                                "StorageReliabilityCounter", "Disk",
                                false, new EnumerationOptions());

                        int matches = 0;
                        foreach (ManagementObject counter in counters)
                        {
                            using (counter)
                            {
                                matches++;
                                if (matches != 1)
                                    break;

                                byte? rawWear = ReadByte(counter, "Wear");
                                byte? rawTemp = ReadByte(counter, "Temperature");
                                ushort? rawHours = ReadUInt16(
                                    counter, "PowerOnHours");

                                wear = rawWear is <= 100 ? rawWear : null;
                                temperature = rawTemp is > 0 and <= 125
                                    ? rawTemp : null;
                                hours = rawHours is > 0 ? rawHours : null;
                                readErrors = ReadUInt64(
                                    counter, "ReadErrorsUncorrected");
                                writeErrors = ReadUInt64(
                                    counter, "WriteErrorsUncorrected");
                            }
                        }

                        if (matches > 1)
                        {
                            // Ambiguous association: never assign counters.
                            wear = null;
                            temperature = null;
                            hours = null;
                            readErrors = null;
                            writeErrors = null;
                        }
                    }
                    catch (ManagementException)
                    {
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                    }

                    // Query NVMe SMART/Health only for an NVMe bus and only
                    // during this on-demand System Information request.
                    StorageNvmeHealthDetails? nvmeHealth = busType == 17
                        ? StorageNvmeHealthReader.TryRead(number.Value)
                        : null;

                    result.Add(number.Value, new StorageReliabilityDetails
                    {
                        NvmeHealth = nvmeHealth,
                        BusTypeCode = busType,
                        MediaTypeCode = mediaType,
                        // A generic Windows Wear=0 is not trustworthy for
                        // SATA SSDs (the Kingston A400 reports a different
                        // endurance value in its vendor ATA SMART E7).
                        // Use the standardized NVMe SMART/Health log when
                        // available; keep Windows wear only as NVMe fallback.
                        WearPercent = busType == 17 ? wear : null,
                        TemperatureCelsius = temperature,
                        PowerOnHours = hours,
                        ReadErrorsUncorrected = readErrors,
                        WriteErrorsUncorrected = writeErrors
                    });
                }
            }
        }
        catch (ManagementException ex)
        {
            diagnostics?.Add($"Windows Storage provider: {ex.Message} (0x{ex.HResult:X8})");
        }
        catch (UnauthorizedAccessException ex)
        {
            diagnostics?.Add($"Windows Storage provider: {ex.Message} (0x{ex.HResult:X8})");
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            diagnostics?.Add($"Windows Storage provider: {ex.Message} (0x{ex.HResult:X8})");
        }

        // A separate, optional read of standard ATA SMART attributes.
        // Failure of the provider never invalidates Windows Storage counters.
        if (knownDevices != null)
        {
            ReadAtaSmartForConfirmedDrives(result, knownDevices, diagnostics);
            ReadAtaSmartFailureStatus(result, knownDevices, diagnostics);
            ReadFailurePredictionFallback(result, knownDevices);
            ReadAtaSmartThresholds(result, knownDevices, diagnostics);
        }

        return result;
    }


    // MSStorageDriver_FailurePredictData exposes the raw ATA SMART sector on
    // supported controllers. It does NOT expose a reliable disk number: match
    // its InstanceName to the exact PNPDeviceID of Win32_DiskDrive, allowing
    // only a numeric instance suffix (_0, _1, ...).
    // Read ALL ATA attributes for confirmed ATA/SATA HDDs and SSDs, then
    // interpret only well-understood counters and documented vendor families.
    // Never associate by position, model name or disk number alone.
    private static void ReadAtaSmartForConfirmedDrives(
        Dictionary<uint, StorageReliabilityDetails> reliability,
        IReadOnlyList<StorageDeviceDetails> knownDevices,
        List<string>? diagnostics)
    {
        try
        {
            ManagementScope scope = new(@"\\.\root\wmi");
            scope.Connect();

            using ManagementObjectSearcher searcher = new(
                scope,
                new ObjectQuery(
                    "SELECT Active, InstanceName, VendorSpecific " +
                    "FROM MSStorageDriver_FailurePredictData"));

            using ManagementObjectCollection records = searcher.Get();

            // Collect all matches first: duplicate WMI entries for a single
            // device make the association ambiguous, so they are rejected.
            Dictionary<uint, List<StorageAtaSmartDetails>> matches = new();

            foreach (ManagementObject record in records)
            {
                using (record)
                {
                    if (record["Active"] is bool active && !active)
                        continue;

                    string? instanceName = ReadString(record, "InstanceName");
                    if (string.IsNullOrWhiteSpace(instanceName) ||
                        record["VendorSpecific"] is not byte[] rawData ||
                        rawData.Length < 362)
                    {
                        continue;
                    }

                    // Only one positively identified physical disk may own
                    // a SMART record; otherwise reject the association.
                    StorageDeviceDetails? owner = null;
                    bool ambiguous = false;
                    foreach (StorageDeviceDetails device in knownDevices)
                    {
                        if (!device.PhysicalDiskIndex.HasValue ||
                            !reliability.TryGetValue(
                                device.PhysicalDiskIndex.Value,
                                out StorageReliabilityDetails? info) ||
                            !MatchesSmartInstanceName(
                                instanceName, device.PnpDeviceId))
                        {
                            continue;
                        }

                        // ATA/SATA is supported regardless of the vendor.
                        // NVMe and virtual/USB/RAID bridges are not decoded as
                        // native ATA SMART without proven pass-through support.
                        if (info.BusTypeCode is not (3 or 11))
                            continue;

                        if (owner != null)
                        {
                            ambiguous = true;
                            break;
                        }

                        owner = device;
                    }

                    if (ambiguous || owner?.PhysicalDiskIndex == null)
                        continue;

                    StorageReliabilityDetails deviceReliability =
                        reliability[owner.PhysicalDiskIndex.Value];
                    StorageAtaSmartDetails? smart =
                        StorageAtaSmartInterpreter.Parse(
                            rawData, owner.Model,
                            deviceReliability.MediaTypeCode);

                    if (smart == null || !smart.HasAnyData)
                        continue;

                    uint index = owner.PhysicalDiskIndex.Value;
                    if (!matches.TryGetValue(
                            index, out List<StorageAtaSmartDetails>? values))
                    {
                        values = new List<StorageAtaSmartDetails>();
                        matches.Add(index, values);
                    }
                    values.Add(smart);
                }
            }

            foreach (KeyValuePair<uint, List<StorageAtaSmartDetails>> item
                     in matches)
            {
                if (item.Value.Count == 1 &&
                    reliability.TryGetValue(item.Key,
                        out StorageReliabilityDetails? info))
                {
                    info.AtaSmart = item.Value[0];
                    info.AtaSmartSource = "MSStorageDriver_FailurePredictData (WMI)";
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or COMException)
        { diagnostics?.Add("MSStorageDriver_FailurePredictData: " + ex.Message + $" (0x{ex.HResult:X8})"); }
    }


    // Match a WMI SMART result to one and only one ATA/SATA physical disk.
    // No matching by array order, fuzzy model names or disk size.
    private static uint? FindConfirmedAtaDiskIndex(
        string? instanceName,
        IReadOnlyDictionary<uint, StorageReliabilityDetails> reliability,
        IReadOnlyList<StorageDeviceDetails> knownDevices)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
            return null;

        uint? match = null;
        foreach (StorageDeviceDetails device in knownDevices)
        {
            if (!device.PhysicalDiskIndex.HasValue ||
                !reliability.TryGetValue(device.PhysicalDiskIndex.Value,
                    out StorageReliabilityDetails? details) ||
                details.BusTypeCode is not (3 or 11) ||
                !MatchesSmartInstanceName(instanceName, device.PnpDeviceId))
                continue;

            if (match.HasValue)
                return null; // Ambiguous identity.

            match = device.PhysicalDiskIndex.Value;
        }

        return match;
    }

    // The drive supplies normalized thresholds independently of RAW SMART
    // attributes. These apply to ATA HDDs and SATA SSDs of any maker.
    private static void ReadAtaSmartThresholds(
        Dictionary<uint, StorageReliabilityDetails> reliability,
        IReadOnlyList<StorageDeviceDetails> knownDevices,
        List<string>? diagnostics)
    {
        try
        {
            ManagementScope scope = new(@"\\.\root\wmi");
            scope.Connect();
            using ManagementObjectSearcher searcher = new(
                scope,
                new ObjectQuery(
                    "SELECT InstanceName, VendorSpecific " +
                    "FROM MSStorageDriver_FailurePredictThresholds"));
            using ManagementObjectCollection records = searcher.Get();

            Dictionary<uint, List<byte[]>> matches = new();
            foreach (ManagementObject record in records)
            {
                using (record)
                {
                    uint? index = FindConfirmedAtaDiskIndex(
                        ReadString(record, "InstanceName"),
                        reliability, knownDevices);
                    if (!index.HasValue ||
                        !reliability.TryGetValue(index.Value,
                            out StorageReliabilityDetails? details) ||
                        details.AtaSmart is not { Attributes.Count: > 0 } ||
                        record["VendorSpecific"] is not byte[] raw ||
                        raw.Length < 362)
                        continue;

                    if (!matches.TryGetValue(index.Value,
                            out List<byte[]>? items))
                    {
                        items = new List<byte[]>();
                        matches.Add(index.Value, items);
                    }
                    items.Add(raw);
                }
            }

            foreach (KeyValuePair<uint, List<byte[]>> pair in matches)
            {
                // Multiple threshold records for one device are ambiguous.
                if (pair.Value.Count == 1 &&
                    reliability[pair.Key].AtaSmart is { } smart)
                    StorageAtaSmartInterpreter.TryApplyThresholds(
                        smart, pair.Value[0]);
            }
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or COMException)
        { diagnostics?.Add("MSStorageDriver_FailurePredictThresholds: " + ex.Message + $" (0x{ex.HResult:X8})"); }
    }

    // Firmware failure prediction is more meaningful than a guessed
    // health percentage. A false value means NO predicted failure, not
    // a guarantee that the drive is healthy.
    private static void ReadAtaSmartFailureStatus(
        Dictionary<uint, StorageReliabilityDetails> reliability,
        IReadOnlyList<StorageDeviceDetails> knownDevices,
        List<string>? diagnostics)
    {
        try
        {
            ManagementScope scope = new(@"\\.\root\wmi");
            scope.Connect();
            using ManagementObjectSearcher searcher = new(
                scope,
                new ObjectQuery(
                    "SELECT Active, InstanceName, PredictFailure " +
                    "FROM MSStorageDriver_FailurePredictStatus"));
            using ManagementObjectCollection records = searcher.Get();

            Dictionary<uint, List<bool>> matches = new();
            foreach (ManagementObject record in records)
            {
                using (record)
                {
                    if (record["Active"] is bool active && !active)
                        continue;

                    uint? index = FindConfirmedAtaDiskIndex(
                        ReadString(record, "InstanceName"),
                        reliability, knownDevices);
                    if (!index.HasValue ||
                        record["PredictFailure"] is not bool failure)
                        continue;

                    if (!matches.TryGetValue(index.Value,
                            out List<bool>? items))
                    {
                        items = new List<bool>();
                        matches.Add(index.Value, items);
                    }
                    items.Add(failure);
                }
            }

            foreach (KeyValuePair<uint, List<bool>> pair in matches)
            {
                if (pair.Value.Count == 1)
                    reliability[pair.Key].AtaSmartPredictedFailure =
                        pair.Value[0];
            }
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or COMException)
        { diagnostics?.Add("MSStorageDriver_FailurePredictStatus: " + ex.Message + $" (0x{ex.HResult:X8})"); }
    }

    private static bool MatchesSmartInstanceName(
        string instanceName, string? pnpDeviceId)
    {
        if (string.IsNullOrWhiteSpace(pnpDeviceId))
            return false;

        string instance = instanceName.Trim();
        string device = pnpDeviceId.Trim();

        if (string.Equals(instance, device,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // MSStorageDriver often adds a _0 instance suffix to the PNP ID.
        // Only a numeric suffix is permitted, never a fuzzy prefix match.
        if (!instance.StartsWith(device + "_",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        ReadOnlySpan<char> suffix = instance.AsSpan(device.Length + 1);
        if (suffix.IsEmpty)
            return false;

        foreach (char digit in suffix)
            if (digit < '0' || digit > '9')
                return false;

        return true;
    }


    private sealed record PhysicalDiskDescription(
        string? DeviceId,
        string? UniqueId,
        string? FriendlyName,
        string? SerialNumber,
        ulong? Size,
        ushort? MediaType);


    private static List<PhysicalDiskDescription>
        ReadPhysicalDiskDescriptions(ManagementScope scope)
    {
        List<PhysicalDiskDescription> result = new();

        try
        {
            using ManagementObjectSearcher searcher = new(
                scope,
                new ObjectQuery("SELECT * FROM MSFT_PhysicalDisk"));

            using ManagementObjectCollection devices = searcher.Get();
            foreach (ManagementObject item in devices)
            {
                using (item)
                {
                    result.Add(new PhysicalDiskDescription(
                        ReadString(item, "DeviceId"),
                        ReadString(item, "UniqueId"),
                        ReadString(item, "FriendlyName"),
                        ReadString(item, "SerialNumber"),
                        ReadUInt64(item, "Size"),
                        ReadUInt16(item, "MediaType")));
                }
            }
        }
        catch (ManagementException)
        {
        }
        catch (System.Runtime.InteropServices.COMException)
        {
        }

        return result;
    }


    private static ushort? ResolveStorageMediaType(
        ManagementBaseObject disk,
        uint diskNumber,
        ushort? busType,
        IReadOnlyList<PhysicalDiskDescription> physicalDisks,
        IReadOnlyList<StorageDeviceDetails>? knownDevices)
    {
        // NVMe protocol itself identifies solid-state storage here.
        if (busType == 17)
            return 4;

        StorageDeviceDetails? source = null;
        if (knownDevices != null)
        {
            foreach (StorageDeviceDetails device in knownDevices)
            {
                if (device.PhysicalDiskIndex != diskNumber)
                    continue;

                if (source != null)
                    return null;

                source = device;
            }
        }

        if (source == null)
            return null;

        string? diskUniqueId = ReadString(disk, "UniqueId");
        PhysicalDiskDescription? matched = null;

        foreach (PhysicalDiskDescription physical in physicalDisks)
        {
            if (physical.MediaType is not (3 or 4 or 5))
                continue;

            bool exactUniqueId =
                !string.IsNullOrWhiteSpace(diskUniqueId) &&
                string.Equals(diskUniqueId, physical.UniqueId,
                    StringComparison.OrdinalIgnoreCase);

            bool matchingNumber = uint.TryParse(
                physical.DeviceId, NumberStyles.None,
                CultureInfo.InvariantCulture, out uint physicalNumber) &&
                physicalNumber == diskNumber;

            bool matchingModel =
                MatchesStorageIdentifier(source.Model, physical.FriendlyName);

            bool matchingSerial =
                MatchesStorageIdentifier(source.SerialNumber,
                    physical.SerialNumber);

            bool matchingSize =
                source.CapacityBytes.HasValue &&
                physical.Size.HasValue &&
                source.CapacityBytes.Value > 0 &&
                physical.Size.Value > 0 &&
                Math.Abs((decimal)source.CapacityBytes.Value -
                    (decimal)physical.Size.Value) <=
                    (decimal)source.CapacityBytes.Value / 100m;

            // UniqueId can identify the actual device if it is identical.
            // Otherwise require the Windows disk index AND two independent
            // corroborations (capacity and model/serial). Never join by name
            // or disk number alone; duplicated disks must remain ambiguous.
            if (!exactUniqueId &&
                !(matchingNumber && matchingSize &&
                  (matchingModel || matchingSerial)))
            {
                continue;
            }

            if (matched != null)
                return null;

            matched = physical;
        }

        return matched?.MediaType;
    }


    private static bool MatchesStorageIdentifier(
        string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) ||
            string.IsNullOrWhiteSpace(right))
            return false;

        // Ignore whitespace, hyphens and model separators, but never use
        // substring matches: two near-identical model names may be different.
        System.Text.StringBuilder normalizedLeft = new();
        System.Text.StringBuilder normalizedRight = new();

        foreach (char c in left)
            if (char.IsLetterOrDigit(c))
                normalizedLeft.Append(char.ToUpperInvariant(c));

        foreach (char c in right)
            if (char.IsLetterOrDigit(c))
                normalizedRight.Append(char.ToUpperInvariant(c));

        return normalizedLeft.Length >= 6 &&
            string.Equals(normalizedLeft.ToString(),
                normalizedRight.ToString(), StringComparison.Ordinal);
    }


    private static byte? ReadByte(
        ManagementBaseObject item,
        string propertyName)
    {
        try
        {
            object? value = item[propertyName];
            return value == null
                ? null
                : Convert.ToByte(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }


    private static string? NormalizeStorageManufacturer(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        string text =
            value.Trim();

        if (string.Equals(
                text,
                "(Стандартные дисковые накопители)",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                text,
                "Стандартные дисковые накопители",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                text,
                "(Standard disk drives)",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                text,
                "Standard disk drives",
                StringComparison.OrdinalIgnoreCase))
        {
            return SettingsService.L(
                "(Стандартные дисковые накопители)",
                "(Standard disk drives)");
        }

        return text;
    }


    private static string? NormalizeStorageMediaType(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        string text =
            value.Trim();

        if (string.Equals(
                text,
                "Fixed hard disk media",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                text,
                "Фиксированный жесткий диск",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                text,
                "Фиксированный носитель",
                StringComparison.OrdinalIgnoreCase))
        {
            return SettingsService.L(
                "Несъёмный жёсткий диск",
                "Fixed hard disk media");
        }

        if (string.Equals(
                text,
                "Removable media",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                text,
                "Съёмный носитель",
                StringComparison.OrdinalIgnoreCase))
        {
            return SettingsService.L(
                "Съёмный носитель",
                "Removable media");
        }

        return text;
    }

}
