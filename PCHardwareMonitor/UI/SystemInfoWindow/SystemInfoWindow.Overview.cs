using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Controls;

namespace PCHardwareMonitor;

public partial class SystemInfoWindow
{
    // ============================================================
    // ОБЗОР
    // ============================================================

    private static string BuildBoardName(
        MotherboardDetails board)
    {
        List<string> parts =
            new();

        if (!string.IsNullOrWhiteSpace(
                board.Manufacturer))
        {
            parts.Add(
                board.Manufacturer);
        }

        if (!string.IsNullOrWhiteSpace(
                board.Model))
        {
            parts.Add(
                board.Model);
        }

        return parts.Count > 0
            ? string.Join(
                " ",
                parts)
            : SettingsService.L(
                "Нет данных",
                "No data");
    }


    private static string BuildWindowsSummary(
        OperatingSystemDetails windows)
    {
        List<string> parts =
            new();

        if (!string.IsNullOrWhiteSpace(
                windows.Name))
        {
            parts.Add(
                windows.Name);
        }

        if (!string.IsNullOrWhiteSpace(
                windows.Edition))
        {
            parts.Add(
                windows.Edition);
        }

        if (!string.IsNullOrWhiteSpace(
                windows.DisplayVersion))
        {
            parts.Add(
                windows.DisplayVersion);
        }

        return parts.Count > 0
            ? string.Join(
                " ",
                parts)
            : SettingsService.L(
                "Нет данных",
                "No data");
    }


    private static string BuildGraphicsSummary(
        IReadOnlyList<GraphicsAdapterDetails> adapters)
    {
        if (adapters.Count == 0)
            return SettingsService.L(
                "Нет данных",
                "No data");

        List<string> names =
            new();

        foreach (GraphicsAdapterDetails adapter in adapters)
        {
            if (!string.IsNullOrWhiteSpace(
                    adapter.Name))
            {
                names.Add(
                    adapter.Name);
            }
        }

        return names.Count > 0
            ? string.Join(
                Environment.NewLine,
                names)
            : SettingsService.L(
                "Нет данных",
                "No data");
    }


    private static string BuildStorageSummary(
        IReadOnlyList<StorageDeviceDetails> devices)
    {
        if (devices.Count == 0)
            return SettingsService.L(
                "Нет данных",
                "No data");

        List<string> lines =
            new();

        foreach (StorageDeviceDetails device in devices)
        {
            string name =
                ValueOrUnavailable(
                    device.Model);

            string size =
                FormatBytes(
                    device.CapacityBytes);

            lines.Add(
                $"{name} — {size}");
        }

        return string.Join(
            Environment.NewLine,
            lines);
    }


    private void PopulateOverview(
        SystemInformationSnapshot snapshot)
    {
        OverviewCpuText.Text =
            ValueOrUnavailable(
                snapshot.Processor.Name);

        OverviewBoardText.Text =
            BuildBoardName(
                snapshot.Motherboard);

        OverviewMemoryText.Text =
            FormatBytes(
                snapshot.Memory.TotalBytes);

        OverviewGpuText.Text =
            BuildGraphicsSummary(
                snapshot.GraphicsAdapters);

        OverviewWindowsText.Text =
            BuildWindowsSummary(
                snapshot.Windows);

        OverviewStorageText.Text =
            BuildStorageSummary(
                snapshot.StorageDevices);
    }


    // ============================================================
    // ПРОЦЕССОР
    // ============================================================

    private void PopulateProcessor(
        ProcessorDetails processor)
    {
        CpuManufacturerText.Text =
            ValueOrUnavailable(
                processor.Manufacturer);

        CpuNameText.Text =
            ValueOrUnavailable(
                processor.Name);

        CpuArchitectureText.Text =
            ValueOrUnavailable(
                processor.Architecture);

        CpuCoresText.Text =
            FormatNumber(
                processor.PhysicalCores);

        CpuThreadsText.Text =
            FormatNumber(
                processor.LogicalProcessors);

        CpuCurrentClockText.Text =
            FormatFrequency(
                processor.CurrentClockSpeedMhz);

        CpuMaxClockText.Text =
            FormatFrequency(
                processor.MaxClockSpeedMhz);

        CpuSocketText.Text =
            ValueOrUnavailable(
                processor.Socket);

        CpuCacheText.Text =
            $"L2: {FormatCache(processor.L2CacheKb)}" +
            $"   •   " +
            $"L3: {FormatCache(processor.L3CacheKb)}";
    }


    // ============================================================
    // МАТЕРИНСКАЯ ПЛАТА
    // ============================================================

    private void PopulateMotherboard(
        MotherboardDetails board)
    {
        BoardManufacturerText.Text =
            ValueOrUnavailable(
                board.Manufacturer);

        BoardModelText.Text =
            ValueOrUnavailable(
                board.Model);

        BoardVersionText.Text =
            ValueOrUnavailable(
                board.Version);

        FirmwareTypeText.Text =
            ValueOrUnavailable(
                board.FirmwareType);

        BiosVersionText.Text =
            ValueOrUnavailable(
                board.BiosVersion);

        BiosDateText.Text =
            FormatDate(
                board.BiosDate);
    }



}
