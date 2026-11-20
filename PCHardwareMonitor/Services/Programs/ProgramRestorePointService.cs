using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management;
using System.Threading;
using System.Threading.Tasks;

namespace PCHardwareMonitor;

internal sealed record ProgramRestorePointResult(string State, DateTime AttemptedUtc, string Description,
    uint? SequenceNumber = null, string Detail = "");

internal static class ProgramRestorePointService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    internal static Task<bool> IsAvailableAsync(ProgramRestorePointResult point) => Task.Run(() =>
    {
        if (point.State != "Created" || point.SequenceNumber is not uint sequence) return false;
        try
        {
            var scope = Scope(); scope.Connect();
            return ReadPoints(scope).TryGetValue(sequence, out string? description) && description == point.Description;
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or
            System.Runtime.InteropServices.COMException or InvalidOperationException or TimeoutException or System.Security.SecurityException)
        { DiagnosticLog.Failure("RestorePoint.Recheck", ex); return false; }
    });

    internal static async Task<ProgramRestorePointResult> CreateAsync(string applicationName, IProgress<string>? progress = null)
    {
        await Gate.WaitAsync();
        try { return await Task.Run(() => Create(applicationName, progress)); }
        finally { Gate.Release(); }
    }

    private static ManagementScope Scope() => new(@"\\.\root\default", new ConnectionOptions
    {
        EnablePrivileges = true, Impersonation = ImpersonationLevel.Impersonate,
        Timeout = TimeSpan.FromSeconds(30)
    });

    private static Dictionary<uint, string> ReadPoints(ManagementScope scope)
    {
        Dictionary<uint, string> points = new();
        using ManagementObjectSearcher query = new(scope,
            new ObjectQuery("SELECT SequenceNumber, Description FROM SystemRestore"),
            new EnumerationOptions { Timeout = TimeSpan.FromSeconds(30), ReturnImmediately = false });
        using ManagementObjectCollection rows = query.Get();
        foreach (ManagementObject row in rows)
        {
            using (row) points[Convert.ToUInt32(row["SequenceNumber"], CultureInfo.InvariantCulture)] =
                Convert.ToString(row["Description"], CultureInfo.InvariantCulture) ?? "";
        }
        return points;
    }

    private static uint Invoke(ManagementClass systemRestore, string description)
    {
        using ManagementBaseObject input = systemRestore.GetMethodParameters("CreateRestorePoint");
        input["Description"] = description;
        input["RestorePointType"] = 1u; // APPLICATION_UNINSTALL
        input["EventType"] = 100u; // BEGIN_SYSTEM_CHANGE, as in Microsoft's WMI CreateRestorePoint example.
        using ManagementBaseObject output = systemRestore.InvokeMethod("CreateRestorePoint", input,
            new InvokeMethodOptions { Timeout = TimeSpan.FromMinutes(3) });
        return Convert.ToUInt32(output["ReturnValue"], CultureInfo.InvariantCulture);
    }

    private static ProgramRestorePointResult Create(string applicationName, IProgress<string>? progress)
    {
        DateTime attempted = DateTime.UtcNow;
        string name = applicationName.Length > 140 ? applicationName[..140] : applicationName;
        string description = SettingsService.L("Thermiqra — перед удалением: ", "Thermiqra — before uninstall: ") +
            name + " [" + Guid.NewGuid().ToString("N")[..8] + "]";
        try
        {
            ManagementScope scope = Scope(); scope.Connect();
            using ManagementClass systemRestore = new(scope, new ManagementPath("SystemRestore"), null);
            progress?.Report(SettingsService.L("Включение защиты системы средствами Windows…", "Enabling System Protection through Windows…"));
            uint enabled = ProgramRestoreProtectionService.Enable(systemRestore);
            if (enabled != 0) return new("Failed", attempted, description, Detail: SettingsService.L(
                "Windows не включила защиту системы. ", "Windows could not enable System Protection. ") + WindowsCode(enabled));
            // Enable returns before the filter is fully ready. Give the provider
            // time to start; do not race creation against enabling protection.
            progress?.Report(SettingsService.L("Подготовка защиты системы…", "Preparing System Protection…"));
            Thread.Sleep(1500);
            var before = ReadPoints(scope);
            progress?.Report(SettingsService.L("Создание точки восстановления… Удаление ещё не началось.",
                "Creating the restore point… Uninstall has not started yet."));
            // The provider manages protection; no frequency or disk-space policy is changed.
            uint begin = Invoke(systemRestore, description);
            for (int retry = 0; retry < 3 && begin is 1058 or 1062 or 0x80070422 or 0x80070426; retry++)
            {
                progress?.Report(SettingsService.L("Ожидание готовности защиты системы…", "Waiting for System Protection to become ready…"));
                Thread.Sleep(1500);
                begin = Invoke(systemRestore, description);
            }
            if (begin != 0) return new("Failed", attempted, description, Detail: WindowsCode(begin));
            // Verify a new record with this unique description, not merely S_OK:
            // frequency throttling can return success without a new checkpoint.
            progress?.Report(SettingsService.L("Проверка созданной точки в Windows…", "Verifying the new restore point in Windows…"));
            for (int attempt = 0; attempt < 10; attempt++)
            {
                foreach (var point in ReadPoints(scope))
                    if (!before.ContainsKey(point.Key) && point.Value == description)
                        return new("Created", attempted, description, point.Key);
                if (attempt < 9) Thread.Sleep(1000); // Worker thread; never blocks the window dispatcher.
            }
            return new("Unconfirmed", attempted, description, Detail: SettingsService.L(
                "Windows приняла запрос включения защиты, но новая точка не появилась. Удаление ещё не началось. Проверьте доступное место и последние точки в разделе «Защита системы».",
                "Windows accepted the request to enable protection, but no new restore point appeared. Uninstall has not started yet. Check free space and recent restore points in System Protection."));
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or
            System.Runtime.InteropServices.COMException or InvalidOperationException or TimeoutException or
            System.Security.SecurityException)
        {
            return new("Failed", attempted, description, Detail: SettingsService.L(
                "Создать и проверить точку восстановления не удалось. ", "Could not create and verify a restore point. ") + ex.Message);
        }
    }

    private static string WindowsCode(uint code)
    {
        string reason = code switch
        {
            1058 or 0x80070422 => SettingsService.L("Защита системы или её служба отключена. ", "System Protection or its service is disabled. "),
            5 or 0x80070005 => SettingsService.L("Windows отказала в доступе. ", "Windows denied access. "),
            112 or 0x80070070 => SettingsService.L("Недостаточно места для точки восстановления. ", "Insufficient space for a restore point. "),
            _ => ""
        };
        return reason + SettingsService.L("Код Windows: ", "Windows code: ") + "0x" + code.ToString("X8", CultureInfo.InvariantCulture);
    }

    internal static string Summary(ProgramRestorePointResult result) => result.State switch
    {
        "Created" => SettingsService.L("Точка восстановления Windows создана", "Windows restore point created") +
            SettingsService.L(" · № ", " · #") + result.SequenceNumber + (result.Detail.Length > 0 ? "\n" + result.Detail : ""),
        "NotRequested" => SettingsService.L("Точка восстановления не запрашивалась", "Restore point was not requested"),
        _ => SettingsService.L("Новая точка восстановления не подтверждена", "A new restore point was not confirmed") +
            (result.Detail.Length > 0 ? "\n" + result.Detail : "")
    };
}
