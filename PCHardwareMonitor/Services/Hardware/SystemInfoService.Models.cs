using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PCHardwareMonitor;



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
    public string AtaSmartSource { get; set; } = "";
    public List<string> DiagnosticMessages { get; } = new();
    // Standard NVMe SMART/Health log: available only when the controller
    // supports the read-only query; null means not read or not available.
    public StorageNvmeHealthDetails? NvmeHealth { get; init; }

    // Windows Storage bus type (e.g. 11 = SATA, 17 = NVMe).
    public ushort? BusTypeCode { get; internal set; }

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
