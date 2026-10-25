using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace PCHardwareMonitor;

public enum StatisticsChartMetric
{
    CpuTemperature,
    CpuLoad,
    RamLoad,
    GpuTemperature,
    GpuLoad,
    GpuHotSpotTemperature,
    GpuMemoryTemperature,
    StorageTemperature
}


public sealed class StatisticsChartSeries
{
    public StatisticsChartMetric Metric { get; set; }

    public string DeviceId { get; set; } = "";

    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset EndUtc { get; set; }

    public List<StatisticsChartPoint> Points { get; } =
        new();
}


public sealed class StatisticsChartPoint
{
    public DateTimeOffset TimestampUtc { get; set; }

    public double Value { get; set; }
}


public sealed class StatisticsExportRow
{
    public DateTimeOffset TimestampUtc { get; set; }

    public string Category { get; set; } = "";

    public string DeviceId { get; set; } = "";

    public string DeviceName { get; set; } = "";

    public string Metric { get; set; } = "";

    public double Value { get; set; }

    public string Unit { get; set; } = "";
}


public sealed class HistoryAnalyticsResult
{
    public StatisticsPeriod Period { get; set; }

    public DateTimeOffset CurrentStartUtc { get; set; }

    public DateTimeOffset CurrentEndUtc { get; set; }

    public DateTimeOffset PreviousStartUtc { get; set; }

    public DateTimeOffset PreviousEndUtc { get; set; }

    public int CurrentSampleCount { get; set; }

    public int PreviousSampleCount { get; set; }

    public MetricStatistics CurrentCpuTemperature { get; set; } =
        new();

    public MetricStatistics PreviousCpuTemperature { get; set; } =
        new();

    public MetricStatistics CurrentCpuLoad { get; set; } =
        new();

    public MetricStatistics PreviousCpuLoad { get; set; } =
        new();

    public MetricStatistics CurrentRamLoad { get; set; } =
        new();

    public MetricStatistics PreviousRamLoad { get; set; } =
        new();

    public AlertEventCounts CurrentAlerts { get; set; } =
        new();

    public AlertEventCounts PreviousAlerts { get; set; } =
        new();

    public WeeklyHistorySummary Last7Days { get; set; } =
        new();

    public List<RecurringAlertSummary> RecurringAlerts { get; } =
        new();
}


public sealed class WeeklyHistorySummary
{
    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset EndUtc { get; set; }

    public int SampleCount { get; set; }

    public int DaysWithSamples { get; set; }

    public MetricStatistics CpuTemperature { get; set; } =
        new();

    public MetricStatistics CpuLoad { get; set; } =
        new();

    public MetricStatistics RamLoad { get; set; } =
        new();

    public AlertEventCounts Alerts { get; set; } =
        new();
}


public sealed class RecurringAlertSummary
{
    public string EventKey { get; set; } = "";

    public StatisticsAlertLevel Level { get; set; }

    public string DeviceName { get; set; } = "";

    public string Subject { get; set; } = "";

    public int Count { get; set; }

    public double MaximumValue { get; set; }

    public string Unit { get; set; } = "";

    public DateTimeOffset LastAtUtc { get; set; }
}


public enum StatisticsPeriod
{
    Today,
    Last24Hours,
    Last7Days,
    Last30Days
}


public sealed class StatisticsSummary
{
    public StatisticsPeriod Period { get; set; }

    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset EndUtc { get; set; }

    public int SampleCount { get; set; }

    public MetricStatistics CpuTemperature { get; set; } =
        new();

    public MetricStatistics CpuLoad { get; set; } =
        new();

    public MetricStatistics RamLoad { get; set; } =
        new();

    public List<GpuStatisticsSummary> Gpus { get; } =
        new();

    public List<StorageStatisticsSummary> StorageDevices { get; } =
        new();
}


public sealed class MetricStatistics
{
    public double? Average { get; set; }

    public double? Maximum { get; set; }

    public DateTimeOffset? MaximumAtUtc { get; set; }
}


public sealed class GpuStatisticsSummary
{
    public string DeviceId { get; set; } = "";

    public string DeviceName { get; set; } = "";

    public MetricStatistics Temperature { get; set; } =
        new();

    public MetricStatistics Load { get; set; } =
        new();

    public MetricStatistics HotSpotTemperature { get; set; } =
        new();

    public MetricStatistics MemoryTemperature { get; set; } =
        new();

    public MetricStatistics MemoryLoad { get; set; } =
        new();
}


public sealed class StorageStatisticsSummary
{
    public string DeviceId { get; set; } = "";

    public string DeviceName { get; set; } = "";

    public MetricStatistics Temperature { get; set; } =
        new();
}


public enum StatisticsAlertLevel
{
    Warning = 1,
    Critical = 2
}


public sealed class AlertEventCounts
{
    public int WarningCount { get; set; }

    public int CriticalCount { get; set; }

    public int TotalCount =>
        WarningCount + CriticalCount;
}


public sealed class StatisticsAlertEvent
{
    public DateTimeOffset TimestampUtc { get; set; }

    public string EventKey { get; set; } = "";

    public StatisticsAlertLevel Level { get; set; }

    public string DeviceName { get; set; } = "";

    public string Subject { get; set; } = "";

    public double Value { get; set; }

    public string Unit { get; set; } = "";
}


public sealed class AllTimeRecord
{
    public string RecordKey { get; set; } = "";

    public string Category { get; set; } = "";

    public string DeviceId { get; set; } = "";

    public string DeviceName { get; set; } = "";

    public double Value { get; set; }

    public DateTimeOffset TimestampUtc { get; set; }

    public string Unit { get; set; } = "";
}


public sealed class AdvisorPerformanceHistory
{
    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset EndUtc { get; set; }

    public List<AdvisorGpuPerformanceSeries> Gpus { get; } =
        new();
}


public sealed class AdvisorGpuPerformanceSeries
{
    public string DeviceId { get; set; } = "";

    public string DeviceName { get; set; } = "";

    public List<AdvisorPerformancePoint> Points { get; } =
        new();
}


public sealed class AdvisorPerformancePoint
{
    public DateTimeOffset TimestampUtc { get; set; }

    public double CpuLoad { get; set; }

    public double? CpuTemperature { get; set; }

    public double GpuLoad { get; set; }

    public double? GpuTemperature { get; set; }
}


public enum AdvisorEvidenceKind
{
    CpuLimited,
    CpuCooling,
    GpuCooling
}


public sealed class AdvisorEvidenceSpan
{
    public bool HasEvidence { get; set; }

    public DateTime FirstDate { get; set; }

    public DateTime LastDate { get; set; }

    public int CalendarSpanDays { get; set; }

    public int DaysWithEvidence { get; set; }
}


internal sealed class DeviceDescriptor
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";
}
