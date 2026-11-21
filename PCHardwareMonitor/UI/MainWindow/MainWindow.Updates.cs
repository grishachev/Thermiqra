using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCHardwareMonitor;

public partial class MainWindow : Window
{



    private async void CheckForUpdatesManually()
    {
        if (_isUpdateCheckRunning ||
            _isUpdateInstallRunning ||
            _isExiting)
        {
            return;
        }

        _isUpdateCheckRunning =
            true;

        try
        {
            UpdateInfo? update =
                await UpdateService
                    .CheckForUpdateAsync();

            if (update == null)
            {
                _notifications.Show(
                    NotificationType.Info,
                    SettingsService.L(
                        "Thermiqra — обновления",
                        "Thermiqra — updates"),
                    SettingsService.L(
                        "Установлена последняя версия Thermiqra.",
                        "You have the latest version of Thermiqra."),
                    null);

                return;
            }

            _pendingUpdate =
                update;

            _notifiedUpdateVersion =
                update.VersionText;

            _notifications.Show(
                NotificationType.Info,
                SettingsService.L(
                    "Доступно обновление Thermiqra",
                    "Thermiqra update available"),
                SettingsService.L(
                    $"Доступна версия {update.VersionText}. Нажмите, чтобы открыть Thermiqra и установить обновление.",
                    $"Version {update.VersionText} is available. Click to open Thermiqra and install the update."),
                ShowMainWindow);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Не удалось вручную проверить обновления Thermiqra: {ex}");

            _notifications.Show(
                NotificationType.Info,
                SettingsService.L(
                    "Thermiqra — обновления",
                    "Thermiqra — updates"),
                SettingsService.L(
                    $"Не удалось проверить обновления.\n{ex.Message}",
                    $"Failed to check for updates.\n{ex.Message}"),
                null);
        }
        finally
        {
            _isUpdateCheckRunning =
                false;
        }
    }


    private async void UpdateCheckTimer_Tick(
        object? sender,
        EventArgs e)
    {
        await CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_isUpdateCheckRunning ||
            _isExiting)
        {
            return;
        }

        _isUpdateCheckRunning = true;

