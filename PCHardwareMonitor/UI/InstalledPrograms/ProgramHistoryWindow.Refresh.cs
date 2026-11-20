using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace PCHardwareMonitor;

internal sealed partial class ProgramHistoryWindow
{
    private sealed record ChangeRow(string Key, string Name, string Version, string Type, string Date, string Continuity,
        DateTime DetectedUtc, string Kind);
    private sealed record HistoryData(List<RemovalRow> Removals, List<ChangeRow> Changes, string Status, string Revision);
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private bool _refreshing, _closed;
    private string _revision = "";

    private static string Revision() => ProgramObservationService.Revision + ":" + ProgramRemovalReviewStore.Revision;

    private void InitializeAutoRefresh()
    {
        Loaded += (_, _) => _refreshTimer.Start();
        Closed += (_, _) => { _closed = true; _refreshTimer.Stop(); };
        _refreshTimer.Tick += async (_, _) =>
        {
            if (_busy || _refreshing || _closed || !IsVisible || !IsEnabled || Revision() == _revision) return;
            _refreshing = true;
            try
            {
                var data = await LoadHistoryAsync();
                if (!_busy && !_closed && IsVisible && IsEnabled) ApplyHistory(data);
            }
            catch (Exception ex) { if (!_closed && !_busy) _status.Text = ex.Message; }
            finally { _refreshing = false; }
        };
    }

    private static Task<HistoryData> LoadHistoryAsync() => Task.Run(() =>
    {
        // Keep the initial revision: a write during this read must trigger
        // another refresh rather than being treated as already displayed.
        string revision = Revision();
        var history = ProgramRemovalReviewStore.ReadHistory();
        var rows = history.Select(r => new RemovalRow { Report = r, Result = r.SearchPending
            ? SettingsService.L("Остатки ещё не проверены", "Leftovers not checked yet")
            : !ProgramRemovalOutcome.CanClean(r.Baseline.NativeResult)
                ? SettingsService.L("Удаление не подтверждено", "Uninstall unconfirmed") : Found(r) }).ToList();
        var observed = ProgramObservationService.Read();
        var changes = observed?.Changes.AsEnumerable().Reverse().Select(c => new ChangeRow(
            c.After.Identity + "|" + c.Type + "|" + c.DetectedUtc.ToString("O"), c.After.Name,
            c.Type == "Changed" ? c.Before.Version + " → " + c.After.Version : c.After.Version,
            c.Type switch { "Added" => SettingsService.L("Появилась в списке", "Registration appeared"),
                "Removed" => SettingsService.L("Исчезла из списка", "Registration disappeared"), _ => SettingsService.L("Изменились сведения", "Details changed") },
            DateText(c.DetectedUtc), c.AfterPause ? SettingsService.L("После перерыва", "After a pause") : SettingsService.L("Во время работы", "While running"),
            c.DetectedUtc, c.Type)).ToList() ?? new();
        string status = observed == null ? SettingsService.L("Первый фоновый снимок ещё не сохранён. Прежние установки задним числом не восстанавливаются.",
            "The first background snapshot has not been saved yet. Past installations cannot be traced retroactively.") :
            SettingsService.L("Последний снимок: ", "Last snapshot: ") + DateText(observed.ScannedUtc);
        if (ProgramObservationService.LastError.Length > 0) status += "\n" + ProgramObservationService.LastError;
        if (ProgramObservedRemovalService.LastError.Length > 0) status += "\n" + ProgramObservedRemovalService.LastError;
        return new HistoryData(rows, changes, status, revision);
    });

    private void ApplyHistory(HistoryData data)
    {
        foreach (var row in data.Removals)
            if (_searchErrors.ContainsKey(row.Report.Baseline.Id))
            {
                row.SearchFailed = true;
                row.Result = SettingsService.L("Поиск не завершён", "Search incomplete");
            }
        _historyData = data; ApplyFilters();
        _status.Text = data.Status; _revision = data.Revision; UpdateButtons();
    }

    private async Task ReloadAsync()
    {
        var data = await LoadHistoryAsync();
        if (!_closed) ApplyHistory(data);
    }
}
