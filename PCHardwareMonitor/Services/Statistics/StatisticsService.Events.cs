using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace PCHardwareMonitor;

public sealed partial class StatisticsService
{
    public void SaveAlertEvent(
        string eventKey,
        StatisticsAlertLevel level,
        string deviceName,
        string subject,
        double value,
        string unit)
    {
        SaveAlertEvent(
            eventKey,
            level,
            deviceName,
            subject,
            value,
            unit,
            DateTimeOffset.UtcNow);
    }

    public void SaveAlertEvent(
        string eventKey,
        StatisticsAlertLevel level,
        string deviceName,
        string subject,
        double value,
        string unit,
        DateTimeOffset timestampUtc)
    {
        timestampUtc =
            timestampUtc.ToUniversalTime();

        using SqliteConnection connection =
            OpenConnection();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO AlertEvents
            (
                TimestampUtc,
                EventKey,
                Level,
                DeviceName,
                Subject,
                Value,
                Unit
            )
            VALUES
            (
                $timestampUtc,
                $eventKey,
                $level,
                $deviceName,
                $subject,
                $value,
                $unit
            );
            """;

        command.Parameters.AddWithValue(
            "$timestampUtc",
            timestampUtc.ToUnixTimeSeconds());

        command.Parameters.AddWithValue(
            "$eventKey",
            eventKey);

        command.Parameters.AddWithValue(
            "$level",
            (int)level);

        command.Parameters.AddWithValue(
            "$deviceName",
            deviceName);

        command.Parameters.AddWithValue(
            "$subject",
            subject);

        command.Parameters.AddWithValue(
            "$value",
            value);

        command.Parameters.AddWithValue(
            "$unit",
            unit);

        command.ExecuteNonQuery();
    }

    public AlertEventCounts GetAlertEventCounts(
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
                counts.WarningCount = count;
            }
            else if (level ==
                     StatisticsAlertLevel.Critical)
            {
                counts.CriticalCount = count;
            }
        }

        return counts;
    }

    public List<StatisticsAlertEvent> GetAlertEvents(
        StatisticsPeriod period,
        int limit = 30)
    {
        limit =
            Math.Clamp(
                limit,
                1,
                200);

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
                EventKey,
                Level,
                DeviceName,
                Subject,
                Value,
                Unit
            FROM AlertEvents
            WHERE TimestampUtc >= $startUtc
              AND TimestampUtc <= $endUtc
            ORDER BY TimestampUtc DESC,
                     Id DESC
            LIMIT $limit;
            """;

        AddRangeParameters(
            command,
            startUtc,
            endUtc);

        command.Parameters.AddWithValue(
            "$limit",
            limit);

        List<StatisticsAlertEvent> result =
            new();

        using SqliteDataReader reader =
            command.ExecuteReader();

        while (reader.Read())
        {
            result.Add(
                new StatisticsAlertEvent
                {
                    TimestampUtc =
                        DateTimeOffset.FromUnixTimeSeconds(
                            reader.GetInt64(0)),
                    EventKey =
                        reader.GetString(1),
                    Level =
                        (StatisticsAlertLevel)
                        reader.GetInt32(2),
                    DeviceName =
                        reader.GetString(3),
                    Subject =
                        reader.GetString(4),
                    Value =
                        reader.GetDouble(5),
                    Unit =
                        reader.GetString(6)
                });
        }

        return result;
    }

    public List<AllTimeRecord> GetAllTimeRecords()
    {
        using SqliteConnection connection =
            OpenConnection();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                RecordKey,
                Category,
                DeviceId,
                DeviceName,
                Value,
                TimestampUtc,
                Unit
            FROM AllTimeRecords
            ORDER BY
                CASE Category
                    WHEN 'CPU' THEN 1
                    WHEN 'GPU' THEN 2
                    WHEN 'RAM' THEN 3
                    WHEN 'STORAGE' THEN 4
                    ELSE 5
                END,
                DeviceName COLLATE NOCASE;
            """;

        List<AllTimeRecord> result =
            new();

        using SqliteDataReader reader =
            command.ExecuteReader();

        while (reader.Read())
        {
            result.Add(
                new AllTimeRecord
                {
                    RecordKey =
                        reader.GetString(0),
                    Category =
                        reader.GetString(1),
                    DeviceId =
                        reader.GetString(2),
                    DeviceName =
                        reader.GetString(3),
                    Value =
                        reader.GetDouble(4),
                    TimestampUtc =
                        DateTimeOffset.FromUnixTimeSeconds(
                            reader.GetInt64(5)),
                    Unit =
                        reader.GetString(6)
                });
        }

        return result;
    }

    public void ClearAll()
    {
        using SqliteConnection connection =
            OpenConnection();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            DELETE FROM AlertEvents;
            DELETE FROM AllTimeRecords;
            DELETE FROM AdvisorDailyEvidence;
            DELETE FROM MonitoringSamples;
            """;

        command.ExecuteNonQuery();

        using SqliteCommand vacuum =
            connection.CreateCommand();

        vacuum.CommandText =
            "VACUUM;";

        vacuum.ExecuteNonQuery();

        _lastSavedAtUtc = null;
    }


}