        try
        {
            UpdateInfo? update =
                await UpdateService
                    .CheckForUpdateAsync();

            if (update == null)
                return;

            if (string.Equals(
                    _notifiedUpdateVersion,
                    update.VersionText,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _notifiedUpdateVersion =
                update.VersionText;

            _pendingUpdate =
                update;

            if (IsVisible &&
                IsActive &&
                WindowState != WindowState.Minimized)
            {
                TryShowPendingUpdate();
            }
            else
            {
                _notifications.Show(
                    NotificationType.Info,
                    SettingsService.L(
                        "Доступно обновление Thermiqra",
                        "Thermiqra update available"),
                    SettingsService.L(
                        $"Доступна версия {update.VersionText}. Нажмите, чтобы открыть Thermiqra и установить обновление.",
                        $"Version {update.VersionText} is available. Click to open Thermiqra and install the update."),
                    ShowMainWindow);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Не удалось проверить обновления Thermiqra: {ex}");
        }
        finally
        {
            _isUpdateCheckRunning = false;
        }
    }

    private async void TryShowPendingUpdate()
    {
        if (_pendingUpdate == null ||
            !IsVisible ||
            !IsActive ||
            WindowState == WindowState.Minimized ||
            _isUpdateInstallRunning)
        {
            return;
        }

        UpdateInfo update =
            _pendingUpdate;

        _pendingUpdate = null;

        if (!update.HasInstaller)
        {
            MessageBoxResult fallbackResult =
                MessageBox.Show(
                    this,
                    SettingsService.L(
                        $"Доступна Thermiqra {update.VersionText}, но автоматический установщик этого релиза не найден.\n\nОткрыть страницу релиза GitHub?",
                        $"Thermiqra {update.VersionText} is available, but the automatic installer for this release was not found.\n\nOpen the GitHub release page?"),
                    SettingsService.L(
                        "Thermiqra — Обновление",
                        "Thermiqra — Update"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (fallbackResult ==
                MessageBoxResult.Yes)
            {
                OpenReleasePage(
                    update);
            }

            return;
        }

        if (!ShowUpdatePrompt(
                update))
        {
            return;
        }

        await InstallUpdateAsync(
            update);
    }

    private bool ShowUpdatePrompt(UpdateInfo update)
    {
        var dialog = UpdatePromptView.Create(update, this, () => OpenReleasePage(update));
        return dialog.ShowDialog() == true;
    }

    private async Task InstallUpdateAsync(
        UpdateInfo update)
    {
        if (_isUpdateInstallRunning)
            return;

        _isUpdateInstallRunning =
            true;

        Window? progressWindow =
            null;

        CancelEventHandler?
            progressClosingHandler =
                null;

        using CancellationTokenSource
            downloadCancellation =
                new();

        bool downloadCompleted =
            false;

        try
        {
            var transferView = new UpdateDownloadView(this);
            progressWindow = transferView.Window;
            Button cancelButton = transferView.CancelButton;

            progressClosingHandler =
                (_, _) =>
                {
                    if (!downloadCompleted)
                    {
                        downloadCancellation
                            .Cancel();
                    }
                };

            progressWindow.Closing +=
                progressClosingHandler;

            cancelButton.Click +=
                (_, _) =>
                {
                    downloadCancellation
                        .Cancel();

                    if (progressWindow != null &&
                        progressWindow.IsVisible)
                    {
                        progressWindow.Close();
                    }
                };

            progressWindow.Show();
            progressWindow.Activate();

            Progress<UpdateDownloadProgress> progress = new(value =>
            {
                if (!downloadCompleted && progressWindow != null && progressWindow.IsVisible) transferView.Update(value);
            });

            string installerPath =
                await UpdateService
                    .DownloadInstallerWithProgressAsync(
                        update,
                        progress,
                        downloadCancellation.Token);

            downloadCancellation
                .Token
                .ThrowIfCancellationRequested();

            downloadCompleted =
                true;

            cancelButton.IsEnabled =
                false;

            transferView.StartingInstallation();

            await Task.Delay(
                250);

            await UpdateService.StartInstallerAsync(installerPath);

            if (progressClosingHandler !=
                null)
            {
                progressWindow.Closing -=
                    progressClosingHandler;
            }

            progressWindow.Close();
            progressWindow =
                null;

            ExitApplication();
        }
        catch (OperationCanceledException)
            when (downloadCancellation
                .IsCancellationRequested)
        {
            System.Diagnostics.Debug.WriteLine(
                "Загрузка обновления Thermiqra отменена пользователем.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Не удалось установить обновление Thermiqra: {ex}");

            if (progressWindow != null)
            {
                if (progressClosingHandler !=
                    null)
                {
                    progressWindow.Closing -=
                        progressClosingHandler;
                }

                if (progressWindow.IsVisible)
                {
                    progressWindow.Close();
                }

                progressWindow =
                    null;
            }

            MessageBox.Show(
                this,
                SettingsService.L(
                    $"Не удалось автоматически установить обновление.\n\n{ex.Message}",
                    $"The update could not be installed automatically.\n\n{ex.Message}"),
                SettingsService.L(
                    "Thermiqra — Обновление",
                    "Thermiqra — Update"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            if (progressWindow != null)
            {
                if (progressClosingHandler !=
                    null)
                {
                    progressWindow.Closing -=
                        progressClosingHandler;
                }

                if (progressWindow.IsVisible)
                {
                    progressWindow.Close();
                }
            }

            _isUpdateInstallRunning =
                false;
        }
    }

    private void OpenReleasePage(
        UpdateInfo update)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName =
                        update.ReleaseUrl,

                    UseShellExecute =
                        true
                });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Не удалось открыть страницу релиза: {ex}");

            MessageBox.Show(
                this,
                SettingsService.L(
                    "Не удалось открыть страницу релиза в браузере.",
                    "Failed to open the release page in the browser."),
                SettingsService.L(
                    "Thermiqra — Обновление",
                    "Thermiqra — Update"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
