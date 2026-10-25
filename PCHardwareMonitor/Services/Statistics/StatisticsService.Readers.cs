using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace PCHardwareMonitor;

public sealed partial class StatisticsService
{
    private static AlertEventCounts ReadAlertEventCounts(
        SqliteConnection connection,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT Level, COUNT(*)
            FROM AlertEvents
            WHERE TimestampUtc >= $startUtc
              AND TimestampUtc <= $endUtc
            GROUP BY Level;
            """;

        AddRangeParameters(
            command,
            startUtc,
            endUtc);

        AlertEventCounts counts =
            new();

        using SqliteDataReader reader =
            command.ExecuteReader();

        while (reader.Read())
        {
            StatisticsAlertLevel level =
                (StatisticsAlertLevel)
                reader.GetInt32(0);

            int count =
                reader.GetInt32(1);

            if (level ==
                StatisticsAlertLevel.Warning)
            {
                counts.WarningCount =
                    count;
            }
            else if (level ==
                     StatisticsAlertLevel.Critical)
            {
                counts.CriticalCount =
                    count;
            }
        }

        return counts;
    }


    private static int ReadDaysWithSamples(
        SqliteConnection connection,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT COUNT(
                DISTINCT date(
                    TimestampUtc,
                    'unixepoch',
                    'localtime'))
            FROM MonitoringSamples
            WHERE TimestampUtc >= $startUtc
              AND TimestampUtc <= $endUtc;
            """;

        AddRangeParameters(
            command,
            startUtc,
            endUtc);

        object? result =
            command.ExecuteScalar();

        return Convert.ToInt32(
            result ?? 0);
    }


    private static List<RecurringAlertSummary>
        ReadRecurringAlertSummaries(
            SqliteConnection connection,
            DateTimeOffset startUtc,
            DateTimeOffset endUtc,
            int limit)
    {
        limit =
            Math.Clamp(
                limit,
                1,
                20);

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                EventKey,
                Level,
                DeviceName,
                Subject,
                Unit,
                COUNT(*) AS EventCount,
                MAX(Value) AS MaximumValue,
                MAX(TimestampUtc) AS LastTimestampUtc
            FROM AlertEvents
            WHERE TimestampUtc >= $startUtc
              AND TimestampUtc <= $endUtc
            GROUP BY
                EventKey,
                Level,
                DeviceName,
                Subject,
                Unit
            HAVING COUNT(*) >= 2
            ORDER BY
                EventCount DESC,
                Level DESC,
                LastTimestampUtc DESC
            LIMIT $limit;
            """;

        AddRangeParameters(
            command,
            startUtc,
            endUtc);

        command.Parameters.AddWithValue(
            "$limit",
            limit);

        List<RecurringAlertSummary> result =
            new();

        using SqliteDataReader reader =
            command.ExecuteReader();

        while (reader.Read())
        {
            result.Add(
                new RecurringAlertSummary
                {
                    EventKey =
                        reader.GetString(0),

                    Level =
                        (StatisticsAlertLevel)
                        reader.GetInt32(1),

                    DeviceName =
                        reader.GetString(2),

                    Subject =
                        reader.GetString(3),

                    Unit =
                        reader.GetString(4),

                    Count =
                        reader.GetInt32(5),

                    MaximumValue =
                        reader.GetDouble(6),

                    LastAtUtc =
                        DateTimeOffset.FromUnixTimeSeconds(
                            reader.GetInt64(7))
                });
        }

        return result;
    }


    private static DateTimeOffset GetPeriodStartUtc(
        StatisticsPeriod period,
        DateTimeOffset endUtc)
    {
        if (period == StatisticsPeriod.Today)
        {
            DateTime localDate =
                endUtc
                    .ToLocalTime()
                    .Date;

            TimeSpan offset =
                TimeZoneInfo.Local
                    .GetUtcOffset(
                        localDate);

            DateTimeOffset localMidnight =
                new(
                    localDate,
                    offset);

            return localMidnight
                .ToUniversalTime();
        }

        return period switch
        {
            StatisticsPeriod.Last24Hours =>
                endUtc.Subtract(
                    TimeSpan.FromHours(24)),

            StatisticsPeriod.Last7Days =>
                endUtc.Subtract(
                    TimeSpan.FromDays(7)),

            StatisticsPeriod.Last30Days =>
                endUtc.Subtract(
                    TimeSpan.FromDays(30)),

            _ =>
                endUtc.Subtract(
                    TimeSpan.FromHours(24))
        };
    }

    private static int GetMainSampleCount(
        SqliteConnection connection,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT COUNT(*)
            FROM MonitoringSamples
            WHERE TimestampUtc >= $startUtc
              AND TimestampUtc <= $endUtc;
            """;

        AddRangeParameters(
            command,
            startUtc,
            endUtc);

        object? result =
            command.ExecuteScalar();

        return Convert.ToInt32(
            result ?? 0);
    }

