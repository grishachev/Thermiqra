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
                    "Model, " +
                    "Manufacturer, " +
                    "SerialNumber, " +
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

    public string? Model { get; init; }

    public string? Manufacturer { get; init; }

    public string? SerialNumber { get; init; }

    public string? Firmware { get; init; }

    public string? InterfaceType { get; init; }

    public string? MediaType { get; init; }

    public ulong? CapacityBytes { get; init; }
}
