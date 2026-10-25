using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PCHardwareMonitor;

public sealed class SystemInfoService
{
    public SystemInformationSnapshot GetSnapshot()
    {
        return new SystemInformationSnapshot
        {
            Windows = ReadWindowsInfo(),
            Processor = ReadProcessorInfo(),
            Motherboard = ReadMotherboardInfo(),
            Memory = ReadMemoryInfo(),
            GraphicsAdapters = ReadGraphicsAdapters(),
            StorageDevices = ReadStorageDevices()
        };
    }


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


    // ============================================================
    // ПРОЦЕССОР
    // ============================================================

    private static ProcessorDetails ReadProcessorInfo()
    {
        ProcessorDetails result =
            new();

        try
        {
            using ManagementObjectSearcher searcher =
                new(
                    "SELECT " +
                    "Manufacturer, " +
                    "Name, " +
                    "Architecture, " +
                    "NumberOfCores, " +
                    "NumberOfLogicalProcessors, " +
                    "CurrentClockSpeed, " +
                    "MaxClockSpeed, " +
                    "SocketDesignation, " +
                    "L2CacheSize, " +
                    "L3CacheSize " +
                    "FROM Win32_Processor");

            using ManagementObjectCollection results =
                searcher.Get();

            foreach (ManagementObject item in results)
            {
                ushort? architectureCode =
                    ReadUInt16(
                        item,
                        "Architecture");

                string? architecture =
                    FormatProcessorArchitecture(
                        architectureCode);

                if (string.IsNullOrWhiteSpace(architecture))
                {
                    architecture =
                        RuntimeInformation
                            .ProcessArchitecture
                            .ToString();
                }

                result =
                    new ProcessorDetails
                    {
                        Manufacturer =
                            ReadString(
                                item,
                                "Manufacturer"),

                        Name =
                            ReadString(
                                item,
                                "Name"),

                        Architecture =
                            architecture,

                        PhysicalCores =
                            ReadPositiveUInt32(
                                item,
                                "NumberOfCores"),

                        LogicalProcessors =
                            ReadPositiveUInt32(
                                item,
                                "NumberOfLogicalProcessors"),

                        CurrentClockSpeedMhz =
                            ReadPositiveUInt32(
                                item,
                                "CurrentClockSpeed"),

                        MaxClockSpeedMhz =
                            ReadPositiveUInt32(
                                item,
                                "MaxClockSpeed"),

                        Socket =
                            ReadString(
                                item,
                                "SocketDesignation"),

                        L2CacheKb =
                            ReadPositiveUInt32(
                                item,
                                "L2CacheSize"),

                        L3CacheKb =
                            ReadPositiveUInt32(
                                item,
                                "L3CacheSize")
                    };

                break;
            }
        }
        catch
        {
            // Возвращаем пустой объект.
        }

        return result;
    }


    private static string? FormatProcessorArchitecture(
        ushort? architecture)
    {
        if (!architecture.HasValue)
            return null;

        return architecture.Value switch
        {
            0 => "x86",
            1 => "MIPS",
            2 => "Alpha",
            3 => "PowerPC",
            5 => "ARM",
            6 => "IA64",
            9 => "x64",
            12 => "ARM64",
            _ => $"Код {architecture.Value}"
        };
    }


    // ============================================================
    // МАТЕРИНСКАЯ ПЛАТА / BIOS
    // ============================================================

