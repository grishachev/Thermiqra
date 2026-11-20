using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PCHardwareMonitor;

internal static class ProgramLeftoverCleanupService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    internal static List<InstalledProgramInfo> LiveInventory(string? removedIdentity)
    {
        InstalledProgramsReadStatus status = new();
        var apps = InstalledProgramsDiscoveryService.ReadRegistryPrograms(default, status);
        var packages = InstalledProgramsDiscoveryService.ReadStorePackagesAsync(CancellationToken.None).GetAwaiter().GetResult();
        if (!packages.Available) throw new IOException(SettingsService.L(
            "Не удалось проверить пакеты Windows. Очистка отложена.", "Windows packages could not be checked. Cleanup deferred."));
        apps.AddRange(packages.Apps);
        if (status.HasFailures || status.MetadataWarnings.Count > 0 || apps.Any(a => a.Identity.Equals(removedIdentity, StringComparison.OrdinalIgnoreCase)))
            throw new IOException(SettingsService.L("Наличие программ изменилось или не удалось проверить все записи. Обновите список.",
                "Installed applications changed or registrations could not all be checked. Refresh the inventory."));
        if (removedIdentity == null) return apps;
        // Store registration was checked above through the package inventory.
        // It is not a registry:HKCU/HKLM uninstall identity.
        if (removedIdentity.StartsWith("msix:", StringComparison.OrdinalIgnoreCase)) return apps;
        using var original = InstalledProgramUninstallPlanReader.OpenRegistration(removedIdentity);
        if (original != null) throw new IOException(SettingsService.L("Программа снова зарегистрирована. Очистка отменена.", "Application is registered again. Cleanup cancelled."));
        return apps;
    }

    internal static async Task<ProgramCleanupBackup> CleanAsync(ProgramLeftoverReport report, IReadOnlyList<ProgramLeftoverProbe> selected,
        IProgress<string>? progress = null)
    {
        if (!await Gate.WaitAsync(0)) throw new IOException(SettingsService.L("Другая очистка уже выполняется.", "Another cleanup is already running."));
        try { return await Task.Run(() => Clean(report, selected, progress)); }
        finally { Gate.Release(); }
    }

    private static ProgramCleanupBackup Clean(ProgramLeftoverReport report, IReadOnlyList<ProgramLeftoverProbe> selected, IProgress<string>? progress)
    {
        if (selected.Count == 0 || selected.Count > 200 || (report.Baseline.Identity.StartsWith("msix:", StringComparison.OrdinalIgnoreCase) &&
            selected.Any(probe => !ProgramPackageDataLayout.ExternalDataScope(probe, report.Baseline))))
            throw new IOException(SettingsService.L("Нет поддерживаемых объектов для очистки.", "No supported objects selected for cleanup."));
        if (!ProgramRemovalOutcome.CanClean(report.Baseline.NativeResult == null ? null : ProgramPendingProcesses.Refresh(report.Baseline.NativeResult))) ProgramLeftoverCleanupPolicy.Deny();
        ProgramCleanupBackup backup = new(Guid.NewGuid().ToString("N"), report.Baseline.Id,
            report.Baseline.Name, DateTime.UtcNow, new(), report.Baseline.Identity);
        ProgramCleanupBackupStore.Save(backup);
        int completed = 0;
        foreach (ProgramLeftoverProbe probe in selected.Distinct())
        {
            progress?.Report(SettingsService.L($"Очистка объекта {++completed} из {selected.Count}…\n\n",
                $"Cleaning item {completed} of {selected.Count}…\n\n") +
                (probe.Kind == "Registry" ? ProgramRegistryIdentity.Display(probe) : probe.Path));
            int index = -1;
            try
            {
                if (probe.Group is not (ProgramLeftoverGroup.Related or ProgramLeftoverGroup.UserData) || !report.Found.Contains(probe) ||
                    !report.Baseline.Probes.Any(p => ProgramLeftoverCleanupPolicy.SameEvidence(p, probe)))
                    ProgramLeftoverCleanupPolicy.Deny();
                if (!ProgramRemovalDecisions.Evaluate(probe, report.Baseline,
                    new(ProgramRemovalOutcome.CanClean(report.Baseline.NativeResult))).Allowed) ProgramLeftoverCleanupPolicy.Deny();
                if (ProgramLeftoverCleanupPolicy.AlreadyAbsent(probe))
                {
                    backup.Entries.Add(new(probe, "", null, "AlreadyAbsent"));
                    ProgramCleanupBackupStore.Save(backup); continue;
                }
                var inventory = LiveInventory(report.Baseline.Identity);
                string root = ProgramLeftoverCleanupPolicy.Root(probe);
                ProgramLeftoverCleanupPolicy.CheckInventory(root, inventory);
                if (probe.Kind is "Directory" or "UserData" or "RecordedFile" or "Shortcut") ProgramSharedComponentGuard.Check(probe.Path);
                ProgramCleanupEntry entry;
                if (probe.Kind == "UserData")
                {
                    ProgramUserDataLeftoverService.Validate(probe, report.Baseline, inventory);
                    entry = new(probe, "", null);
                }
                else if (probe.Kind == "Directory")
                {
                    if (report.Baseline.ObservationOnly && !ProgramLeftoverCleanupPolicy.IsEmptyDirectoryTree(root))
                        ProgramLeftoverCleanupPolicy.Deny();
                    if (probe.EvidenceTarget == ProgramRecordedInstallCleanup.DirectoryEvidence)
                        ProgramRecordedInstallCleanup.Validate(probe, report.Baseline);
                    else ProgramLeftoverCleanupPolicy.CheckDirectory(root);
                    entry = new(probe, "", null);
                }
                else if (probe.Kind == "RecordedFile")
                { ProgramRecordedInstallCleanup.Validate(probe, report.Baseline); entry = new(probe, "", null); }
                else if (probe.Kind == "Shortcut")
                { ProgramShortcutLeftoverService.Validate(probe); entry = new(probe, "", null); }
                else if (probe.Kind == "Registry") entry = new(probe, "", probe.EvidenceTarget == ProgramSettingsRegistryCleanup.Evidence
                    ? ProgramSettingsRegistryCleanup.Prepare(probe, report.Baseline) : ProgramLeftoverRegistryCleanup.Capture(probe));
                else { ProgramLeftoverCleanupPolicy.Deny(); return backup; }
                index = backup.Entries.Count;
                backup.Entries.Add(entry);
                ProgramMutationBarrier.Execute(
                    () => ProgramCleanupBackupStore.Save(backup),
                    () =>
                    {
                        ProgramLeftoverCleanupPolicy.CheckInventory(root, LiveInventory(report.Baseline.Identity));
                        if (probe.Kind is "Directory" or "UserData" or "RecordedFile" or "Shortcut")
                            ProgramSharedComponentGuard.Check(probe.Path);
                    },
                    () =>
                    {
                        if (probe.Kind == "UserData")
                        {
                            ProgramUserDataLeftoverService.Validate(probe, report.Baseline, LiveInventory(report.Baseline.Identity));
                            ProgramRecycleBin.SendAsync(probe.Path).GetAwaiter().GetResult();
                        }
                        else if (probe.Kind == "Directory")
                        {
                            if (report.Baseline.ObservationOnly && !ProgramLeftoverCleanupPolicy.IsEmptyDirectoryTree(root))
                                ProgramLeftoverCleanupPolicy.Deny();
                            if (probe.EvidenceTarget == ProgramRecordedInstallCleanup.DirectoryEvidence)
                                ProgramRecordedInstallCleanup.Validate(probe, report.Baseline);
                            else ProgramLeftoverCleanupPolicy.CheckDirectory(root);
                            ProgramRecycleBin.SendAsync(root).GetAwaiter().GetResult();
                        }
                        else if (probe.Kind == "RecordedFile")
                        {
                            ProgramRecordedInstallCleanup.Validate(probe, report.Baseline);
                            ProgramRecycleBin.SendAsync(probe.Path).GetAwaiter().GetResult();
                        }
                        else if (probe.Kind == "Shortcut")
                        {
                            ProgramShortcutLeftoverService.Validate(probe);
                            ProgramRecycleBin.SendAsync(probe.Path).GetAwaiter().GetResult();
                        }
                        else if (probe.EvidenceTarget == ProgramSettingsRegistryCleanup.Evidence)
                        {
                            ProgramSettingsRegistryCleanup.Prepare(probe, report.Baseline);
                            ProgramSettingsRegistryCleanup.Remove(probe, entry.Registry!);
                        }
                        else ProgramLeftoverRegistryCleanup.Remove(probe, entry.Registry!);
                    });
                backup.Entries[index] = entry with { State = probe.Kind is "Directory" or "Shortcut" or "UserData" or "RecordedFile" ? "Recycled" : "Removed" };
                ProgramCleanupBackupStore.Save(backup);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or
                ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.COMException)
            {
                var failure = index >= 0 ? backup.Entries[index] : new ProgramCleanupEntry(probe, "", null);
                bool absent = ProgramLeftoverCleanupPolicy.AlreadyAbsent(probe);
                failure = ProgramCleanupFailurePolicy.Record(failure, index >= 0, absent, ex.Message);
                if (index >= 0) backup.Entries[index] = failure; else backup.Entries.Add(failure);
                ProgramCleanupBackupStore.Save(backup);
            }
        }
        return backup;
    }

    internal static async Task<ProgramCleanupBackup> RestoreAsync(ProgramCleanupBackup backup)
    {
        if (!await Gate.WaitAsync(0)) throw new IOException(SettingsService.L("Другая очистка уже выполняется.", "Another cleanup is already running."));
        try { return await Task.Run(() => Restore(backup)); }
        finally { Gate.Release(); }
    }

    private static ProgramCleanupBackup Restore(ProgramCleanupBackup backup)
    {
        if (!Guid.TryParseExact(backup.Id, "N", out _)) throw new IOException(SettingsService.L("Некорректный идентификатор резервной копии.", "Invalid backup identity."));
        backup = ProgramCleanupBackupStore.ReadAll().FirstOrDefault(b => b.Id == backup.Id)
            ?? throw new IOException(SettingsService.L("Копия уже отсутствует или срок её хранения истёк.", "Backup is unavailable or its retention has expired."));
        var contents = ProgramRecoveryInventory.Inspect(backup);
        if (!contents.CanRestore) throw new IOException(SettingsService.L(
            "Доступных копий для восстановления нет. Обновите список.", "No accessible backups are available to restore. Refresh the list."));
        for (int i = 0; i < backup.Entries.Count; i++)
        {
            ProgramCleanupEntry entry = backup.Entries[i];
            if (entry.State is "Restored" or "Recycled" or "Expired" or "Discarded") continue;
            if (!contents.Available.Contains(entry))
            {
                var inspection = ProgramRecoveryInventory.Inspect(backup with { Entries = new() { entry } });
                backup.Entries[i] = inspection.Visible
                    ? entry with { State = "Unavailable", Detail = string.Join("\n", inspection.Issues) }
                    : entry with { State = "Missing", Registry = null, BackupPath = "", Detail = "" };
                ProgramCleanupBackupStore.Save(backup);
                continue;
            }
            try
            {
                string root = ProgramLeftoverCleanupPolicy.Root(entry.Probe);
                ProgramLeftoverCleanupPolicy.CheckInventory(root, LiveInventory(backup.Identity));
                if (entry.Probe.Kind == "Directory")
                {
                    if (entry.BackupPath != ProgramLeftoverCleanupPolicy.BackupPath(root, backup.Id) ||
                        !Directory.Exists(entry.BackupPath) || Directory.Exists(root) || File.Exists(root) ||
                        InstalledProgramLocationService.HasReparseAncestor(entry.BackupPath))
                        throw new IOException(SettingsService.L("Копия недоступна или исходный путь уже занят. Перезапись запрещена.", "Backup unavailable or original path already occupied. Overwrite refused."));
                    Directory.Move(entry.BackupPath, root);
                }
                else if (entry.Registry != null)
                {
                    if (entry.Probe.EvidenceTarget is ProgramSettingsRegistryCleanup.Evidence or ProgramOrphanRegistration.Evidence)
                        ProgramSettingsRegistryCleanup.Restore(entry.Probe, entry.Registry);
                    else ProgramLeftoverRegistryCleanup.Restore(entry.Probe, entry.Registry);
                }
                else throw new IOException(SettingsService.L("Для объекта нет сохранённой копии.", "No saved backup for this object."));
                backup.Entries[i] = entry with { State = "Restored", Detail = "", Registry = null, BackupPath = "" };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                System.Security.SecurityException or InvalidOperationException or System.ComponentModel.Win32Exception or FormatException)
            { backup.Entries[i] = entry with { Detail = ex.Message }; }
            ProgramCleanupBackupStore.Save(backup);
        }
        return backup;
    }

    internal static async Task<T> ExclusiveAsync<T>(Func<T> action)
    {
        await Gate.WaitAsync();
        try { return await Task.Run(action); }
        finally { Gate.Release(); }
    }
}
