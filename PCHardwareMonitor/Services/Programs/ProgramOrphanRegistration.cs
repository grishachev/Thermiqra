using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PCHardwareMonitor;

internal sealed record ProgramOrphanRegistrationCandidate(InstalledProgramInfo App, string Executable,
    ProgramLeftoverProbe Probe, ProgramRegistrySnapshot Snapshot);

internal static class ProgramOrphanRegistration
{
    internal const string Evidence = "orphan-uninstall-registration";
    internal const string Parent = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    internal static ProgramOrphanRegistrationCandidate? Read(InstalledProgramInfo app)
    {
        if (!app.Identity.StartsWith("registry:", StringComparison.Ordinal)) return null;
        using var key = InstalledProgramUninstallPlanReader.OpenRegistration(app.Identity);
        if (key == null || key.SubKeyCount != 0) return null;
        string ReadValue(string name) => Convert.ToString(key.GetValue(name)) ?? "";
        if (ReadValue("DisplayName").Trim() != app.Name || ReadValue("DisplayVersion") != app.Version ||
            ReadValue("WindowsInstaller") == "1" || ReadValue("SystemComponent") == "1" ||
            ReadValue("NoRemove") == "1" || ReadValue("ParentKeyName").Length > 0) return null;
        string root = ProgramLeftoverPathPolicy.Normalize(ReadValue("InstallLocation"));
        ProgramLeftoverCleanupPolicy.CheckRoot(root);
        ProgramLeftoverCleanupPolicy.CheckAncestors(root);
        // An unavailable volume is not proof that an application was removed.
        File.GetAttributes(Path.GetPathRoot(root)!);
        if (!Missing(root)) return null;
        string command = Environment.ExpandEnvironmentVariables(ReadValue("UninstallString").Trim());
        string executable;
        if (command.StartsWith('"'))
        {
            int end = command.IndexOf('"', 1);
            if (end < 2) return null;
            executable = command[1..end];
        }
        else
        {
            int end = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (end < 1) return null;
            executable = command[..(end + 4)];
        }
        executable = ProgramLeftoverPathPolicy.Normalize(executable);
        if (!executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            !ProgramLeftoverPathPolicy.Inside(executable, root) || !Missing(executable)) return null;
        string[] identity = app.Identity.Split(':', 4);
        ProgramLeftoverProbe probe = new("Registry", Parent + "\\" + identity[3], ProgramLeftoverGroup.UserData,
            "Устаревшая запись удаления: папка приложения и деинсталлятор отсутствуют. Удаляется только запись с резервной копией.",
            "Stale uninstall registration: the application folder and uninstaller are missing. Only the registration is removed, with a backup.",
            identity[1], identity[2], root, Evidence);
        var snapshot = ProgramSettingsRegistryCleanup.Capture(probe);
        probe = probe with { EvidenceFingerprint = ProgramSettingsRegistryCleanup.Fingerprint(snapshot) };
        return new(app with { InstallLocation = root }, executable, probe, snapshot);
    }

    private static bool Missing(string path)
    {
        ProgramLeftoverCleanupPolicy.CheckAncestors(path);
        try { File.GetAttributes(path); return false; }
        catch (FileNotFoundException) { return true; }
        catch (DirectoryNotFoundException) { return true; }
    }

    internal static Task<ProgramLeftoverReport> RemoveAsync(ProgramOrphanRegistrationCandidate approved) =>
        ProgramLeftoverCleanupService.ExclusiveAsync(() => Remove(approved));

    private static ProgramLeftoverReport Remove(ProgramOrphanRegistrationCandidate approved)
    {
        using var scope = new ProgramNativeRemovalScope(approved.App.Identity);
        var fresh = Read(approved.App) ?? throw new IOException(SettingsService.L(
            "Состояние записи изменилось. Обновите список программ.", "Registration state changed. Refresh the application list."));
        if (!ProgramLeftoverCleanupPolicy.SameEvidence(approved.Probe, fresh.Probe))
            throw new IOException(SettingsService.L("Запись изменилась после проверки.", "Registration changed after inspection."));
        var inventory = ProgramLeftoverCleanupService.LiveInventory(null);
        var plan = new InstalledProgramUninstallPlan(fresh.App.Identity, fresh.App.Name, fresh.App.Version, "Win32", "", "", fresh.Probe.Hive == "HKLM");
        var baseline = ProgramInstallationMemory.CaptureForRemoval(fresh.App, plan, inventory);
        baseline = baseline with { NativeResult = new(InstalledProgramRemovalState.Unconfirmed, null,
            SettingsService.L("Начата обработка устаревшей записи. Штатный деинсталлятор не запускался.",
                "Stale-registration handling started. The native uninstaller was not run.")) };
        ProgramRemovalReviewStore.SaveBaseline(baseline);
        ProgramCleanupBackup backup = new(Guid.NewGuid().ToString("N"), baseline.Id, fresh.App.Name, DateTime.UtcNow,
            new() { new(fresh.Probe, "", fresh.Snapshot) }, fresh.App.Identity);
        ProgramCleanupBackupStore.Save(backup); // Durable registry payload before the only mutation.
        try
        {
            // Recheck absence and exact registration again after snapshot collection.
            var last = Read(fresh.App) ?? throw new IOException(SettingsService.L("Состояние программы изменилось.", "Application state changed."));
            if (!ProgramLeftoverCleanupPolicy.SameEvidence(fresh.Probe, last.Probe))
                throw new IOException(SettingsService.L("Запись изменилась после резервирования.", "Registration changed after backup."));
            ProgramSettingsRegistryCleanup.Remove(last.Probe, fresh.Snapshot);
            backup.Entries[0] = backup.Entries[0] with { State = "Removed" };
            ProgramCleanupBackupStore.Save(backup);
        }
        catch (Exception ex)
        {
            backup.Entries[0] = backup.Entries[0] with { State = "Failed", Detail = ex.Message };
            ProgramCleanupBackupStore.Save(backup); throw;
        }
        baseline = baseline with { NativeResult = new(InstalledProgramRemovalState.RegistrationRemoved, null, "", true) };
        ProgramRemovalReviewStore.SaveBaseline(baseline);
        var report = ProgramLeftoverDiscoveryService.Inspect(baseline);
        ProgramRemovalReviewStore.SaveReport(report); return report;
    }
}
