using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace PCHardwareMonitor;

public sealed partial class StatisticsService
{
    private void InitializeDatabase()
    {
        using SqliteConnection connection =
            OpenConnection();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS MonitoringSamples
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TimestampUtc INTEGER NOT NULL,
                CpuTemperature REAL NULL,
                CpuLoad REAL NULL,
                RamLoad REAL NULL,
                BenchmarkMask INTEGER NOT NULL DEFAULT 0,
                BenchmarkClassified INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS IX_MonitoringSamples_TimestampUtc
                ON MonitoringSamples(TimestampUtc);

            CREATE TABLE IF NOT EXISTS GpuSamples
            (
                SampleId INTEGER NOT NULL,
                DeviceId TEXT NOT NULL,
                DeviceName TEXT NOT NULL,
                Temperature REAL NULL,
                Load REAL NULL,
                HotSpotTemperature REAL NULL,
                MemoryTemperature REAL NULL,
                MemoryLoad REAL NULL,
                PRIMARY KEY (SampleId, DeviceId),
                FOREIGN KEY (SampleId)
                    REFERENCES MonitoringSamples(Id)
                    ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS IX_GpuSamples_DeviceId
                ON GpuSamples(DeviceId);

            CREATE TABLE IF NOT EXISTS StorageSamples
            (
                SampleId INTEGER NOT NULL,
                DeviceId TEXT NOT NULL,
                DeviceName TEXT NOT NULL,
                Temperature REAL NULL,
                PRIMARY KEY (SampleId, DeviceId),
                FOREIGN KEY (SampleId)
                    REFERENCES MonitoringSamples(Id)
                    ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS IX_StorageSamples_DeviceId
                ON StorageSamples(DeviceId);

            CREATE TABLE IF NOT EXISTS AlertEvents
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TimestampUtc INTEGER NOT NULL,
                EventKey TEXT NOT NULL,
                Level INTEGER NOT NULL,
                DeviceName TEXT NOT NULL,
                Subject TEXT NOT NULL,
                Value REAL NOT NULL,
                Unit TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_AlertEvents_TimestampUtc
                ON AlertEvents(TimestampUtc);

            CREATE TABLE IF NOT EXISTS AllTimeRecords
            (
                RecordKey TEXT PRIMARY KEY,
                Category TEXT NOT NULL,
                DeviceId TEXT NOT NULL,
                DeviceName TEXT NOT NULL,
                Value REAL NOT NULL,
                TimestampUtc INTEGER NOT NULL,
                Unit TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS AdvisorDailyEvidence
            (
                LocalDate TEXT NOT NULL,
                DeviceId TEXT NOT NULL,
                DeviceName TEXT NOT NULL,

                CpuLimitedSamples INTEGER NOT NULL DEFAULT 0,
                CpuMaxLoad REAL NULL,
                CpuMaxTemperature REAL NULL,
                CpuGpuLoadSum REAL NOT NULL DEFAULT 0,

                GpuLimitedSamples INTEGER NOT NULL DEFAULT 0,
                GpuMaxLoad REAL NULL,
                GpuMaxTemperature REAL NULL,
                GpuCpuLoadSum REAL NOT NULL DEFAULT 0,

                BothHighSamples INTEGER NOT NULL DEFAULT 0,

                PRIMARY KEY (LocalDate, DeviceId)
            );

            CREATE INDEX IF NOT EXISTS IX_AdvisorDailyEvidence_DeviceId
                ON AdvisorDailyEvidence(DeviceId);
            """;

        command.ExecuteNonQuery();

        EnsureColumnExists(
            connection,
            "MonitoringSamples",
            "BenchmarkMask",
            "INTEGER NOT NULL DEFAULT 0");

        EnsureColumnExists(
            connection,
            "MonitoringSamples",
            "BenchmarkClassified",
            "INTEGER NOT NULL DEFAULT 0");
    }

    private static void EnsureColumnExists(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnDefinition)
    {
        using SqliteCommand checkCommand =
            connection.CreateCommand();

        checkCommand.CommandText =
            $"PRAGMA table_info({tableName});";

        bool exists =
            false;

        using (SqliteDataReader reader =
               checkCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                string existingColumn =
                    reader.GetString(1);

                if (string.Equals(
                        existingColumn,
                        columnName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    exists =
                        true;

                    break;
                }
            }
        }

        if (exists)
            return;

        using SqliteCommand alterCommand =
            connection.CreateCommand();

        alterCommand.CommandText =
            $"ALTER TABLE {tableName} " +
            $"ADD COLUMN {columnName} {columnDefinition};";

        alterCommand.ExecuteNonQuery();
    }

    private DateTimeOffset? LoadLastSampleTimestamp()
    {
        using SqliteConnection connection =
            OpenConnection();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            "SELECT MAX(TimestampUtc) FROM MonitoringSamples;";

        object? result =
            command.ExecuteScalar();

        if (result == null ||
            result == DBNull.Value)
        {
            return null;
        }

        long unixSeconds =
            Convert.ToInt64(result);

        return DateTimeOffset
            .FromUnixTimeSeconds(
                unixSeconds);
    }


}
