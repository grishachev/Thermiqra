using System.Windows;
using System.Windows.Controls;

namespace PCHardwareMonitor;

internal sealed class ProgramLeftoverReviewWindow : Window
{
    private ProgramLeftoverReviewView? _view;
    private readonly ProgramOperationSurface _search = new();
    private bool _searching, _started;
    internal ProgramLeftoverReport? Report => _view?.Report;
    internal string SearchError { get; private set; } = "";

    private ProgramLeftoverReviewWindow(Window owner)
    {
        Owner = owner; Title = SettingsService.L("Thermiqra — Анализ остатков", "Thermiqra — Leftover review");
        Width = 900; Height = 680; MinWidth = 720; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false; FontFamily = owner.FontFamily;
        SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
        SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
        if (owner.TryFindResource(typeof(Button)) is Style buttons) Resources[typeof(Button)] = buttons;
        if (owner.TryFindResource(typeof(System.Windows.Controls.Primitives.ScrollBar)) is Style scrollbars)
            Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] = scrollbars;
        Closing += (_, args) => { if (_searching || _view?.IsWorking == true) args.Cancel = true;
            else { _view?.CancelDecision(); _search.CancelDecision(); } };
        ContentRendered += (_, _) => WindowPlacementService.ConstrainToWorkArea(this);
    }

    internal ProgramLeftoverReviewWindow(Window owner, ProgramLeftoverReport report) : this(owner)
    { ShowReport(report); }

    internal ProgramLeftoverReviewWindow(Window owner, ProgramRemovalBaseline baseline) : this(owner)
    {
        Content = _search; _searching = true;
        var progress = _search.Working(SettingsService.L("Поиск остатков", "Finding leftovers"),
            baseline.Name + "\n\n" + SettingsService.L("Проверяем файлы, папки и записи реестра…", "Checking files, folders and registry entries…"));
        Loaded += async (_, _) =>
        {
            if (_started) return; _started = true;
            try
            {
                var report = await ProgramLeftoverRecheckService.RecheckAsync(baseline);
                _searching = false; ShowReport(report);
            }
            catch (System.Exception ex)
            {
                SearchError = ex.Message; _searching = false; progress.Dispose();
                await _search.AskAsync(SettingsService.L("Поиск не завершён", "Search incomplete"), ex.Message,
                    SettingsService.L("Закрыть", "Close"));
                Close();
            }
            finally { _searching = false; progress.Dispose(); }
        };
    }

    private void ShowReport(ProgramLeftoverReport report)
    { _view = new(this, report); Content = _view; }
}
