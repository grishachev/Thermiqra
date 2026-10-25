using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PCHardwareMonitor;

public partial class SystemInfoWindow
{
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
    // НАКОПИТЕЛИ — одноразовое чтение счётчиков надёжности Windows
    // ============================================================

    private int _storageReliabilityRequestId;

    private async void PopulateStorageDevices(
        IReadOnlyList<StorageDeviceDetails> devices)
    {
        int requestId = ++_storageReliabilityRequestId;

        RenderStorageDevices(devices, null, isReading: devices.Count > 0);

        if (devices.Count == 0)
            return;

        IReadOnlyDictionary<uint, StorageReliabilityDetails> reliability;

        try
        {
            // Some controller requests may take time. Never run them on
            // the window thread or in the 1-second monitoring loop.
            reliability = await Task.Run(() =>
                SystemInfoService.ReadStorageReliabilityCounters(devices));
        }
        catch
        {
            reliability = new Dictionary<uint, StorageReliabilityDetails>();
        }

        if (requestId != _storageReliabilityRequestId ||
            Dispatcher.HasShutdownStarted)
        {
            return;
        }

        RenderStorageDevices(devices, reliability, isReading: false);
    }


    private void RenderStorageDevices(
        IReadOnlyList<StorageDeviceDetails> devices,
        IReadOnlyDictionary<uint, StorageReliabilityDetails>? reliability,
        bool isReading)
    {
        StorageDevicesPanel.Children.Clear();

        if (devices.Count == 0)
        {
            StorageDevicesPanel.Children.Add(
                CreateEmptyMessage(SettingsService.L(
                    "Физические накопители не обнаружены.",
                    "No physical storage devices detected.")));
            return;
        }

        for (int i = 0; i < devices.Count; i++)
        {
            StorageDeviceDetails device = devices[i];

            StorageReliabilityDetails? details = null;
            if (device.PhysicalDiskIndex.HasValue && reliability != null)
            {
                reliability.TryGetValue(
                    device.PhysicalDiskIndex.Value, out details);
            }

            string pending = SettingsService.L(
                "Определяется...", "Detecting...");
            string unknown = SettingsService.L(
                "Не определён", "Unknown");

            string interfaceText = isReading ? pending :
                GetStorageBusDescription(details?.BusTypeCode) ?? unknown;
            string mediaText = isReading ? pending :
                GetStorageMediaDescription(
                    details?.MediaTypeCode, details?.BusTypeCode) ?? unknown;

            List<InformationRow> rows = new()
            {
                new(SettingsService.L("Модель", "Model"),
                    ValueOrUnavailable(device.Model)),
                new(SettingsService.L("Производитель", "Manufacturer"),
                    ValueOrUnavailable(device.Manufacturer)),
                new(SettingsService.L("Серийный номер", "Serial number"),
                    ValueOrUnavailable(device.SerialNumber)),
                new(SettingsService.L("Прошивка", "Firmware"),
                    ValueOrUnavailable(device.Firmware)),
                new(SettingsService.L("Подключение", "Connection"),
                    interfaceText),
                new(SettingsService.L("Тип носителя", "Media type"),
                    mediaText),
                new(SettingsService.L("Ёмкость", "Capacity"),
                    FormatBytes(device.CapacityBytes))
            };

            if (isReading)
            {
                rows.Add(new InformationRow(
                    SettingsService.L("Диагностика", "Diagnostics"),
                    SettingsService.L(
                        "Считывание показателей накопителя...",
                        "Reading drive reliability counters...")));
            }
            else if (details is { HasAnyData: true } counters)
            {
                if (counters.HasWindowsCounters)
                {
                    rows.Add(new InformationRow(
                        SettingsService.L("Источник показателей", "Counter source"),
                        "Windows Storage"));
                }

                // Generic SATA Windows wear may be falsely zero. Show this
                // counter only as NVMe fallback when the standard log fails.
                if (counters.BusTypeCode == 17 &&
                    counters.NvmeHealth == null &&
                    counters.WearPercent.HasValue)
                {
                    rows.Add(new InformationRow(
                        SettingsService.L(
                            "Использовано ресурса памяти",
                            "Estimated endurance used"),
                        $"{counters.WearPercent.Value} %"));
                }

                if (counters.PowerOnHours.HasValue &&
                    counters.NvmeHealth == null)
                {
                    rows.Add(new InformationRow(
                        SettingsService.L("Наработка", "Power-on time"),
                        SettingsService.L(
                            $"{counters.PowerOnHours.Value:N0} ч",
                            $"{counters.PowerOnHours.Value:N0} h")));
                }

                // Prefer the controller's own NVMe SMART/Health reading.
                // The Windows Storage counter remains an ATA/NVMe fallback.
                int? driveTemperature =
                    counters.NvmeHealth?.CompositeTemperatureCelsius;
                if (!driveTemperature.HasValue &&
                    counters.TemperatureCelsius is byte windowsTemperature)
                    driveTemperature = windowsTemperature;

                if (driveTemperature.HasValue)
                {
                    rows.Add(new InformationRow(
                        SettingsService.L(
                            "Температура накопителя",
                            "Drive temperature"),
                        $"{driveTemperature.Value} °C"));
                }

                if (counters.ReadErrorsUncorrected.HasValue)
                {
                    rows.Add(new InformationRow(
                        SettingsService.L(
                            "Неисправленные ошибки чтения",
                            "Uncorrected read errors"),
                        $"{counters.ReadErrorsUncorrected.Value:N0}"));
                }

                if (counters.WriteErrorsUncorrected.HasValue)
                {
                    rows.Add(new InformationRow(
                        SettingsService.L(
                            "Неисправленные ошибки записи",
                            "Uncorrected write errors"),
                        $"{counters.WriteErrorsUncorrected.Value:N0}"));
                }
            }
            else
            {
                rows.Add(new InformationRow(
                    SettingsService.L("Диагностика", "Diagnostics"),
                    SettingsService.L(
                        "Windows не предоставила показателей для этого накопителя.",
                        "Windows did not provide reliability counters for this drive.")));
            }

            // The ATA reader captures all available attributes on confirmed
            // ATA/SATA devices; only verified meanings are interpreted.
            if (!isReading && details?.AtaSmart is { HasAnyData: true } ata)
            {
                rows.Add(new InformationRow(
                    SettingsService.L("Источник SMART", "SMART source"),
                    "ATA SMART (Windows WMI)"));

                rows.Add(new InformationRow(
                    SettingsService.L(
                        "Считано ATA SMART-атрибутов",
                        "ATA SMART attributes read"),
                    $"{ata.Attributes.Count:N0}"));

                if (!string.IsNullOrWhiteSpace(ata.RecognizedFamily))
                    rows.Add(new InformationRow(
                        SettingsService.L(
                            "Распознанное семейство", "Recognized family"),
                        ata.RecognizedFamily));
            }

            // Manufacturer/firmware SMART failure prediction applies to
            // positively identified ATA/SATA drives of any maker.
            if (!isReading && details?.BusTypeCode is 3 or 11 &&
                details.AtaSmartPredictedFailure is bool predictedFailure)
            {
                rows.Add(new InformationRow(
                    SettingsService.L(
                        "Прогноз отказа по SMART", "SMART failure prediction"),
                    predictedFailure
                        ? SettingsService.L(
                            "Да — накопитель прогнозирует отказ",
                            "Yes — drive predicts failure")
                        : SettingsService.L(
                            "Нет", "No")));
            }

            // ATA sector attributes are interpreted only for confirmed HDDs.
            // Missing counters are unknown, never assumed to be zero.
            if (!isReading && details?.MediaTypeCode == 3)
            {
                if (details.AtaSmart is { HasAnyData: true } smart)
                {
                    if (smart.ReallocatedSectors.HasValue)
                        rows.Add(new InformationRow(
                            SettingsService.L(
                                "Переназначенные сектора (05)",
                                "Reallocated sectors (05)"),
                            $"{smart.ReallocatedSectors.Value:N0}"));

                    if (smart.PendingSectors.HasValue)
                        rows.Add(new InformationRow(
                            SettingsService.L(
                                "Нестабильные сектора (C5)",
                                "Pending sectors (C5)"),
                            $"{smart.PendingSectors.Value:N0}"));

                    if (smart.UncorrectableSectors.HasValue)
                        rows.Add(new InformationRow(
                            SettingsService.L(
                                "Неисправимые сектора (C6)",
                                "Uncorrectable sectors (C6)"),
                            $"{smart.UncorrectableSectors.Value:N0}"));

                    if (smart.ReallocatedSectors is > 0 ||
                        smart.PendingSectors is > 0 ||
                        smart.UncorrectableSectors is > 0)
                    {
                        rows.Add(new InformationRow(
                            SettingsService.L("Рекомендация", "Recommendation"),
                            SettingsService.L(
                                "Есть ненулевые SMART-счётчики. Проверьте диск и резервную копию данных.",
                                "Some SMART counters are nonzero. Check the drive and your data backup.")));
                    }
                }
                else
                {
                    rows.Add(new InformationRow(
                        SettingsService.L("ATA SMART", "ATA SMART"),
                        SettingsService.L(
                            "Данные недоступны через Windows.",
                            "Data unavailable through Windows.")));
                }
            }

            // Kingston A400 (SA400S37): E7 RAW byte[0] is remaining
            // estimated NAND endurance. Do not use the normalized E7 value
            // or extend this interpretation to other SATA SSD models.
            if (!isReading &&
                details?.AtaSmart?.KingstonA400LifeRemainingPercent
                    is byte kingstonLifeLeft)
            {
                rows.Add(new InformationRow(
                    SettingsService.L(
                        "Остаток ресурса SSD (E7)",
                        "SSD endurance remaining (E7)"),
                    $"{kingstonLifeLeft} %"));

                rows.Add(new InformationRow(
                    SettingsService.L(
                        "Что означает показатель", "What this means"),
                    SettingsService.L(
                        "Оценка остатка ресурса записи по данным SSD, не прогноз срока службы.",
                        "Drive-reported remaining write endurance, not a lifetime prediction.")));
            }

            // The NVMe controller's standard Health Information log is more
            // specific than the generic Windows Storage reliability counters.
            // Never replace an unavailable log with invented zero values.
            if (!isReading && details?.BusTypeCode == 17)
            {
                if (details.NvmeHealth is { } nvme)
                {
                    rows.Add(new InformationRow(
                        SettingsService.L("Источник SMART", "SMART source"),
                        "NVMe SMART/Health (Windows)"));

                    rows.Add(new InformationRow(
                        SettingsService.L(
                            "Критические предупреждения",
                            "Critical warnings"),
                        DescribeNvmeCriticalWarning(nvme.CriticalWarning)));

                    rows.Add(new InformationRow(
                        SettingsService.L(
                            "Резерв памяти", "Available spare"),
                        $"{nvme.AvailableSparePercent} %"));

                    rows.Add(new InformationRow(
                        SettingsService.L(
                            "Порог резерва", "Spare threshold"),
                        $"{nvme.AvailableSpareThresholdPercent} %"));

                    rows.Add(new InformationRow(
                        SettingsService.L(
                            "Использовано ресурса памяти",
                            "Estimated endurance used"),
                        $"{nvme.PercentageUsed} %"));

                    rows.Add(new InformationRow(
                        SettingsService.L("Наработка", "Power-on time"),
                        SettingsService.L(
                            $"{nvme.PowerOnHours:N0} ч",
                            $"{nvme.PowerOnHours:N0} h")));

                    rows.Add(new InformationRow(
                        SettingsService.L(
                            "Всего прочитано (NVMe)",
                            "Total host reads (NVMe)"),
                        FormatNvmeHostData(nvme.DataUnitsRead)));

                    rows.Add(new InformationRow(
                        SettingsService.L(
                            "Всего записано (NVMe)",
                            "Total host writes (NVMe)"),
                        FormatNvmeHostData(nvme.DataUnitsWritten)));

                    rows.Add(new InformationRow(
                        SettingsService.L(
                            "Циклы питания", "Power cycles"),
                        $"{nvme.PowerCycles:N0}"));

                    rows.Add(new InformationRow(
                        SettingsService.L(
                            "Ошибки носителя", "Media errors"),
                        $"{nvme.MediaErrors:N0}"));

                    rows.Add(new InformationRow(
                        SettingsService.L(
                            "Записи в журнале ошибок",
                            "Error log entries"),
                        $"{nvme.ErrorLogEntries:N0}"));

                    rows.Add(new InformationRow(
                        SettingsService.L(
                            "Нештатные отключения питания",
                            "Unsafe shutdowns"),
                        $"{nvme.UnsafeShutdowns:N0}"));

                    if (nvme.CriticalWarning != 0 ||
                        nvme.AvailableSparePercent <
                            nvme.AvailableSpareThresholdPercent)
                    {
                        rows.Add(new InformationRow(
                            SettingsService.L(
                                "Рекомендация", "Recommendation"),
                            SettingsService.L(
                                "Накопитель сообщает о критическом предупреждении или недостатке резерва. Проверьте резервную копию важных данных.",
                                "The drive reports a critical warning or low spare capacity. Check your backup of important data.")));
                    }
                }
                else
                {
                    rows.Add(new InformationRow(
                        SettingsService.L("NVMe SMART", "NVMe SMART"),
                        SettingsService.L(
                            "Контроллер не предоставил журнал SMART/Health.",
                            "The controller did not provide a SMART/Health log.")));
                }
            }

            // Read-only, evidence-based conclusion. A missing SMART value is
            // never turned into zero or used to claim a healthy drive.
            StorageConditionResult condition =
                EvaluateStorageCondition(details, isReading);

            Border card = CreateInformationCard(
                SettingsService.L(
                    $"Накопитель {i + 1}",
                    $"Drive {i + 1}"),
                rows);

            StackPanel section = new();
            section.Children.Add(CreateStorageConditionBanner(condition));
            section.Children.Add(card);

            if (!isReading && details?.AtaSmart is { Attributes.Count: > 0 } rawAta)
                section.Children.Add(CreateAtaSmartAttributesPanel(rawAta));

            StorageDevicesPanel.Children.Add(section);
        }
    }


