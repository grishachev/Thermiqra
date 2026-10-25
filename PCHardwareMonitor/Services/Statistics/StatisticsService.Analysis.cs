using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace PCHardwareMonitor;

public sealed partial class StatisticsService
{
    public StatisticsSummary GetSummary(
        StatisticsPeriod period)
    {
        DateTimeOffset endUtc =
            DateTimeOffset.UtcNow;

        DateTimeOffset startUtc =
            GetPeriodStartUtc(
                period,
                endUtc);

        using SqliteConnection connection =
            OpenConnection();

        StatisticsSummary summary =
            new()
            {
                Period = period,
                StartUtc = startUtc,
                EndUtc = endUtc,
                SampleCount = GetMainSampleCount(
                    connection,
                    startUtc,
                    endUtc),
                CpuTemperature = ReadMainMetric(
                    connection,
                    "CpuTemperature",
                    startUtc,
                    endUtc),
                CpuLoad = ReadMainMetric(
                    connection,
                    "CpuLoad",
                    startUtc,
                    endUtc),
                RamLoad = ReadMainMetric(
                    connection,
                    "RamLoad",
                    startUtc,
                    endUtc)
            };

        foreach (DeviceDescriptor device in
                 ReadDevices(
                     connection,
                     "GpuSamples",
                     startUtc,
                     endUtc))
        {
            summary.Gpus.Add(
                new GpuStatisticsSummary
                {
                    DeviceId = device.Id,
                    DeviceName = device.Name,
                    Temperature = ReadDeviceMetric(
                        connection,
                        "GpuSamples",
                        "Temperature",
                        device.Id,
                        startUtc,
                        endUtc),
                    Load = ReadDeviceMetric(
                        connection,
                        "GpuSamples",
                        "Load",
                        device.Id,
                        startUtc,
                        endUtc),
                    HotSpotTemperature = ReadDeviceMetric(
                        connection,
                        "GpuSamples",
                        "HotSpotTemperature",
                        device.Id,
                        startUtc,
                        endUtc),
                    MemoryTemperature = ReadDeviceMetric(
                        connection,
                        "GpuSamples",
                        "MemoryTemperature",
                        device.Id,
                        startUtc,
                        endUtc),
                    MemoryLoad = ReadDeviceMetric(
                        connection,
                        "GpuSamples",
                        "MemoryLoad",
                        device.Id,
                        startUtc,
                        endUtc)
                });
        }

        foreach (DeviceDescriptor device in
                 ReadDevices(
                     connection,
                     "StorageSamples",
                     startUtc,
                     endUtc))
        {
            summary.StorageDevices.Add(
                new StorageStatisticsSummary
                {
                    DeviceId = device.Id,
                    DeviceName = device.Name,
                    Temperature = ReadDeviceMetric(
                        connection,
                        "StorageSamples",
                        "Temperature",
                        device.Id,
                        startUtc,
                        endUtc)
                });
        }

        return summary;
    }


