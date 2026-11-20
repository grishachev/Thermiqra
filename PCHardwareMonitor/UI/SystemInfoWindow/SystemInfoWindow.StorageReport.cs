using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PCHardwareMonitor;

public partial class SystemInfoWindow
{
    private void AppendStorageHealthReport(StringBuilder text, IReadOnlyList<StorageDeviceDetails> devices)
    {
        text.AppendLine(SettingsService.L("[СОСТОЯНИЕ НАКОПИТЕЛЕЙ / SMART]", "[DRIVE CONDITION / SMART]"));
        // Export exactly the completed read used by the visible condition
        // banner. Do not make a second device query while saving the report.
        if (_storageReliabilityLoading)
        {
            text.AppendLine(SettingsService.L("Чтение SMART ещё выполняется; оценка состояния не завершена.",
                "SMART is still being read; condition assessment is incomplete."));
            return;
        }
        if (_storageReliabilityForReport == null)
        {
            text.AppendLine(SettingsService.L("Результат проверки состояния пока отсутствует.",
                "No condition inspection result is available yet."));
            return;
        }
        foreach (var device in devices)
        {
            text.AppendLine();
            text.AppendLine(device.Model ?? SettingsService.L("Неизвестный накопитель", "Unknown drive"));
            text.AppendLine($"  PhysicalDrive: {device.PhysicalDiskIndex?.ToString() ?? "—"}; PNP: {device.PnpDeviceId ?? "—"}");
            StorageReliabilityDetails? details = null;
            if (device.PhysicalDiskIndex is uint index) _storageReliabilityForReport.TryGetValue(index, out details);
            text.AppendLine("  " + SettingsService.L("Подключение", "Connection") + ": " +
                (GetStorageBusDescription(details?.BusTypeCode) ?? "—") + $" [{details?.BusTypeCode?.ToString() ?? "—"}]");
            text.AppendLine("  " + SettingsService.L("Тип носителя", "Media type") + ": " +
                (GetStorageMediaDescription(details?.MediaTypeCode, details?.BusTypeCode) ?? "—"));
            var condition = EvaluateStorageCondition(details, isReading: false);
            string status = condition.Condition switch
            {
                StorageCondition.Normal => SettingsService.L("Норма", "Normal"),
                StorageCondition.Attention => SettingsService.L("Требует внимания", "Needs attention"),
                StorageCondition.Critical => SettingsService.L("Критическое состояние", "Critical"),
                _ => SettingsService.L("Недостаточно данных", "Insufficient data")
            };
            text.AppendLine("  " + SettingsService.L("Состояние", "Condition") + ": " + status);
            text.AppendLine("  " + condition.Description);
            if (details == null) continue;
            text.AppendLine("  " + SettingsService.L("Прогноз отказа ATA SMART", "ATA SMART failure prediction") + ": " +
                (details.AtaSmartPredictedFailure is bool failure
                    ? failure ? SettingsService.L("Да", "Yes") : SettingsService.L("Нет", "No") : "—"));
            text.AppendLine("  " + SettingsService.L("Источник ATA SMART", "ATA SMART source") + ": " +
                (details.AtaSmartSource.Length > 0 ? details.AtaSmartSource : "—"));
            if (details.AtaSmart is { } smart)
            {
                text.AppendLine("  " + SettingsService.L("Семейство SMART", "SMART family") + ": " + (smart.RecognizedFamily ?? "—"));
                text.AppendLine("  ATA SMART: ID CURRENT WORST THRESHOLD RAW (hex)");
                foreach (var attribute in smart.Attributes.OrderBy(a => a.Id))
                    text.AppendLine($"    {attribute.Id:X2} {attribute.Current,3} {attribute.Worst,3} " +
                        $"{attribute.Threshold?.ToString() ?? "—",3} {attribute.RawValue:X12}");
            }
            if (details.NvmeHealth is { } nvme)
                text.AppendLine($"  NVMe: CriticalWarning=0x{nvme.CriticalWarning:X2}; PercentageUsed={nvme.PercentageUsed}; " +
                    $"AvailableSpare={nvme.AvailableSparePercent}; SpareThreshold={nvme.AvailableSpareThresholdPercent}; MediaErrors={nvme.MediaErrors}");
            foreach (string message in details.DiagnosticMessages) text.AppendLine("  " + message);
        }
        if (_storageReadDiagnostics.Count > 0)
        {
            text.AppendLine();
            text.AppendLine(SettingsService.L("[ОШИБКИ ИСТОЧНИКОВ SMART]", "[SMART SOURCE ERRORS]"));
            foreach (string message in _storageReadDiagnostics) text.AppendLine(message);
        }
    }
}