    private static MetricStatistics ReadMainMetric(
        SqliteConnection connection,
        string columnName,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            $"""
            SELECT
                AVG({columnName}),
                MAX({columnName})
            FROM MonitoringSamples
            WHERE TimestampUtc >= $startUtc
              AND TimestampUtc <= $endUtc
              AND {columnName} IS NOT NULL;
            """;

        AddRangeParameters(
            command,
            startUtc,
            endUtc);

        using SqliteDataReader reader =
            command.ExecuteReader();

        double? average = null;
        double? maximum = null;

        if (reader.Read())
        {
            average = ReadNullableDouble(
                reader,
                0);

            maximum = ReadNullableDouble(
                reader,
                1);
        }

        reader.Close();

        return new MetricStatistics
        {
            Average = average,
            Maximum = maximum,
            MaximumAtUtc = maximum.HasValue
                ? ReadMainMetricMaximumTime(
                    connection,
                    columnName,
                    startUtc,
                    endUtc)
                : null
        };
    }

    private static DateTimeOffset? ReadMainMetricMaximumTime(
        SqliteConnection connection,
        string columnName,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            $"""
            SELECT TimestampUtc
            FROM MonitoringSamples
            WHERE TimestampUtc >= $startUtc
              AND TimestampUtc <= $endUtc
              AND {columnName} IS NOT NULL
            ORDER BY {columnName} DESC,
                     TimestampUtc DESC
            LIMIT 1;
            """;

        AddRangeParameters(
            command,
            startUtc,
            endUtc);

        return ReadTimestamp(
            command.ExecuteScalar());
    }

    private static MetricStatistics ReadDeviceMetric(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string deviceId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            $"""
            SELECT
                AVG(d.{columnName}),
                MAX(d.{columnName})
            FROM {tableName} d
            INNER JOIN MonitoringSamples m
                ON m.Id = d.SampleId
            WHERE d.DeviceId = $deviceId
              AND m.TimestampUtc >= $startUtc
              AND m.TimestampUtc <= $endUtc
              AND d.{columnName} IS NOT NULL;
            """;

        command.Parameters.AddWithValue(
            "$deviceId",
            deviceId);

        AddRangeParameters(
            command,
            startUtc,
            endUtc);

        using SqliteDataReader reader =
            command.ExecuteReader();

        double? average = null;
        double? maximum = null;

        if (reader.Read())
        {
            average = ReadNullableDouble(
                reader,
                0);

            maximum = ReadNullableDouble(
                reader,
                1);
        }

        reader.Close();

        return new MetricStatistics
        {
            Average = average,
            Maximum = maximum,
            MaximumAtUtc = maximum.HasValue
                ? ReadDeviceMetricMaximumTime(
                    connection,
                    tableName,
                    columnName,
                    deviceId,
                    startUtc,
                    endUtc)
                : null
        };
    }

    private static DateTimeOffset? ReadDeviceMetricMaximumTime(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string deviceId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            $"""
            SELECT m.TimestampUtc
            FROM {tableName} d
            INNER JOIN MonitoringSamples m
                ON m.Id = d.SampleId
            WHERE d.DeviceId = $deviceId
              AND m.TimestampUtc >= $startUtc
              AND m.TimestampUtc <= $endUtc
              AND d.{columnName} IS NOT NULL
            ORDER BY d.{columnName} DESC,
                     m.TimestampUtc DESC
            LIMIT 1;
            """;

        command.Parameters.AddWithValue(
            "$deviceId",
            deviceId);

        AddRangeParameters(
            command,
            startUtc,
            endUtc);

        return ReadTimestamp(
            command.ExecuteScalar());
    }

