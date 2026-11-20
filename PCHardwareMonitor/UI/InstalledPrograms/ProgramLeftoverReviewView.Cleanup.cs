using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PCHardwareMonitor;

internal sealed partial class ProgramLeftoverReviewView
{
    private readonly Dictionary<CheckBox, ProgramLeftoverProbe> _selections = new();
    private Button? _clean, _restore, _close;
    private bool _cleanupBusy;
    private CheckBox? _cleanupCheckpoint;

    private CheckBox? CreateSelection(ProgramLeftoverProbe probe)
    {
        if (!ProgramRemovalDecisions.Evaluate(probe, _report.Baseline,
            new(ProgramRemovalOutcome.CanClean(_report.Baseline.NativeResult))).Allowed) return null;
        if (probe.Group is not (ProgramLeftoverGroup.Related or ProgramLeftoverGroup.UserData) ||
            (probe.Kind is not ("Directory" or "UserData" or "RecordedFile") && (probe.Kind is not ("Registry" or "Shortcut") || probe.EvidenceRoot.Length == 0))) return null;
        CheckBox check = new() { IsChecked = true, Margin = new Thickness(0, 0, 0, 8),
            Content = SettingsService.L("Выбрать для очистки", "Select for cleanup") };
        ProgramWindowChrome.CheckBox(check);
        check.SetResourceReference(Control.ForegroundProperty, "PrimaryTextBrush");
        _selections.Add(check, probe);
        check.Checked += (_, _) => UpdateActions();
        check.Unchecked += (_, _) => UpdateActions();
        return check;
    }

    private UIElement CreateActions()
    {
        StackPanel footer = new() { Margin = new Thickness(0, 10, 0, 0) };
        footer.Children.Add(Text(SettingsService.L(
            "Файлы можно вернуть из Корзины, записи реестра — из «Восстановления».",
            "Files can be restored from Recycle Bin; registry entries from Recovery."), 11, "SecondaryTextBrush"));
        if (!_embedded && _selections.Values.Any(p => p.Kind == "Registry"))
        {
            _cleanupCheckpoint = new CheckBox { IsChecked = true, Margin = new Thickness(0, 8, 0, 0),
                Content = SettingsService.L("Создать точку восстановления перед очисткой реестра", "Create a restore point before registry cleanup") };
            ProgramWindowChrome.CheckBox(_cleanupCheckpoint);
            _cleanupCheckpoint.SetResourceReference(Control.ForegroundProperty, "PrimaryTextBrush");
            footer.Children.Add(_cleanupCheckpoint);
        }
        WrapPanel buttons = new() { HorizontalAlignment = HorizontalAlignment.Right };
        Button Make(string ru, string en) => new() { Content = SettingsService.L(ru, en), FontSize = 14, MinHeight = 38,
            Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(8, 8, 0, 0), MinWidth = 110 };
        _clean = Make("Очистить выбранное", "Clean selected");
        _restore = Make("Восстановление", "Recovery");
        _close = Make("Закрыть", "Close");
        _close.IsCancel = true;
        if (_embedded) _restore.Visibility = Visibility.Collapsed;
        _close.Click += (_, _) => _owner.Close();
        _clean.Click += async (_, _) => await CleanSelectedAsync();
        _restore.Click += async (_, _) =>
        {
            SetBusy(true);
            try
            {
                await ProgramCleanupPresentation.ShowRecoveryAsync(_owner, _report.Baseline.Id);
                await RefreshReportAsync();
            }
            finally { SetBusy(false); }
        };
        buttons.Children.Add(_clean); buttons.Children.Add(_restore); buttons.Children.Add(_close);
        footer.Children.Add(buttons);
        UpdateActions();
        return footer;
    }

    private void UpdateActions()
    {
        _counts.Text = _report.Found.Count == 0 ? (_report.Limitations.Count > 0
            ? SettingsService.L("Проверка с ограничениями", "Inspection has limitations")
            : SettingsService.L("Остатки не найдены", "No leftovers found")) :
            SettingsService.L($"Найдено: {_report.Found.Count} · Выбрано: {_selections.Keys.Count(c => c.IsChecked == true)}",
                $"Found: {_report.Found.Count} · Selected: {_selections.Keys.Count(c => c.IsChecked == true)}");
        if (_clean != null) _clean.IsEnabled = !_cleanupBusy && _selections.Keys.Any(c => c.IsChecked == true);
        if (_restore != null) _restore.IsEnabled = !_cleanupBusy;
    }

    private void SetBusy(bool busy)
    {
        _cleanupBusy = busy;
        ActivityChanged?.Invoke(busy);
        foreach (CheckBox check in _selections.Keys) check.IsEnabled = !busy;
        if (_cleanupCheckpoint != null) _cleanupCheckpoint.IsEnabled = !busy;
        if (_close != null) _close.IsEnabled = !busy;
        UpdateActions();
    }

