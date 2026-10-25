using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace PCHardwareMonitor;

public sealed partial class StatisticsService
{
    public int ReadExportRows(
        StatisticsPeriod period,
        Action<StatisticsExportRow> rowHandler)
    {
        ArgumentNullException.ThrowIfNull(
            rowHandler);

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
                Category,
                DeviceId,
                DeviceName,
                Metric,
                Value,
                Unit
            FROM
            (
                SELECT
                    TimestampUtc,
                    'CPU' AS Category,
                    'CPU' AS DeviceId,
                    'CPU' AS DeviceName,
                    'Temperature' AS Metric,
                    CpuTemperature AS Value,
                    '°C' AS Unit
                FROM MonitoringSamples
                WHERE TimestampUtc >= $startUtc
                  AND TimestampUtc <= $endUtc
                  AND CpuTemperature IS NOT NULL

                UNION ALL

                SELECT
                    TimestampUtc,
                    'CPU',
                    'CPU',
                    'CPU',
                    'Load',
                    CpuLoad,
                    '%'
                FROM MonitoringSamples
                WHERE TimestampUtc >= $startUtc
                  AND TimestampUtc <= $endUtc
                  AND CpuLoad IS NOT NULL

                UNION ALL

                SELECT
                    TimestampUtc,
                    'RAM',
                    'RAM',
                    'RAM',
                    'Load',
                    RamLoad,
                    '%'
                FROM MonitoringSamples
                WHERE TimestampUtc >= $startUtc
                  AND TimestampUtc <= $endUtc
                  AND RamLoad IS NOT NULL

                UNION ALL

                SELECT
                    m.TimestampUtc,
                    'GPU',
                    g.DeviceId,
                    g.DeviceName,
                    'Temperature',
                    g.Temperature,
                    '°C'
                FROM GpuSamples g
                INNER JOIN MonitoringSamples m
                    ON m.Id = g.SampleId
                WHERE m.TimestampUtc >= $startUtc
                  AND m.TimestampUtc <= $endUtc
                  AND g.Temperature IS NOT NULL

                UNION ALL

                SELECT
                    m.TimestampUtc,
                    'GPU',
                    g.DeviceId,
                    g.DeviceName,
                    'Load',
                    g.Load,
                    '%'
                FROM GpuSamples g
                INNER JOIN MonitoringSamples m
                    ON m.Id = g.SampleId
                WHERE m.TimestampUtc >= $startUtc
                  AND m.TimestampUtc <= $endUtc
                  AND g.Load IS NOT NULL

                UNION ALL

                SELECT
                    m.TimestampUtc,
                    'GPU',
                    g.DeviceId,
                    g.DeviceName,
                    'HotSpotTemperature',
                    g.HotSpotTemperature,
                    '°C'
                FROM GpuSamples g
                INNER JOIN MonitoringSamples m
                    ON m.Id = g.SampleId
                WHERE m.TimestampUtc >= $startUtc
                  AND m.TimestampUtc <= $endUtc
                  AND g.HotSpotTemperature IS NOT NULL

                UNION ALL

                SELECT
                    m.TimestampUtc,
                    'GPU',
                    g.DeviceId,
                    g.DeviceName,
                    'MemoryTemperature',
                    g.MemoryTemperature,
                    '°C'
                FROM GpuSamples g
                INNER JOIN MonitoringSamples m
                    ON m.Id = g.SampleId
                WHERE m.TimestampUtc >= $startUtc
                  AND m.TimestampUtc <= $endUtc
                  AND g.MemoryTemperature IS NOT NULL

                UNION ALL

                SELECT
                    m.TimestampUtc,
                    'GPU',
                    g.DeviceId,
                    g.DeviceName,
                    'MemoryLoad',
                    g.MemoryLoad,
                    '%'
                FROM GpuSamples g
                INNER JOIN MonitoringSamples m
                    ON m.Id = g.SampleId
                WHERE m.TimestampUtc >= $startUtc
                  AND m.TimestampUtc <= $endUtc
                  AND g.MemoryLoad IS NOT NULL

                UNION ALL

                SELECT
                    m.TimestampUtc,
                    'STORAGE',
                    s.DeviceId,
                    s.DeviceName,
                    'Temperature',
                    s.Temperature,
                    '°C'
                FROM StorageSamples s
                INNER JOIN MonitoringSamples m
                    ON m.Id = s.SampleId
                WHERE m.TimestampUtc >= $startUtc
                  AND m.TimestampUtc <= $endUtc
                  AND s.Temperature IS NOT NULL
            )
            ORDER BY
                TimestampUtc,
                Category,
                DeviceName COLLATE NOCASE,
                Metric;
            """;

        AddRangeParameters(
            command,
            startUtc,
            endUtc);

        int count =
            0;

        using SqliteDataReader reader =
            command.ExecuteReader();

        while (reader.Read())
        {
            rowHandler(
                new StatisticsExportRow
                {
                    TimestampUtc =
                        DateTimeOffset.FromUnixTimeSeconds(
                            reader.GetInt64(0)),

                    Category =
                        reader.GetString(1),

                    DeviceId =
                        reader.GetString(2),

                    DeviceName =
                        reader.GetString(3),

                    Metric =
                        reader.GetString(4),

                    Value =
                        reader.GetDouble(5),

                    Unit =
                        reader.GetString(6)
                });

            count++;
        }

        return count;
    }



}
