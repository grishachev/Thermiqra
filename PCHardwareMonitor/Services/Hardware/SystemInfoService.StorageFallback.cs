using System;
using System.Collections.Generic;

namespace PCHardwareMonitor;

public sealed partial class SystemInfoService
{
    private static void ReadFailurePredictionFallback(Dictionary<uint, StorageReliabilityDetails> result,
        IReadOnlyList<StorageDeviceDetails> devices)
    {
        foreach (var device in devices)
        {
            if (device.PhysicalDiskIndex is not uint number) continue;
            if (!result.TryGetValue(number, out var details))
                result.Add(number, details = new StorageReliabilityDetails());
            // A missing Windows Storage provider must not suppress the native
            // fallback. The reader verifies native bus and identity itself.
            if ((details.BusTypeCode.HasValue && details.BusTypeCode is not (3 or 11)) ||
                (details.AtaSmart?.HasAnyData == true && details.AtaSmartPredictedFailure.HasValue)) continue;
            var response = StorageFailurePredictionReader.Read(device);
            details.DiagnosticMessages.Add(response.Diagnostic);
            details.BusTypeCode ??= response.ConfirmedBus;
            if (response.PredictFailure is bool failure)
                details.AtaSmartPredictedFailure = details.AtaSmartPredictedFailure == true || failure;
            if (details.AtaSmart?.HasAnyData != true && response.AtaData != null)
            {
                var smart = StorageAtaSmartInterpreter.Parse(response.AtaData, device.Model, details.MediaTypeCode);
                if (smart?.HasAnyData == true)
                {
                    details.AtaSmart = smart;
                    details.AtaSmartSource = "IOCTL_STORAGE_PREDICT_FAILURE";
                }
            }
        }
    }
}
