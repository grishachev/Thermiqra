using System;
using System.Diagnostics;

namespace PCHardwareMonitor;

public static class StartupService
{
    private const string TaskName =
        "Thermiqra";

    private const string LegacyTaskName =
        "PC Hardware Monitor";


    // ============================================================
    // ВКЛЮЧЕНИЕ / ВЫКЛЮЧЕНИЕ АВТОЗАПУСКА
    // ============================================================

    public static bool SetEnabled(
        bool enabled,
        out string? error)
    {
        error = null;

        try
        {
            if (enabled)
            {
                string? exePath =
                    Environment.ProcessPath;

                if (string.IsNullOrWhiteSpace(
                        exePath))
                {
                    error =
                        SettingsService.L("Не удалось определить путь к Thermiqra.exe.", "Could not locate Thermiqra.exe.");

                    return false;
                }

                // Удаляем старую задачу со старым названием,
                // если она осталась от предыдущей версии.
                DeleteTask(
                    LegacyTaskName,
                    ignoreErrors: true);

                // На всякий случай удаляем старую Thermiqra-задачу
                // перед созданием новой.
                DeleteTask(
                    TaskName,
                    ignoreErrors: true);

                string arguments =
                    $"/Create " +
                    $"/TN \"{TaskName}\" " +
                    $"/TR \"\\\"{exePath}\\\" --background\" " +
                    $"/SC ONLOGON " +
                    $"/RL HIGHEST " +
                    $"/IT " +
                    $"/DELAY 0000:10 " +
                    $"/F";

                return RunSchtasks(
                    arguments,
                    out error);
            }

            // Если автозапуск выключается —
            // удаляем и новое, и старое название.
            DeleteTask(
                TaskName,
                ignoreErrors: true);

            DeleteTask(
                LegacyTaskName,
                ignoreErrors: true);

            return true;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Failure("Startup.Change", ex);
            error =
                ex.Message;

            return false;
        }
    }


    // ============================================================
    // ПРОВЕРКА НАЛИЧИЯ АВТОЗАПУСКА
    // ============================================================

    public static bool IsEnabled()
    {
        return
            TaskExists(TaskName) ||
            TaskExists(LegacyTaskName);
    }


    // ============================================================
    // ПРОВЕРКА ЗАДАЧИ
    // ============================================================

    private static bool TaskExists(
        string taskName)
    {
        try
        {
            return RunSchtasks($"/Query /TN \"{taskName}\"", out _);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Failure("Startup.Query", ex);
            return false;
        }
    }


    // ============================================================
    // УДАЛЕНИЕ ЗАДАЧИ
    // ============================================================

    private static bool DeleteTask(
        string taskName,
        bool ignoreErrors)
    {
        bool success =
            RunSchtasks(
                $"/Delete /TN \"{taskName}\" /F",
                out _);

        return
            success ||
            ignoreErrors;
    }


    // ============================================================
    // ЗАПУСК SCHTASKS
    // ============================================================

    private static bool RunSchtasks(
        string arguments,
        out string? error)
    {
        error = null;

        ProcessStartInfo startInfo =
            new()
            {
                FileName =
                    "schtasks.exe",

                Arguments =
                    arguments,

                UseShellExecute =
                    false,

                CreateNoWindow =
                    true,

                RedirectStandardOutput =
                    true,

                RedirectStandardError =
                    true
            };

        using Process? process =
            Process.Start(startInfo);

        if (process == null)
        {
            error =
                SettingsService.L("Не удалось запустить Планировщик заданий Windows.", "Could not start Windows Task Scheduler.");

            return false;
        }

        // Drain both redirected pipes concurrently: either can fill its buffer.
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(15000) || !System.Threading.Tasks.Task.WaitAll(new System.Threading.Tasks.Task[] { outputTask, errorTask }, 2000))
        {
            error = SettingsService.L("Планировщик заданий не ответил вовремя. Проверьте автозапуск в Windows.",
                "Task Scheduler did not respond in time. Check startup settings in Windows.");
            DiagnosticLog.Failure("Startup.Timeout", new TimeoutException());
            return false;
        }
        string output = outputTask.Result;
        string errorOutput = errorTask.Result;

        if (process.ExitCode == 0)
        {
            return true;
        }

        error =
            !string.IsNullOrWhiteSpace(
                errorOutput)
                ? errorOutput.Trim()
                : output.Trim();

        return false;
    }
}