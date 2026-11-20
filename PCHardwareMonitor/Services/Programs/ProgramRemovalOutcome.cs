namespace PCHardwareMonitor;

internal static class ProgramRemovalOutcome
{
    internal static bool CanClean(InstalledProgramRemovalResult? result) => result == null ||
        !result.HelperObservationIncomplete && (result.PendingHelpers?.Count ?? 0) == 0 && (result.State == InstalledProgramRemovalState.Removed || result.RegistrationAbsent &&
        result.State is InstalledProgramRemovalState.Failed or InstalledProgramRemovalState.Unconfirmed or InstalledProgramRemovalState.RegistrationRemoved);

    internal static string Summary(InstalledProgramRemovalResult result)
    {
        string code = result.ExitCode is int value ? SettingsService.L($" Код: {value}.", $" Code: {value}.") : "";
        if (result.HelperObservationIncomplete)
            return SettingsService.L("Завершение штатного удаления не подтверждено.", "Native uninstall completion is unconfirmed.") + code + "\n" + SettingsService.L(
                "Завершение вспомогательных процессов не удалось проверить. Дополнительная очистка заблокирована до перезагрузки Windows и новой проверки.",
                "Helper-process completion could not be checked. Additional cleanup is blocked until Windows restarts and a fresh inspection completes.") +
                (string.IsNullOrWhiteSpace(result.Detail) ? "" : "\n" + result.Detail[..System.Math.Min(result.Detail.Length, 2048)]);
        string state = result.State switch
        {
            InstalledProgramRemovalState.Removed => SettingsService.L("Штатное удаление завершено.", "Native uninstall finished."),
            InstalledProgramRemovalState.Cancelled => SettingsService.L("Деинсталлятор сообщил об отмене.", "The uninstaller reported cancellation."),
            InstalledProgramRemovalState.RebootRequired => SettingsService.L("Деинсталлятор сообщил о необходимости перезагрузки.", "The uninstaller reported that a restart is required."),
            InstalledProgramRemovalState.Failed => SettingsService.L("Деинсталлятор сообщил об ошибке.", "The uninstaller reported an error."),
            InstalledProgramRemovalState.RegistrationRemoved => SettingsService.L("Устаревшая запись Windows удалена с резервной копией. Штатный деинсталлятор не запускался.",
                "The stale Windows registration was removed with a backup. The native uninstaller was not run."),
            _ => SettingsService.L("Завершение штатного удаления не подтверждено.", "Native uninstall completion is unconfirmed.")
        };
        return state + code + ((result.PendingHelpers?.Count ?? 0) > 0 ? "\n" + SettingsService.L(
            "Вспомогательный процесс ещё выполняется. Очистка заблокирована; повторите поиск после его завершения.",
            "A helper process is still running. Cleanup is blocked; search again after it finishes.") : "") + (string.IsNullOrWhiteSpace(result.Detail) ? "" : "\n" + result.Detail[..System.Math.Min(result.Detail.Length, 2048)]) +
            "\n" + (result.RegistrationAbsent || result.State == InstalledProgramRemovalState.Removed
            ? SettingsService.L("Запись программы в Windows отсутствует. Оставшиеся файлы и настройки проверяются отдельно.",
                "The Windows application registration is absent. Remaining files and settings are checked separately.")
            : SettingsService.L("Отсутствие записи программы не подтверждено. Анализ доступен, дополнительная очистка заблокирована.",
                "Application registration absence is not confirmed. Inspection is available; additional cleanup is blocked."));
    }
}
