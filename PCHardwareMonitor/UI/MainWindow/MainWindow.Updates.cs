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

    private bool ShowUpdatePrompt(
        UpdateInfo update)
    {
        string description =
            SettingsService.L(
                $"Доступна новая версия Thermiqra {update.VersionText}.",
                $"A new version of Thermiqra {update.VersionText} is available.");

        string whatsNew =
            string.IsNullOrWhiteSpace(
                update.Summary)

                ? string.Empty

                : SettingsService.L(
                    "Что нового:",
                    "What's new:") +
                  Environment.NewLine +
                  update.Summary +
                  Environment.NewLine +
                  Environment.NewLine;

        Window dialog =
            new()
            {
                Owner = this,
                Title =
                    SettingsService.L(
                        "Thermiqra — Обновление",
                        "Thermiqra — Update"),
                Width = 560,
                SizeToContent =
                    SizeToContent.Height,
                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner,
                ResizeMode =
                    ResizeMode.NoResize,
                ShowInTaskbar = false
            };

        dialog.SetResourceReference(
            Window.BackgroundProperty,
            "WindowBackgroundBrush");

        Grid root =
            new()
            {
                Margin =
                    new Thickness(
                        22)
            };

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        TextBlock title =
            new()
            {
                Text =
                    SettingsService.L(
                        $"Доступна Thermiqra {update.VersionText}",
                        $"Thermiqra {update.VersionText} is available"),
                FontSize = 20,
                FontWeight =
                    FontWeights.Bold,
                TextWrapping =
                    TextWrapping.Wrap
            };

        title.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        Grid.SetRow(
            title,
            0);

        root.Children.Add(
            title);

        TextBlock version =
            new()
            {
                Text =
                    SettingsService.L(
                        $"Текущая версия: {update.CurrentVersionText}",
                        $"Current version: {update.CurrentVersionText}"),
                Margin =
                    new Thickness(
                        0,
                        7,
                        0,
                        0),
                FontSize = 12
            };

        version.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        Grid.SetRow(
            version,
            1);

        root.Children.Add(
            version);

        TextBlock details =
            new()
            {
                Text =
                    description +
                    Environment.NewLine +
                    Environment.NewLine +
                    whatsNew +
                    SettingsService.L(
                        "Нажмите «Обновить» — Thermiqra автоматически скачает установщик, проверит его целостность и установит новую версию. После установки Thermiqra запустится снова. Настройки и статистика будут сохранены.",
                        "Click “Update” — Thermiqra will automatically download the installer, verify its integrity, and install the new version. Thermiqra will start again after installation. Your settings and statistics will be preserved."),
                Margin =
                    new Thickness(
                        0,
                        18,
                        0,
                        20),
                TextWrapping =
                    TextWrapping.Wrap,
                LineHeight = 19
            };

        details.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        Grid.SetRow(
            details,
            2);

        root.Children.Add(
            details);

        StackPanel buttons =
            new()
            {
                Orientation =
                    Orientation.Horizontal,
                HorizontalAlignment =
                    HorizontalAlignment.Right
            };

        Button laterButton =
            new()
            {
                Content =
                    SettingsService.L(
                        "Позже",
                        "Later"),
                MinWidth = 100,
                Padding =
                    new Thickness(
                        16,
                        8,
                        16,
                        8),
                Margin =
                    new Thickness(
                        0,
                        0,
                        10,
                        0),
                IsCancel = true
            };

        laterButton.SetResourceReference(
            Control.BackgroundProperty,
            "InputBackgroundBrush");

        laterButton.SetResourceReference(
            Control.ForegroundProperty,
            "PrimaryTextBrush");

        laterButton.SetResourceReference(
            Control.BorderBrushProperty,
            "BorderBrush");

        Button updateButton =
            new()
            {
                Content =
                    SettingsService.L(
                        "Обновить",
                        "Update"),
                MinWidth = 110,
                Padding =
                    new Thickness(
                        16,
                        8,
                        16,
                        8),
                IsDefault = true
            };

        updateButton.SetResourceReference(
            Control.BackgroundProperty,
            "InputBackgroundBrush");

        updateButton.SetResourceReference(
            Control.ForegroundProperty,
            "PrimaryTextBrush");

        updateButton.SetResourceReference(
            Control.BorderBrushProperty,
            "AccentBrush");

        laterButton.Click +=
            (_, _) =>
            {
                dialog.DialogResult =
                    false;
            };

        updateButton.Click +=
            (_, _) =>
            {
                dialog.DialogResult =
                    true;
            };

        buttons.Children.Add(
            laterButton);

        buttons.Children.Add(
            updateButton);

        Grid.SetRow(
            buttons,
            3);

        root.Children.Add(
            buttons);

        dialog.Content =
            root;

        return dialog.ShowDialog() ==
               true;
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
            progressWindow =
                CreateUpdateProgressWindow(
                    out ProgressBar progressBar,
                    out TextBlock statusText,
                    out Button cancelButton);

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

            Progress<int> progress =
                new(
                    percent =>
                    {
                        progressBar.Value =
                            percent;

                        statusText.Text =
                            SettingsService.L(
                                $"Скачивание обновления… {percent}%",
                                $"Downloading update… {percent}%");
                    });

            string installerPath =
                await UpdateService
                    .DownloadInstallerAsync(
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

            statusText.Text =
                SettingsService.L(
                    "Запуск установки…",
                    "Starting installation…");

            progressBar.Value =
                100;

            await Task.Delay(
                250);

            UpdateService.StartInstaller(
                installerPath);

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

    private Window CreateUpdateProgressWindow(
        out ProgressBar progressBar,
        out TextBlock statusText,
        out Button cancelButton)
    {
        Window window =
            new()
            {
                Owner = this,
                Title =
                    SettingsService.L(
                        "Thermiqra — Обновление",
                        "Thermiqra — Update"),
                Width = 460,
                Height = 205,
                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner,
                ResizeMode =
                    ResizeMode.NoResize,
                ShowInTaskbar = false
            };

        window.SetResourceReference(
            Window.BackgroundProperty,
            "WindowBackgroundBrush");

        StackPanel panel =
            new()
            {
                Margin =
                    new Thickness(
                        22)
            };

        statusText =
            new TextBlock
            {
                Text =
                    SettingsService.L(
                        "Подготовка к скачиванию…",
                        "Preparing download…"),
                FontSize = 14,
                FontWeight =
                    FontWeights.SemiBold,
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        14)
            };

        statusText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "PrimaryTextBrush");

        progressBar =
            new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Height = 18
            };

        progressBar.SetResourceReference(
            Control.BackgroundProperty,
            "InputBackgroundBrush");

        progressBar.SetResourceReference(
            Control.ForegroundProperty,
            "AccentBrush");

        cancelButton =
            new Button
            {
                Content =
                    SettingsService.L(
                        "Отмена",
                        "Cancel"),
                MinWidth = 100,
                Padding =
                    new Thickness(
                        16,
                        8,
                        16,
                        8),
                Margin =
                    new Thickness(
                        0,
                        16,
                        0,
                        0),
                HorizontalAlignment =
                    HorizontalAlignment.Right
            };

        cancelButton.SetResourceReference(
            Control.BackgroundProperty,
            "InputBackgroundBrush");

        cancelButton.SetResourceReference(
            Control.ForegroundProperty,
            "PrimaryTextBrush");

        cancelButton.SetResourceReference(
            Control.BorderBrushProperty,
            "BorderBrush");

        panel.Children.Add(
            statusText);

        panel.Children.Add(
            progressBar);

        panel.Children.Add(
            cancelButton);

        window.Content =
            panel;

        return window;
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
