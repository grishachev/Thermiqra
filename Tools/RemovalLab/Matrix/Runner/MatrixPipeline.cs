using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;
using PCHardwareMonitor;

namespace Thermiqra.RemovalLab;

internal static class MatrixPipeline
{
    internal static async Task Run(CaseResult test)
    {
        Fixture fixture = await Fixture.Install(test.Id);
        fixture.VerifyNeighbors(test); fixture.VerifyRegistry(test);
        foreach (var file in fixture.Files) test.Require(File.Exists(file.Path) && Fixture.Hash(file.Path) == file.Hash, "Installed fixture hash matches");
        string? sharedFile = null;
        if (test.Id == "Shared") sharedFile = PrepareShared(fixture);
        if (test.Id == "Reparse") Directory.CreateSymbolicLink(Path.Combine(fixture.Roots[0], "ExternalLink"), Path.GetDirectoryName(fixture.Files.Single(f => f.Preserve).Path)!);
        var status = new InstalledProgramsReadStatus();
        var inventory = InstalledProgramsDiscoveryService.ReadRegistryPrograms(default, status);
        test.Require(!status.HasFailures, "Installed-program sources available");
        var app = inventory.Single(a => a.Name == fixture.Name);
        test.Require(app.Kind == (test.Id.StartsWith("Msi") ? "MSI" : "Win32"), "Fixture recognized by real discovery");
        var plan = await InstalledProgramUninstallPlanReader.ReadAsync(app, quiet: true);
        using var scope = new ProgramNativeRemovalScope(app.Identity);
        var baseline = ProgramInstallationMemory.CaptureForRemoval(app, plan, inventory);
        ProgramRemovalReviewStore.SaveBaseline(baseline);
        var native = await InstalledProgramUninstallService.ExecuteAsync(app, plan, quiet: true);
        baseline = baseline with { NativeResult = native };
        ProgramRemovalReviewStore.SaveBaseline(baseline);
        var report = await ProgramLeftoverRecheckService.RecheckAsync(baseline);
        test.Require(ProgramRemovalReviewStore.HasReport(baseline.Id), "Uninstall remains inspectable in history");
        File.WriteAllText(Path.Combine(fixture.Control, "native-result.json"), System.Text.Json.JsonSerializer.Serialize(native, Program.Json));

        if (test.Id is "Error" or "Cancel" or "MsiCancel" or "MsiError" or "MsiReboot")
        {
            var expected = test.Id switch { "Cancel" => InstalledProgramRemovalState.Unconfirmed,
                "MsiCancel" => InstalledProgramRemovalState.Cancelled, "MsiReboot" => InstalledProgramRemovalState.RebootRequired,
                _ => InstalledProgramRemovalState.Failed };
            test.Require(native.State == expected, "Native outcome classified: " + expected);
            test.Require(!ProgramRemovalOutcome.CanClean(native), "No additional cleanup after cancellation, retained registration or pending reboot");
            if (test.Id == "MsiReboot") { test.BaselineId = baseline.Id; test.Status = "AwaitingReboot"; }
            fixture.VerifyNeighbors(test); return;
        }
        if (test.Id == "PartialError") test.Require(native.State == InstalledProgramRemovalState.Failed && native.RegistrationAbsent, "Error with removed registration still allows inspection");
        else test.Require(native.State == InstalledProgramRemovalState.Removed, "Native removal confirmed");

        if (test.Id == "Helper")
        {
            test.Require(!ProgramRemovalOutcome.CanClean(report.Baseline.NativeResult), "Running child prevents cleanup after parent exit");
            File.WriteAllText(Path.Combine(fixture.Control, "release"), "continue");
            for (int i = 0; i < 100; i++)
            {
                await Task.Delay(100);
                var pending = ProgramPendingProcesses.Refresh(report.Baseline.NativeResult!);
                if (pending.PendingHelpers?.Count is null or 0) break;
            }
            report = await ProgramLeftoverRecheckService.RecheckAsync(report.Baseline);
            test.Require(ProgramRemovalOutcome.CanClean(report.Baseline.NativeResult), "Child completion unlocks a fresh inspection");
        }

        if (test.Id is "Shared" or "Reparse")
        {
            _ = await Clean(report);
            if (sharedFile != null)
            {
                test.Require(File.Exists(sharedFile) && File.ReadAllText(sharedFile) == "Shared fixture - preserve", "Referenced shared DLL remains");
                using var shared = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs");
                test.Require(shared?.GetValue(sharedFile) is int count && count == 1, "Shared-component reference count unchanged");
                using var peer = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\ThermiqraMatrixSharedPeer");
                test.Require(peer != null, "Other application's uninstall registration remains");
            }
            if (test.Id == "Reparse") test.Require(Directory.Exists(Path.Combine(fixture.Roots[0], "ExternalLink")), "Linked folder excluded");
            fixture.VerifyNeighbors(test);
            var remaining = await ProgramLeftoverRecheckService.RecheckAsync(report.Baseline);
            test.Require(remaining.Found.Count > 0 || remaining.Limitations.Count > 0, "Protected or unchecked resources cannot become a clean result");
            return;
        }

        if (test.Id == "Locked")
        {
            using var holder = Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "MatrixSubject.exe"), "--hold " + test.Id)
                { UseShellExecute = false, CreateNoWindow = true }) ?? throw new IOException("Lock helper failed");
            try
            {
                await WaitForFile(Path.Combine(fixture.Control, "lock.ready"));
                var locked = await Clean(report);
                test.Require(locked.Entries.Any(e => e.State is "Failed" or "Recycled"), "Locked content is either retained or confirmed recycled; no permanent fallback");
            }
            finally { File.WriteAllText(Path.Combine(fixture.Control, "release"), "continue"); await holder.WaitForExitAsync(); }
            if (Directory.Exists(fixture.Roots[0]))
                test.Require(Fixture.Hash(Path.Combine(fixture.Roots[0], "Runtime", "locked.dat")) == fixture.Files.First(f => f.Path.EndsWith("locked.dat")).Hash, "Retained locked file unchanged");
            report = await ProgramLeftoverRecheckService.RecheckAsync(report.Baseline);
        }

        if (test.Id == "Denied")
        {
            var directory = new DirectoryInfo(Path.Combine(fixture.Roots[0], "Runtime"));
            var before = directory.GetAccessControl();
            var restricted = directory.GetAccessControl();
            restricted.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ReadData | FileSystemRights.Delete,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Deny));
            try
            {
                directory.SetAccessControl(restricted);
                var denied = await Clean(report);
                test.Require(denied.Entries.Any(e => e.State == "Failed"), "Access denial is reported, not successful deletion");
            }
            finally
            {
                var restore = new DirectorySecurity();
                restore.SetSecurityDescriptorBinaryForm(before.GetSecurityDescriptorBinaryForm(), AccessControlSections.Access);
                directory.SetAccessControl(restore);
            }
            report = await ProgramLeftoverRecheckService.RecheckAsync(report.Baseline);
        }

        var backup = await Clean(report);
        test.Require(!backup.Entries.Any(e => e.State is "Failed" or "Prepared"), "Selected cleanup actions completed");
        fixture.VerifyGone(test);
        var checkedReport = await ProgramLeftoverRecheckService.RecheckAsync(report.Baseline);
        test.Require(checkedReport.Found.Count == 0, "Real discovery finds no remaining fixture objects");

        if (test.Id is "Restore" or "Collision")
        {
            test.Require(backup.Entries.Any(e => e.Registry != null && e.State == "Removed"), "Registry backup exists before recovery");
            if (test.Id == "Collision")
            {
                using (var occupied = Registry.CurrentUser.CreateSubKey(fixture.RegistryPath)) occupied.SetValue("RunId", "foreign-new-data");
                var rejected = await ProgramLeftoverCleanupService.RestoreAsync(backup);
                using var retained = Registry.CurrentUser.OpenSubKey(fixture.RegistryPath);
                test.Require(retained?.GetValue("RunId") as string == "foreign-new-data", "Recovery never overwrites new registry contents");
                test.Require(!rejected.Entries.Any(e => e.State == "Restored"), "Collision is visible as incomplete recovery");
            }
            else
            {
                var restored = await ProgramLeftoverCleanupService.RestoreAsync(backup);
                test.Require(restored.Entries.Any(e => e.State == "Restored"), "Real recovery service restored registry data");
                fixture.VerifyRegistry(test);
                await RecycleRecovery.RestoreFixtureFolders(fixture, backup, test);
                foreach (var file in fixture.Files) test.Require(File.Exists(file.Path) && Fixture.Hash(file.Path) == file.Hash, "Restored file matches independent pre-uninstall hash");
            }
            fixture.VerifyNeighbors(test);
        }
    }

    private static async Task<ProgramCleanupBackup> Clean(ProgramLeftoverReport report)
    {
        var selected = report.Found.Where(p => ProgramRemovalDecisions.Evaluate(p, report.Baseline,
            new(ProgramRemovalOutcome.CanClean(report.Baseline.NativeResult))).Allowed).ToList();
        return selected.Count > 0 ? await ProgramLeftoverCleanupService.CleanAsync(report, selected) :
            new(Guid.NewGuid().ToString("N"), report.Baseline.Id, report.Baseline.Name, DateTime.UtcNow, new(), report.Baseline.Identity);
    }
    internal static async Task ResumeReboot(CaseResult test)
    {
        test.Require(Guid.TryParseExact(test.BaselineId, "N", out _), "Known pre-reboot report identity");
        var saved = ProgramRemovalReviewStore.ReadHistory().Single(r => r.Baseline.Id == test.BaselineId);
        var refreshed = await ProgramLeftoverRecheckService.RecheckAsync(saved.Baseline);
        test.Require(ProgramRemovalOutcome.CanClean(refreshed.Baseline.NativeResult), "Real restart releases pending-reboot hold after fresh registration check");
        var cleanup = await Clean(refreshed);
        test.Require(!cleanup.Entries.Any(e => e.State is "Failed" or "Prepared"), "Post-reboot cleanup finished");
        Fixture.Load("MsiReboot").VerifyGone(test);
    }
    private static async Task WaitForFile(string path)
    { for (int i = 0; i < 100 && !File.Exists(path); i++) await Task.Delay(100); if (!File.Exists(path)) throw new IOException("Helper did not signal readiness"); }
    private static string PrepareShared(Fixture fixture)
    {
        string root = Path.Combine(fixture.Roots[0], "SharedEngine"); Directory.CreateDirectory(root);
        string library = Path.Combine(root, "Shared.dll"); File.WriteAllText(library, "Shared fixture - preserve");
        string executable = Path.Combine(root, "MatrixSubject.exe"); File.Copy(Path.Combine(AppContext.BaseDirectory, "MatrixSubject.exe"), executable);
        using (var shared = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs")) shared.SetValue(library, 1, RegistryValueKind.DWord);
        using (var peer = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\ThermiqraMatrixSharedPeer"))
        {
            peer.SetValue("DisplayName", "ThermiqraMatrixSharedPeer"); peer.SetValue("DisplayVersion", "1.0"); peer.SetValue("Publisher", "ThermiqraLab"); peer.SetValue("InstallLocation", root);
            peer.SetValue("DisplayIcon", executable); peer.SetValue("UninstallString", "\"" + executable + "\" --uninstall Shared");
        }
        return library;
    }
}
