using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PCHardwareMonitor;

internal enum InstalledProgramRemovalState { Removed, Cancelled, RebootRequired, Failed, Unconfirmed, RegistrationRemoved }
internal sealed record InstalledProgramRemovalResult(InstalledProgramRemovalState State, int? ExitCode, string Detail,
    bool RegistrationAbsent = false, System.Collections.Generic.List<ProgramPendingProcess>? PendingHelpers = null,
    DateTime? AttemptedUtc = null, bool HelperObservationIncomplete = false);

internal static class InstalledProgramUninstallService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    internal static async Task<InstalledProgramRemovalResult> ExecuteAsync(InstalledProgramInfo app,
        InstalledProgramUninstallPlan approved, bool quiet = false)
    {
        if (!await Gate.WaitAsync(0)) throw new InvalidOperationException(SettingsService.L(
            "Уже выполняется удаление другой программы.", "Another application removal is already running."));
        string? journal = null;
        DateTime attemptedUtc = DateTime.UtcNow;
        InstalledProgramRemovalResult? result = null;
        try
        {
            // Revalidate after the confirmation dialog, before any side effect.
            var live = await InstalledProgramUninstallPlanReader.ReadAsync(app, quiet);
            if (live.Fingerprint != approved.Fingerprint) throw new InvalidOperationException(SettingsService.L(
                "Команда удаления изменилась. Обновите список и повторите действие.",
                "Uninstall command changed. Refresh the list and try again."));
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Thermiqra", "UninstallLogs");
            Directory.CreateDirectory(directory);
            journal = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N") + ".json");
            ProgramOperationJournal.Write(journal, new { StartedUtc = DateTime.UtcNow, Plan = live });
            int code;
            string detail = "";
            bool completionObserved = true;
            bool observationIncomplete = false;
            System.Collections.Generic.List<ProgramPendingProcess>? pending = null;
            if (live.Kind == "MSIX")
            {
                string family = live.Identity[5..];
                var operation = await InstalledProgramPackageOperations.RunAsync(
                    "$p=" + InstalledProgramPackageOperations.FamilyQuery(family) +
                    "; if($p.Count -ne 1 -or $p[0].PackageFullName -cne " + InstalledProgramPackageOperations.Literal(live.PackageFullName) +
                    "){throw 'Package changed before removal'}; if($p[0].NonRemovable -or $p[0].IsFramework -or $p[0].IsResourcePackage -or $p[0].IsPartOfSystem -or [string]$p[0].SignatureKind -eq 'System'){throw 'Protected package'}; " +
                    "Remove-AppxPackage -Package $p[0].PackageFullName -ErrorAction Stop", false);
                code = operation.Code;
                detail = operation.Output;
            }
            else
            {
                ProcessStartInfo start = new()
                {
                    FileName = live.Executable, Arguments = live.Arguments, UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(live.Executable)!,
                    Verb = live.Machine ? "runas" : "open"
                };
                using ProgramUninstallerTail tail = new(app.InstallLocation, live.Executable);
                using Process process = Process.Start(start) ?? throw new IOException(SettingsService.L(
                    "Не удалось запустить деинсталлятор.", "Could not start the uninstaller."));
                tail.SetParent(process.Id);
                // No forced termination: a vendor may be halfway through its operation.
                await process.WaitForExitAsync();
                code = process.ExitCode;
                completionObserved = await tail.WaitAsync();
                observationIncomplete = !tail.ObservationAvailable;
                if (!completionObserved) pending = tail.PendingProcesses();
            }
            if (code is 3010 or 1641)
                result = InstalledProgramNativeOutcome.Classify(live.Kind, code, null, detail, pending);
            else if (live.Kind == "MSI" && code == 1602)
                result = InstalledProgramNativeOutcome.Classify(live.Kind, code, null, detail, pending);
            else
            {
                bool? present = null;
                try
                {
                    for (int attempt = 0; attempt < 30; attempt++)
                    {
                        present = await IsPresentAsync(live);
                        if (present == false || (code != 0 && attempt >= 2)) break;
                        await Task.Delay(2000);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                    InvalidOperationException or System.Security.SecurityException) { detail = ex.Message; }
                string finalDetail = !completionObserved && present == false ? SettingsService.L(
                    "Запись удаления исчезла; завершение всех вспомогательных процессов не подтверждено. Список остатков проверяется повторно.",
                    "The uninstall registration disappeared; completion of every helper process was not confirmed. Leftover locations are checked again.") +
                    (detail.Length > 0 ? "\n" + detail : "") : detail;
                result = InstalledProgramNativeOutcome.Classify(live.Kind, code, present, finalDetail, pending);
            }
            return FinishJournal(journal, approved, result with { AttemptedUtc = attemptedUtc, HelperObservationIncomplete = observationIncomplete });
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            result = new(InstalledProgramRemovalState.Cancelled, 1223, "");
            return FinishJournal(journal, approved, result with { AttemptedUtc = attemptedUtc });
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException or
            UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
        {
            // Preserve the attempt for inspection even if starting/waiting or
            // verification failed. No absence is assumed after an exception.
            result = new(InstalledProgramRemovalState.Failed, null, ex.Message, HelperObservationIncomplete: true);
            return FinishJournal(journal, approved, result with { AttemptedUtc = attemptedUtc });
        }
        finally { Gate.Release(); }
    }

    private static InstalledProgramRemovalResult FinishJournal(string? journal, InstalledProgramUninstallPlan plan,
        InstalledProgramRemovalResult result)
    {
        if (journal == null) return result;
        try { ProgramOperationJournal.Write(journal, new { FinishedUtc = DateTime.UtcNow, Plan = plan, Result = result, Completed = true }); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            DiagnosticLog.Failure("Uninstall.Journal", ex);
            return result with { Detail = result.Detail + "\n" + SettingsService.L(
                "Не удалось сохранить итоговый журнал удаления. Исходная попытка и снимок сохранены; проверьте результат в истории.",
                "Could not save the final uninstall journal. The initial attempt and snapshot were retained; review the result in history.") };
        }
        return result;
    }

    private static async Task<bool> IsPresentAsync(InstalledProgramUninstallPlan plan)
    {
        if (plan.Kind == "MSIX")
            return await InstalledProgramPackageOperations.QueryAsync(
                "$p=" + InstalledProgramPackageOperations.FamilyQuery(plan.Identity[5..]) + "; [Console]::Write($p.Count)") != "0";
        using var registration = InstalledProgramUninstallPlanReader.OpenRegistration(plan.Identity);
        return registration != null;
    }
}
