using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace PCHardwareMonitor;

public sealed partial class StatisticsService
{
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



}