    // The complete RAW table is available on every confirmed ATA/SATA disk,
    // including unknown models. No unverified percentage/sector interpretation.
    private static Expander CreateAtaSmartAttributesPanel(
        StorageAtaSmartDetails smart)
    {
        string title = SettingsService.L(
            $"Технические SMART-данные ({smart.Attributes.Count} атрибутов)",
            $"Technical SMART data ({smart.Attributes.Count} attributes)");

        TextBlock header = new()
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(4, 3, 4, 3)
        };
        header.SetResourceReference(TextBlock.ForegroundProperty,
            "AccentBrush");

        TextBlock explanation = new()
        {
            Text = SettingsService.L(
                "RAW — исходные данные (HEX), зависящие от производителя. ПОРОГ задаёт прошивка, ! — текущее значение достигло порога. Отсутствующие данные не считаются нулём.",
                "RAW values (HEX) are vendor-defined. THR is the firmware threshold; ! means the current value reached it. Missing data are not treated as zero."),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        };
        explanation.SetResourceReference(TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        List<string> lines = new()
        {
            SettingsService.L(
                " ID  ТЕК. ХУДШ. ПОРОГ    RAW (HEX)",
                " ID  CURR WORST THR      RAW (HEX)")
        };
        foreach (StorageAtaSmartAttribute attribute in smart.Attributes)
        {
            string threshold = attribute.Threshold is byte value
                ? $"{value,3}"
                : "  -";
            string failure = attribute.Threshold is byte limit &&
                             limit > 0 && attribute.Current <= limit
                ? " !"
                : "  ";
            lines.Add($" {attribute.Id:X2}   {attribute.Current,3}   " +
                      $"{attribute.Worst,3}  {threshold}  " +
                      $"0x{attribute.RawValue:X12}{failure}");
        }

        TextBlock values = new()
        {
            Text = string.Join(Environment.NewLine, lines),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12.5,
            FontWeight = FontWeights.Medium
        };
        values.SetResourceReference(TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        // The parent Storage tab already owns vertical scrolling. An inner
        // ScrollViewer (even with its vertical bar disabled) intercepts wheel
        // events over the SMART table and makes the whole page feel stuck.
        // The compact monospace columns fit directly in the content panel.
        StackPanel content = new();
        content.Children.Add(explanation);
        content.Children.Add(values);

        Border contents = new()
        {
            Padding = new Thickness(14, 10, 14, 12),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = content
        };
        contents.SetResourceReference(Border.BackgroundProperty,
            "CardBackgroundBrush");
        contents.SetResourceReference(Border.BorderBrushProperty,
            "BorderBrush");

        Expander expander = new()
        {
            Header = header,
            Content = contents,
            IsExpanded = false,
            Margin = new Thickness(0, 0, 0, 10),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        expander.SetResourceReference(Control.ForegroundProperty,
            "AccentBrush");
        return expander;
    }

    // NVMe data-unit counters are unsigned 128-bit values. One data unit is
    // 1,000 x 512 bytes (decimal 512,000 B), per the NVMe SMART/Health log.
    // Use integer arithmetic to avoid floating-point precision loss.
    private static string FormatNvmeHostData(System.Numerics.BigInteger dataUnits)
    {
        System.Numerics.BigInteger gigabytes =
            dataUnits * 512_000 / 1_000_000_000;
        return SettingsService.L(
            $"{gigabytes:N0} ГБ", $"{gigabytes:N0} GB");
    }

    // The condition describes only evidence available from this read.
    // Endurance percentages, power-on hours and unsafe shutdown counts do not
    // by themselves imply an imminent failure or a synthetic health score.
    private enum StorageCondition
    {
        Normal,
        Attention,
        Critical,
        Insufficient
    }

    private sealed record StorageConditionResult(
        StorageCondition Condition,
        string Description);

    private static StorageConditionResult EvaluateStorageCondition(
        StorageReliabilityDetails? details,
        bool isReading)
    {
        if (isReading)
            return new StorageConditionResult(
                StorageCondition.Insufficient,
                SettingsService.L(
                    "Идёт чтение SMART и показателей Windows.",
                    "Reading SMART and Windows storage counters."));

        if (details == null)
            return new StorageConditionResult(
                StorageCondition.Insufficient,
                SettingsService.L(
                    "Накопитель не предоставил данных для оценки состояния.",
                    "The drive did not provide enough data to assess its condition."));

        if (details.NvmeHealth is { } nvme)
        {
            // NVMe critical warning: spare, reliability, read-only, backup
            // device. Temperature warning is handled as attention below.
            if ((nvme.CriticalWarning & 0x1D) != 0 ||
                nvme.AvailableSparePercent <=
                    nvme.AvailableSpareThresholdPercent)
            {
                return new StorageConditionResult(
                    StorageCondition.Critical,
                    SettingsService.L(
                        "Контроллер сообщает о серьёзном предупреждении или исчерпании резерва. Сохраните важные данные и проверьте накопитель.",
                        "The controller reports a serious warning or depleted spare capacity. Back up important data and check the drive."));
            }

            if (nvme.CriticalWarning != 0)
                return new StorageConditionResult(
                    StorageCondition.Attention,
                    SettingsService.L(
                        "Контроллер сообщает о предупреждении SMART. Проверьте его подробности ниже.",
                        "The controller reports a SMART warning. Review the details below."));

            if (nvme.MediaErrors > 0)
                return new StorageConditionResult(
                    StorageCondition.Attention,
                    SettingsService.L(
                        "NVMe зафиксировал ошибки носителя. Проверьте резервную копию и наблюдайте за счётчиком ошибок.",
                        "NVMe has recorded media errors. Check your backup and monitor the error count."));

            if (nvme.PercentageUsed >= 100)
                return new StorageConditionResult(
                    StorageCondition.Attention,
                    SettingsService.L(
                        "Накопитель сообщил об использовании расчётного ресурса записи. Это не означает немедленный отказ.",
                        "The drive reports that its estimated write endurance has been used. This does not mean immediate failure."));

            if (details.ReadErrorsUncorrected is > 0 ||
                details.WriteErrorsUncorrected is > 0)
                return new StorageConditionResult(
                    StorageCondition.Attention,
                    SettingsService.L(
                        "Windows сообщает о неисправленных ошибках чтения или записи. Проверьте накопитель и резервную копию.",
                        "Windows reports uncorrected read or write errors. Check the drive and your backup."));

            return new StorageConditionResult(
                StorageCondition.Normal,
                SettingsService.L(
                    $"Нет критических предупреждений и ошибок носителя. Резерв {nvme.AvailableSparePercent}%, использованный ресурс {nvme.PercentageUsed}%.",
                    $"No critical warnings or media errors. Spare {nvme.AvailableSparePercent}%, endurance used {nvme.PercentageUsed}%."));
        }

        // SMART RETURN STATUS / WMI PredictFailure is a firmware-provided
        // verdict, not a model-name heuristic. Check it before other ATA
        // indicators; a predicted failure must not be hidden by zero RAWs.
        if (details.BusTypeCode is 3 or 11 &&
            details.AtaSmartPredictedFailure == true)
            return new StorageConditionResult(
                StorageCondition.Critical,
                SettingsService.L(
                    "Сам накопитель прогнозирует отказ по SMART. Сохраните важные данные и проверьте диск.",
                    "The drive itself predicts a SMART failure. Back up important data and check the drive."));

        if (details.BusTypeCode is 3 or 11 &&
            details.AtaSmart is { Attributes.Count: > 0 } ataThresholds)
        {
            StorageAtaSmartAttribute? failed =
                ataThresholds.Attributes.FirstOrDefault(a =>
                    a.Threshold is byte limit &&
                    limit > 0 && a.Current <= limit);
            if (failed != null)
            {
                bool preFailure = (failed.Flags & 0x0001) != 0;
                return new StorageConditionResult(
                    preFailure ? StorageCondition.Critical :
                        StorageCondition.Attention,
                    SettingsService.L(
                        $"SMART-атрибут {failed.Id:X2} достиг порога производителя (текущее {failed.Current}, порог {failed.Threshold}). Проверьте резервную копию и накопитель.",
                        $"SMART attribute {failed.Id:X2} reached its manufacturer threshold (current {failed.Current}, threshold {failed.Threshold}). Check your backup and the drive."));
            }
        }

        // Windows reliability counters can indicate a problem even if ATA
        // sector attributes are unavailable.
        if (details.ReadErrorsUncorrected is > 0 ||
            details.WriteErrorsUncorrected is > 0)
            return new StorageConditionResult(
                StorageCondition.Attention,
                SettingsService.L(
                    "Windows обнаружила неисправленные ошибки чтения или записи. Проверьте данные и резервную копию.",
                    "Windows reports uncorrected read or write errors. Check your data and backup."));

        if (details.MediaTypeCode == 3)
        {
            if (details.AtaSmart is not { } smart || !smart.HasAnyData)
                return new StorageConditionResult(
                    StorageCondition.Insufficient,
                    SettingsService.L(
                        "Секторные SMART-счётчики HDD недоступны.",
                        "The HDD's sector-related SMART counters are unavailable."));

            if (smart.ReallocatedSectors is > 0 ||
                smart.PendingSectors is > 0 ||
                smart.UncorrectableSectors is > 0)
                return new StorageConditionResult(
                    StorageCondition.Attention,
                    SettingsService.L(
                        "Есть проблемные секторные SMART-счётчики (05, C5 или C6). Проверьте диск и резервную копию.",
                        "One or more sector-related SMART counters (05, C5 or C6) are nonzero. Check the drive and your backup."));

            if (smart.ReallocatedSectors == 0 &&
                smart.PendingSectors == 0 &&
                smart.UncorrectableSectors == 0)
                return new StorageConditionResult(
                    StorageCondition.Normal,
                    SettingsService.L(
                        "По проверенным SMART-счётчикам 05, C5 и C6 проблемных секторов не обнаружено.",
                        "No problematic sectors were reported by the checked SMART counters 05, C5 and C6."));

            return new StorageConditionResult(
                StorageCondition.Insufficient,
                SettingsService.L(
                    "Часть важных SMART-счётчиков недоступна. Вывод о состоянии пока неполный.",
                    "Some important SMART counters are unavailable. The condition assessment is incomplete."));
        }

        // The family-specific Kingston A400 rule identifies SSD Life Left
        // from E7 RAW. A life percentage alone is not a health assessment:
        // corroborate it with the confirmed family's uncorrectable-error
        // (01) and reallocation-event (C4) counters. Never assume missing
        // attributes have a RAW value of zero.
        if (details.AtaSmart is { KingstonA400LifeRemainingPercent: byte kingstonLifeLeft } kingstonSmart)
        {
            StorageAtaSmartAttribute? uncorrectableErrors =
                kingstonSmart.Attributes.FirstOrDefault(a => a.Id == 0x01);
            StorageAtaSmartAttribute? reallocationEvents =
                kingstonSmart.Attributes.FirstOrDefault(a => a.Id == 0xC4);

            if (uncorrectableErrors?.RawValue > 0 ||
                reallocationEvents?.RawValue > 0)
                return new StorageConditionResult(
                    StorageCondition.Attention,
                    SettingsService.L(
                        "SMART Kingston A400 сообщает о неисправимых ошибках (01) или событиях переназначения (C4). Проверьте резервную копию и накопитель.",
                        "Kingston A400 SMART reports uncorrectable errors (01) or reallocation events (C4). Check the drive and your backup."));

            if (kingstonLifeLeft <= 10)
                return new StorageConditionResult(
                    StorageCondition.Attention,
                    SettingsService.L(
                        $"По SMART E7 осталось {kingstonLifeLeft}% расчётного ресурса записи. Рекомендуется проверить резервную копию и запланировать проверку SSD.",
                        $"SMART E7 reports {kingstonLifeLeft}% of rated write endurance remaining. Check your backup and plan an SSD review."));

            if (uncorrectableErrors?.RawValue == 0 &&
                reallocationEvents?.RawValue == 0)
                return new StorageConditionResult(
                    StorageCondition.Normal,
                    SettingsService.L(
                        $"По проверенным SMART-показателям: ресурс записи {kingstonLifeLeft}% (E7), неисправимых ошибок (01) и переназначений (C4) нет. Это не гарантия отсутствия других неисправностей.",
                        $"Checked SMART indicators: {kingstonLifeLeft}% write endurance remains (E7), with no uncorrectable errors (01) or reallocation events (C4). This does not rule out other failures."));

            return new StorageConditionResult(
                StorageCondition.Insufficient,
                SettingsService.L(
                    $"По SMART E7 осталось {kingstonLifeLeft}% расчётного ресурса записи, но показатели ошибок 01 или C4 недоступны.",
                    $"SMART E7 reports {kingstonLifeLeft}% of rated write endurance remaining, but error indicators 01 or C4 are unavailable."));
        }

        // Generic SATA/ATA SSD support: firmware says no failure is
        // predicted and some actual SMART attributes were readable. Do not
        // pretend this guarantees full physical health or 100% endurance.
        if (details.BusTypeCode is 3 or 11 &&
            details.MediaTypeCode == 4 &&
            details.AtaSmartPredictedFailure == false &&
            details.AtaSmart is { Attributes.Count: > 0 })
            return new StorageConditionResult(
                StorageCondition.Normal,
                SettingsService.L(
                    "Накопитель не прогнозирует отказ по SMART; доступные пороговые показатели не нарушены. Это не гарантия отсутствия других неисправностей.",
                    "The drive reports no predicted SMART failure; available normalized thresholds are not breached. This does not rule out other faults."));

        // Windows fallback wear is evaluated only for an NVMe device.
        // A SATA SSD can report 0% here despite meaningful ATA SMART wear.
        if (details.BusTypeCode == 17 &&
            details.MediaTypeCode == 4 && details.WearPercent is >= 100)
            return new StorageConditionResult(
                StorageCondition.Attention,
                SettingsService.L(
                    "По данным Windows расчётный ресурс записи SSD использован. Нужна дополнительная SMART-проверка.",
                    "Windows reports the SSD's estimated write endurance as used. Further SMART diagnostics are needed."));

        if (details.BusTypeCode == 17 &&
            details.MediaTypeCode == 4 && details.WearPercent is >= 90)
            return new StorageConditionResult(
                StorageCondition.Attention,
                SettingsService.L(
                    "Windows сообщает о значительном износе памяти SSD. Проверьте резервную копию.",
                    "Windows reports substantial SSD memory wear. Check your backup."));

        return new StorageConditionResult(
            StorageCondition.Insufficient,
            SettingsService.L(
                "Температуры, часов работы или одного процента износа недостаточно для надёжной SMART-оценки.",
                "Temperature, power-on hours or a wear percentage alone are insufficient for a reliable SMART assessment."));
    }


    private static Border CreateStorageConditionBanner(
        StorageConditionResult result)
    {
        (string stateText, Color color) = result.Condition switch
        {
            StorageCondition.Normal => (
                SettingsService.L("Норма", "Normal"),
                Color.FromRgb(99, 205, 150)),
            StorageCondition.Attention => (
                SettingsService.L("Требует внимания", "Needs attention"),
                Color.FromRgb(242, 181, 68)),
            StorageCondition.Critical => (
                SettingsService.L("Критично", "Critical"),
                Color.FromRgb(244, 108, 108)),
            _ => (
                SettingsService.L("Недостаточно данных", "Insufficient data"),
                Color.FromRgb(172, 185, 198))
        };

        Border banner = new()
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(15, 12, 15, 12),
            Margin = new Thickness(0, 0, 0, 8),
            BorderBrush = new SolidColorBrush(
                Color.FromArgb(165, color.R, color.G, color.B))
        };
        banner.SetResourceReference(
            Border.BackgroundProperty, "CardBackgroundBrush");

        StackPanel content = new();
        StackPanel heading = new() { Orientation = Orientation.Horizontal };

        Ellipse marker = new()
        {
            Width = 9,
            Height = 9,
            Margin = new Thickness(0, 0, 9, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Fill = new SolidColorBrush(color)
        };
        heading.Children.Add(marker);

        TextBlock status = new()
        {
            Text = SettingsService.L(
                $"СОСТОЯНИЕ НАКОПИТЕЛЯ: {stateText.ToUpperInvariant()}",
                $"DRIVE CONDITION: {stateText.ToUpperInvariant()}"),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(color),
            TextWrapping = TextWrapping.Wrap
        };
        heading.Children.Add(status);
        content.Children.Add(heading);

        TextBlock description = new()
        {
            Text = result.Description,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(18, 5, 0, 0)
        };
        description.SetResourceReference(
            TextBlock.ForegroundProperty, "PrimaryTextBrush");
        content.Children.Add(description);

        banner.Child = content;
        return banner;
    }


    private static string DescribeNvmeCriticalWarning(byte warning)
    {
        if (warning == 0)
            return SettingsService.L("Нет", "None");

        List<string> reasons = new();
        if ((warning & 0x01) != 0)
            reasons.Add(SettingsService.L(
                "Мало резервной памяти", "Low spare capacity"));
        if ((warning & 0x02) != 0)
            reasons.Add(SettingsService.L(
                "Температура за пределами нормы", "Temperature warning"));
        if ((warning & 0x04) != 0)
            reasons.Add(SettingsService.L(
                "Снижена надёжность", "Reliability degraded"));
        if ((warning & 0x08) != 0)
            reasons.Add(SettingsService.L(
                "Только чтение", "Read-only mode"));
        if ((warning & 0x10) != 0)
            reasons.Add(SettingsService.L(
                "Ошибка резервного питания", "Backup device failure"));
        if ((warning & 0xE0) != 0)
            reasons.Add(SettingsService.L(
                "Неизвестный сигнал", "Unrecognized warning"));

        return string.Join("; ", reasons) + $" (0x{warning:X2})";
    }

    private static string? GetStorageBusDescription(ushort? code)
    {
        return code switch
        {
            1 => "SCSI",
            2 => "ATAPI",
            3 => "ATA",
            4 => "IEEE 1394",
            6 => "Fibre Channel",
            7 => "USB",
            8 => "RAID",
            9 => "iSCSI",
            10 => "SAS",
            11 => "SATA",
            12 => "SD",
            13 => "MMC",
            15 => SettingsService.L("Виртуальный диск", "Virtual disk"),
            16 => "Storage Spaces",
            17 => "NVMe",
            19 => "UFS",
            _ => null
        };
    }


    private static string? GetStorageMediaDescription(
        ushort? mediaType, ushort? busType)
    {
        if (busType == 17)
            return "NVMe SSD";

        return mediaType switch
        {
            3 => "HDD",
            4 => "SSD",
            5 => "SCM",
            _ => null
        };
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



}
