using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PCHardwareMonitor;

public sealed class HardwareChangeService
{
    private const int CurrentSchemaVersion = 1;

    private static readonly string DataDirectory =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "Thermiqra");

    private static readonly string BaselinePath =
        Path.Combine(
            DataDirectory,
            "hardware-baseline.json");

    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            WriteIndented = true
        };


    public HardwareChangeResult CheckAndUpdate()
    {
        SystemInformationSnapshot snapshot =
            new SystemInfoService()
                .GetSnapshot();

        HardwareBaseline current =
            BuildBaseline(
                snapshot);

        HardwareBaseline? previous =
            LoadBaseline();

        if (previous == null)
        {
            SaveBaseline(
                current);

            return new HardwareChangeResult
            {
                BaselineCreated = true
            };
        }

        HardwareBaseline mergedCurrent =
            MergeMissingCurrentData(
                previous,
                current);

        List<string> changes =
            Compare(
                previous,
                mergedCurrent);

        SaveBaseline(
            mergedCurrent);

        HardwareChangeResult result =
            new();

        result.Changes.AddRange(
            changes);

        return result;
    }


    private static HardwareBaseline BuildBaseline(
        SystemInformationSnapshot snapshot)
    {
        HardwareBaseline baseline =
            new()
            {
                SchemaVersion =
                    CurrentSchemaVersion,

                CapturedAtUtc =
                    DateTimeOffset.UtcNow,

                ProcessorName =
                    Normalize(
                        snapshot.Processor.Name),

                TotalMemoryBytes =
                    snapshot.Memory.TotalBytes
            };

        foreach (MemoryModuleDetails module in
                 snapshot.Memory.Modules)
        {
            baseline.MemoryModules.Add(
                new HardwareBaselineItem
                {
                    Id =
                        BuildMemoryModuleId(
                            module),

                    Name =
                        BuildMemoryModuleName(
                            module),

                    CapacityBytes =
                        module.CapacityBytes
                });
        }

        foreach (GraphicsAdapterDetails adapter in
                 snapshot.GraphicsAdapters)
        {
            baseline.GraphicsAdapters.Add(
                new HardwareBaselineItem
                {
                    Id =
                        BuildGraphicsAdapterId(
                            adapter),

                    Name =
                        Normalize(
                            adapter.Name) ??
                        "GPU"
                });
        }

        foreach (StorageDeviceDetails storage in
                 snapshot.StorageDevices)
        {
            if (string.Equals(
                    Normalize(
                        storage.InterfaceType),
                    "USB",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            baseline.StorageDevices.Add(
                new HardwareBaselineItem
                {
                    Id =
                        BuildStorageDeviceId(
                            storage),

                    Name =
                        Normalize(
                            storage.Model) ??
                        Normalize(
                            storage.DeviceId) ??
                        SettingsService.L(
                            "Накопитель",
                            "Storage"),

                    CapacityBytes =
                        storage.CapacityBytes
                });
        }

        baseline.MemoryModules =
            NormalizeItems(
                baseline.MemoryModules);

        baseline.GraphicsAdapters =
            NormalizeItems(
                baseline.GraphicsAdapters);

        baseline.StorageDevices =
            NormalizeItems(
                baseline.StorageDevices);

        return baseline;
    }


    private static HardwareBaseline MergeMissingCurrentData(
        HardwareBaseline previous,
        HardwareBaseline current)
    {
        if (string.IsNullOrWhiteSpace(
                current.ProcessorName))
        {
            current.ProcessorName =
                previous.ProcessorName;
        }

        if (!current.TotalMemoryBytes.HasValue ||
            current.TotalMemoryBytes.Value == 0)
        {
            current.TotalMemoryBytes =
                previous.TotalMemoryBytes;
        }

        if (current.MemoryModules.Count == 0)
        {
            current.MemoryModules =
                previous.MemoryModules;
        }

        if (current.GraphicsAdapters.Count == 0)
        {
            current.GraphicsAdapters =
                previous.GraphicsAdapters;
        }

        if (current.StorageDevices.Count == 0)
        {
            current.StorageDevices =
                previous.StorageDevices;
        }

        return current;
    }


    private static List<string> Compare(
        HardwareBaseline previous,
        HardwareBaseline current)
    {
        List<string> changes =
            new();

        if (!string.IsNullOrWhiteSpace(
                previous.ProcessorName) &&
            !string.IsNullOrWhiteSpace(
                current.ProcessorName) &&
            !string.Equals(
                previous.ProcessorName,
                current.ProcessorName,
                StringComparison.OrdinalIgnoreCase))
        {
            changes.Add(
                SettingsService.L(
                    $"Процессор изменён: {previous.ProcessorName} → {current.ProcessorName}",
                    $"Processor changed: {previous.ProcessorName} → {current.ProcessorName}"));
        }

        bool memoryTotalChanged =
            previous.TotalMemoryBytes.HasValue &&
            current.TotalMemoryBytes.HasValue &&
            previous.TotalMemoryBytes.Value !=
            current.TotalMemoryBytes.Value;

        bool memoryModulesChanged =
            !ItemsEqual(
                previous.MemoryModules,
                current.MemoryModules);

        if (memoryTotalChanged)
        {
            changes.Add(
                SettingsService.L(
                    $"Объём RAM изменён: {FormatBytes(previous.TotalMemoryBytes)} → {FormatBytes(current.TotalMemoryBytes)}",
                    $"RAM capacity changed: {FormatBytes(previous.TotalMemoryBytes)} → {FormatBytes(current.TotalMemoryBytes)}"));
        }
        else if (previous.MemoryModules.Count > 0 &&
                 current.MemoryModules.Count > 0 &&
                 memoryModulesChanged)
        {
            changes.Add(
                SettingsService.L(
                    "Изменилась конфигурация модулей оперативной памяти.",
                    "The installed memory module configuration has changed."));
        }

        AppendCollectionChanges(
            changes,
            previous.GraphicsAdapters,
            current.GraphicsAdapters,
            SettingsService.L(
                "Добавлена видеокарта",
                "Graphics adapter added"),
            SettingsService.L(
                "Удалена видеокарта",
                "Graphics adapter removed"));

        AppendCollectionChanges(
            changes,
            previous.StorageDevices,
            current.StorageDevices,
            SettingsService.L(
                "Добавлен накопитель",
                "Storage device added"),
            SettingsService.L(
                "Удалён накопитель",
                "Storage device removed"));

        return changes;
    }


    private static void AppendCollectionChanges(
        List<string> changes,
        IReadOnlyList<HardwareBaselineItem> previous,
        IReadOnlyList<HardwareBaselineItem> current,
        string addedText,
        string removedText)
    {
        Dictionary<string, HardwareBaselineItem> previousMap =
            previous
                .GroupBy(
                    item =>
                        item.Id,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.First(),
                    StringComparer.OrdinalIgnoreCase);

        Dictionary<string, HardwareBaselineItem> currentMap =
            current
                .GroupBy(
                    item =>
                        item.Id,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.First(),
                    StringComparer.OrdinalIgnoreCase);

        foreach (HardwareBaselineItem item in
                 currentMap.Values
                     .Where(
                         item =>
                             !previousMap.ContainsKey(
                                 item.Id))
                     .OrderBy(
                         item =>
                             item.Name,
                         StringComparer.OrdinalIgnoreCase))
        {
            changes.Add(
                $"{addedText}: " +
                $"{BuildDeviceDisplayName(item)}");
        }

        foreach (HardwareBaselineItem item in
                 previousMap.Values
                     .Where(
                         item =>
                             !currentMap.ContainsKey(
                                 item.Id))
                     .OrderBy(
                         item =>
                             item.Name,
                         StringComparer.OrdinalIgnoreCase))
        {
            changes.Add(
                $"{removedText}: " +
                $"{BuildDeviceDisplayName(item)}");
        }
    }


    private static bool ItemsEqual(
        IReadOnlyList<HardwareBaselineItem> left,
        IReadOnlyList<HardwareBaselineItem> right)
    {
        if (left.Count !=
            right.Count)
        {
            return false;
        }

        string[] leftIds =
            left
                .Select(
                    item =>
                        $"{item.Id}|{item.CapacityBytes}")
                .OrderBy(
                    value =>
                        value,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        string[] rightIds =
            right
                .Select(
                    item =>
                        $"{item.Id}|{item.CapacityBytes}")
                .OrderBy(
                    value =>
                        value,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        return leftIds.SequenceEqual(
            rightIds,
            StringComparer.OrdinalIgnoreCase);
    }


    private static List<HardwareBaselineItem> NormalizeItems(
        IEnumerable<HardwareBaselineItem> items)
    {
        return items
            .Where(
                item =>
                    !string.IsNullOrWhiteSpace(
                        item.Id))
            .GroupBy(
                item =>
                    item.Id,
                StringComparer.OrdinalIgnoreCase)
            .Select(
                group =>
                    group.First())
            .OrderBy(
                item =>
                    item.Id,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }


    private static string BuildMemoryModuleId(
        MemoryModuleDetails module)
    {
        string slot =
            Normalize(
                module.Slot) ??
            "?";

        string part =
            Normalize(
                module.PartNumber) ??
            "?";

        string serial =
            Normalize(
                module.SerialNumber) ??
            "?";

        string capacity =
            module.CapacityBytes?
                .ToString() ??
            "?";

        return
            $"{slot}|{part}|{serial}|{capacity}";
    }


    private static string BuildMemoryModuleName(
        MemoryModuleDetails module)
    {
        string part =
            Normalize(
                module.PartNumber) ??
            SettingsService.L(
                "Модуль памяти",
                "Memory module");

        string slot =
            Normalize(
                module.Slot) ??
            SettingsService.L(
                "слот неизвестен",
                "unknown slot");

        return
            $"{part} ({slot})";
    }


    private static string BuildGraphicsAdapterId(
        GraphicsAdapterDetails adapter)
    {
        return
            Normalize(
                adapter.PnpDeviceId) ??
            Normalize(
                adapter.Name) ??
            Normalize(
                adapter.VideoProcessor) ??
            Guid.NewGuid()
                .ToString("N");
    }


    private static string BuildStorageDeviceId(
        StorageDeviceDetails storage)
    {
        string? serial =
            Normalize(
                storage.SerialNumber);

        if (!string.IsNullOrWhiteSpace(
                serial))
        {
            return
                $"SERIAL:{serial}";
        }

        string model =
            Normalize(
                storage.Model) ??
            "?";

        string capacity =
            storage.CapacityBytes?
                .ToString() ??
            "?";

        string interfaceType =
            Normalize(
                storage.InterfaceType) ??
            "?";

        return
            $"MODEL:{model}|SIZE:{capacity}|IF:{interfaceType}";
    }


    private static string BuildDeviceDisplayName(
        HardwareBaselineItem item)
    {
        if (item.CapacityBytes.HasValue &&
            item.CapacityBytes.Value > 0)
        {
            return
                $"{item.Name} " +
                $"({FormatBytes(item.CapacityBytes)})";
        }

        return item.Name;
    }


    private static string FormatBytes(
        ulong? bytes)
    {
        if (!bytes.HasValue ||
            bytes.Value == 0)
        {
            return "—";
        }

        double gigabytes =
            bytes.Value /
            1024d /
            1024d /
            1024d;

        return SettingsService.L(
            $"{gigabytes:F1} ГБ",
            $"{gigabytes:F1} GB");
    }


    private static string? Normalize(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        return value.Trim();
    }


    private static HardwareBaseline? LoadBaseline()
    {
        if (!File.Exists(
                BaselinePath))
        {
            return null;
        }

        try
        {
            string json =
                File.ReadAllText(
                    BaselinePath);

            HardwareBaseline? baseline =
                JsonSerializer.Deserialize<HardwareBaseline>(
                    json,
                    JsonOptions);

            if (baseline == null ||
                baseline.SchemaVersion !=
                CurrentSchemaVersion)
            {
                return null;
            }

            baseline.MemoryModules ??=
                new List<HardwareBaselineItem>();

            baseline.GraphicsAdapters ??=
                new List<HardwareBaselineItem>();

            baseline.StorageDevices ??=
                new List<HardwareBaselineItem>();

            return baseline;
        }
        catch
        {
            return null;
        }
    }


    private static void SaveBaseline(
        HardwareBaseline baseline)
    {
        Directory.CreateDirectory(
            DataDirectory);

        string tempPath =
            BaselinePath +
            ".tmp";

        string json =
            JsonSerializer.Serialize(
                baseline,
                JsonOptions);

        File.WriteAllText(
            tempPath,
            json);

        File.Move(
            tempPath,
            BaselinePath,
            overwrite:
            true);
    }


    private sealed class HardwareBaseline
    {
        public int SchemaVersion { get; set; }

        public DateTimeOffset CapturedAtUtc { get; set; }

        public string? ProcessorName { get; set; }

        public ulong? TotalMemoryBytes { get; set; }

        public List<HardwareBaselineItem> MemoryModules { get; set; } =
            new();

        public List<HardwareBaselineItem> GraphicsAdapters { get; set; } =
            new();

        public List<HardwareBaselineItem> StorageDevices { get; set; } =
            new();
    }


    private sealed class HardwareBaselineItem
    {
        public string Id { get; set; } = "";

        public string Name { get; set; } = "";

        public ulong? CapacityBytes { get; set; }
    }
}


public sealed class HardwareChangeResult
{
    public bool BaselineCreated { get; set; }

    public List<string> Changes { get; } =
        new();

    public bool HasChanges =>
        Changes.Count > 0;
}
