using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace PCHardwareMonitor;

public sealed partial class StatisticsService
{
    private static long InsertMainSample(
        SqliteConnection connection,
        SqliteTransaction transaction,
        HardwareSnapshot snapshot,
        DateTimeOffset timestampUtc,
        BenchmarkWorkloadKind benchmarkWorkload)
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
                RamLoad,
                BenchmarkMask,
                BenchmarkClassified
            )
            VALUES
            (
                $timestampUtc,
                $cpuTemperature,
                $cpuLoad,
                $ramLoad,
                $benchmarkMask,
                1
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

        command.Parameters.AddWithValue(
            "$benchmarkMask",
            (int)benchmarkWorkload);

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


    private static void UpdateAdvisorDailyEvidence(
        SqliteConnection connection,
        SqliteTransaction transaction,
        HardwareSnapshot snapshot,
        DateTimeOffset timestampUtc,
        BenchmarkWorkloadKind benchmarkWorkload)
    {
        if ((benchmarkWorkload &
             (BenchmarkWorkloadKind.Cpu |
              BenchmarkWorkloadKind.Gpu)) != 0)
        {
            return;
        }

        if (!snapshot.Cpu.Load.HasValue)
            return;

        double cpuLoad =
            snapshot.Cpu.Load.Value;

        string localDate =
            timestampUtc
                .ToLocalTime()
                .ToString(
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture);

        foreach (GpuInfo gpu in snapshot.Gpus)
        {
            if (!gpu.Load.HasValue)
                continue;

            double gpuLoad =
                gpu.Load.Value;

            bool cpuLimited =
                cpuLoad >= 90 &&
                gpuLoad <= 70;

            bool gpuLimited =
                gpuLoad >= 95 &&
                cpuLoad <= 80;

            bool bothHigh =
                cpuLoad >= 90 &&
                gpuLoad >= 90;

            if (!cpuLimited &&
                !gpuLimited &&
                !bothHigh)
            {
                continue;
            }

            string deviceId =
                NormalizeDeviceId(
                    gpu.Id,
                    gpu.Name);

            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction =
                transaction;

            command.CommandText =
                """
                INSERT INTO AdvisorDailyEvidence
                (
                    LocalDate,
                    DeviceId,
                    DeviceName,

                    CpuLimitedSamples,
                    CpuMaxLoad,
                    CpuMaxTemperature,
                    CpuGpuLoadSum,

                    GpuLimitedSamples,
                    GpuMaxLoad,
                    GpuMaxTemperature,
                    GpuCpuLoadSum,

                    BothHighSamples
                )
                VALUES
                (
                    $localDate,
                    $deviceId,
                    $deviceName,

                    $cpuLimitedSamples,
                    $cpuMaxLoad,
                    $cpuMaxTemperature,
                    $cpuGpuLoadSum,

                    $gpuLimitedSamples,
                    $gpuMaxLoad,
                    $gpuMaxTemperature,
                    $gpuCpuLoadSum,

                    $bothHighSamples
                )
                ON CONFLICT(LocalDate, DeviceId) DO UPDATE SET
                    DeviceName =
                        excluded.DeviceName,

                    CpuLimitedSamples =
                        AdvisorDailyEvidence.CpuLimitedSamples +
                        excluded.CpuLimitedSamples,

                    CpuMaxLoad =
                        CASE
                            WHEN excluded.CpuMaxLoad IS NULL
                                THEN AdvisorDailyEvidence.CpuMaxLoad
                            WHEN AdvisorDailyEvidence.CpuMaxLoad IS NULL
                                THEN excluded.CpuMaxLoad
                            ELSE MAX(
                                AdvisorDailyEvidence.CpuMaxLoad,
                                excluded.CpuMaxLoad)
                        END,

                    CpuMaxTemperature =
                        CASE
                            WHEN excluded.CpuMaxTemperature IS NULL
                                THEN AdvisorDailyEvidence.CpuMaxTemperature
                            WHEN AdvisorDailyEvidence.CpuMaxTemperature IS NULL
                                THEN excluded.CpuMaxTemperature
                            ELSE MAX(
                                AdvisorDailyEvidence.CpuMaxTemperature,
                                excluded.CpuMaxTemperature)
                        END,

                    CpuGpuLoadSum =
                        AdvisorDailyEvidence.CpuGpuLoadSum +
                        excluded.CpuGpuLoadSum,

                    GpuLimitedSamples =
                        AdvisorDailyEvidence.GpuLimitedSamples +
                        excluded.GpuLimitedSamples,

                    GpuMaxLoad =
                        CASE
                            WHEN excluded.GpuMaxLoad IS NULL
                                THEN AdvisorDailyEvidence.GpuMaxLoad
                            WHEN AdvisorDailyEvidence.GpuMaxLoad IS NULL
                                THEN excluded.GpuMaxLoad
                            ELSE MAX(
                                AdvisorDailyEvidence.GpuMaxLoad,
                                excluded.GpuMaxLoad)
                        END,

                    GpuMaxTemperature =
                        CASE
                            WHEN excluded.GpuMaxTemperature IS NULL
                                THEN AdvisorDailyEvidence.GpuMaxTemperature
                            WHEN AdvisorDailyEvidence.GpuMaxTemperature IS NULL
                                THEN excluded.GpuMaxTemperature
                            ELSE MAX(
                                AdvisorDailyEvidence.GpuMaxTemperature,
                                excluded.GpuMaxTemperature)
                        END,

                    GpuCpuLoadSum =
                        AdvisorDailyEvidence.GpuCpuLoadSum +
                        excluded.GpuCpuLoadSum,

                    BothHighSamples =
                        AdvisorDailyEvidence.BothHighSamples +
                        excluded.BothHighSamples;
                """;

            command.Parameters.AddWithValue(
                "$localDate",
                localDate);

            command.Parameters.AddWithValue(
                "$deviceId",
                deviceId);

            command.Parameters.AddWithValue(
                "$deviceName",
                gpu.Name);

            command.Parameters.AddWithValue(
                "$cpuLimitedSamples",
                cpuLimited
                    ? 1
                    : 0);

            command.Parameters.AddWithValue(
                "$cpuMaxLoad",
                cpuLimited
                    ? cpuLoad
                    : DBNull.Value);

            command.Parameters.AddWithValue(
                "$cpuMaxTemperature",
                cpuLimited &&
                snapshot.Cpu.Temperature.HasValue
                    ? snapshot.Cpu.Temperature.Value
                    : DBNull.Value);

            command.Parameters.AddWithValue(
                "$cpuGpuLoadSum",
                cpuLimited
                    ? gpuLoad
                    : 0);

            command.Parameters.AddWithValue(
                "$gpuLimitedSamples",
                gpuLimited
                    ? 1
                    : 0);

            command.Parameters.AddWithValue(
                "$gpuMaxLoad",
                gpuLimited
                    ? gpuLoad
                    : DBNull.Value);

            command.Parameters.AddWithValue(
                "$gpuMaxTemperature",
                gpuLimited &&
                gpu.Temperature.HasValue
                    ? gpu.Temperature.Value
                    : DBNull.Value);

            command.Parameters.AddWithValue(
                "$gpuCpuLoadSum",
                gpuLimited
                    ? cpuLoad
                    : 0);

            command.Parameters.AddWithValue(
                "$bothHighSamples",
                bothHigh
                    ? 1
                    : 0);

            command.ExecuteNonQuery();
        }
    }



}
