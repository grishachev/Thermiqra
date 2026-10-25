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



}
