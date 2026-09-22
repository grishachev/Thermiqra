using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace PCHardwareMonitor;

public sealed class StatisticsService
{
    private static readonly TimeSpan SaveInterval =
        TimeSpan.FromMinutes(1);

    private static readonly TimeSpan RetentionPeriod =
        TimeSpan.FromDays(30);

    private static readonly TimeSpan CleanupInterval =
        TimeSpan.FromHours(24);

    private readonly string _connectionString;

    private DateTimeOffset? _lastSavedAtUtc;

    private DateTimeOffset _lastCleanupUtc =
        DateTimeOffset.MinValue;

    public string DatabasePath { get; }

    public StatisticsService()
    {
        string directory =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "Thermiqra");

        Directory.CreateDirectory(directory);

        DatabasePath =
            Path.Combine(
                directory,
                "statistics.db");

        SqliteConnectionStringBuilder builder =
            new()
            {
                DataSource = DatabasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            };

        _connectionString =
            builder.ToString();

        InitializeDatabase();

        BackfillAllTimeRecordsIfNeeded();

        _lastSavedAtUtc =
            LoadLastSampleTimestamp();
    }

    public bool SaveSnapshot(
        HardwareSnapshot snapshot)
    {
        return SaveSnapshot(
            snapshot,
            DateTimeOffset.UtcNow);
    }

    public bool SaveSnapshot(
        HardwareSnapshot snapshot,
        DateTimeOffset timestampUtc)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        timestampUtc =
            timestampUtc.ToUniversalTime();

        if (_lastSavedAtUtc.HasValue)
        {
            TimeSpan elapsed =
                timestampUtc -
                _lastSavedAtUtc.Value;

            if (elapsed >= TimeSpan.Zero &&
                elapsed < SaveInterval)
            {
                return false;
            }
        }

        using SqliteConnection connection =
            OpenConnection();

        using SqliteTransaction transaction =
            connection.BeginTransaction();

        long sampleId =
            InsertMainSample(
                connection,
                transaction,
                snapshot,
                timestampUtc);

        InsertGpuSamples(
            connection,
            transaction,
            sampleId,
            snapshot);

        InsertStorageSamples(
            connection,
            transaction,
            sampleId,
            snapshot);

        UpdateAllTimeRecords(
            connection,
            transaction,
            snapshot,
            timestampUtc);

        transaction.Commit();

        _lastSavedAtUtc =
            timestampUtc;

        CleanupOldDataIfNeeded(
            timestampUtc);

        return true;
    }


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
                RamLoad REAL NULL
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
            """;

        command.ExecuteNonQuery();
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

    private static long InsertMainSample(
        SqliteConnection connection,
        SqliteTransaction transaction,
        HardwareSnapshot snapshot,
        DateTimeOffset timestampUtc)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
            """
            INSERT INTO MonitoringSamples
            (
                TimestampUtc,
                CpuTemperature,
                CpuLoad,
                RamLoad
            )
            VALUES
            (
                $timestampUtc,
                $cpuTemperature,
                $cpuLoad,
                $ramLoad
            );

            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue(
            "$timestampUtc",
            timestampUtc.ToUnixTimeSeconds());

        AddNullableFloat(
            command,
            "$cpuTemperature",
            snapshot.Cpu.Temperature);

        AddNullableFloat(
            command,
            "$cpuLoad",
            snapshot.Cpu.Load);

        AddNullableFloat(
            command,
            "$ramLoad",
            snapshot.Memory.Load);

        object? result =
            command.ExecuteScalar();

        return Convert.ToInt64(result);
    }

    private static void InsertGpuSamples(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long sampleId,
        HardwareSnapshot snapshot)
    {
        foreach (GpuInfo gpu in snapshot.Gpus)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;

            command.CommandText =
                """
                INSERT INTO GpuSamples
                (
                    SampleId,
                    DeviceId,
                    DeviceName,
                    Temperature,
                    Load,
                    HotSpotTemperature,
                    MemoryTemperature,
                    MemoryLoad
                )
                VALUES
                (
                    $sampleId,
                    $deviceId,
                    $deviceName,
                    $temperature,
                    $load,
                    $hotSpotTemperature,
                    $memoryTemperature,
                    $memoryLoad
                );
                """;

            command.Parameters.AddWithValue(
                "$sampleId",
                sampleId);

            command.Parameters.AddWithValue(
                "$deviceId",
                NormalizeDeviceId(
                    gpu.Id,
                    gpu.Name));

            command.Parameters.AddWithValue(
                "$deviceName",
                gpu.Name);

            AddNullableFloat(
                command,
                "$temperature",
                gpu.Temperature);

            AddNullableFloat(
                command,
                "$load",
                gpu.Load);

            AddNullableFloat(
                command,
                "$hotSpotTemperature",
                gpu.HotSpotTemperature);

            AddNullableFloat(
                command,
                "$memoryTemperature",
                gpu.MemoryTemperature);

            AddNullableFloat(
                command,
                "$memoryLoad",
                gpu.MemoryLoad);

            command.ExecuteNonQuery();
        }
    }

    private static void InsertStorageSamples(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long sampleId,
        HardwareSnapshot snapshot)
    {
        foreach (StorageDeviceInfo storage in
                 snapshot.StorageDevices)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;

            command.CommandText =
                """
                INSERT INTO StorageSamples
                (
                    SampleId,
                    DeviceId,
                    DeviceName,
                    Temperature
                )
                VALUES
                (
                    $sampleId,
                    $deviceId,
                    $deviceName,
                    $temperature
                );
                """;

            command.Parameters.AddWithValue(
                "$sampleId",
                sampleId);

            command.Parameters.AddWithValue(
                "$deviceId",
                NormalizeDeviceId(
                    storage.Id,
                    storage.Name));

            command.Parameters.AddWithValue(
                "$deviceName",
                storage.Name);

            AddNullableFloat(
                command,
                "$temperature",
                storage.Temperature);

            command.ExecuteNonQuery();
        }
    }


    private static void UpdateAllTimeRecords(
        SqliteConnection connection,
        SqliteTransaction transaction,
        HardwareSnapshot snapshot,
        DateTimeOffset timestampUtc)
    {
        if (snapshot.Cpu.Temperature.HasValue)
        {
            UpsertAllTimeRecord(
                connection,
                transaction,
                "CPU_TEMP",
                "CPU",
                "CPU",
                string.IsNullOrWhiteSpace(
                    snapshot.Cpu.Name)
                    ? "CPU"
                    : snapshot.Cpu.Name,
                snapshot.Cpu.Temperature.Value,
                timestampUtc,
                "°C");
        }

        if (snapshot.Memory.Load.HasValue)
        {
            UpsertAllTimeRecord(
                connection,
                transaction,
                "RAM_LOAD",
                "RAM",
                "RAM",
                "RAM",
                snapshot.Memory.Load.Value,
                timestampUtc,
                "%");
        }

        foreach (GpuInfo gpu in snapshot.Gpus)
        {
            if (!gpu.Temperature.HasValue)
                continue;

            string deviceId =
                NormalizeDeviceId(
                    gpu.Id,
                    gpu.Name);

            UpsertAllTimeRecord(
                connection,
                transaction,
                $"GPU_TEMP:{deviceId}",
                "GPU",
                deviceId,
                gpu.Name,
                gpu.Temperature.Value,
                timestampUtc,
                "°C");
        }

        foreach (StorageDeviceInfo storage in
                 snapshot.StorageDevices)
        {
            if (!storage.Temperature.HasValue)
                continue;

            string deviceId =
                NormalizeDeviceId(
                    storage.Id,
                    storage.Name);

            UpsertAllTimeRecord(
                connection,
                transaction,
                $"STORAGE_TEMP:{deviceId}",
                "STORAGE",
                deviceId,
                storage.Name,
                storage.Temperature.Value,
                timestampUtc,
                "°C");
        }
    }

    private static void UpsertAllTimeRecord(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string recordKey,
        string category,
        string deviceId,
        string deviceName,
        double value,
        DateTimeOffset timestampUtc,
        string unit)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
            """
            INSERT INTO AllTimeRecords
            (
                RecordKey,
                Category,
                DeviceId,
                DeviceName,
                Value,
                TimestampUtc,
                Unit
            )
            VALUES
            (
                $recordKey,
                $category,
                $deviceId,
                $deviceName,
                $value,
                $timestampUtc,
                $unit
            )
            ON CONFLICT(RecordKey) DO UPDATE SET
                Category = excluded.Category,
                DeviceId = excluded.DeviceId,
                DeviceName = excluded.DeviceName,
                Value = excluded.Value,
                TimestampUtc = excluded.TimestampUtc,
                Unit = excluded.Unit
            WHERE excluded.Value > AllTimeRecords.Value;
            """;

        command.Parameters.AddWithValue(
            "$recordKey",
            recordKey);

        command.Parameters.AddWithValue(
            "$category",
            category);

        command.Parameters.AddWithValue(
            "$deviceId",
            deviceId);

        command.Parameters.AddWithValue(
            "$deviceName",
            deviceName);

        command.Parameters.AddWithValue(
            "$value",
            value);

        command.Parameters.AddWithValue(
            "$timestampUtc",
            timestampUtc.ToUnixTimeSeconds());

        command.Parameters.AddWithValue(
            "$unit",
            unit);

        command.ExecuteNonQuery();
    }

    private void BackfillAllTimeRecordsIfNeeded()
    {
        using (SqliteConnection connection =
               OpenConnection())
        {
            using SqliteCommand countCommand =
                connection.CreateCommand();

            countCommand.CommandText =
                "SELECT COUNT(*) FROM AllTimeRecords;";

            int count =
                Convert.ToInt32(
                    countCommand.ExecuteScalar() ?? 0);

            if (count > 0)
                return;
        }

        StatisticsSummary summary =
            GetSummary(
                StatisticsPeriod.Last30Days);

        using SqliteConnection targetConnection =
            OpenConnection();

        using SqliteTransaction transaction =
            targetConnection.BeginTransaction();

        AddBackfilledRecord(
            targetConnection,
            transaction,
            "CPU_TEMP",
            "CPU",
            "CPU",
            "CPU",
            summary.CpuTemperature,
            "°C");

        AddBackfilledRecord(
            targetConnection,
            transaction,
            "RAM_LOAD",
            "RAM",
            "RAM",
            "RAM",
            summary.RamLoad,
            "%");

        foreach (GpuStatisticsSummary gpu in
                 summary.Gpus)
        {
            AddBackfilledRecord(
                targetConnection,
                transaction,
                $"GPU_TEMP:{gpu.DeviceId}",
                "GPU",
                gpu.DeviceId,
                gpu.DeviceName,
                gpu.Temperature,
                "°C");
        }

        foreach (StorageStatisticsSummary storage in
                 summary.StorageDevices)
        {
            AddBackfilledRecord(
                targetConnection,
                transaction,
                $"STORAGE_TEMP:{storage.DeviceId}",
                "STORAGE",
                storage.DeviceId,
                storage.DeviceName,
                storage.Temperature,
                "°C");
        }

        transaction.Commit();
    }

    private static void AddBackfilledRecord(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string recordKey,
        string category,
        string deviceId,
        string deviceName,
        MetricStatistics metric,
        string unit)
    {
        if (!metric.Maximum.HasValue ||
            !metric.MaximumAtUtc.HasValue)
        {
            return;
        }

        UpsertAllTimeRecord(
            connection,
            transaction,
            recordKey,
            category,
            deviceId,
            deviceName,
            metric.Maximum.Value,
            metric.MaximumAtUtc.Value,
            unit);
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


internal sealed class DeviceDescriptor
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";
}