    private async Task CleanSelectedAsync()
    {
        if (_cleanupBusy || _surface.HasDecision) return;
        var selected = _selections.Where(p => p.Key.IsChecked == true).Select(p => p.Value).ToList();
        if (selected.Count == 0) return;
        // Selection plus this click starts cleanup. The service revalidates
        // evidence and handles vanished items before performing each action.
        SetBusy(true);
        try
        {
            if (_cleanupCheckpoint?.IsChecked == true && selected.Any(p => p.Kind == "Registry") &&
                !await PrepareRegistryCheckpointAsync()) return;
            ProgramCleanupBackup result;
            bool verified;
            using (var working = _surface.Working(SettingsService.L("Выполняется очистка", "Cleanup in progress"),
                SettingsService.L("Подготовка очистки. Дождитесь результата…", "Preparing cleanup. Please wait for the result…")))
            {
                bool cleaning = true;
                result = await ProgramLeftoverCleanupService.CleanAsync(_report, selected, new Progress<string>(text => { if (cleaning) working.Update(text); }));
                cleaning = false;
                working.Phase(SettingsService.L("Проверка результата", "Verifying the result"),
                    SettingsService.L("Очистка закончена. Проверяем, что осталось…", "Cleanup finished. Checking what remains…"));
                verified = await RefreshReportAsync(showError: false);
            }
            SetBusy(false);
            CleanupFinished?.Invoke();
            bool finish = await _surface.AskAsync(SettingsService.L("Результат очистки", "Cleanup result"),
                ProgramCleanupPresentation.ResultContent(_report, result, verified, _refreshError), SettingsService.L("Закрыть", "Close"),
                ProgramCleanupVerdicts.Evaluate(verified, _report.SearchPending, _report.Found.Count, _report.Limitations.Count,
                    result.Entries.Count(e => e.State is "Failed" or "Prepared" or "Unavailable"), _refreshError.Length > 0) == ProgramCleanupVerdict.Clean ? null :
                    SettingsService.L("Оставшиеся объекты", "Remaining items"));
            if (finish) _owner.Close();
        }
        catch (Exception ex)
        {
            await _surface.AskAsync(SettingsService.L("Очистка не завершена", "Cleanup did not complete"),
                SettingsService.L("Проверьте отчёт. Уже перенесённые файлы доступны в Корзине, копии реестра — в разделе «Восстановление».\n",
                    "Review the report. Recycled files are in Recycle Bin; registry backups are available in Recovery.\n") + ex.Message,
                SettingsService.L("К остаткам", "Back to leftovers"));
        }
        finally { SetBusy(false); }
    }

    private async Task<bool> PrepareRegistryCheckpointAsync()
    {
        if (ProgramCleanupCheckpointPolicy.CanReuse(_report.Baseline.RestorePoint, DateTime.UtcNow) &&
            await ProgramRestorePointService.IsAvailableAsync(_report.Baseline.RestorePoint!)) return true;
        ProgramRestorePointResult point;
        using (var preparing = _surface.Working(SettingsService.L("Точка восстановления", "Restore point"),
            SettingsService.L("Очистка ещё не началась…", "Cleanup has not started yet…")))
            point = await ProgramRestorePointService.CreateAsync(_report.Baseline.Name, new Progress<string>(preparing.Update));
        _report = _report with { Baseline = _report.Baseline with { RestorePoint = point } };
        await Task.Run(() => ProgramRemovalReviewStore.SaveReport(_report));
        if (point.State != "Created" && !await _surface.AskAsync(SettingsService.L("Точка восстановления не создана", "Restore point was not created"),
            ProgramRestorePointService.Summary(point) + "\n\n" + SettingsService.L(
                "Продолжить очистку с резервными копиями реестра, но без новой точки Windows?",
                "Continue cleanup with registry backups but without a new Windows restore point?"),
            SettingsService.L("Продолжить без точки", "Continue without checkpoint"), SettingsService.L("Отмена", "Cancel"))) return false;
        return true;
    }

    private string _refreshError = "";
    private async Task<bool> RefreshReportAsync(bool showError = true)
    {
        bool updated = false;
        _refreshError = "";
        var choices = _selections.GroupBy(pair => pair.Value)
            .ToDictionary(group => group.Key, group => group.First().Key.IsChecked == true);
        try
        {
            _report = await Task.Run(() => ProgramLeftoverDiscoveryService.Inspect(_report.Baseline));
            updated = true;
            await Task.Run(() => ProgramRemovalReviewStore.SaveReport(_report));
        }
        catch (Exception ex)
        {
            _refreshError = ex.Message;
            if (showError) await _surface.AskAsync(SettingsService.L("Отчёт не обновлён", "Report was not refreshed"), ex.Message, "OK");
        }
        RenderItems();
        foreach (var pair in _selections)
        {
            var prior = choices.FirstOrDefault(choice => ProgramLeftoverCleanupPolicy.SameEvidence(choice.Key, pair.Value));
            if (prior.Key != null) pair.Key.IsChecked = prior.Value;
        }
        return updated;
    }
}
