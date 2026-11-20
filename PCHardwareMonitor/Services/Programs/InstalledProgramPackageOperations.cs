using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PCHardwareMonitor;

internal static class InstalledProgramPackageOperations
{
    internal static string Literal(string value) => "'" + value.Replace("'", "''") + "'";
    internal static string FamilyQuery(string family) =>
        "@(Get-AppxPackage -PackageTypeFilter Main -ErrorAction Stop | Where-Object { $_.PackageFamilyName -ceq " + Literal(family) + " })";

    internal static async Task<string> QueryAsync(string command) => (await RunAsync(command, true)).Output;

    internal static async Task<(int Code, string Output)> RunAsync(string command, bool readOnly)
    {
        ProcessStartInfo start = new()
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe"),
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add("[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false); $ErrorActionPreference='Stop'; try { " + command + " } catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }");
        using Process process = Process.Start(start) ?? throw new IOException(SettingsService.L("Не удалось запустить операцию с пакетом Windows.", "Windows package operation did not start."));
        using CancellationTokenSource timeout = new();
        if (readOnly) timeout.CancelAfter(TimeSpan.FromSeconds(15));
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            // Only a read-only query may be stopped. Never kill an uninstaller.
            if (readOnly && !process.HasExited) process.Kill(entireProcessTree: true);
            throw new IOException(SettingsService.L("Windows не ответила на запрос пакетов.", "Windows package query timed out."));
        }
        await Task.WhenAll(output, error);
        if (readOnly && process.ExitCode != 0) throw new IOException(error.Result.Trim());
        return (process.ExitCode, process.ExitCode == 0 ? output.Result.Trim() : error.Result.Trim());
    }
}
