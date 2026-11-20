using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PCHardwareMonitor;

internal sealed partial class ProgramUninstallWindow
{
    private async Task PrepareAsync()
    {
        _busy = true;
        Working(0, SettingsService.L("Проверка программы…", "Checking the application…"),
            SettingsService.L("Проверяем актуальную запись Windows и штатный деинсталлятор. Изменения ещё не выполняются.",
                "Checking the current Windows registration and native uninstaller. No changes are being made yet."));
        try
        {
            _plan = await Task.Run(() => InstalledProgramUninstallPlanReader.ReadAsync(_app));
            StackPanel confirmation = new();
            confirmation.Children.Add(Message(SettingsService.L($"Версия: {_plan.Version} · Тип: {_plan.Kind}", $"Version: {_plan.Version} · Type: {_plan.Kind}"), 16));
            confirmation.Children.Add(Message(SettingsService.L(
                "Будет запущен штатный деинсталлятор программы. Он может удалить настройки и данные по правилам её разработчика. Затем здесь же появится анализ оставшихся файлов и записей реестра. Дополнительная очистка выполняется только по вашему выбору.",
                "The application's native uninstaller will run. It may remove settings and data according to the developer's rules. Remaining files and registry entries will then be reviewed here. Additional cleanup runs only with your selection.")));
            _restorePoint.Content = new TextBlock { Text = SettingsService.L(
                "Создать точку восстановления Windows. Если защита отключена — включить её.",
                "Create a Windows restore point. Enable System Protection if it is off."), TextWrapping = TextWrapping.Wrap };
            ProgramWindowChrome.CheckBox(_restorePoint); confirmation.Children.Add(_restorePoint);
            confirmation.Children.Add(Message(SettingsService.L(
                "Точка восстановления защищает системные настройки; это не полная копия программы и личных файлов.",
                "A restore point protects system settings; it is not a full backup of the application or personal files.")));
            var confirmationFrame = ProgramWindowChrome.Frame(confirmation, 18);
            confirmationFrame.VerticalAlignment = VerticalAlignment.Top;
            _surface.Present(confirmationFrame);
            Stage(0, SettingsService.L("Всё готово к удалению. Проверьте выбранную программу.", "Ready to uninstall. Review the selected application."), false);
            _start.Visibility = Visibility.Visible; _close.IsEnabled = true;
        }
        catch (Exception ex)
        {
            if (!await PrepareOrphanAsync()) Finish(SettingsService.L("Удаление не началось", "Uninstall has not started"), ex.Message);
        }
        finally { _busy = false; }
    }