    private static MotherboardDetails ReadMotherboardInfo()
    {
        string? manufacturer = null;
        string? model = null;
        string? version = null;

        string? biosVersion = null;
        DateTime? biosDate = null;

        try
        {
            using ManagementObjectSearcher boardSearcher =
                new(
                    "SELECT " +
                    "Manufacturer, " +
                    "Product, " +
                    "Version " +
                    "FROM Win32_BaseBoard");

            using ManagementObjectCollection boardResults =
                boardSearcher.Get();

            foreach (ManagementObject item in boardResults)
            {
                manufacturer =
                    ReadString(
                        item,
                        "Manufacturer");

                model =
                    ReadString(
                        item,
                        "Product");

                version =
                    ReadString(
                        item,
                        "Version");

                break;
            }
        }
        catch
        {
            // Оставляем данные платы пустыми.
        }

        try
        {
            using ManagementObjectSearcher biosSearcher =
                new(
                    "SELECT " +
                    "SMBIOSBIOSVersion, " +
                    "Version, " +
                    "ReleaseDate " +
                    "FROM Win32_BIOS");

            using ManagementObjectCollection biosResults =
                biosSearcher.Get();

            foreach (ManagementObject item in biosResults)
            {
                biosVersion =
                    ReadString(
                        item,
                        "SMBIOSBIOSVersion");

                if (string.IsNullOrWhiteSpace(biosVersion))
                {
                    biosVersion =
                        ReadString(
                            item,
                            "Version");
                }

                biosDate =
                    ReadWmiDate(
                        item,
                        "ReleaseDate");

                break;
            }
        }
        catch
        {
            // Оставляем данные BIOS пустыми.
        }

        return new MotherboardDetails
        {
            Manufacturer = manufacturer,
            Model = model,
            Version = version,
            FirmwareType = ReadFirmwareType(),
            BiosVersion = biosVersion,
            BiosDate = biosDate
        };
    }


