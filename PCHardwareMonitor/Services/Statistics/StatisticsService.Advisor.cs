using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace PCHardwareMonitor;

public sealed partial class StatisticsService
{
    public StatisticsChartSeries GetAdvisorRamLoadSeries(
        StatisticsPeriod period,
        int maxPoints = 1200)
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

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                TimestampUtc,
                RamLoad
            FROM MonitoringSamples
            WHERE TimestampUtc >= $startUtc
              AND TimestampUtc <= $endUtc
              AND RamLoad IS NOT NULL
              AND BenchmarkClassified = 1
              AND (BenchmarkMask & $benchmarkMask) = 0
            ORDER BY TimestampUtc;
            """;

        AddRangeParameters(
            command,
            startUtc,
            endUtc);

        command.Parameters.AddWithValue(
            "$benchmarkMask",
            (int)BenchmarkWorkloadKind.Memory);

        List<StatisticsChartPoint> points =
            new();

        using SqliteDataReader reader =
            command.ExecuteReader();

        while (reader.Read())
        {
            points.Add(
                new StatisticsChartPoint
                {
                    TimestampUtc =
                        DateTimeOffset.FromUnixTimeSeconds(
                            reader.GetInt64(0)),

                    Value =
                        reader.GetDouble(1)
                });
        }

        StatisticsChartSeries series =
            new()
            {
                Metric =
                    StatisticsChartMetric.RamLoad,

                StartUtc =
                    startUtc,

                EndUtc =
                    endUtc
            };

        series.Points.AddRange(
            DownsampleChartPoints(
                points,
                maxPoints));

        return series;
    }


    public AdvisorPerformanceHistory
        GetAdvisorPerformanceHistory(
            StatisticsPeriod period)
    {
        DateTimeOffset endUtc =
            DateTimeOffset.UtcNow;

        DateTimeOffset startUtc =
            GetPeriodStartUtc(
                period,
                endUtc);

        AdvisorPerformanceHistory result =
            new()
            {
                StartUtc =
                    startUtc,

                EndUtc =
                    endUtc
            };

        using SqliteConnection connection =
            OpenConnection();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                m.TimestampUtc,
                m.CpuLoad,
                m.CpuTemperature,
                g.DeviceId,
                g.DeviceName,
                g.Load,
                g.Temperature
            FROM MonitoringSamples m
            INNER JOIN GpuSamples g
                ON g.SampleId = m.Id
            WHERE m.TimestampUtc >= $startUtc
              AND m.TimestampUtc <= $endUtc
              AND m.CpuLoad IS NOT NULL
              AND g.Load IS NOT NULL
              AND m.BenchmarkClassified = 1
              AND (m.BenchmarkMask & $benchmarkMask) = 0
            ORDER BY
                g.DeviceId,
                m.TimestampUtc;
            """;

        AddRangeParameters(
            command,
            startUtc,
            endUtc);

        command.Parameters.AddWithValue(
            "$benchmarkMask",
            (int)(
                BenchmarkWorkloadKind.Cpu |
                BenchmarkWorkloadKind.Gpu));

        Dictionary<string, AdvisorGpuPerformanceSeries>
            seriesByDevice =
                new(
                    StringComparer.OrdinalIgnoreCase);

        using SqliteDataReader reader =
            command.ExecuteReader();

        while (reader.Read())
        {
            string deviceId =
                reader.GetString(3);

            string deviceName =
                reader.GetString(4);

            if (!seriesByDevice.TryGetValue(
                    deviceId,
                    out AdvisorGpuPerformanceSeries? series))
            {
                series =
                    new AdvisorGpuPerformanceSeries
                    {
                        DeviceId =
                            deviceId,

                        DeviceName =
                            deviceName
                    };

                seriesByDevice.Add(
                    deviceId,
                    series);

                result.Gpus.Add(
                    series);
            }

            series.Points.Add(
                new AdvisorPerformancePoint
                {
                    TimestampUtc =
                        DateTimeOffset.FromUnixTimeSeconds(
                            reader.GetInt64(0)),

                    CpuLoad =
                        reader.GetDouble(1),

                    CpuTemperature =
                        reader.IsDBNull(2)
                            ? null
                            : reader.GetDouble(2),

                    GpuLoad =
                        reader.GetDouble(5),

                    GpuTemperature =
                        reader.IsDBNull(6)
                            ? null
                            : reader.GetDouble(6)
                });
        }

        return result;
    }


    public AdvisorEvidenceSpan GetAdvisorEvidenceSpan(
        string deviceId,
        AdvisorEvidenceKind kind,
        double warningTemperature)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return new AdvisorEvidenceSpan();
        }

        using SqliteConnection connection =
            OpenConnection();

        using SqliteCommand command =
            connection.CreateCommand();

        string whereCondition =
            kind switch
            {
                AdvisorEvidenceKind.CpuLimited =>
                    "CpuLimitedSamples >= 5",

                AdvisorEvidenceKind.CpuCooling =>
                    "CpuLimitedSamples >= 5 AND " +
                    "CpuMaxTemperature IS NOT NULL AND " +
                    "CpuMaxTemperature >= $warningTemperature",

                AdvisorEvidenceKind.GpuCooling =>
                    "GpuLimitedSamples >= 5 AND " +
                    "GpuMaxTemperature IS NOT NULL AND " +
                    "GpuMaxTemperature >= $warningTemperature",

                _ =>
                    "1 = 0"
            };

        command.CommandText =
            $"""
            SELECT
                MIN(LocalDate),
                MAX(LocalDate),
                COUNT(*)
            FROM AdvisorDailyEvidence
            WHERE DeviceId = $deviceId
              AND {whereCondition};
            """;

        command.Parameters.AddWithValue(
            "$deviceId",
            deviceId);

        command.Parameters.AddWithValue(
            "$warningTemperature",
            warningTemperature);

        using SqliteDataReader reader =
            command.ExecuteReader();

        if (!reader.Read() ||
            reader.IsDBNull(0) ||
            reader.IsDBNull(1))
        {
            return new AdvisorEvidenceSpan();
        }

        string firstText =
            reader.GetString(0);

        string lastText =
            reader.GetString(1);

        int daysWithEvidence =
            reader.GetInt32(2);

        if (!DateTime.TryParseExact(
                firstText,
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out DateTime firstDate) ||
            !DateTime.TryParseExact(
                lastText,
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out DateTime lastDate))
        {
            return new AdvisorEvidenceSpan();
        }

        int calendarSpanDays =
            Math.Max(
                1,
                (lastDate.Date -
                 firstDate.Date).Days +
                1);

        return new AdvisorEvidenceSpan
        {
            HasEvidence =
                true,

            FirstDate =
                firstDate.Date,

            LastDate =
                lastDate.Date,

            CalendarSpanDays =
                calendarSpanDays,

            DaysWithEvidence =
                daysWithEvidence
        };
    }



}
