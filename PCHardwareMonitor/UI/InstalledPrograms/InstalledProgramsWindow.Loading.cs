using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PCHardwareMonitor;

public partial class MainWindow
{
    private sealed partial class InstalledProgramsWindow
    {
        private async Task RefreshAsync()
        {
            if (_uninstallBusy) return;
            _loadCancellation?.Cancel();
            CancellationTokenSource current = new();
            _loadCancellation = current;
            CancellationToken token = current.Token;
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            _refreshButton.IsEnabled = false;
            _statusText.Text = SettingsService.L(
                "Чтение списка установленных программ Windows...",
                "Reading the Windows installed-program list...");
            try
            {
                var cached = await Task.Run(() => InstalledProgramsCacheService.Read(token), token);
                if (!ReferenceEquals(_loadCancellation, current) || token.IsCancellationRequested || !IsVisible) return;
                List<InstalledProgramInfo> previous = _programs.Count > 0
                    ? _programs.ToList() : cached?.Programs ?? new List<InstalledProgramInfo>();
                if (previous.Count > 0)
                {
                    List<InstalledProgramInfo> verified = await Task.Run(
                        () => InstalledProgramsCacheVerifier.VerifyRegistryEntries(previous, token), token);
                    if (!ReferenceEquals(_loadCancellation, current) || token.IsCancellationRequested || !IsVisible) return;
                    ReplaceInventory(verified);
                    _statusText.Text = SettingsService.L(
                        "Сохранённый список сверен с Windows. Уточняем сведения и приложения Store...",
                        "Saved inventory checked against Windows. Updating details and Store applications...");
                }
                // Independent Windows sources are read together, not serially.
                InstalledProgramsReadStatus registryStatus = new();
                var packagesTask = InstalledProgramsDiscoveryService.ReadStorePackagesAsync(token);
                List<InstalledProgramInfo> desktop = await Task.Run(
                    () => InstalledProgramsDiscoveryService.ReadRegistryPrograms(token, registryStatus), token);
                if (!ReferenceEquals(_loadCancellation, current) ||
                    token.IsCancellationRequested || !IsVisible) return;
                if (_programs.Count == 0) ReplaceInventory(desktop);
                (List<InstalledProgramInfo> packages, bool available) = await packagesTask;
                if (!ReferenceEquals(_loadCancellation, current) || token.IsCancellationRequested || !IsVisible) return;
                List<InstalledProgramInfo> initial = desktop.Concat(packages).ToList();
                // An unavailable package source is not proof that its apps were uninstalled.
                initial = registryStatus.PreserveUnconfirmed(initial, previous, available);
                List<InstalledProgramInfo> dated = await Task.Run(
                    () => InstalledProgramsDiscoveryService.EnrichMissingDates(initial, token), token);
                if (!ReferenceEquals(_loadCancellation, current) ||
                    token.IsCancellationRequested || !IsVisible) return;
                List<InstalledProgramInfo> observed = await Task.Run(
                    () => InstalledProgramsHistoryService.RecordObservations(dated, token), token);
                ProgramObservationService.RequestScan();
                if (!ReferenceEquals(_loadCancellation, current) ||
                    token.IsCancellationRequested || !IsVisible) return;
                ReplaceInventory(InstalledProgramsCacheService.KeepDisplaySizes(observed, previous));
                List<InstalledProgramInfo> interim = _programs.ToList();
                await Task.Run(() => InstalledProgramsCacheService.Save(interim, token), token);
                if (!ReferenceEquals(_loadCancellation, current) || token.IsCancellationRequested || !IsVisible) return;

                // The user can refresh again immediately; the earlier job is
                // cancelled, and its late progress callbacks are ignored.
                _refreshButton.IsEnabled = !_uninstallBusy;
                _statusText.Text = SettingsService.L(
                    "Размеры уточняются по очереди в фоне. Просмотр и сортировка доступны.",
                    "Sizes are checked sequentially in the background. Browsing and sorting remain available.");
                IProgress<(InstalledProgramInfo Program, int Completed, int Total)> updates =
                    new Progress<(InstalledProgramInfo Program, int Completed, int Total)>(update =>
                    {
                        if (!ReferenceEquals(_loadCancellation, current) ||
                            token.IsCancellationRequested || !IsVisible) return;
                        UpdateMeasuredSize(update.Program);
                        _statusText.Text = SettingsService.L(
                            $"Фоновый расчёт размеров: {update.Completed:N0} из {update.Total:N0}. Можно продолжать работу.",
                            $"Background size scan: {update.Completed:N0} of {update.Total:N0}. You can keep working.");
                    });
                List<InstalledProgramInfo> measured = await Task.Run(
                    () => InstalledProgramSizeService.EnrichMissingSizes(observed, token, updates), token);
                if (!ReferenceEquals(_loadCancellation, current) ||
                    token.IsCancellationRequested || !IsVisible) return;
                foreach (InstalledProgramInfo item in measured)
                    UpdateMeasuredSize(item);
                List<InstalledProgramInfo> final = _programs.ToList();
                await Task.Run(() =>
                {
                    InstalledProgramsCacheService.Save(final, token);
                    InstalledProgramsDiagnosticsService.Save(final, available, elapsed.ElapsedMilliseconds, token, registryStatus);
                }, token);
                if (!ReferenceEquals(_loadCancellation, current) || token.IsCancellationRequested || !IsVisible) return;
                int hiddenCount = _programs.Count(item => item.IsAuxiliary);
                _statusText.Text = registryStatus.HasFailures
                    ? SettingsService.L(
                        "Список обновлён частично. Недоступные записи сохранены из предыдущего списка; их наличие сейчас не подтверждено.",
                        "Inventory partially refreshed. Unavailable entries were retained from the previous list; their presence is currently unconfirmed.")
                    : available
                    ? SettingsService.L(
                        $"Готово. Всего найдено: {_programs.Count}; скрыто встроенных и служебных: {hiddenCount}.",
                        $"Ready. Found: {_programs.Count}; hidden built-in/supporting: {hiddenCount}.")
                    : SettingsService.L(
                        $"Обычные программы: {_programs.Count}. MSIX/Store недоступны.",
                        $"Desktop programs: {_programs.Count}. MSIX/Store unavailable.");
            }
            catch (OperationCanceledException)
            {
                // Closing or refreshing cancels the previous scan.
            }
            catch (Exception ex)
            {
                if (ReferenceEquals(_loadCancellation, current) &&
                    !token.IsCancellationRequested && IsVisible)
                    _statusText.Text = SettingsService.L(
                        "Не удалось прочитать сведения об установленных программах: ",
                        "Could not read installed-program information: ") + ex.Message;
            }
            finally
            {
                // Cancel any source still running after an early failure.
                current.Cancel();
                if (ReferenceEquals(_loadCancellation, current))
                {
                    _refreshButton.IsEnabled = !_uninstallBusy;
                    _loadCancellation = null;
                }
                current.Dispose();
            }
        }

        private void ReplaceInventory(IReadOnlyList<InstalledProgramInfo> programs)
        {
            string? selected = (_programsTable.SelectedItem as InstalledProgramInfo)?.Identity;
            _programs.Clear();
            _programs.AddRange(programs);
            UpdateNavigationButtons();
            ApplyFilter();
            if (selected != null)
                _programsTable.SelectedItem = _visiblePrograms.FirstOrDefault(p => p.Identity == selected);
        }
    }
}
