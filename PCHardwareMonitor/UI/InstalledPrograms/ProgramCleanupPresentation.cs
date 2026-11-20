using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PCHardwareMonitor;

internal static class ProgramCleanupPresentation
{
    internal static UIElement ResultContent(ProgramLeftoverReport report, ProgramCleanupBackup backup, bool verified, string error)
    {
        StackPanel panel = new() { Margin = new Thickness(14) };
        TextBlock Make(string text, double size, bool bold = false)
        {
            TextBlock block = new() { Text = text, FontSize = size, LineHeight = size * 1.5, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal };
            block.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryTextBrush"); return block;
        }
        panel.Children.Add(Make(report.Baseline.Name, 15));
        int removed = backup.Entries.Count(e => e.State is "Recycled" or "Removed" or "Moved");
        int failed = backup.Entries.Count(e => e.State is "Failed" or "Prepared" or "Unavailable");
        var verdict = ProgramCleanupVerdicts.Evaluate(verified, report.SearchPending, report.Found.Count,
            report.Limitations.Count, failed, error.Length > 0);
        string headline = verdict switch
        {
            ProgramCleanupVerdict.VerificationIncomplete => SettingsService.L("Результат требует проверки", "Result requires verification"),
            ProgramCleanupVerdict.ActionsIncomplete => SettingsService.L("Не все действия завершены", "Some actions did not complete"),
            ProgramCleanupVerdict.Remaining => SettingsService.L($"Осталось объектов: {report.Found.Count}", $"Remaining items: {report.Found.Count}"),
            ProgramCleanupVerdict.Limited => SettingsService.L("Остатки не найдены; проверка с ограничениями", "No leftovers found; inspection has limitations"),
            _ => SettingsService.L("Очистка завершена. Остатки не найдены", "Cleanup complete. No leftovers found")
        };
        panel.Children.Add(Make(headline, 22, true));
        panel.Children.Add(Make(SettingsService.L($"Удалено объектов: {removed}.", $"Items removed: {removed}.") +
            (failed > 0 ? SettingsService.L($" Не завершено: {failed}.", $" Incomplete: {failed}.") : ""), 15));
        if (verdict == ProgramCleanupVerdict.Clean) panel.Children.Add(Make(SettingsService.L(
            "Повторная проверка выполнена автоматически. В проверенных расположениях ничего не осталось.",
            "Post-cleanup verification ran automatically. Nothing remains in the checked locations."), 14));
        if (!verified || report.Limitations.Count > 0 || error.Length > 0)
            panel.Children.Add(ProgramWindowChrome.Details(SettingsService.L("Сведения о проверке", "Verification details"),
                Make(Verification(report, verified, error), 14)));
        panel.Children.Add(ProgramWindowChrome.Details(SettingsService.L("Журнал очистки", "Cleanup log"), Make(Describe(backup), 14)));
        return panel;
    }

    internal static string Verification(ProgramLeftoverReport report, bool verified, string error)
    {
        if (!verified) return SettingsService.L(
            "Повторная проверка не завершена. Результаты действий показаны ниже, но отсутствие остатков не подтверждено.",
            "Post-cleanup verification did not complete. Action results are below, but absence of leftovers is unconfirmed.") +
            (error.Length > 0 ? "\n" + error : "");
        string result = report.Found.Count == 0 ? SettingsService.L(
            "Повторная проверка завершена: в проверенных расположениях остатки не найдены.",
            "Post-cleanup verification complete: no leftovers found in the checked locations.") : SettingsService.L(
            $"Повторная проверка завершена. Осталось объектов: {report.Found.Count}.",
            $"Post-cleanup verification complete. Remaining items: {report.Found.Count}.");
        if (report.Found.Count > 0)
            result += "\n" + string.Join("\n", report.Found.Take(10).Select(p => p.Kind == "Registry"
                ? ProgramRegistryIdentity.Display(p) : p.Path)) + (report.Found.Count > 10 ? "\n…" : "");
        if (report.Limitations.Count > 0) result += SettingsService.L(
            $"\nПроверка с ограничениями: {report.Limitations.Count}. Полное отсутствие остатков не подтверждается.",
            $"\nInspection limitations: {report.Limitations.Count}. Complete absence of leftovers is not confirmed.");
        if (error.Length > 0) result += SettingsService.L("\nНе удалось сохранить обновлённый отчёт: ", "\nCould not save the updated report: ") + error;
        return result;
    }

    internal static void ShowResult(Window owner, ProgramCleanupBackup backup) =>
        InstalledProgramActionDialog.Show(owner, SettingsService.L("Thermiqra — Результат операции", "Thermiqra — Operation result"),
            Describe(backup), "OK", false);

    internal static string Describe(ProgramCleanupBackup backup)
    {
        string State(ProgramCleanupEntry entry) => entry.State switch
        {
            "Moved" => SettingsService.L("Перенесён в резервную копию", "Moved to backup"),
            "Removed" => SettingsService.L("Запись удалена, копия сохранена", "Registration removed; backup saved"),
            "Recycled" => SettingsService.L("Отправлен в Корзину Windows", "Sent to Windows Recycle Bin"),
            "AlreadyAbsent" => SettingsService.L("После анализа объект исчез до начала очистки. Thermiqra его не удаляла и в Корзину не переносила.",
                "The item disappeared after analysis, before cleanup started. Thermiqra did not delete or recycle it."),
            "Expired" => SettingsService.L("Срок хранения копии истёк", "Backup retention expired"),
            "Discarded" => SettingsService.L("Копия удалена", "Backup discarded"),
            "Missing" => SettingsService.L("Копия уже отсутствует", "Backup is already absent"),
            "Unavailable" => SettingsService.L("Копия недоступна; действие пропущено", "Backup unavailable; action skipped"),
            "Restored" => SettingsService.L("Восстановлен", "Restored"),
            "Prepared" => SettingsService.L("Действие не подтверждено; требуется проверка", "Action unconfirmed; review required"),
            _ => SettingsService.L("Пропущен или действие не завершено", "Skipped or action incomplete")
        };
        string details = string.Join("\n\n", backup.Entries.Select(e =>
            (e.Probe.Kind == "Registry" ? e.Probe.Hive + " (" + e.Probe.View + ")\\" : "") + e.Probe.Path + "\n" +
            State(e) + (e.Detail.Length > 0 ? "\n" + e.Detail : "") +
            (e.BackupPath.Length > 0 && e.State != "Restored" ? "\n" + SettingsService.L("Копия: ", "Backup: ") + e.BackupPath : "")));
        int recycled = backup.Entries.Count(e => e.State == "Recycled");
        int registry = backup.Entries.Count(e => e.State == "Removed");
        int absent = backup.Entries.Count(e => e.State == "AlreadyAbsent");
        if (recycled + registry + absent > 0)
            details = SettingsService.L($"В Корзине: {recycled} · Записей реестра удалено: {registry} · Уже отсутствует: {absent}",
                $"Recycled: {recycled} · Registry entries removed: {registry} · Already absent: {absent}") + "\n\n" + details;
        return backup.Name + "\n\n" + details;
    }

    internal static Task ShowRecoveryAsync(Window owner, string? reviewId = null)
    {
        new ProgramRecoveryWindow(owner, reviewId).ShowDialog();
        return Task.CompletedTask;
    }
}