    public HistoryAnalyticsResult GetHistoryAnalytics(
        StatisticsPeriod period)
    {
        DateTimeOffset currentEndUtc =
            DateTimeOffset.UtcNow;

        DateTimeOffset currentStartUtc =
            GetPeriodStartUtc(
                period,
                currentEndUtc);

        long currentStartUnix =
            currentStartUtc.ToUnixTimeSeconds();

        long currentEndUnix =
            currentEndUtc.ToUnixTimeSeconds();

        long durationSeconds =
            Math.Max(
                1,
                currentEndUnix -
                currentStartUnix);

        DateTimeOffset previousEndUtc =
            DateTimeOffset.FromUnixTimeSeconds(
                currentStartUnix - 1);

        DateTimeOffset previousStartUtc =
            DateTimeOffset.FromUnixTimeSeconds(
                currentStartUnix -
                durationSeconds -
                1);

        DateTimeOffset weekEndUtc =
            currentEndUtc;

        DateTimeOffset weekStartUtc =
            weekEndUtc.Subtract(
                TimeSpan.FromDays(7));

        using SqliteConnection connection =
            OpenConnection();

        HistoryAnalyticsResult result =
            new()
            {
                Period =
                    period,

                CurrentStartUtc =
                    currentStartUtc,

                CurrentEndUtc =
                    currentEndUtc,

                PreviousStartUtc =
                    previousStartUtc,

                PreviousEndUtc =
                    previousEndUtc,

                CurrentSampleCount =
                    GetMainSampleCount(
                        connection,
                        currentStartUtc,
                        currentEndUtc),

                PreviousSampleCount =
                    GetMainSampleCount(
                        connection,
                        previousStartUtc,
                        previousEndUtc),

                CurrentCpuTemperature =
                    ReadMainMetric(
                        connection,
                        "CpuTemperature",
                        currentStartUtc,
                        currentEndUtc),

                PreviousCpuTemperature =
                    ReadMainMetric(
                        connection,
                        "CpuTemperature",
                        previousStartUtc,
                        previousEndUtc),

                CurrentCpuLoad =
                    ReadMainMetric(
                        connection,
                        "CpuLoad",
                        currentStartUtc,
                        currentEndUtc),

                PreviousCpuLoad =
                    ReadMainMetric(
                        connection,
                        "CpuLoad",
                        previousStartUtc,
                        previousEndUtc),

                CurrentRamLoad =
                    ReadMainMetric(
                        connection,
                        "RamLoad",
                        currentStartUtc,
                        currentEndUtc),

                PreviousRamLoad =
                    ReadMainMetric(
                        connection,
                        "RamLoad",
                        previousStartUtc,
                        previousEndUtc),

                CurrentAlerts =
                    ReadAlertEventCounts(
                        connection,
                        currentStartUtc,
                        currentEndUtc),

                PreviousAlerts =
                    ReadAlertEventCounts(
                        connection,
                        previousStartUtc,
                        previousEndUtc),

                Last7Days =
                    new WeeklyHistorySummary
                    {
                        StartUtc =
                            weekStartUtc,

                        EndUtc =
                            weekEndUtc,

                        SampleCount =
                            GetMainSampleCount(
                                connection,
                                weekStartUtc,
                                weekEndUtc),

                        DaysWithSamples =
                            ReadDaysWithSamples(
                                connection,
                                weekStartUtc,
                                weekEndUtc),

                        CpuTemperature =
                            ReadMainMetric(
                                connection,
                                "CpuTemperature",
                                weekStartUtc,
                                weekEndUtc),

                        CpuLoad =
                            ReadMainMetric(
                                connection,
                                "CpuLoad",
                                weekStartUtc,
                                weekEndUtc),

                        RamLoad =
                            ReadMainMetric(
                                connection,
                                "RamLoad",
                                weekStartUtc,
                                weekEndUtc),

                        Alerts =
                            ReadAlertEventCounts(
                                connection,
                                weekStartUtc,
                                weekEndUtc)
                    }
            };

        result.RecurringAlerts.AddRange(
            ReadRecurringAlertSummaries(
                connection,
                currentStartUtc,
                currentEndUtc,
                6));

        return result;
    }


    public StatisticsChartSeries GetChartSeries(
        StatisticsPeriod period,
        StatisticsChartMetric metric,
        string? deviceId = null,
        int maxPoints = 520)
    {
        maxPoints =
            Math.Clamp(
                maxPoints,
                60,
                1200);

        DateTimeOffset endUtc =
            DateTimeOffset.UtcNow;

        DateTimeOffset startUtc =
            GetPeriodStartUtc(
                period,
                endUtc);

        using SqliteConnection connection =
            OpenConnection();

        List<StatisticsChartPoint> points =
            metric switch
            {
                StatisticsChartMetric.CpuTemperature =>
                    ReadMainChartPoints(
                        connection,
                        "CpuTemperature",
                        startUtc,
                        endUtc),

                StatisticsChartMetric.CpuLoad =>
                    ReadMainChartPoints(
                        connection,
                        "CpuLoad",
                        startUtc,
                        endUtc),

                StatisticsChartMetric.RamLoad =>
                    ReadMainChartPoints(
                        connection,
                        "RamLoad",
                        startUtc,
                        endUtc),

                StatisticsChartMetric.GpuTemperature =>
                    ReadDeviceChartPoints(
                        connection,
                        "GpuSamples",
                        "Temperature",
                        deviceId,
                        startUtc,
                        endUtc),

                StatisticsChartMetric.GpuLoad =>
                    ReadDeviceChartPoints(
                        connection,
                        "GpuSamples",
                        "Load",
                        deviceId,
                        startUtc,
                        endUtc),

                StatisticsChartMetric.GpuHotSpotTemperature =>
                    ReadDeviceChartPoints(
                        connection,
                        "GpuSamples",
                        "HotSpotTemperature",
                        deviceId,
                        startUtc,
                        endUtc),

                StatisticsChartMetric.GpuMemoryTemperature =>
                    ReadDeviceChartPoints(
                        connection,
                        "GpuSamples",
                        "MemoryTemperature",
                        deviceId,
                        startUtc,
                        endUtc),

                StatisticsChartMetric.StorageTemperature =>
                    ReadDeviceChartPoints(
                        connection,
                        "StorageSamples",
                        "Temperature",
                        deviceId,
                        startUtc,
                        endUtc),

                _ =>
                    new List<StatisticsChartPoint>()
            };

        StatisticsChartSeries series =
            new()
            {
                Metric = metric,
                DeviceId = deviceId ?? "",
                StartUtc = startUtc,
                EndUtc = endUtc
            };

        series.Points.AddRange(
            DownsampleChartPoints(
                points,
                maxPoints));

        return series;
    }



}