    private static string? ReadFirmwareType()
    {
        try
        {
            if (!GetFirmwareType(
                    out NativeFirmwareType firmwareType))
            {
                return null;
            }

            return firmwareType switch
            {
                NativeFirmwareType.Bios => "BIOS",
                NativeFirmwareType.Uefi => "UEFI",
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }


    // ============================================================
    // ОПЕРАТИВНАЯ ПАМЯТЬ
    // ============================================================

    private static MemoryDetails ReadMemoryInfo()
    {
        ulong? totalBytes =
            ReadTotalPhysicalMemory();

        uint? physicalSlotCount =
            ReadPhysicalMemorySlotCount();

        List<MemoryModuleDetails> modules =
            new();

        try
        {
            using ManagementObjectSearcher searcher =
                new(
                    "SELECT " +
                    "BankLabel, " +
                    "DeviceLocator, " +
                    "Capacity, " +
                    "Manufacturer, " +
                    "PartNumber, " +
                    "SerialNumber, " +
                    "Speed, " +
                    "ConfiguredClockSpeed, " +
                    "SMBIOSMemoryType, " +
                    "ConfiguredVoltage, " +
                    "FormFactor, " +
                    "DataWidth, " +
                    "TotalWidth " +
                    "FROM Win32_PhysicalMemory");

            using ManagementObjectCollection results =
                searcher.Get();

            foreach (ManagementObject item in results)
            {
                string? slot =
                    ReadString(
                        item,
                        "DeviceLocator");

                if (string.IsNullOrWhiteSpace(slot))
                {
                    slot =
                        ReadString(
                            item,
                            "BankLabel");
                }

                uint? configuredSpeed =
                    ReadPositiveUInt32(
                        item,
                        "ConfiguredClockSpeed");

                uint? reportedSpeed =
                    ReadPositiveUInt32(
                        item,
                        "Speed");

                modules.Add(
                    new MemoryModuleDetails
                    {
                        Slot = slot,

                        CapacityBytes =
                            ReadUInt64(
                                item,
                                "Capacity"),

                        Manufacturer =
                            ReadString(
                                item,
                                "Manufacturer"),

                        PartNumber =
                            ReadString(
                                item,
                                "PartNumber"),

                        SerialNumber =
                            ReadString(
                                item,
                                "SerialNumber"),

                        FrequencyMhz =
                            configuredSpeed ??
                            reportedSpeed,

                        ReportedSpeedMhz =
                            reportedSpeed,

                        ConfiguredSpeedMhz =
                            configuredSpeed,

                        SmbiosMemoryType =
                            ReadUInt16(
                                item,
                                "SMBIOSMemoryType"),

                        ConfiguredVoltageMv =
                            ReadPositiveUInt32(
                                item,
                                "ConfiguredVoltage"),

                        FormFactor =
                            ReadUInt16(
                                item,
                                "FormFactor"),

                        DataWidthBits =
                            ReadUInt16(
                                item,
                                "DataWidth"),

                        TotalWidthBits =
                            ReadUInt16(
                                item,
                                "TotalWidth")
                    });
            }
        }
        catch
        {
            // Если WMI не отдаст модули,
            // список останется пустым.
        }

        if (!totalBytes.HasValue &&
            modules.Count > 0)
        {
            ulong sum = 0;

            foreach (MemoryModuleDetails module in modules)
            {
                if (module.CapacityBytes.HasValue)
                {
                    sum +=
                        module.CapacityBytes.Value;
                }
            }

            if (sum > 0)
                totalBytes = sum;
        }

        return new MemoryDetails
        {
            TotalBytes = totalBytes,
            PhysicalSlotCount = physicalSlotCount,
            Modules = modules
        };
    }


    private static uint? ReadPhysicalMemorySlotCount()
    {
        try
        {
            using ManagementObjectSearcher searcher =
                new(
                    "SELECT " +
                    "MemoryDevices " +
                    "FROM Win32_PhysicalMemoryArray");

            using ManagementObjectCollection results =
                searcher.Get();

            uint totalSlots = 0;
            bool found = false;

            foreach (ManagementObject item in results)
            {
                uint? slots =
                    ReadPositiveUInt32(
                        item,
                        "MemoryDevices");

                if (!slots.HasValue)
                    continue;

                if (uint.MaxValue - totalSlots <
                    slots.Value)
                {
                    return null;
                }

                totalSlots +=
                    slots.Value;

                found = true;
            }

            return found &&
                   totalSlots > 0
                ? totalSlots
                : null;
        }
        catch
        {
            return null;
        }
    }


    private static ulong? ReadTotalPhysicalMemory()
    {
        try
        {
            using ManagementObjectSearcher searcher =
                new(
                    "SELECT " +
                    "TotalPhysicalMemory " +
                    "FROM Win32_ComputerSystem");

            using ManagementObjectCollection results =
                searcher.Get();

            foreach (ManagementObject item in results)
            {
                return ReadUInt64(
                    item,
                    "TotalPhysicalMemory");
            }
        }
        catch
        {
        }

        return null;
    }


    // ============================================================
    // ВИДЕОКАРТЫ
    // ============================================================

    private static IReadOnlyList<GraphicsAdapterDetails>
        ReadGraphicsAdapters()
    {
        List<GraphicsAdapterDetails> adapters =
            new();

        try
        {
            using ManagementObjectSearcher searcher =
                new(
                    "SELECT " +
                    "Name, " +
                    "AdapterCompatibility, " +
                    "DriverVersion, " +
                    "DriverDate, " +
                    "PNPDeviceID, " +
                    "VideoProcessor " +
                    "FROM Win32_VideoController");

            using ManagementObjectCollection results =
                searcher.Get();

            foreach (ManagementObject item in results)
            {
                string? name =
                    ReadString(
                        item,
                        "Name");

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                adapters.Add(
                    new GraphicsAdapterDetails
                    {
                        Name = name,

                        Manufacturer =
                            ReadString(
                                item,
                                "AdapterCompatibility"),

                        DriverVersion =
                            ReadString(
                                item,
                                "DriverVersion"),

                        DriverDate =
                            ReadWmiDate(
                                item,
                                "DriverDate"),

                        PnpDeviceId =
                            ReadString(
                                item,
                                "PNPDeviceID"),

                        VideoProcessor =
                            ReadString(
                                item,
                                "VideoProcessor"),

                        // Win32_VideoController.AdapterRAM ненадёжен
                        // для современных видеокарт с большим объёмом VRAM.
                        // Поэтому здесь намеренно не подставляем потенциально
                        // неверное значение. Надёжный источник добавим отдельно.
                        DedicatedMemoryBytes = null
                    });
            }
        }
        catch
        {
            // Если WMI не отдаст видеокарты,
            // список останется пустым.
        }

        return adapters;
    }


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
        catch
        {
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
            IReadOnlyList<StorageDeviceDetails>? knownDevices = null)
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
        catch (ManagementException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (System.Runtime.InteropServices.COMException)
        {
        }

        // A separate, optional read of standard ATA SMART attributes.
        // Failure of the provider never invalidates Windows Storage counters.
        if (knownDevices != null)
        {
            ReadAtaSmartForConfirmedDrives(result, knownDevices);
            ReadAtaSmartThresholds(result, knownDevices);
            ReadAtaSmartFailureStatus(result, knownDevices);
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
        IReadOnlyList<StorageDeviceDetails> knownDevices)
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
                }
            }
        }
        catch (ManagementException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (System.Runtime.InteropServices.COMException)
        {
        }
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
        IReadOnlyList<StorageDeviceDetails> knownDevices)
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
        catch (ManagementException) { }
        catch (UnauthorizedAccessException) { }
        catch (System.Runtime.InteropServices.COMException) { }
    }

    // Firmware failure prediction is more meaningful than a guessed
    // health percentage. A false value means NO predicted failure, not
    // a guarantee that the drive is healthy.
    private static void ReadAtaSmartFailureStatus(
        Dictionary<uint, StorageReliabilityDetails> reliability,
        IReadOnlyList<StorageDeviceDetails> knownDevices)
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
        catch (ManagementException) { }
        catch (UnauthorizedAccessException) { }
        catch (System.Runtime.InteropServices.COMException) { }
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


    private static string? NormalizeOsArchitecture(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        string text =
            value.Trim();

        if (text.Contains(
                "64",
                StringComparison.OrdinalIgnoreCase))
        {
            return SettingsService.L(
                "64-разрядная",
                "64-bit");
        }

        if (text.Contains(
                "32",
                StringComparison.OrdinalIgnoreCase) ||
            text.Contains(
                "x86",
                StringComparison.OrdinalIgnoreCase))
        {
            return SettingsService.L(
                "32-разрядная",
                "32-bit");
        }

        return text;
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


    // ============================================================
    // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ WMI
    // ============================================================

    private static string? ReadString(
        ManagementBaseObject item,
        string propertyName)
    {
        try
        {
            object? value =
                item[propertyName];

            if (value == null)
                return null;

            string? text =
                Convert.ToString(
                    value,
                    CultureInfo.InvariantCulture);

            return CleanText(text);
        }
        catch
        {
            return null;
        }
    }


    private static ushort? ReadUInt16(
        ManagementBaseObject item,
        string propertyName)
    {
        try
        {
            object? value =
                item[propertyName];

            if (value == null)
                return null;

            return Convert.ToUInt16(
                value,
                CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }


    private static uint? ReadUInt32(
        ManagementBaseObject item,
        string propertyName)
    {
        try
        {
            object? value =
                item[propertyName];

            if (value == null)
                return null;

            return Convert.ToUInt32(
                value,
                CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }


    private static uint? ReadPositiveUInt32(
        ManagementBaseObject item,
        string propertyName)
    {
        uint? value =
            ReadUInt32(
                item,
                propertyName);

        if (!value.HasValue ||
            value.Value == 0)
        {
            return null;
        }

        return value;
    }


    private static ulong? ReadUInt64(
        ManagementBaseObject item,
        string propertyName)
    {
        try
        {
            object? value =
                item[propertyName];

            if (value == null)
                return null;

            return Convert.ToUInt64(
                value,
                CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }


    private static DateTime? ReadWmiDate(
        ManagementBaseObject item,
        string propertyName)
    {
        string? value =
            ReadString(
                item,
                propertyName);

        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            return ManagementDateTimeConverter
                .ToDateTime(value);
        }
        catch
        {
            return null;
        }
    }


    private static string? CleanText(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string result =
            value.Trim();

        if (result.Equals(
                "To Be Filled By O.E.M.",
                StringComparison.OrdinalIgnoreCase) ||
            result.Equals(
                "Default string",
                StringComparison.OrdinalIgnoreCase) ||
            result.Equals(
                "Unknown",
                StringComparison.OrdinalIgnoreCase) ||
            result.Equals(
                "Not Specified",
                StringComparison.OrdinalIgnoreCase) ||
            result.Equals(
                "None",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return result;
    }


    // ============================================================
    // WINDOWS API
    // ============================================================

    private enum NativeFirmwareType : uint
    {
        Unknown = 0,
        Bios = 1,
        Uefi = 2,
        Maximum = 3
    }


    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFirmwareType(
        out NativeFirmwareType firmwareType);
}


// ================================================================
// ОБЩИЙ СНИМОК СИСТЕМЫ
// ================================================================

public sealed class SystemInformationSnapshot
{
    public OperatingSystemDetails Windows { get; init; } =
        new();

    public ProcessorDetails Processor { get; init; } =
        new();

    public MotherboardDetails Motherboard { get; init; } =
        new();

    public MemoryDetails Memory { get; init; } =
        new();

    public IReadOnlyList<GraphicsAdapterDetails>
        GraphicsAdapters { get; init; } =
            Array.Empty<GraphicsAdapterDetails>();

    public IReadOnlyList<StorageDeviceDetails>
        StorageDevices { get; init; } =
            Array.Empty<StorageDeviceDetails>();
}


// ================================================================
// WINDOWS
// ================================================================

public sealed class OperatingSystemDetails
{
    public string? Name { get; init; }

    public string? Edition { get; init; }

    public string? DisplayVersion { get; init; }

    public string? Version { get; init; }

    public string? BuildNumber { get; init; }

    public string? Architecture { get; init; }

    public string? ComputerName { get; init; }

    public DateTime? InstallDate { get; init; }
}


// ================================================================
// ПРОЦЕССОР
// ================================================================

public sealed class ProcessorDetails
{
    public string? Manufacturer { get; init; }

    public string? Name { get; init; }

    public string? Architecture { get; init; }

    public uint? PhysicalCores { get; init; }

    public uint? LogicalProcessors { get; init; }

    public uint? CurrentClockSpeedMhz { get; init; }

    public uint? MaxClockSpeedMhz { get; init; }

    public string? Socket { get; init; }

    public uint? L2CacheKb { get; init; }

    public uint? L3CacheKb { get; init; }
}


// ================================================================
// МАТЕРИНСКАЯ ПЛАТА
// ================================================================

public sealed class MotherboardDetails
{
    public string? Manufacturer { get; init; }

    public string? Model { get; init; }

    public string? Version { get; init; }

    public string? FirmwareType { get; init; }

    public string? BiosVersion { get; init; }

    public DateTime? BiosDate { get; init; }
}


// ================================================================
// ПАМЯТЬ
// ================================================================

public sealed class MemoryDetails
{
    public ulong? TotalBytes { get; init; }

    public uint? PhysicalSlotCount { get; init; }

    public IReadOnlyList<MemoryModuleDetails> Modules { get; init; } =
        Array.Empty<MemoryModuleDetails>();
}


public sealed class MemoryModuleDetails
{
    public string? Slot { get; init; }

    public ulong? CapacityBytes { get; init; }

    public string? Manufacturer { get; init; }

    public string? PartNumber { get; init; }

    public string? SerialNumber { get; init; }

    public uint? FrequencyMhz { get; init; }

    public uint? ReportedSpeedMhz { get; init; }

    public uint? ConfiguredSpeedMhz { get; init; }

    public ushort? SmbiosMemoryType { get; init; }

    public uint? ConfiguredVoltageMv { get; init; }

    public ushort? FormFactor { get; init; }

    public ushort? DataWidthBits { get; init; }

    public ushort? TotalWidthBits { get; init; }
}


// ================================================================
// ВИДЕОКАРТЫ
// ================================================================

public sealed class GraphicsAdapterDetails
{
    public string? Name { get; init; }

    public string? Manufacturer { get; init; }

    public string? DriverVersion { get; init; }

    public DateTime? DriverDate { get; init; }

    public string? PnpDeviceId { get; init; }

    public string? VideoProcessor { get; init; }

    public ulong? DedicatedMemoryBytes { get; init; }
}


// ================================================================
// НАКОПИТЕЛИ
// ================================================================

public sealed class StorageDeviceDetails
{
    public string? DeviceId { get; init; }

    public uint? PhysicalDiskIndex { get; init; }

    // Used to match an ATA SMART instance to a single physical HDD or
    // a supported SATA SSD, never to identify one by array position.
    public string? PnpDeviceId { get; init; }

    public string? Model { get; init; }

    public string? Manufacturer { get; init; }

    public string? SerialNumber { get; init; }

    public string? Firmware { get; init; }

    public string? InterfaceType { get; init; }

    public string? MediaType { get; init; }

    public ulong? CapacityBytes { get; init; }
}


// Windows Storage Management reliability counters. Values can be unavailable
// even when the physical disk is detected; missing is NOT the same as zero.
public sealed class StorageReliabilityDetails
{
    // Standard NVMe SMART/Health log: available only when the controller
    // supports the read-only query; null means not read or not available.
    public StorageNvmeHealthDetails? NvmeHealth { get; init; }

    // Windows Storage bus type (e.g. 11 = SATA, 17 = NVMe).
    public ushort? BusTypeCode { get; init; }

    // MSFT_PhysicalDisk.MediaType: 3 = HDD, 4 = SSD, 5 = SCM.
    // This stays null if physical device identity could not be confirmed.
    public ushort? MediaTypeCode { get; init; }

    // MSFT_StorageReliabilityCounter.Wear is USED endurance, not health.
    // In this implementation it is kept only for NVMe fallback, because
    // generic SATA wear can be misleading (e.g. Kingston A400 shows 0%).
    public byte? WearPercent { get; init; }

    public byte? TemperatureCelsius { get; init; }

    public ushort? PowerOnHours { get; init; }

    public ulong? ReadErrorsUncorrected { get; init; }

    public ulong? WriteErrorsUncorrected { get; init; }

    // ATA SMART firmware prediction; null if the driver did not report it.
    public bool? AtaSmartPredictedFailure { get; set; }

    // Optional ATA SMART counters, identified by an exact PNP device match.
    public StorageAtaSmartDetails? AtaSmart { get; set; }

    public bool HasWindowsCounters =>
        WearPercent.HasValue ||
        TemperatureCelsius.HasValue ||
        PowerOnHours.HasValue ||
        ReadErrorsUncorrected.HasValue ||
        WriteErrorsUncorrected.HasValue;

    public bool HasAnyData =>
        HasWindowsCounters || NvmeHealth != null ||
        AtaSmartPredictedFailure.HasValue || AtaSmart?.HasAnyData == true;
}


// Standard ATA SMART raw attributes. The vendor's interpretation can vary:
// HDD 05/C5/C6 are kept separate from Kingston A400-specific SSD E7.
public sealed class StorageAtaSmartDetails
{
    // Attribute 05 (0x05).
    public ulong? ReallocatedSectors { get; init; }

    // Attribute 197 (0xC5).
    public ulong? PendingSectors { get; init; }

    // Attribute 198 (0xC6).
    public ulong? UncorrectableSectors { get; init; }

    // Kingston SA400S37 SATA SSD, attribute E7 (0xE7), raw byte[0].
    // Remaining rated NAND endurance, NOT an overall health percentage.
    public byte? KingstonA400LifeRemainingPercent { get; init; }

    // Full ATA attribute set (including unknown IDs) for technical review.
    public IReadOnlyList<StorageAtaSmartAttribute> Attributes { get; set; } =
        Array.Empty<StorageAtaSmartAttribute>();

    // Matched drive family, if a maker-specific RAW rule was verified.
    public string? RecognizedFamily { get; init; }

    public bool HasAnyData =>
        Attributes.Count > 0 ||
        ReallocatedSectors.HasValue ||
        PendingSectors.HasValue ||
        UncorrectableSectors.HasValue ||
        KingstonA400LifeRemainingPercent.HasValue;
}