    private async Task StartAsync()
    {
        if (_busy || _plan == null) return;
        _busy = true;
        try
        {
            using var observationScope = new ProgramNativeRemovalScope(_app.Identity);
            bool restore = _restorePoint.IsChecked == true;
            Working(0, SettingsService.L("Сохранение сведений о программе…", "Saving application associations…"),
                SettingsService.L("Сохраняем известные расположения для последующего анализа остатков.", "Saving known locations for the subsequent leftover review."));
            var baseline = await Task.Run(() =>
            {
                var value = ProgramInstallationMemory.CaptureForRemoval(_app, _plan, _inventory);
                ProgramRemovalReviewStore.SaveBaseline(value); return value;
            });
            ProgramRestorePointResult point;
            if (restore)
            {
                Working(1, SettingsService.L("Создание точки восстановления…", "Creating a restore point…"),
                    SettingsService.L("Дождитесь завершения. Удаление программы ещё не началось.", "Please wait. Application removal has not started yet."));
                bool creating = true;
                try
                {
                    point = await ProgramRestorePointService.CreateAsync(_plan.Name, new Progress<string>(text =>
                    { if (creating && !_closed && _phase == 1) _status.Text = text; }));
                }
                finally { creating = false; }
            }
            else point = new("NotRequested", DateTime.UtcNow, "");
            baseline = baseline with { RestorePoint = point };
            await Task.Run(() => ProgramRemovalReviewStore.SaveBaseline(baseline));
            if (restore && point.State != "Created")
            {
                Stage(1, SettingsService.L("Точка восстановления не подтверждена", "Restore point was not confirmed"), false);
                _footer.Visibility = Visibility.Collapsed;
                bool proceed = await _surface.AskAsync(SettingsService.L("Продолжить без новой точки?", "Continue without a new restore point?"),
                    ProgramRestorePointService.Summary(point), SettingsService.L("Продолжить удаление", "Continue uninstall"),
                    SettingsService.L("Отменить удаление", "Cancel uninstall"));
                if (!proceed) { Finish(SettingsService.L("Удаление отменено", "Uninstall cancelled"),
                    SettingsService.L("Деинсталлятор не запускался.", "The uninstaller was not started.")); return; }
            }
            Working(2, SettingsService.L("Выполняется штатное удаление…", "Native uninstall is running…"),
                ProgramRestorePointService.Summary(point) + "\n\n" + SettingsService.L(
                    "Если деинсталлятор открыл своё окно, завершите действия в нём. Затем Thermiqra проверит результат здесь.",
                    "If the uninstaller opened its own window, complete its steps there. Thermiqra will then verify the result here."));
            Attempted = true;
            var result = await Task.Run(() => InstalledProgramUninstallService.ExecuteAsync(_app, _plan));
            baseline = baseline with { NativeResult = result };
            await Task.Run(() => ProgramRemovalReviewStore.SaveBaseline(baseline));
            Working(3, SettingsService.L("Проверка остатков после удаления…", "Checking leftovers after uninstall…"),
                SettingsService.L("Ожидаем завершения уборки деинсталлятором и проверяем сохранённые расположения. Окно останется открытым.",
                    "Waiting for the uninstaller to finish cleanup and checking saved locations. This window will stay open."));
            _report = await ProgramLeftoverSettlementService.InspectAfterUninstallAsync(baseline);
            try { await Task.Run(() => ProgramRemovalReviewStore.SaveReport(_report)); }
            catch (Exception ex) { _report.Limitations.Add(new("Не удалось сохранить отчёт", "Could not save report", ex.Message)); }
            _review = new ProgramLeftoverReviewView(this, _report, embedded: true);
            _review.ActivityChanged += working =>
            {
                if (_closed) return;
                // The embedded view owns the single progress indicator and phase text.
                _progress.Visibility = Visibility.Collapsed;
                _status.Visibility = working ? Visibility.Collapsed : Visibility.Visible;
                _status.Text = working ? "" :
                    (result.State != InstalledProgramRemovalState.Removed || result.HelperObservationIncomplete) ? ProgramRemovalOutcome.Summary(result) :
                        SettingsService.L("Штатное удаление завершено. Результаты показаны ниже.", "Native uninstall finished. Results are shown below.");
            };
            _review.CleanupFinished += () => Stage(4, SettingsService.L("Результат дополнительной очистки", "Additional cleanup result"), false);
            bool empty = _report.Found.Count == 0 && _report.Limitations.Count == 0;
            Stage(empty ? 4 : 3, (result.State != InstalledProgramRemovalState.Removed || result.HelperObservationIncomplete) ? ProgramRemovalOutcome.Summary(result) : empty ? SettingsService.L("Удаление завершено. В проверенных расположениях остатки не найдены.",
                "Uninstall finished. No leftovers were found in the checked locations.") :
                SettingsService.L("Штатное удаление завершено. Выберите остатки для очистки или закройте окно.",
                    "Native uninstall finished. Select leftovers to clean or close the window."), false);
            _surface.Content = _review; _footer.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex) { Finish(SettingsService.L("Операция не завершена", "Operation did not complete"), ex.Message); }
        finally { _busy = false; }
    }

    private static string ResultTitle(InstalledProgramRemovalState state) => state switch
    {
        InstalledProgramRemovalState.Cancelled => SettingsService.L("Удаление отменено", "Uninstall cancelled"),
        InstalledProgramRemovalState.RebootRequired => SettingsService.L("Требуется перезагрузка", "Restart required"),
        InstalledProgramRemovalState.Unconfirmed => SettingsService.L("Удаление пока не подтверждено", "Uninstall is not confirmed yet"),
        _ => SettingsService.L("Ошибка штатного удаления", "Native uninstall failed")
    };

    private static string ResultDetails(InstalledProgramRemovalResult result)
    {
        string details = result.State switch
        {
            InstalledProgramRemovalState.Cancelled => SettingsService.L("Деинсталлятор сообщил об отмене. Список программ будет обновлён.", "The uninstaller reported cancellation. The application list will refresh."),
            InstalledProgramRemovalState.RebootRequired => SettingsService.L("Для завершения нужна перезагрузка. Thermiqra не перезагружает компьютер автоматически.", "A restart is required to finish. Thermiqra does not restart the computer automatically."),
            InstalledProgramRemovalState.Unconfirmed => SettingsService.L("Запись программы всё ещё присутствует или не удалось проверить её отсутствие. Очистка остатков не запускается.", "The registration is still present or its absence could not be verified. Leftover cleanup will not run."),
            _ => SettingsService.L("Штатный деинсталлятор завершился с ошибкой. Дополнительная очистка не выполнялась.", "The native uninstaller reported a failure. Additional cleanup did not run.")
        };
        if (result.ExitCode is int code && code != 0) details += SettingsService.L("\nКод: ", "\nCode: ") + code;
        if (result.Detail.Length > 0) details += "\n" + result.Detail;
        return details;
    }
}
