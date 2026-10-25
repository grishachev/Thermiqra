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



}
