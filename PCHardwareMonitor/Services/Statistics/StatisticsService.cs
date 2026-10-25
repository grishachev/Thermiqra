using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace PCHardwareMonitor;

public sealed partial class StatisticsService
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

        BenchmarkWorkloadKind benchmarkWorkload =
            BenchmarkDetectionService.Detect(
                snapshot);

        using SqliteConnection connection =
            OpenConnection();

        using SqliteTransaction transaction =
            connection.BeginTransaction();

        long sampleId =
            InsertMainSample(
                connection,
                transaction,
                snapshot,
                timestampUtc,
                benchmarkWorkload);

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

        UpdateAdvisorDailyEvidence(
            connection,
            transaction,
            snapshot,
            timestampUtc,
            benchmarkWorkload);

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



}