    private static List<DeviceDescriptor> ReadDevices(
        SqliteConnection connection,
        string tableName,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            $"""
            SELECT
                d.DeviceId,
                MAX(d.DeviceName)
            FROM {tableName} d
            INNER JOIN MonitoringSamples m
                ON m.Id = d.SampleId
            WHERE m.TimestampUtc >= $startUtc
              AND m.TimestampUtc <= $endUtc
            GROUP BY d.DeviceId
            ORDER BY MAX(d.DeviceName) COLLATE NOCASE;
            """;

        AddRangeParameters(
            command,
            startUtc,
            endUtc);

        List<DeviceDescriptor> result =
            new();

        using SqliteDataReader reader =
            command.ExecuteReader();

        while (reader.Read())
        {
            result.Add(
                new DeviceDescriptor
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1)
                });
        }

        return result;
    }


    private static List<StatisticsChartPoint> ReadMainChartPoints(
        SqliteConnection connection,
        string columnName,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            $"""
            SELECT TimestampUtc, {columnName}
            FROM MonitoringSamples
            WHERE TimestampUtc >= $startUtc
              AND TimestampUtc <= $endUtc
              AND {columnName} IS NOT NULL
            ORDER BY TimestampUtc;
            """;

        AddRangeParameters(
            command,
            startUtc,
            endUtc);

        List<StatisticsChartPoint> result =
            new();

        using SqliteDataReader reader =
            command.ExecuteReader();

        while (reader.Read())
        {
            result.Add(
                new StatisticsChartPoint
                {
                    TimestampUtc =
                        DateTimeOffset.FromUnixTimeSeconds(
                            reader.GetInt64(0)),
                    Value =
                        reader.GetDouble(1)
                });
        }

        return result;
    }

    private static List<StatisticsChartPoint> ReadDeviceChartPoints(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string? deviceId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return new List<StatisticsChartPoint>();
        }

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            $"""
            SELECT m.TimestampUtc, d.{columnName}
            FROM {tableName} d
            INNER JOIN MonitoringSamples m
                ON m.Id = d.SampleId
            WHERE d.DeviceId = $deviceId
              AND m.TimestampUtc >= $startUtc
              AND m.TimestampUtc <= $endUtc
              AND d.{columnName} IS NOT NULL
            ORDER BY m.TimestampUtc;
            """;

        command.Parameters.AddWithValue(
            "$deviceId",
            deviceId);

        AddRangeParameters(
            command,
            startUtc,
            endUtc);

        List<StatisticsChartPoint> result =
            new();

        using SqliteDataReader reader =
            command.ExecuteReader();

        while (reader.Read())
        {
            result.Add(
                new StatisticsChartPoint
                {
                    TimestampUtc =
                        DateTimeOffset.FromUnixTimeSeconds(
                            reader.GetInt64(0)),
                    Value =
                        reader.GetDouble(1)
                });
        }

        return result;
    }

    private static List<StatisticsChartPoint> DownsampleChartPoints(
        List<StatisticsChartPoint> points,
        int maxPoints)
    {
        if (points.Count <= maxPoints)
        {
            return points;
        }

        List<StatisticsChartPoint> result =
            new(maxPoints);

        double step =
            (points.Count - 1) /
            (double)(maxPoints - 1);

        int lastIndex = -1;

        for (int i = 0;
             i < maxPoints;
             i++)
        {
            int index =
                (int)Math.Round(
                    i * step);

            index =
                Math.Clamp(
                    index,
                    0,
                    points.Count - 1);

            if (index == lastIndex)
                continue;

            result.Add(
                points[index]);

            lastIndex = index;
        }

        if (result.Count == 0 ||
            result[^1].TimestampUtc != points[^1].TimestampUtc)
        {
            result.Add(
                points[^1]);
        }

        return result;
    }

    private static void AddRangeParameters(
        SqliteCommand command,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc)
    {
        command.Parameters.AddWithValue(
            "$startUtc",
            startUtc.ToUnixTimeSeconds());

        command.Parameters.AddWithValue(
            "$endUtc",
            endUtc.ToUnixTimeSeconds());
    }

    private static double? ReadNullableDouble(
        SqliteDataReader reader,
        int ordinal)
    {
        return reader.IsDBNull(
            ordinal)
                ? null
                : reader.GetDouble(
                    ordinal);
    }

    private static DateTimeOffset? ReadTimestamp(
        object? value)
    {
        if (value == null ||
            value == DBNull.Value)
        {
            return null;
        }

        return DateTimeOffset
            .FromUnixTimeSeconds(
                Convert.ToInt64(
                    value));
    }

    private void CleanupOldDataIfNeeded(
        DateTimeOffset nowUtc)
    {
        if (_lastCleanupUtc != DateTimeOffset.MinValue &&
            nowUtc - _lastCleanupUtc < CleanupInterval)
        {
            return;
        }

        long cutoffUnixSeconds =
            nowUtc
                .Subtract(RetentionPeriod)
                .ToUnixTimeSeconds();

        using SqliteConnection connection =
            OpenConnection();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            DELETE FROM MonitoringSamples
            WHERE TimestampUtc < $cutoffUtc;

            DELETE FROM AlertEvents
            WHERE TimestampUtc < $cutoffUtc;
            """;

        command.Parameters.AddWithValue(
            "$cutoffUtc",
            cutoffUnixSeconds);

        command.ExecuteNonQuery();

        _lastCleanupUtc =
            nowUtc;
    }

    private SqliteConnection OpenConnection()
    {
        SqliteConnection connection =
            new(_connectionString);

        connection.Open();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            PRAGMA foreign_keys = ON;
            PRAGMA busy_timeout = 5000;
            """;

        command.ExecuteNonQuery();

        return connection;
    }

    private static void AddNullableFloat(
        SqliteCommand command,
        string parameterName,
        float? value)
    {
        command.Parameters.AddWithValue(
            parameterName,
            value.HasValue
                ? value.Value
                : DBNull.Value);
    }

    private static string NormalizeDeviceId(
        string id,
        string name)
    {
        if (!string.IsNullOrWhiteSpace(id))
            return id;

        return name;
    }

}
