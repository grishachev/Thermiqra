using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace PCHardwareMonitor;

internal sealed partial class ProgramUninstallWindow
{
    private async Task<bool> PrepareOrphanAsync()
    {
        try { _orphan = await Task.Run(() => ProgramOrphanRegistration.Read(_app)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or
            System.ComponentModel.Win32Exception or System.Security.SecurityException) { return false; }
        if (_orphan == null) return false;
        _steps[1].Visibility = Visibility.Collapsed; _steps[2].Visibility = Visibility.Collapsed;
        Stage(0, SettingsService.L("Найдена устаревшая запись установки", "Stale installation registration found"), false);
        _surface.Present(Message(SettingsService.L(
            "Папка программы и штатный деинсталлятор по зарегистрированному пути отсутствуют. Можно убрать устаревшую запись из списка Windows. Перед этим будет сохранена резервная копия записи.\n\nФайлы, настройки и игры этим действием не удаляются. После удаления записи здесь появится отдельный анализ оставшихся данных.",
            "The application folder and native uninstaller are missing at the registered location. You can remove the stale Windows listing. A backup of the registration will be saved first.\n\nThis action does not delete files, settings or games. A separate review of remaining data will appear here afterwards."), 16));
        _start.Content = SettingsService.L("Удалить устаревшую запись", "Remove stale registration");
        _start.Visibility = Visibility.Visible; _close.IsEnabled = true; _footer.Visibility = Visibility.Visible;
        return true;
    }

    private async Task RemoveOrphanAsync()
    {
        if (_busy || _orphan == null) return;
        _busy = true;
        try
        {
            Working(0, SettingsService.L("Сохранение копии и удаление устаревшей записи…", "Backing up and removing the stale registration…"),
                SettingsService.L("Проверяем состояние записи и доступность диска. Файлы программы не удаляются.",
                    "Rechecking the registration and volume availability. Application files are not being deleted."));
            Attempted = true;
            _report = await ProgramOrphanRegistration.RemoveAsync(_orphan);
            _review = new(this, _report, embedded: true);
            string status = SettingsService.L("Устаревшая запись удалена. Результаты проверки оставшихся данных показаны ниже.",
                "The stale registration was removed. Remaining-data inspection results are shown below.");
            _review.ActivityChanged += working =>
            {
                if (_closed) return;
                _progress.Visibility = Visibility.Collapsed;
                _status.Visibility = working ? Visibility.Collapsed : Visibility.Visible;
                _status.Text = working ? "" : status;
            };
            _review.CleanupFinished += () => Stage(4, SettingsService.L("Результат дополнительной очистки", "Additional cleanup result"), false);
            Stage(_report.Found.Count == 0 && _report.Limitations.Count == 0 ? 4 : 3, status, false);
            _surface.Content = _review; _footer.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex) { Finish(SettingsService.L("Операция не завершена", "Operation did not complete"), ex.Message); }
        finally { _busy = false; }
    }
}
