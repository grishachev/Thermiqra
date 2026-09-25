using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Controls;

namespace PCHardwareMonitor;

public partial class SystemInfoWindow : Window
{
    private readonly SystemInfoService _systemInfoService =
        new();

    private readonly StatisticsService? _statistics;

    private SystemInformationSnapshot? _snapshot;

    public SystemInfoWindow()
        : this(null)
    {
    }


    public SystemInfoWindow(
        StatisticsService? statistics)
    {
        InitializeComponent();

        _statistics =
            statistics;

        SettingsService.ApplyLanguageToWindow(
            this);

        SaveDiagnosticReportButton.Content =
            SettingsService.L(
                "Сохранить отчёт",
                "Save report");

        Loaded +=
            SystemInfoWindow_Loaded;
    }


    private void SystemInfoWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        Loaded -=
            SystemInfoWindow_Loaded;

        LoadSystemInformation();
    }


    private void LoadSystemInformation()
    {
        try
        {
            StatusText.Text =
                SettingsService.TranslateText(
                    "Чтение информации о системе...");

            SystemInformationSnapshot snapshot =
                _systemInfoService.GetSnapshot();

            _snapshot =
                snapshot;

            PopulateOverview(snapshot);

            PopulateProcessor(
                snapshot.Processor);

            PopulateMotherboard(
                snapshot.Motherboard);

            PopulateMemory(
                snapshot.Memory);

            PopulateGraphicsAdapters(
                snapshot.GraphicsAdapters);

            PopulateStorageDevices(
                snapshot.StorageDevices);

            PopulateWindows(
                snapshot.Windows);

            string computerName =
                ValueOrUnavailable(
                    snapshot.Windows.ComputerName);

            CyberHeaderComputerNameText.Text =
                computerName;

            SteamHeaderComputerNameText.Text =
                computerName;

            FrostHeaderComputerNameText.Text =
                computerName;

            MilitaryHeaderComputerNameText.Text =
                computerName;

            StatusText.Text =
                SettingsService.TranslateText(
                    $"Данные загружены: " +
                    $"{DateTime.Now:HH:mm:ss}");
        }
        catch (Exception ex)
        {
            StatusText.Text =
                SettingsService.TranslateText(
                    $"Не удалось получить данные: " +
                    $"{ex.Message}");
        }
    }


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


    // ============================================================
    // ПАМЯТЬ
    // ============================================================

    private void PopulateMemory(
        MemoryDetails memory)
    {
        MemoryTotalText.Text =
            FormatBytes(
                memory.TotalBytes);

        MemoryModuleCountText.Text =
            memory.Modules.Count.ToString();

        MemoryModulesPanel
            .Children
            .Clear();

        if (memory.Modules.Count == 0)
        {
            MemoryModulesPanel
                .Children
                .Add(
                    CreateEmptyMessage(
                        "Модули памяти не обнаружены."));
            return;
        }

        for (int i = 0;
             i < memory.Modules.Count;
             i++)
        {
            MemoryModuleDetails module =
                memory.Modules[i];

            Border card =
                CreateInformationCard(
                    $"Модуль {i + 1}",
                    new List<InformationRow>
                    {
                        new(
                            "Слот / банк",
                            ValueOrUnavailable(
                                module.Slot)),

                        new(
                            "Объём",
                            FormatBytes(
                                module.CapacityBytes)),

                        new(
                            "Производитель",
                            ValueOrUnavailable(
                                module.Manufacturer)),

                        new(
                            "Part Number",
                            ValueOrUnavailable(
                                module.PartNumber)),

                        new(
                            "Серийный номер",
                            ValueOrUnavailable(
                                module.SerialNumber)),

                        new(
                            "Тип памяти",
                            FormatMemoryType(
                                module.SmbiosMemoryType)),

                        new(
                            "Заявленная скорость",
                            FormatMemorySpeed(
                                module.ReportedSpeedMhz)),

                        new(
                            "Настроенная скорость",
                            FormatMemorySpeed(
                                module.ConfiguredSpeedMhz)),

                        new(
                            "Форм-фактор",
                            FormatMemoryFormFactor(
                                module.FormFactor)),

                        new(
                            "Настроенное напряжение (WMI)",
                            FormatMemoryVoltage(
                                module.ConfiguredVoltageMv)),

                        new(
                            "Ширина данных",
                            FormatMemoryWidth(
                                module.DataWidthBits)),

                        new(
                            "Полная ширина",
                            FormatMemoryWidth(
                                module.TotalWidthBits))
                    });

            MemoryModulesPanel
                .Children
                .Add(
                    card);
        }
    }


    private void MemoryDetailsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        MemorySummaryView.Visibility =
            Visibility.Collapsed;

        MemoryDetailsView.Visibility =
            Visibility.Visible;

        HardwareSnapshot? hardwareSnapshot =
            HardwareMonitorService.LastSnapshot;

        PopulateMemoryDetails(
            hardwareSnapshot);
    }


    private void MemoryDetailsBackButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        MemoryDetailsView.Visibility =
            Visibility.Collapsed;

        MemorySummaryView.Visibility =
            Visibility.Visible;
    }


    private void PopulateMemoryDetails(
        HardwareSnapshot? hardwareSnapshot)
    {
        MemoryDetailsPanel
            .Children
            .Clear();

        if (_snapshot != null)
        {
            MemoryDetailsPanel
                .Children
                .Add(
                    CreateInformationCard(
                        "Текущая конфигурация Windows / SMBIOS",
                        new List<InformationRow>
                        {
                            new(
                                "Установлено",
                                FormatBytes(
                                    _snapshot.Memory.TotalBytes)),

                            new(
                                "Модулей",
                                _snapshot.Memory.Modules.Count
                                    .ToString()),

                            new(
                                "Тип памяти",
                                BuildCurrentMemoryTypeText(
                                    _snapshot.Memory)),

                            new(
                                "Настроенная скорость",
                                BuildCurrentMemorySpeedText(
                                    _snapshot.Memory))
                        }));

            MemoryDetailsPanel
                .Children
                .Add(
                    CreateMemoryUpgradeCard(
                        _snapshot.Memory,
                        hardwareSnapshot));
        }

        if (hardwareSnapshot == null ||
            hardwareSnapshot.MemorySpdModules.Count == 0)
        {
            MemoryDetailsPanel
                .Children
                .Add(
                    CreateEmptyMessage(
                        "Расширенные SPD-данные ещё не готовы. " +
                        "Подожди несколько секунд и открой " +
                        "«Подробнее о памяти» повторно."));

            return;
        }

        for (int i = 0;
             i < hardwareSnapshot.MemorySpdModules.Count;
             i++)
        {
            MemorySpdModuleInfo module =
                hardwareSnapshot.MemorySpdModules[i];

            List<InformationRow> rows =
                new()
                {
                    new(
                        "Ёмкость",
                        FormatSpdCapacity(
                            module.CapacityGb))
                };

            if (module.Jedec != null)
            {
                rows.Add(
                    new InformationRow(
                        "JEDEC",
                        BuildJedecSummary(
                            module.Jedec)));

                rows.Add(
                    new InformationRow(
                        "JEDEC — поддерживаемые CL",
                        module.Jedec.SupportedCasLatencies.Count > 0
                            ? string.Join(
                                ", ",
                                module.Jedec.SupportedCasLatencies)
                            : "Нет данных"));

                rows.Add(
                    new InformationRow(
                        "JEDEC — минимумы",
                        BuildJedecMinimums(
                            module.Jedec)));
            }

            if (module.XmpProfiles.Count > 0)
            {
                foreach (MemoryXmpProfileInfo profile
                         in module.XmpProfiles)
                {
                    rows.Add(
                        new InformationRow(
                            $"{module.XmpVersion ?? "XMP"} — профиль " +
                            $"{profile.ProfileNumber}",
                            BuildXmpProfileSummary(
                                profile)));
                }
            }

            rows.Add(
                new InformationRow(
                    "SPD-тайминги",
                    BuildSpdTimingsText(
                        module.Timings)));

            MemoryDetailsPanel
                .Children
                .Add(
                    CreateInformationCard(
                        ValueOrUnavailable(
                            module.Name),
                        rows));
        }
    }


    private static string BuildCurrentMemoryTypeText(
        MemoryDetails memory)
    {
        List<string> values =
            memory.Modules
                .Select(
                    module =>
                        FormatMemoryType(
                            module.SmbiosMemoryType))
                .Where(
                    value =>
                        value !=
                        SettingsService.L(
                            "Нет данных",
                            "No data"))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        return values.Count > 0
            ? string.Join(
                ", ",
                values)
            : SettingsService.L(
                "Нет данных",
                "No data");
    }


    private static string BuildCurrentMemorySpeedText(
        MemoryDetails memory)
    {
        List<uint> speeds =
            memory.Modules
                .Where(
                    module =>
                        module.ConfiguredSpeedMhz.HasValue)
                .Select(
                    module =>
                        module.ConfiguredSpeedMhz!.Value)
                .Distinct()
                .OrderBy(
                    value =>
                        value)
                .ToList();

        return speeds.Count > 0
            ? string.Join(
                ", ",
                speeds.Select(
                    speed =>
                        $"{speed} MT/s"))
            : SettingsService.L(
                "Нет данных",
                "No data");
    }


    private static string FormatSpdCapacity(
        float? gigabytes)
    {
        return gigabytes.HasValue
            ? SettingsService.L(
                $"{gigabytes.Value:0.#} ГБ",
                $"{gigabytes.Value:0.#} GB")
            : SettingsService.L(
                "Нет данных",
                "No data");
    }


    private static string BuildJedecSummary(
        MemoryJedecInfo jedec)
    {
        string voltage =
            jedec.NominalVoltageVolts.HasValue
                ? SettingsService.L(
                    $"{jedec.NominalVoltageVolts.Value:0.00} В",
                    $"{jedec.NominalVoltageVolts.Value:0.00} V")
                : SettingsService.L(
                    "напряжение не определено",
                    "voltage not determined");

        return SettingsService.L(
            $"до {jedec.MaximumDataRateMt} MT/s" +
            $"   •   " +
            $"tCKmin {jedec.MinimumCycleTimeNs:0.###} нс" +
            $"   •   " +
            $"{voltage}",
            $"up to {jedec.MaximumDataRateMt} MT/s" +
            $"   •   " +
            $"tCKmin {jedec.MinimumCycleTimeNs:0.###} ns" +
            $"   •   " +
            $"{voltage}");
    }


    private static string BuildJedecMinimums(
        MemoryJedecInfo jedec)
    {
        return SettingsService.L(
            $"tAA {jedec.MinimumCasLatencyTimeNs:0.###} нс" +
            $"   •   " +
            $"tRCD {jedec.MinimumRasToCasDelayTimeNs:0.###} нс" +
            $"   •   " +
            $"tRP {jedec.MinimumRowPrechargeDelayTimeNs:0.###} нс" +
            $"   •   " +
            $"tRAS {jedec.MinimumActiveToPrechargeDelayTimeNs:0.###} нс",
            $"tAA {jedec.MinimumCasLatencyTimeNs:0.###} ns" +
            $"   •   " +
            $"tRCD {jedec.MinimumRasToCasDelayTimeNs:0.###} ns" +
            $"   •   " +
            $"tRP {jedec.MinimumRowPrechargeDelayTimeNs:0.###} ns" +
            $"   •   " +
            $"tRAS {jedec.MinimumActiveToPrechargeDelayTimeNs:0.###} ns");
    }


    private static string BuildXmpProfileSummary(
        MemoryXmpProfileInfo profile)
    {
        return
            $"{profile.DataRateMt} MT/s" +
            $"   •   " +
            $"CL{profile.CasLatency}-" +
            $"{profile.RasToCasDelay}-" +
            $"{profile.RowPrechargeDelay}-" +
            $"{profile.ActiveToPrechargeDelay}" +
            $"   •   " +
            SettingsService.L(
                $"{profile.VoltageVolts:0.00} В",
                $"{profile.VoltageVolts:0.00} V");
    }


    private static string BuildSpdTimingsText(
        IReadOnlyList<MemorySpdTimingInfo> timings)
    {
        if (timings.Count == 0)
            return SettingsService.L(
                "Нет данных",
                "No data");

        return string.Join(
            Environment.NewLine,
            timings.Select(
                timing =>
                    $"{timing.Name}: " +
                    SettingsService.L(
                        $"{timing.ValueNanoseconds:0.###} нс",
                        $"{timing.ValueNanoseconds:0.###} ns")));
    }


    private Border CreateMemoryUpgradeCard(
        MemoryDetails memory,
        HardwareSnapshot? hardwareSnapshot)
    {
        MemoryXmpProfileInfo? preferredXmp =
            FindCommonPreferredXmpProfile(
                hardwareSnapshot);

        string? exactPartNumber =
            FindSinglePartNumber(
                memory);

        List<InformationRow> rows =
            new()
            {
                new(
                    "Конфигурация",
                    BuildMemoryModuleConfigurationText(
                        memory)),

                new(
                    "Слоты",
                    BuildMemorySlotText(
                        memory)),

                new(
                    "Part Number",
                    BuildMemoryPartNumbersText(
                        memory)),

                new(
                    "Настроенная скорость",
                    BuildCurrentMemorySpeedText(
                        memory)),

                new(
                    "XMP-ориентир",
                    preferredXmp != null
                        ? BuildXmpProfileSummary(
                            preferredXmp)
                        : SettingsService.L(
                            "Не подтверждён",
                            "Not confirmed")),

                new(
                    "Что искать",
                    BuildMemoryUpgradeTargetText(
                        memory,
                        preferredXmp))
            };

        if (!string.IsNullOrWhiteSpace(
                exactPartNumber))
        {
            rows.Add(
                new InformationRow(
                    SettingsService.L(
                        "Наиболее близкое совпадение",
                        "Closest match"),
                    SettingsService.L(
                        $"В первую очередь ищите модуль с Part Number {exactPartNumber}.",
                        $"First look for a module with Part Number {exactPartNumber}.")));
        }

        rows.Add(
            new InformationRow(
                SettingsService.L(
                    "Важно",
                    "Important"),
                SettingsService.L(
                    "Смешивание разных комплектов памяти " +
                    "не гарантирует работу XMP на той же скорости. " +
                    "Итоговый режим зависит от материнской платы, " +
                    "процессора и BIOS.",
                    "Mixing different memory kits does not guarantee " +
                    "XMP operation at the same speed. The final operating " +
                    "mode depends on the motherboard, processor, and BIOS.")));

        return CreateInformationCard(
            SettingsService.L(
                "Для апгрейда",
                "For upgrade"),
            rows);
    }


    private static string BuildMemoryModuleConfigurationText(
        MemoryDetails memory)
    {
        if (memory.Modules.Count == 0)
            return SettingsService.L(
                "Нет данных",
                "No data");

        List<double> capacitiesGb =
            memory.Modules
                .Where(
                    module =>
                        module.CapacityBytes.HasValue &&
                        module.CapacityBytes.Value > 0)
                .Select(
                    module =>
                        module.CapacityBytes!.Value /
                        1024d /
                        1024d /
                        1024d)
                .ToList();

        if (capacitiesGb.Count ==
                memory.Modules.Count &&
            capacitiesGb.Count > 0)
        {
            double first =
                capacitiesGb[0];

            bool allEqual =
                capacitiesGb.All(
                    value =>
                        Math.Abs(
                            value - first) <
                        0.01);

            if (allEqual)
            {
                return
                    $"{memory.Modules.Count}×" +
                    $"{FormatCompactGigabytes(first)}";
            }
        }

        return string.Join(
            " + ",
            memory.Modules.Select(
                module =>
                    FormatModuleCapacity(
                        module.CapacityBytes)));
    }


    private static string BuildMemorySlotText(
        MemoryDetails memory)
    {
        int occupied =
            memory.Modules.Count;

        if (memory.PhysicalSlotCount.HasValue &&
            memory.PhysicalSlotCount.Value >=
                occupied)
        {
            uint free =
                memory.PhysicalSlotCount.Value -
                (uint)occupied;

            return SettingsService.L(
                $"{occupied} из " +
                $"{memory.PhysicalSlotCount.Value} занято" +
                $"   •   свободно {free}" +
                $"   •   по данным WMI",
                $"{occupied} of " +
                $"{memory.PhysicalSlotCount.Value} used" +
                $"   •   free {free}" +
                $"   •   reported by WMI");
        }

        return SettingsService.L(
            $"{occupied} занято" +
            $"   •   общее число слотов не подтверждено",
            $"{occupied} used" +
            $"   •   total slot count not confirmed");
    }


    private static string BuildMemoryPartNumbersText(
        MemoryDetails memory)
    {
        List<string> partNumbers =
            memory.Modules
                .Select(
                    module =>
                        module.PartNumber?.Trim())
                .Where(
                    value =>
                        !string.IsNullOrWhiteSpace(
                            value))
                .Select(
                    value =>
                        value!)
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        return partNumbers.Count > 0
            ? string.Join(
                ", ",
                partNumbers)
            : SettingsService.L(
                "Нет данных",
                "No data");
    }


    private static string? FindSinglePartNumber(
        MemoryDetails memory)
    {
        List<string> partNumbers =
            memory.Modules
                .Select(
                    module =>
                        module.PartNumber?.Trim())
                .Where(
                    value =>
                        !string.IsNullOrWhiteSpace(
                            value))
                .Select(
                    value =>
                        value!)
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        return partNumbers.Count == 1
            ? partNumbers[0]
            : null;
    }


    private static MemoryXmpProfileInfo?
        FindCommonPreferredXmpProfile(
            HardwareSnapshot? hardwareSnapshot)
    {
        if (hardwareSnapshot == null ||
            hardwareSnapshot.MemorySpdModules.Count == 0)
        {
            return null;
        }

        MemorySpdModuleInfo firstModule =
            hardwareSnapshot.MemorySpdModules[0];

        foreach (MemoryXmpProfileInfo candidate
                 in firstModule.XmpProfiles
                     .OrderByDescending(
                         profile =>
                             profile.DataRateMt))
        {
            bool presentInEveryModule =
                hardwareSnapshot.MemorySpdModules.All(
                    module =>
                        module.XmpProfiles.Any(
                            profile =>
                                XmpProfilesMatch(
                                    candidate,
                                    profile)));

            if (presentInEveryModule)
                return candidate;
        }

        return null;
    }


    private static bool XmpProfilesMatch(
        MemoryXmpProfileInfo left,
        MemoryXmpProfileInfo right)
    {
        return
            left.DataRateMt ==
                right.DataRateMt &&
            left.CasLatency ==
                right.CasLatency &&
            left.RasToCasDelay ==
                right.RasToCasDelay &&
            left.RowPrechargeDelay ==
                right.RowPrechargeDelay &&
            left.ActiveToPrechargeDelay ==
                right.ActiveToPrechargeDelay &&
            Math.Abs(
                left.VoltageVolts -
                right.VoltageVolts) <
                0.005;
    }


    private static string BuildMemoryUpgradeTargetText(
        MemoryDetails memory,
        MemoryXmpProfileInfo? preferredXmp)
    {
        List<string> parts =
            new();

        string memoryType =
            BuildCurrentMemoryTypeText(
                memory);

        if (memoryType != "Нет данных")
        {
            parts.Add(
                memoryType);
        }

        string? perModuleCapacity =
            FindCommonModuleCapacityText(
                memory);

        if (!string.IsNullOrWhiteSpace(
                perModuleCapacity))
        {
            parts.Add(
                SettingsService.L(
                    $"{perModuleCapacity} на модуль",
                    $"{perModuleCapacity} per module"));
        }

        if (preferredXmp != null)
        {
            parts.Add(
                $"{preferredXmp.DataRateMt} MT/s");

            parts.Add(
                $"CL{preferredXmp.CasLatency}-" +
                $"{preferredXmp.RasToCasDelay}-" +
                $"{preferredXmp.RowPrechargeDelay}-" +
                $"{preferredXmp.ActiveToPrechargeDelay}");

            parts.Add(
                SettingsService.L(
                    $"{preferredXmp.VoltageVolts:0.00} В",
                    $"{preferredXmp.VoltageVolts:0.00} V"));
        }
        else
        {
            string configuredSpeed =
                BuildCurrentMemorySpeedText(
                    memory);

            if (configuredSpeed != "Нет данных")
            {
                parts.Add(
                    configuredSpeed);
            }
        }

        return parts.Count > 0
            ? string.Join(
                "   •   ",
                parts)
            : SettingsService.L(
                "Недостаточно подтверждённых данных",
                "Not enough confirmed data");
    }


    private static string? FindCommonModuleCapacityText(
        MemoryDetails memory)
    {
        if (memory.Modules.Count == 0)
            return null;

        List<ulong> capacities =
            memory.Modules
                .Where(
                    module =>
                        module.CapacityBytes.HasValue &&
                        module.CapacityBytes.Value > 0)
                .Select(
                    module =>
                        module.CapacityBytes!.Value)
                .ToList();

        if (capacities.Count !=
            memory.Modules.Count)
        {
            return null;
        }

        ulong first =
            capacities[0];

        if (!capacities.All(
                value =>
                    value == first))
        {
            return null;
        }

        return FormatModuleCapacity(
            first);
    }


    private static string FormatModuleCapacity(
        ulong? bytes)
    {
        if (!bytes.HasValue ||
            bytes.Value == 0)
        {
            return SettingsService.L(
                "Нет данных",
                "No data");
        }

        double gigabytes =
            bytes.Value /
            1024d /
            1024d /
            1024d;

        return FormatCompactGigabytes(
            gigabytes);
    }


    private static string FormatCompactGigabytes(
        double gigabytes)
    {
        double rounded =
            Math.Round(
                gigabytes);

        return Math.Abs(
                   gigabytes - rounded) <
               0.01
            ? SettingsService.L(
                $"{rounded:0} ГБ",
                $"{rounded:0} GB")
            : SettingsService.L(
                $"{gigabytes:0.#} ГБ",
                $"{gigabytes:0.#} GB");
    }


    // ============================================================
    // ВИДЕОКАРТЫ
    // ============================================================

    private void PopulateGraphicsAdapters(
        IReadOnlyList<GraphicsAdapterDetails> adapters)
    {
        GraphicsAdaptersPanel
            .Children
            .Clear();

        if (adapters.Count == 0)
        {
            GraphicsAdaptersPanel
                .Children
                .Add(
                    CreateEmptyMessage(
                        "Видеоадаптеры не обнаружены."));
            return;
        }

        for (int i = 0;
             i < adapters.Count;
             i++)
        {
            GraphicsAdapterDetails adapter =
                adapters[i];

            Border card =
                CreateInformationCard(
                    $"Видеоадаптер {i + 1}",
                    new List<InformationRow>
                    {
                        new(
                            "Модель",
                            ValueOrUnavailable(
                                adapter.Name)),

                        new(
                            "Производитель",
                            ValueOrUnavailable(
                                adapter.Manufacturer)),

                        new(
                            "Графический процессор",
                            ValueOrUnavailable(
                                adapter.VideoProcessor)),

                        new(
                            "Видеопамять",
                            adapter.DedicatedMemoryBytes.HasValue
                                ? FormatBytes(
                                    adapter.DedicatedMemoryBytes)
                                : "Пока не определяется надёжно"),

                        new(
                            "Версия драйвера",
                            ValueOrUnavailable(
                                adapter.DriverVersion)),

                        new(
                            "Дата драйвера",
                            FormatDate(
                                adapter.DriverDate))
                    });

            GraphicsAdaptersPanel
                .Children
                .Add(
                    card);
        }
    }


    // ============================================================
    // НАКОПИТЕЛИ
    // ============================================================

    private void PopulateStorageDevices(
        IReadOnlyList<StorageDeviceDetails> devices)
    {
        StorageDevicesPanel
            .Children
            .Clear();

        if (devices.Count == 0)
        {
            StorageDevicesPanel
                .Children
                .Add(
                    CreateEmptyMessage(
                        "Физические накопители не обнаружены."));
            return;
        }

        for (int i = 0;
             i < devices.Count;
             i++)
        {
            StorageDeviceDetails device =
                devices[i];

            Border card =
                CreateInformationCard(
                    $"Накопитель {i + 1}",
                    new List<InformationRow>
                    {
                        new(
                            "Модель",
                            ValueOrUnavailable(
                                device.Model)),

                        new(
                            "Производитель",
                            ValueOrUnavailable(
                                device.Manufacturer)),

                        new(
                            "Серийный номер",
                            ValueOrUnavailable(
                                device.SerialNumber)),

                        new(
                            "Прошивка",
                            ValueOrUnavailable(
                                device.Firmware)),

                        new(
                            "Интерфейс",
                            ValueOrUnavailable(
                                device.InterfaceType)),

                        new(
                            "Тип носителя",
                            ValueOrUnavailable(
                                device.MediaType)),

                        new(
                            "Ёмкость",
                            FormatBytes(
                                device.CapacityBytes))
                    });

            StorageDevicesPanel
                .Children
                .Add(
                    card);
        }
    }


    // ============================================================
    // WINDOWS
    // ============================================================

    private void PopulateWindows(
        OperatingSystemDetails windows)
    {
        WindowsNameText.Text =
            ValueOrUnavailable(
                windows.Name);

        WindowsEditionText.Text =
            ValueOrUnavailable(
                windows.Edition);

        string versionText =
            ValueOrUnavailable(
                windows.DisplayVersion);

        if (!string.IsNullOrWhiteSpace(
                windows.Version))
        {
            versionText +=
                $"   ({windows.Version})";
        }

        WindowsVersionText.Text =
            versionText;

        WindowsBuildText.Text =
            ValueOrUnavailable(
                windows.BuildNumber);

        WindowsArchitectureText.Text =
            ValueOrUnavailable(
                windows.Architecture);

        ComputerNameText.Text =
            ValueOrUnavailable(
                windows.ComputerName);

        WindowsInstallDateText.Text =
            FormatDate(
                windows.InstallDate);
    }


    // ============================================================
    // ДИНАМИЧЕСКИЕ КАРТОЧКИ
    // ============================================================

    private Border CreateInformationCard(
        string title,
        IReadOnlyList<InformationRow> rows)
    {
        Border card =
            new()
            {
                BorderThickness =
                    new Thickness(1),

                Padding =
                    new Thickness(18),

                Margin =
                    new Thickness(
                        0, 0, 0, 12)
            };

        card.SetResourceReference(
            Border.BackgroundProperty,
            "CardBackgroundBrush");

        card.SetResourceReference(
            Border.BorderBrushProperty,
            "BorderBrush");

        card.SetResourceReference(
            Border.CornerRadiusProperty,
            "PanelCornerRadius");

        StackPanel body =
            new();

        TextBlock heading =
            new()
            {
                Text =
                    SettingsService.IsRussian
                        ? title
                        : SettingsService.TranslateText(
                            title),
                FontSize = 16,
                FontWeight =
                    FontWeights.Bold,
                Margin =
                    new Thickness(
                        0, 0, 0, 12)
            };

        heading.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        body.Children.Add(
            heading);

        foreach (InformationRow row in rows)
        {
            Grid line =
                new()
                {
                    Margin =
                        new Thickness(
                            0, 4, 0, 4)
                };

            line.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(210)
                });

            line.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(
                            1,
                            GridUnitType.Star)
                });

            TextBlock label =
                new()
                {
                    Text =
                        SettingsService.IsRussian
                            ? row.Label
                            : SettingsService.TranslateText(
                                row.Label),

                    FontSize = 11,

                    FontWeight =
                        FontWeights.SemiBold,

                    VerticalAlignment =
                        VerticalAlignment.Center
                };

            label.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            TextBlock value =
                new()
                {
                    Text =
                        SettingsService.IsRussian
                            ? row.Value
                            : SettingsService.TranslateText(
                                row.Value),

                    FontSize = 13,

                    TextWrapping =
                        TextWrapping.Wrap,

                    VerticalAlignment =
                        VerticalAlignment.Center
                };

            value.SetResourceReference(
                TextBlock.ForegroundProperty,
                "PrimaryTextBrush");

            Grid.SetColumn(
                label,
                0);

            Grid.SetColumn(
                value,
                1);

            line.Children.Add(
                label);

            line.Children.Add(
                value);

            body.Children.Add(
                line);
        }

        card.Child =
            body;

        return card;
    }


    private Border CreateEmptyMessage(
        string text)
    {
        Border card =
            new()
            {
                BorderThickness =
                    new Thickness(1),

                Padding =
                    new Thickness(18),

                Margin =
                    new Thickness(
                        0, 0, 0, 12)
            };

        card.SetResourceReference(
            Border.BackgroundProperty,
            "CardBackgroundBrush");

        card.SetResourceReference(
            Border.BorderBrushProperty,
            "BorderBrush");

        card.SetResourceReference(
            Border.CornerRadiusProperty,
            "PanelCornerRadius");

        TextBlock message =
            new()
            {
                Text =
                    SettingsService.IsRussian
                        ? text
                        : SettingsService.TranslateText(
                            text),
                FontSize = 13
            };

        message.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        card.Child =
            message;

        return card;
    }


    // ============================================================
    // ФОРМАТИРОВАНИЕ
    // ============================================================

    private static string ValueOrUnavailable(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? SettingsService.L(
                "Нет данных",
                "No data")
            : value.Trim();
    }


    private static string FormatNumber(
        uint? value)
    {
        return value.HasValue
            ? value.Value.ToString()
            : SettingsService.L(
                "Нет данных",
                "No data");
    }


    private static string FormatFrequency(
        uint? megahertz)
    {
        if (!megahertz.HasValue)
        {
            return SettingsService.L(
                "Нет данных",
                "No data");
        }

        if (megahertz.Value >= 1000)
        {
            return SettingsService.L(
                $"{megahertz.Value / 1000.0:F2} ГГц",
                $"{megahertz.Value / 1000.0:F2} GHz");
        }

        return SettingsService.L(
            $"{megahertz.Value} МГц",
            $"{megahertz.Value} MHz");
    }


    private static string FormatMemorySpeed(
        uint? speed)
    {
        return speed.HasValue
            ? $"{speed.Value} MT/s"
            : SettingsService.L(
                "Нет данных",
                "No data");
    }


    private static string FormatMemoryType(
        ushort? memoryType)
    {
        if (!memoryType.HasValue)
        {
            return SettingsService.L(
                "Нет данных",
                "No data");
        }

        return memoryType.Value switch
        {
            20 => "DDR",
            21 => "DDR2",
            22 => "DDR2 FB-DIMM",
            24 => "DDR3",
            26 => "DDR4",
            27 => "LPDDR",
            28 => "LPDDR2",
            29 => "LPDDR3",
            30 => "LPDDR4",
            34 => "DDR5",
            35 => "LPDDR5",
            _ => SettingsService.L(
                $"Код SMBIOS {memoryType.Value}",
                $"SMBIOS code {memoryType.Value}")
        };
    }


    private static string FormatMemoryFormFactor(
        ushort? formFactor)
    {
        if (!formFactor.HasValue)
        {
            return SettingsService.L(
                "Нет данных",
                "No data");
        }

        return formFactor.Value switch
        {
            8 => "DIMM",
            12 => "SO-DIMM",
            22 => "FB-DIMM",
            _ => SettingsService.L(
                $"Код {formFactor.Value}",
                $"Code {formFactor.Value}")
        };
    }


    private static string FormatMemoryVoltage(
        uint? millivolts)
    {
        if (!millivolts.HasValue)
        {
            return SettingsService.L(
                "Нет данных",
                "No data");
        }

        return SettingsService.L(
            $"{millivolts.Value / 1000.0:F2} В",
            $"{millivolts.Value / 1000.0:F2} V");
    }


    private static string FormatMemoryWidth(
        ushort? bits)
    {
        return bits.HasValue
            ? SettingsService.L(
                $"{bits.Value} бит",
                $"{bits.Value} bits")
            : SettingsService.L(
                "Нет данных",
                "No data");
    }


    private static string FormatCache(
        uint? kilobytes)
    {
        if (!kilobytes.HasValue)
        {
            return SettingsService.L(
                "Нет данных",
                "No data");
        }

        if (kilobytes.Value >= 1024)
        {
            return SettingsService.L(
                $"{kilobytes.Value / 1024.0:F1} МБ",
                $"{kilobytes.Value / 1024.0:F1} MB");
        }

        return SettingsService.L(
            $"{kilobytes.Value} КБ",
            $"{kilobytes.Value} KB");
    }


    private static string FormatDate(
        DateTime? date)
    {
        if (!date.HasValue)
        {
            return SettingsService.L(
                "Нет данных",
                "No data");
        }

        return date.Value.ToString(
            SettingsService.IsRussian
                ? "dd.MM.yyyy"
                : "yyyy-MM-dd");
    }


    private static string FormatBytes(
        ulong? bytes)
    {
        if (!bytes.HasValue ||
            bytes.Value == 0)
        {
            return SettingsService.L(
                "Нет данных",
                "No data");
        }

        double gigabytes =
            bytes.Value /
            1024d /
            1024d /
            1024d;

        if (gigabytes >= 1024)
        {
            return SettingsService.L(
                $"{gigabytes / 1024d:F2} ТБ",
                $"{gigabytes / 1024d:F2} TB");
        }

        return SettingsService.L(
            $"{gigabytes:F1} ГБ",
            $"{gigabytes:F1} GB");
    }


    // ============================================================
    // ДИАГНОСТИЧЕСКИЙ ОТЧЁТ
    // ============================================================

    private void SaveDiagnosticReportButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_snapshot == null)
        {
            StatusText.Text =
                SettingsService.L(
                    "Информация о системе ещё не загружена.",
                    "System information has not been loaded yet.");

            return;
        }

        try
        {
            SaveFileDialog dialog =
                new()
                {
                    Title =
                        SettingsService.L(
                            "Сохранить диагностический отчёт Thermiqra",
                            "Save Thermiqra diagnostic report"),

                    FileName =
                        $"Thermiqra_Diagnostic_" +
                        $"{DateTime.Now:yyyy-MM-dd_HH-mm}.txt",

                    DefaultExt =
                        ".txt",

                    AddExtension =
                        true,

                    Filter =
                        SettingsService.L(
                            "Текстовый файл (*.txt)|*.txt|Все файлы (*.*)|*.*",
                            "Text file (*.txt)|*.txt|All files (*.*)|*.*")
                };

            bool? result =
                dialog.ShowDialog(
                    this);

            if (result != true)
                return;

            string report =
                BuildDiagnosticReport(
                    _snapshot);

            File.WriteAllText(
                dialog.FileName,
                report,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier:
                    true));

            StatusText.Text =
                SettingsService.L(
                    $"Диагностический отчёт сохранён: {dialog.FileName}",
                    $"Diagnostic report saved: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            StatusText.Text =
                SettingsService.L(
                    $"Не удалось сохранить отчёт: {ex.Message}",
                    $"Failed to save the report: {ex.Message}");

            MessageBox.Show(
                this,
                SettingsService.L(
                    $"Не удалось сохранить диагностический отчёт.\n\n{ex.Message}",
                    $"Failed to save the diagnostic report.\n\n{ex.Message}"),
                "Thermiqra",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }


    private string BuildDiagnosticReport(
        SystemInformationSnapshot systemSnapshot)
    {
        StringBuilder text =
            new();

        text.AppendLine(
            "THERMIQRA — " +
            SettingsService.L(
                "ДИАГНОСТИЧЕСКИЙ ОТЧЁТ",
                "DIAGNOSTIC REPORT"));

        text.AppendLine(
            new string(
                '=',
                62));

        text.AppendLine();

        text.AppendLine(
            $"{SettingsService.L("Создан", "Generated")}: " +
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        text.AppendLine(
            $"{SettingsService.L("Версия Thermiqra", "Thermiqra version")}: " +
            $"{GetThermiqraVersion()}");

        text.AppendLine(
            SettingsService.L(
                "Отчёт создан локально. Thermiqra не отправляет эти данные автоматически.",
                "This report was created locally. Thermiqra does not send this data automatically."));

        text.AppendLine();

        text.AppendLine(
            BuildCopyText(
                systemSnapshot));

        text.AppendLine();
        text.AppendLine();

        AppendCurrentMonitoringSection(
            text);

        text.AppendLine();

        AppendSpdXmpSection(
            text);

        text.AppendLine();

        AppendHistorySection(
            text);

        return text
            .ToString()
            .TrimEnd();
    }


    private static void AppendCurrentMonitoringSection(
        StringBuilder text)
    {
        text.AppendLine(
            $"[{SettingsService.L("ТЕКУЩИЙ МОНИТОРИНГ", "CURRENT MONITORING")}]");

        HardwareSnapshot? snapshot =
            HardwareMonitorService.LastSnapshot;

        if (snapshot == null)
        {
            text.AppendLine(
                SettingsService.L(
                    "Текущий снимок датчиков пока недоступен.",
                    "The current sensor snapshot is not available yet."));

            return;
        }

        text.AppendLine();

        text.AppendLine(
            $"CPU: {snapshot.Cpu.Name}");

        text.AppendLine(
            $"  {SettingsService.L("Температура", "Temperature")}: " +
            $"{FormatLiveTemperature(snapshot.Cpu.Temperature)}");

        text.AppendLine(
            $"  {SettingsService.L("Загрузка", "Load")}: " +
            $"{FormatLivePercent(snapshot.Cpu.Load)}");

        text.AppendLine();

        text.AppendLine(
            $"GPU:");

        if (snapshot.Gpus.Count == 0)
        {
            text.AppendLine(
                $"  {SettingsService.L("Нет данных", "No data")}");
        }
        else
        {
            for (int i = 0;
                 i < snapshot.Gpus.Count;
                 i++)
            {
                GpuInfo gpu =
                    snapshot.Gpus[i];

                text.AppendLine(
                    $"  GPU {i + 1}: {gpu.Name}");

                text.AppendLine(
                    $"    {SettingsService.L("Температура", "Temperature")}: " +
                    $"{FormatLiveTemperature(gpu.Temperature)}");

                text.AppendLine(
                    $"    Hot Spot: " +
                    $"{FormatLiveTemperature(gpu.HotSpotTemperature)}");

                text.AppendLine(
                    $"    {SettingsService.L("Температура VRAM", "VRAM temperature")}: " +
                    $"{FormatLiveTemperature(gpu.MemoryTemperature)}");

                text.AppendLine(
                    $"    {SettingsService.L("Загрузка", "Load")}: " +
                    $"{FormatLivePercent(gpu.Load)}");

                text.AppendLine(
                    $"    {SettingsService.L("Загрузка памяти GPU", "GPU memory load")}: " +
                    $"{FormatLivePercent(gpu.MemoryLoad)}");
            }
        }

        text.AppendLine();

        text.AppendLine(
            $"RAM:");

        text.AppendLine(
            $"  {SettingsService.L("Загрузка", "Load")}: " +
            $"{FormatLivePercent(snapshot.Memory.Load)}");

        text.AppendLine(
            $"  {SettingsService.L("Используется", "Used")}: " +
            $"{FormatLiveGigabytes(snapshot.Memory.UsedGb)}");

        text.AppendLine(
            $"  {SettingsService.L("Доступно", "Available")}: " +
            $"{FormatLiveGigabytes(snapshot.Memory.AvailableGb)}");

        text.AppendLine(
            $"  {SettingsService.L("Всего", "Total")}: " +
            $"{FormatLiveGigabytes(snapshot.Memory.TotalGb)}");

        text.AppendLine();

        text.AppendLine(
            $"[{SettingsService.L("ТЕМПЕРАТУРЫ НАКОПИТЕЛЕЙ", "STORAGE TEMPERATURES")}]");

        if (snapshot.StorageDevices.Count == 0)
        {
            text.AppendLine(
                SettingsService.L(
                    "Нет данных",
                    "No data"));
        }
        else
        {
            foreach (StorageDeviceInfo storage
                     in snapshot.StorageDevices)
            {
                text.AppendLine(
                    $"{storage.Name}: " +
                    $"{FormatLiveTemperature(storage.Temperature)}");
            }
        }

        text.AppendLine();

        text.AppendLine(
            $"[{SettingsService.L("ЛОГИЧЕСКИЕ ДИСКИ", "LOGICAL DRIVES")}]");

        if (snapshot.Drives.Count == 0)
        {
            text.AppendLine(
                SettingsService.L(
                    "Нет данных",
                    "No data"));
        }
        else
        {
            foreach (LogicalDriveInfo drive
                     in snapshot.Drives)
            {
                long usedBytes =
                    Math.Max(
                        0,
                        drive.TotalBytes -
                        drive.FreeBytes);

                double usedPercent =
                    drive.TotalBytes > 0
                        ? usedBytes * 100.0 /
                          drive.TotalBytes
                        : 0;

                string label =
                    string.IsNullOrWhiteSpace(
                        drive.VolumeLabel)
                        ? string.Empty
                        : $" ({drive.VolumeLabel})";

                text.AppendLine(
                    $"{drive.Name}{label}");

                text.AppendLine(
                    $"  {SettingsService.L("Всего", "Total")}: " +
                    $"{FormatLongBytes(drive.TotalBytes)}");

                text.AppendLine(
                    $"  {SettingsService.L("Занято", "Used")}: " +
                    $"{FormatLongBytes(usedBytes)} " +
                    $"({usedPercent:F1} %)");

                text.AppendLine(
                    $"  {SettingsService.L("Свободно", "Free")}: " +
                    $"{FormatLongBytes(drive.FreeBytes)}");
            }
        }
    }


    private static void AppendSpdXmpSection(
        StringBuilder text)
    {
        text.AppendLine(
            $"[{SettingsService.L("SPD / JEDEC / XMP", "SPD / JEDEC / XMP")}]");

        HardwareSnapshot? snapshot =
            HardwareMonitorService.LastSnapshot;

        if (snapshot == null ||
            snapshot.MemorySpdModules.Count == 0)
        {
            text.AppendLine(
                SettingsService.L(
                    "Расширенные SPD/XMP-данные пока недоступны.",
                    "Extended SPD/XMP data is not available yet."));

            return;
        }

        for (int i = 0;
             i < snapshot.MemorySpdModules.Count;
             i++)
        {
            MemorySpdModuleInfo module =
                snapshot.MemorySpdModules[i];

            if (i > 0)
                text.AppendLine();

            string moduleName =
                string.IsNullOrWhiteSpace(
                    module.Name)
                    ? SettingsService.L(
                        $"Модуль {i + 1}",
                        $"Module {i + 1}")
                    : module.Name;

            text.AppendLine(
                $"{SettingsService.L("Модуль", "Module")} {i + 1}: " +
                $"{moduleName}");

            text.AppendLine(
                $"  {SettingsService.L("Ёмкость", "Capacity")}: " +
                $"{FormatSpdCapacity(module.CapacityGb)}");

            if (module.Jedec != null)
            {
                text.AppendLine(
                    $"  JEDEC: " +
                    $"{BuildJedecSummary(module.Jedec)}");

                if (module.Jedec.SupportedCasLatencies.Count > 0)
                {
                    text.AppendLine(
                        $"  JEDEC CL: " +
                        $"{string.Join(", ", module.Jedec.SupportedCasLatencies)}");
                }
            }
            else
            {
                text.AppendLine(
                    $"  JEDEC: " +
                    $"{SettingsService.L("Нет данных", "No data")}");
            }

            if (module.XmpProfiles.Count == 0)
            {
                text.AppendLine(
                    $"  XMP: " +
                    $"{SettingsService.L("Профили не обнаружены", "No profiles detected")}");
            }
            else
            {
                foreach (MemoryXmpProfileInfo profile
                         in module.XmpProfiles)
                {
                    text.AppendLine(
                        $"  {module.XmpVersion ?? "XMP"} " +
                        $"{SettingsService.L("профиль", "profile")} " +
                        $"{profile.ProfileNumber}: " +
                        $"{BuildXmpProfileSummary(profile)}");
                }
            }

            if (module.Timings.Count > 0)
            {
                text.AppendLine(
                    $"  {SettingsService.L("SPD-тайминги", "SPD timings")}:");

                foreach (MemorySpdTimingInfo timing
                         in module.Timings)
                {
                    text.AppendLine(
                        $"    {timing.Name}: " +
                        $"{timing.ValueNanoseconds:0.###} ns");
                }
            }
        }
    }


    private void AppendHistorySection(
        StringBuilder text)
    {
        text.AppendLine(
            $"[{SettingsService.L("ИСТОРИЯ — ПОСЛЕДНИЕ 30 ДНЕЙ", "HISTORY — LAST 30 DAYS")}]");

        if (_statistics == null)
        {
            text.AppendLine(
                SettingsService.L(
                    "Сервис статистики недоступен. Остальная часть отчёта сохранена.",
                    "The statistics service is unavailable. The rest of the report was saved."));

            return;
        }

        try
        {
            StatisticsSummary summary =
                _statistics.GetSummary(
                    StatisticsPeriod.Last30Days);

            text.AppendLine(
                $"{SettingsService.L("Сохранённых точек", "Saved samples")}: " +
                $"{summary.SampleCount}");

            if (summary.SampleCount > 0)
            {
                text.AppendLine(
                    $"{SettingsService.L("Период данных", "Data range")}: " +
                    $"{FormatHistoryDate(summary.StartUtc)} — " +
                    $"{FormatHistoryDate(summary.EndUtc)}");
            }

            text.AppendLine();

            AppendHistoryMetric(
                text,
                "CPU / " +
                SettingsService.L(
                    "температура",
                    "temperature"),
                summary.CpuTemperature.Average,
                summary.CpuTemperature.Maximum,
                summary.CpuTemperature.MaximumAtUtc,
                "°C");

            AppendHistoryMetric(
                text,
                "CPU / " +
                SettingsService.L(
                    "загрузка",
                    "load"),
                summary.CpuLoad.Average,
                summary.CpuLoad.Maximum,
                summary.CpuLoad.MaximumAtUtc,
                "%");

            AppendHistoryMetric(
                text,
                "RAM / " +
                SettingsService.L(
                    "загрузка",
                    "load"),
                summary.RamLoad.Average,
                summary.RamLoad.Maximum,
                summary.RamLoad.MaximumAtUtc,
                "%");

            foreach (GpuStatisticsSummary gpu
                     in summary.Gpus)
            {
                AppendHistoryMetric(
                    text,
                    $"GPU / {gpu.DeviceName} / " +
                    SettingsService.L(
                        "температура",
                        "temperature"),
                    gpu.Temperature.Average,
                    gpu.Temperature.Maximum,
                    gpu.Temperature.MaximumAtUtc,
                    "°C");

                AppendHistoryMetric(
                    text,
                    $"GPU / {gpu.DeviceName} / " +
                    SettingsService.L(
                        "загрузка",
                        "load"),
                    gpu.Load.Average,
                    gpu.Load.Maximum,
                    gpu.Load.MaximumAtUtc,
                    "%");

                AppendHistoryMetric(
                    text,
                    $"GPU / {gpu.DeviceName} / Hot Spot",
                    gpu.HotSpotTemperature.Average,
                    gpu.HotSpotTemperature.Maximum,
                    gpu.HotSpotTemperature.MaximumAtUtc,
                    "°C");

                AppendHistoryMetric(
                    text,
                    $"GPU / {gpu.DeviceName} / VRAM",
                    gpu.MemoryTemperature.Average,
                    gpu.MemoryTemperature.Maximum,
                    gpu.MemoryTemperature.MaximumAtUtc,
                    "°C");
            }

            foreach (StorageStatisticsSummary storage
                     in summary.StorageDevices)
            {
                AppendHistoryMetric(
                    text,
                    $"{SettingsService.L("Накопитель", "Storage")} / " +
                    $"{storage.DeviceName} / " +
                    SettingsService.L(
                        "температура",
                        "temperature"),
                    storage.Temperature.Average,
                    storage.Temperature.Maximum,
                    storage.Temperature.MaximumAtUtc,
                    "°C");
            }

            AlertEventCounts counts =
                _statistics.GetAlertEventCounts(
                    StatisticsPeriod.Last30Days);

            text.AppendLine();

            text.AppendLine(
                $"WARNING: {counts.WarningCount}   •   " +
                $"CRITICAL: {counts.CriticalCount}");

            text.AppendLine();
            text.AppendLine(
                $"[{SettingsService.L("РЕКОРДЫ ЗА ВСЁ ВРЕМЯ", "ALL-TIME RECORDS")}]");

            List<AllTimeRecord> records =
                _statistics.GetAllTimeRecords();

            if (records.Count == 0)
            {
                text.AppendLine(
                    SettingsService.L(
                        "Рекордов пока нет.",
                        "No records yet."));
            }
            else
            {
                foreach (AllTimeRecord record
                         in records)
                {
                    text.AppendLine(
                        $"{GetDiagnosticRecordName(record)}: " +
                        $"{FormatHistoryValue(record.Value, record.Unit)}" +
                        $"   •   " +
                        $"{FormatHistoryDate(record.TimestampUtc)}");
                }
            }
        }
        catch (Exception ex)
        {
            text.AppendLine(
                SettingsService.L(
                    $"Не удалось прочитать историю: {ex.Message}",
                    $"Failed to read history: {ex.Message}"));
        }
    }


    private static void AppendHistoryMetric(
        StringBuilder text,
        string name,
        double? average,
        double? maximum,
        DateTimeOffset? maximumAtUtc,
        string unit)
    {
        text.AppendLine(
            $"{name}:");

        text.AppendLine(
            $"  {SettingsService.L("Среднее", "Average")}: " +
            $"{FormatHistoryValue(average, unit)}");

        text.AppendLine(
            $"  {SettingsService.L("Максимум", "Maximum")}: " +
            $"{FormatHistoryValue(maximum, unit)}" +
            $"   •   " +
            $"{SettingsService.L("зафиксирован", "recorded")}: " +
            $"{FormatHistoryDate(maximumAtUtc)}");
    }


    private static string GetDiagnosticRecordName(
        AllTimeRecord record)
    {
        return record.Category switch
        {
            "CPU" =>
                SettingsService.L(
                    "CPU / максимальная температура",
                    "CPU / maximum temperature"),

            "GPU" =>
                $"GPU / {record.DeviceName}",

            "RAM" =>
                SettingsService.L(
                    "RAM / максимальная загрузка",
                    "RAM / maximum load"),

            "STORAGE" =>
                SettingsService.L(
                    $"Накопитель / {record.DeviceName}",
                    $"Storage / {record.DeviceName}"),

            _ =>
                record.DeviceName
        };
    }


    private static string GetThermiqraVersion()
    {
        Version? version =
            typeof(SystemInfoWindow)
                .Assembly
                .GetName()
                .Version;

        if (version == null)
            return "—";

        return version.Revision > 0
            ? $"{version.Major}." +
              $"{version.Minor}." +
              $"{version.Build}." +
              $"{version.Revision}"
            : $"{version.Major}." +
              $"{version.Minor}." +
              $"{version.Build}";
    }


    private static string FormatLiveTemperature(
        float? value)
    {
        return value.HasValue
            ? $"{value.Value:F1} °C"
            : "—";
    }


    private static string FormatLivePercent(
        float? value)
    {
        return value.HasValue
            ? $"{value.Value:F1} %"
            : "—";
    }


    private static string FormatLiveGigabytes(
        float? value)
    {
        return value.HasValue
            ? SettingsService.L(
                $"{value.Value:F1} ГБ",
                $"{value.Value:F1} GB")
            : "—";
    }


    private static string FormatLongBytes(
        long bytes)
    {
        if (bytes < 0)
            return "—";

        double value =
            bytes;

        string[] units =
            SettingsService.IsRussian
                ? new[]
                {
                    "Б",
                    "КБ",
                    "МБ",
                    "ГБ",
                    "ТБ"
                }
                : new[]
                {
                    "B",
                    "KB",
                    "MB",
                    "GB",
                    "TB"
                };

        int unitIndex =
            0;

        while (value >= 1024 &&
               unitIndex <
               units.Length - 1)
        {
            value /=
                1024;

            unitIndex++;
        }

        return
            $"{value:F1} " +
            $"{units[unitIndex]}";
    }


    private static string FormatHistoryValue(
        double? value,
        string unit)
    {
        if (!value.HasValue)
            return "—";

        return unit == "%"
            ? $"{value.Value:F1} %"
            : $"{value.Value:F1} °C";
    }


    private static string FormatHistoryValue(
        double value,
        string unit)
    {
        return unit == "%"
            ? $"{value:F1} %"
            : $"{value:F1} °C";
    }


    private static string FormatHistoryDate(
        DateTimeOffset valueUtc)
    {
        DateTimeOffset local =
            valueUtc.ToLocalTime();

        return local.ToString(
            SettingsService.IsRussian
                ? "dd.MM.yyyy HH:mm"
                : "yyyy-MM-dd HH:mm");
    }


    private static string FormatHistoryDate(
        DateTimeOffset? valueUtc)
    {
        return valueUtc.HasValue
            ? FormatHistoryDate(
                valueUtc.Value)
            : "—";
    }


    // ============================================================
    // КОПИРОВАНИЕ ИНФОРМАЦИИ
    // ============================================================

    private void CopyInformationButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_snapshot == null)
        {
            StatusText.Text =
                SettingsService.TranslateText(
                    "Информация о системе ещё не загружена.");

            return;
        }

        try
        {
            string text =
                BuildCopyText(
                    _snapshot);

            Clipboard.SetText(
                text);

            StatusText.Text =
                SettingsService.TranslateText(
                    "Информация скопирована в буфер обмена.");
        }
        catch (Exception ex)
        {
            StatusText.Text =
                SettingsService.TranslateText(
                    $"Не удалось скопировать данные: " +
                    $"{ex.Message}");
        }
    }


    private static string BuildCopyText(
        SystemInformationSnapshot snapshot)
    {
        StringBuilder text =
            new();

        text.AppendLine(
            "THERMIQRA — ИНФОРМАЦИЯ О СИСТЕМЕ");

        text.AppendLine(
            "================================");

        text.AppendLine();

        text.AppendLine(
            "[WINDOWS]");

        AppendCopyLine(
            text,
            "Система",
            snapshot.Windows.Name);

        AppendCopyLine(
            text,
            "Редакция",
            snapshot.Windows.Edition);

        AppendCopyLine(
            text,
            "Версия",
            snapshot.Windows.DisplayVersion);

        AppendCopyLine(
            text,
            "Версия ОС",
            snapshot.Windows.Version);

        AppendCopyLine(
            text,
            "Сборка",
            snapshot.Windows.BuildNumber);

        AppendCopyLine(
            text,
            "Архитектура",
            snapshot.Windows.Architecture);

        AppendCopyLine(
            text,
            "Имя компьютера",
            snapshot.Windows.ComputerName);

        text.AppendLine(
            $"Дата установки: " +
            $"{FormatDate(snapshot.Windows.InstallDate)}");

        text.AppendLine();

        text.AppendLine(
            "[ПРОЦЕССОР]");

        AppendCopyLine(
            text,
            "Производитель",
            snapshot.Processor.Manufacturer);

        AppendCopyLine(
            text,
            "Модель",
            snapshot.Processor.Name);

        AppendCopyLine(
            text,
            "Архитектура",
            snapshot.Processor.Architecture);

        text.AppendLine(
            $"Физические ядра: " +
            $"{FormatNumber(snapshot.Processor.PhysicalCores)}");

        text.AppendLine(
            $"Логические потоки: " +
            $"{FormatNumber(snapshot.Processor.LogicalProcessors)}");

        text.AppendLine(
            $"Текущая частота: " +
            $"{FormatFrequency(snapshot.Processor.CurrentClockSpeedMhz)}");

        text.AppendLine(
            $"Максимальная частота: " +
            $"{FormatFrequency(snapshot.Processor.MaxClockSpeedMhz)}");

        AppendCopyLine(
            text,
            "Сокет",
            snapshot.Processor.Socket);

        text.AppendLine(
            $"Кэш L2: " +
            $"{FormatCache(snapshot.Processor.L2CacheKb)}");

        text.AppendLine(
            $"Кэш L3: " +
            $"{FormatCache(snapshot.Processor.L3CacheKb)}");

        text.AppendLine();

        text.AppendLine(
            "[МАТЕРИНСКАЯ ПЛАТА]");

        AppendCopyLine(
            text,
            "Производитель",
            snapshot.Motherboard.Manufacturer);

        AppendCopyLine(
            text,
            "Модель",
            snapshot.Motherboard.Model);

        AppendCopyLine(
            text,
            "Версия платы",
            snapshot.Motherboard.Version);

        AppendCopyLine(
            text,
            "Тип прошивки",
            snapshot.Motherboard.FirmwareType);

        AppendCopyLine(
            text,
            "Версия BIOS",
            snapshot.Motherboard.BiosVersion);

        text.AppendLine(
            $"Дата BIOS: " +
            $"{FormatDate(snapshot.Motherboard.BiosDate)}");

        text.AppendLine();

        text.AppendLine(
            "[ОПЕРАТИВНАЯ ПАМЯТЬ]");

        text.AppendLine(
            $"Установлено: " +
            $"{FormatBytes(snapshot.Memory.TotalBytes)}");

        text.AppendLine(
            $"Обнаружено модулей: " +
            $"{snapshot.Memory.Modules.Count}");

        for (int i = 0;
             i < snapshot.Memory.Modules.Count;
             i++)
        {
            MemoryModuleDetails module =
                snapshot.Memory.Modules[i];

            text.AppendLine();

            text.AppendLine(
                $"Модуль {i + 1}:");

            AppendCopyLine(
                text,
                "  Слот / банк",
                module.Slot);

            text.AppendLine(
                $"  Объём: " +
                $"{FormatBytes(module.CapacityBytes)}");

            AppendCopyLine(
                text,
                "  Производитель",
                module.Manufacturer);

            AppendCopyLine(
                text,
                "  Part Number",
                module.PartNumber);

            AppendCopyLine(
                text,
                "  Серийный номер",
                module.SerialNumber);

            text.AppendLine(
                $"  Частота: " +
                $"{FormatFrequency(module.FrequencyMhz)}");
        }

        text.AppendLine();

        text.AppendLine(
            "[ВИДЕОКАРТЫ]");

        if (snapshot.GraphicsAdapters.Count == 0)
        {
            text.AppendLine(
                "Нет данных");
        }
        else
        {
            for (int i = 0;
                 i < snapshot.GraphicsAdapters.Count;
                 i++)
            {
                GraphicsAdapterDetails adapter =
                    snapshot.GraphicsAdapters[i];

                if (i > 0)
                    text.AppendLine();

                text.AppendLine(
                    $"Видеоадаптер {i + 1}:");

                AppendCopyLine(
                    text,
                    "  Модель",
                    adapter.Name);

                AppendCopyLine(
                    text,
                    "  Производитель",
                    adapter.Manufacturer);

                AppendCopyLine(
                    text,
                    "  Графический процессор",
                    adapter.VideoProcessor);

                text.AppendLine(
                    $"  Видеопамять: " +
                    $"{(adapter.DedicatedMemoryBytes.HasValue
                        ? FormatBytes(adapter.DedicatedMemoryBytes)
                        : "Не определяется надёжно")}");

                AppendCopyLine(
                    text,
                    "  Версия драйвера",
                    adapter.DriverVersion);

                text.AppendLine(
                    $"  Дата драйвера: " +
                    $"{FormatDate(adapter.DriverDate)}");
            }
        }

        text.AppendLine();

        text.AppendLine(
            "[ФИЗИЧЕСКИЕ НАКОПИТЕЛИ]");

        if (snapshot.StorageDevices.Count == 0)
        {
            text.AppendLine(
                "Нет данных");
        }
        else
        {
            for (int i = 0;
                 i < snapshot.StorageDevices.Count;
                 i++)
            {
                StorageDeviceDetails device =
                    snapshot.StorageDevices[i];

                if (i > 0)
                    text.AppendLine();

                text.AppendLine(
                    $"Накопитель {i + 1}:");

                AppendCopyLine(
                    text,
                    "  Модель",
                    device.Model);

                AppendCopyLine(
                    text,
                    "  Производитель",
                    device.Manufacturer);

                AppendCopyLine(
                    text,
                    "  Серийный номер",
                    device.SerialNumber);

                AppendCopyLine(
                    text,
                    "  Прошивка",
                    device.Firmware);

                AppendCopyLine(
                    text,
                    "  Интерфейс",
                    device.InterfaceType);

                AppendCopyLine(
                    text,
                    "  Тип носителя",
                    device.MediaType);

                text.AppendLine(
                    $"  Ёмкость: " +
                    $"{FormatBytes(device.CapacityBytes)}");
            }
        }

        return SettingsService.TranslateText(
            text
                .ToString()
                .TrimEnd());
    }


    private static void AppendCopyLine(
        StringBuilder text,
        string label,
        string? value)
    {
        text.AppendLine(
            $"{label}: " +
            $"{ValueOrUnavailable(value)}");
    }


    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }


    private sealed record InformationRow(
        string Label,
        string Value);
}
