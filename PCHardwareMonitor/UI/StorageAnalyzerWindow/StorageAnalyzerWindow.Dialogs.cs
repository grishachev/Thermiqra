using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCHardwareMonitor;

public partial class StorageAnalyzerWindow
{

    private List<CleanupPlanItem>? ShowSafeCleanupPlanDialog(
        IReadOnlyList<CleanupPlanItem> plan)
    {
        Window dialog =
            CreateThemedDialogWindow(
                SettingsService.L(
                    "Thermiqra — Безопасная очистка",
                    "Thermiqra — Safe cleanup"),
                640,
                620);

        Grid root =
            new()
            {
                Margin =
                    new Thickness(
                        20)
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
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        StackPanel header =
            new();

        TextBlock title =
            CreateDialogText(
                SettingsService.L(
                    "Что будет очищено",
                    "What will be cleaned"),
                18,
                FontWeights.Bold,
                "PrimaryTextBrush");

        TextBlock description =
            CreateDialogText(
                SettingsService.L(
                    "Thermiqra включает сюда только области с проверенным сценарием очистки. Будет удалено содержимое указанных папок; сами папки останутся. Эти временные данные удаляются напрямую, чтобы действительно освободить место. Занятые и защищённые файлы будут пропущены.",
                    "Thermiqra includes only areas with a verified cleanup procedure. The contents of the listed folders will be deleted; the folders themselves will remain. These temporary data are deleted directly so disk space is actually freed. Files in use or protected will be skipped."),
                11,
                FontWeights.Normal,
                "SecondaryTextBrush");

        description.Margin =
            new Thickness(
                0, 7, 0, 0);

        description.TextWrapping =
            TextWrapping.Wrap;

        header.Children.Add(
            title);

        header.Children.Add(
            description);

        root.Children.Add(
            header);

        ScrollViewer scroll =
            new()
            {
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Auto,

                Margin =
                    new Thickness(
                        0, 14, 0, 14)
            };

        StackPanel itemsPanel =
            new();

        List<(CheckBox CheckBox, CleanupPlanItem Item)> checkBoxes =
            new();

        TextBlock totalText =
            CreateDialogText(
                "",
                13,
                FontWeights.Bold,
                "AccentBrush");

        void UpdateTotal()
        {
            long selectedBytes =
                checkBoxes
                    .Where(
                        pair =>
                            pair.CheckBox.IsChecked ==
                            true)
                    .Sum(
                        pair =>
                            pair.Item.AllocatedBytes);

            totalText.Text =
                SettingsService.L(
                    $"Выбрано: примерно {FormatBytes(selectedBytes)}",
                    $"Selected: approximately {FormatBytes(selectedBytes)}");
        }

        foreach (CleanupPlanItem item
                 in plan)
        {
            Border card =
                new()
                {
                    BorderThickness =
                        new Thickness(1),

                    Padding =
                        new Thickness(
                            12),

                    Margin =
                        new Thickness(
                            0, 0, 0, 8)
                };

            card.SetResourceReference(
                Border.BackgroundProperty,
                "CardBackgroundBrush");

            card.SetResourceReference(
                Border.BorderBrushProperty,
                "BorderBrush");

            card.SetResourceReference(
                Border.CornerRadiusProperty,
                "MiniCardCornerRadius");

            Grid cardGrid =
                new();

            cardGrid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        GridLength.Auto
                });

            cardGrid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(
                            1,
                            GridUnitType.Star)
                });

            string? browserProcess = GetBrowserProcessForCacheRoot(item.Path);
            bool browserRunning = browserProcess != null && IsBrowserProcessRunning(browserProcess);

            CheckBox checkBox =
                new()
                {
                    // A running browser stays selected so that Thermiqra
                    // requests permission to close it BEFORE deletion.
                    // The worker will still verify and refuse an open browser.
                    IsChecked =
                        true,

                    VerticalAlignment =
                        VerticalAlignment.Top,

                    Margin =
                        new Thickness(
                            0, 2, 10, 0),

                    Tag =
                        item
                };

            checkBox.SetResourceReference(
                Control.ForegroundProperty,
                "PrimaryTextBrush");

            StackPanel itemContent =
                new();

            TextBlock itemTitle =
                CreateDialogText(
                    $"{item.Title} — {FormatBytes(item.AllocatedBytes)}",
                    12,
                    FontWeights.Bold,
                    "PrimaryTextBrush");

            TextBlock itemDescription =
                CreateDialogText(
                    item.Description + (browserRunning
                        ? SettingsService.L(
                            " Если браузер открыт, Thermiqra предложит закрыть его автоматически с вашего согласия.",
                            " If the browser is running, Thermiqra will offer to close it automatically with your permission.")
                        : string.Empty),
                    10,
                    FontWeights.Normal,
                    "SecondaryTextBrush");

            itemDescription.Margin =
                new Thickness(
                    0, 4, 0, 0);

            itemDescription.TextWrapping =
                TextWrapping.Wrap;

            TextBlock itemPath =
                CreateDialogText(
                    item.Path,
                    9,
                    FontWeights.Normal,
                    "SecondaryTextBrush");

            itemPath.Margin =
                new Thickness(
                    0, 4, 0, 0);

            itemPath.TextWrapping =
                TextWrapping.Wrap;

            itemContent.Children.Add(
                itemTitle);

            itemContent.Children.Add(
                itemDescription);

            itemContent.Children.Add(
                itemPath);

            Grid.SetColumn(
                itemContent,
                1);

            cardGrid.Children.Add(
                checkBox);

            cardGrid.Children.Add(
                itemContent);

            card.Child =
                cardGrid;

            itemsPanel.Children.Add(
                card);

            checkBoxes.Add(
                (checkBox, item));

            checkBox.Checked +=
                (_, _) =>
                    UpdateTotal();

            checkBox.Unchecked +=
                (_, _) =>
                    UpdateTotal();
        }

        scroll.Content =
            itemsPanel;

        Grid.SetRow(
            scroll,
            1);

        root.Children.Add(
            scroll);

        Grid footer =
            new();

        footer.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        footer.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        footer.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        totalText.VerticalAlignment =
            VerticalAlignment.Center;

        footer.Children.Add(
            totalText);

        Button cancelButton =
            CreateDialogButton(
                SettingsService.L(
                    "Отмена",
                    "Cancel"));

        cancelButton.MinWidth =
            100;

        cancelButton.Margin =
            new Thickness(
                10, 0, 8, 0);

        cancelButton.Click +=
            (_, _) =>
            {
                dialog.DialogResult =
                    false;
            };

        Button cleanButton =
            CreateDialogButton(
                SettingsService.L(
                    "Очистить выбранное",
                    "Clean selected"));

        cleanButton.MinWidth =
            150;

        cleanButton.SetResourceReference(
            Control.BorderBrushProperty,
            "AccentBrush");

        cleanButton.SetResourceReference(
            Control.ForegroundProperty,
            "AccentBrush");

        cleanButton.Click +=
            (_, _) =>
            {
                List<(CheckBox CheckBox, CleanupPlanItem Item)> selectedRows =
                    checkBoxes.Where(pair => pair.CheckBox.IsChecked == true).ToList();
                if (selectedRows.Count == 0)
                    return;

                // Check at click time, not just when the plan window opened.
                // Do not silently accept a selected cache that cannot be cleaned.
                List<CleanupPlanItem> runningBrowsers = selectedRows
                    .Select(pair => pair.Item)
                    .Where(item =>
                    {
                        string? process = GetBrowserProcessForCacheRoot(item.Path);
                        return process != null && IsBrowserProcessRunning(process);
                    })
                    .ToList();

                if (runningBrowsers.Count > 0)
                {
                    bool? decision = ShowRunningBrowserCleanupDialog(dialog, runningBrowsers);
                    if (decision == null)
                        return; // Return to the selection without deleting anything.

                    if (decision == false)
                    {
                        // Explicit opt-out from the running browsers only.
                        // Other selected safe areas are preserved.
                        foreach (var pair in selectedRows)
                        {
                            string? process = GetBrowserProcessForCacheRoot(pair.Item.Path);
                            if (process != null && IsBrowserProcessRunning(process))
                                pair.CheckBox.IsChecked = false;
                        }
                        if (!checkBoxes.Any(pair => pair.CheckBox.IsChecked == true))
                        {
                            description.Text = SettingsService.L(
                                "Сейчас нет выбранных областей для очистки. Браузеры пропущены по вашему выбору. Закройте нужный браузер, отметьте его кэш и нажмите «Очистить выбранное» ещё раз.",
                                "Nothing is selected for cleanup. Running browsers were skipped at your request. Close the browser, select its cache and choose Clean selected again.");
                            return;
                        }
                    }
                    // On retry, browser processes were checked by the dialog.
                    // The worker will check AGAIN in case they reopen.
                }

                dialog.DialogResult = true;
            };

        Grid.SetColumn(
            cancelButton,
            1);

        Grid.SetColumn(
            cleanButton,
            2);

        footer.Children.Add(
            cancelButton);

        footer.Children.Add(
            cleanButton);

        Grid.SetRow(
            footer,
            2);

        root.Children.Add(
            footer);

        dialog.Content =
            root;

        UpdateTotal();

        bool? result =
            dialog.ShowDialog();

        if (result !=
            true)
        {
            return null;
        }

        return checkBoxes
            .Where(
                pair =>
                    pair.CheckBox.IsChecked ==
                    true)
            .Select(
                pair =>
                    pair.Item)
            .ToList();
    }


    // A browser that the user selected for cleaning is closed by Thermiqra
    // only after the user explicitly agrees. Unknown process executables are
    // never terminated, and the cleanup worker revalidates paths and activity.
    private bool? ShowRunningBrowserCleanupDialog(
        Window planDialog,
        IReadOnlyList<CleanupPlanItem> browserItems)
    {
        Window warning = CreateThemedDialogWindow(
            SettingsService.L(
                "Thermiqra — Закрыть браузеры",
                "Thermiqra — Close browsers"),
            640, 445);
        warning.Owner = planDialog;
        warning.ResizeMode = ResizeMode.NoResize;

        Grid root = new() { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        StackPanel content = new();

        TextBlock heading = CreateDialogText(
            SettingsService.L(
                "Для очистки кэша нужно закрыть браузер",
                "The browser must close before cache cleanup"),
            17, FontWeights.Bold, "PrimaryTextBrush");
        heading.TextWrapping = TextWrapping.Wrap;
        content.Children.Add(heading);

        TextBlock explanation = CreateDialogText(
            SettingsService.L(
                "Thermiqra может самостоятельно закрыть перечисленные браузеры и продолжить очистку. Открытые вкладки, несохранённый текст и загрузки могут быть потеряны. Если обычное закрытие не сработает, оставшиеся процессы браузера будут завершены принудительно. Закрыть браузеры и очистить кэш?",
                "Thermiqra can close the listed browsers automatically and continue cleanup. Open tabs, unsaved text and downloads may be lost. If normal shutdown fails, remaining browser processes will be terminated. Close the browsers and clean their caches?"),
            11.5, FontWeights.Normal, "SecondaryTextBrush");
        explanation.TextWrapping = TextWrapping.Wrap;
        explanation.Margin = new Thickness(0, 12, 0, 12);
        content.Children.Add(explanation);

        TextBlock browserNames = CreateDialogText(
            "", 12.5, FontWeights.Bold, "AccentBrush");
        browserNames.TextWrapping = TextWrapping.Wrap;
        content.Children.Add(browserNames);

        TextBlock state = CreateDialogText(
            SettingsService.L(
                "Браузеры будут закрыты только после вашего согласия.",
                "Browsers will close only after your confirmation."),
            10.5, FontWeights.Normal, "SecondaryTextBrush");
        state.TextWrapping = TextWrapping.Wrap;
        state.Margin = new Thickness(0, 15, 0, 0);
        content.Children.Add(state);

        ScrollViewer contentScroll = new()
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        contentScroll.Content = content;
        root.Children.Add(contentScroll);

        string[] GetRunningNames() => browserItems
            .Where(item =>
            {
                string? process = GetBrowserProcessForCacheRoot(item.Path);
                return process != null && IsBrowserProcessRunning(process);
            })
            .Select(item =>
            {
                int delimiter = item.Title.IndexOf(" — ", StringComparison.Ordinal);
                return delimiter > 0 ? item.Title[..delimiter] : item.Title;
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        void UpdateRunningNames(string[] names) => browserNames.Text =
            string.Join("\n", names.Select(name => "• " + name));
        UpdateRunningNames(GetRunningNames());

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 15, 0, 0)
        };

        Button back = CreateDialogButton(SettingsService.L("Назад", "Back"));
        back.MinWidth = 75;
        back.Click += (_, _) => warning.DialogResult = false;

        Button skip = CreateDialogButton(SettingsService.L(
            "Очистить остальное", "Clean other areas"));
        skip.MinWidth = 145;
        skip.Margin = new Thickness(8, 0, 0, 0);

        Button closeAndClean = CreateDialogButton(SettingsService.L(
            "Закрыть и очистить", "Close and clean"));
        closeAndClean.MinWidth = 145;
        closeAndClean.Margin = new Thickness(8, 0, 0, 0);
        closeAndClean.SetResourceReference(Control.BorderBrushProperty, "AccentBrush");
        closeAndClean.SetResourceReference(Control.ForegroundProperty, "AccentBrush");

        bool? decision = null;
        bool closing = false;
        warning.Closing += (_, e) =>
        {
            if (closing && decision != true)
                e.Cancel = true;
        };
        skip.Click += (_, _) =>
        {
            decision = false;
            warning.DialogResult = true;
        };
        closeAndClean.Click += async (_, _) =>
        {
            if (closing)
                return;

            // The browser may have exited since the dialog opened.
            string[] processNames = browserItems
                .Select(item => GetBrowserProcessForCacheRoot(item.Path))
                .Where(name => name != null)
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            closing = true;
            back.IsEnabled = false;
            skip.IsEnabled = false;
            closeAndClean.IsEnabled = false;
            state.Text = SettingsService.L(
                "Закрываем браузеры и проверяем завершение процессов...",
                "Closing browsers and checking that their processes have exited...");

            try
            {
                bool closed = await Task.Run(() =>
                    CloseSelectedBrowsersAfterConsent(processNames));
                string[] remaining = GetRunningNames();
                if (closed && remaining.Length == 0)
                {
                    decision = true;
                    warning.DialogResult = true;
                    return;
                }

                UpdateRunningNames(remaining);
                state.Text = SettingsService.L(
                    "Не все браузеры удалось закрыть. Очистка их кэша не начнётся. Можно повторить попытку или очистить остальные области.",
                    "Not all browsers could be closed. Their caches will not be cleaned. You can try again or clean other selected areas.");
            }
            catch
            {
                state.Text = SettingsService.L(
                    "Не удалось завершить браузер. Его кэш не будет удалён без успешной проверки закрытия.",
                    "Could not close the browser. Its cache will not be deleted without a successful shutdown check.");
            }
            finally
            {
                closing = false;
                back.IsEnabled = true;
                skip.IsEnabled = true;
                closeAndClean.IsEnabled = true;
            }
        };

        buttons.Children.Add(back);
        buttons.Children.Add(skip);
        buttons.Children.Add(closeAndClean);
        Grid.SetRow(buttons, 1);
        root.Children.Add(buttons);
        warning.Content = root;
        warning.ShowDialog();
        return decision;
    }


    private DeleteChoice ShowDeleteChoiceDialog(
        SelectedStorageEntry entry,
        StorageGuidance guidance)
    {
        Window dialog =
            CreateThemedDialogWindow(
                SettingsService.L(
                    "Thermiqra — Удаление",
                    "Thermiqra — Delete"),
                650,
                guidance.Level ==
                    StorageSafetyLevel.Protected
                    ? 470
                    : 430);

        Grid root =
            new()
            {
                Margin =
                    new Thickness(
                        20)
            };

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        StackPanel content =
            new();

        TextBlock title =
            CreateDialogText(
                SettingsService.L(
                    entry.IsDirectory
                        ? "Удалить выбранную папку?"
                        : "Удалить выбранный файл?",
                    entry.IsDirectory
                        ? "Delete the selected folder?"
                        : "Delete the selected file?"),
                18,
                FontWeights.Bold,
                "PrimaryTextBrush");

        TextBlock name =
            CreateDialogText(
                $"{entry.Name} — {FormatBytes(entry.AllocatedBytes)}",
                12,
                FontWeights.Bold,
                "AccentBrush");

        name.Margin =
            new Thickness(
                0, 10, 0, 0);

        TextBlock path =
            CreateDialogText(
                entry.Path,
                9.5,
                FontWeights.Normal,
                "SecondaryTextBrush");

        path.Margin =
            new Thickness(
                0, 4, 0, 0);

        path.TextWrapping =
            TextWrapping.Wrap;

        TextBlock safety =
            CreateDialogText(
                $"{guidance.SafetyTitle}\n{guidance.SafetyDescription}",
                10.5,
                FontWeights.Normal,
                guidance.Level ==
                    StorageSafetyLevel.Protected
                    ? "AccentBrush"
                    : "PrimaryTextBrush");

        safety.Margin =
            new Thickness(
                0, 14, 0, 0);

        safety.TextWrapping =
            TextWrapping.Wrap;

        TextBlock recycleText =
            CreateDialogText(
                SettingsService.L(
                    "В корзину — можно будет восстановить. Место окончательно освободится после очистки Корзины.",
                    "Recycle — the item can still be restored. Disk space is fully released after the Recycle Bin is emptied."),
                10.5,
                FontWeights.Normal,
                "PrimaryTextBrush");

        recycleText.Margin =
            new Thickness(
                0, 14, 0, 0);

        recycleText.TextWrapping =
            TextWrapping.Wrap;

        TextBlock permanentText =
            CreateDialogText(
                SettingsService.L(
                    "Безвозвратно — удаление сразу освобождает место, восстановить объект через Корзину будет нельзя.",
                    "Permanently — disk space is released immediately, and the item cannot be restored through the Recycle Bin."),
                10.5,
                FontWeights.Normal,
                "PrimaryTextBrush");

        permanentText.Margin =
            new Thickness(
                0, 8, 0, 0);

        permanentText.TextWrapping =
            TextWrapping.Wrap;

        content.Children.Add(
            title);

        content.Children.Add(
            name);

        content.Children.Add(
            path);

        content.Children.Add(
            safety);

        content.Children.Add(
            recycleText);

        content.Children.Add(
            permanentText);

        root.Children.Add(
            content);

        StackPanel buttons =
            new()
            {
                Orientation =
                    Orientation.Horizontal,

                HorizontalAlignment =
                    HorizontalAlignment.Right,

                Margin =
                    new Thickness(
                        0, 18, 0, 0)
            };

        DeleteChoice choice =
            DeleteChoice.Cancel;

        Button cancel =
            CreateDialogButton(
                SettingsService.L(
                    "Отмена",
                    "Cancel"));

        cancel.MinWidth =
            95;

        cancel.Click +=
            (_, _) =>
            {
                choice =
                    DeleteChoice.Cancel;

                dialog.DialogResult =
                    false;
            };

        Button recycle =
            CreateDialogButton(
                SettingsService.L(
                    "В корзину",
                    "Recycle"));

        recycle.MinWidth =
            110;

        recycle.Margin =
            new Thickness(
                8, 0, 0, 0);

        recycle.Click +=
            (_, _) =>
            {
                choice =
                    DeleteChoice.Recycle;

                dialog.DialogResult =
                    true;
            };

        Button permanent =
            CreateDialogButton(
                SettingsService.L(
                    "Безвозвратно",
                    "Permanently"));

        permanent.MinWidth =
            125;

        permanent.Margin =
            new Thickness(
                8, 0, 0, 0);

        permanent.SetResourceReference(
            Control.BorderBrushProperty,
            "AccentBrush");

        permanent.SetResourceReference(
            Control.ForegroundProperty,
            "AccentBrush");

        permanent.Click +=
            (_, _) =>
            {
                choice =
                    DeleteChoice.Permanent;

                dialog.DialogResult =
                    true;
            };

        buttons.Children.Add(
            cancel);

        buttons.Children.Add(
            recycle);

        buttons.Children.Add(
            permanent);

        Grid.SetRow(
            buttons,
            1);

        root.Children.Add(
            buttons);

        dialog.Content =
            root;

        dialog.ShowDialog();

        return choice;
    }


    private DownloadsClearChoice ShowDownloadsClearChoiceDialog(
        long bytes,
        FolderContentStats stats)
    {
        Window dialog =
            CreateThemedDialogWindow(
                SettingsService.L(
                    "Thermiqra — Очистить «Загрузки»",
                    "Thermiqra — Clean Downloads"),
                620,
                390);

        Grid root =
            new()
            {
                Margin =
                    new Thickness(
                        20)
            };

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        StackPanel content =
            new();

        TextBlock title =
            CreateDialogText(
                SettingsService.L(
                    "Как удалить содержимое «Загрузок»?",
                    "How should Downloads contents be removed?"),
                18,
                FontWeights.Bold,
                "PrimaryTextBrush");

        TextBlock summary =
            CreateDialogText(
                SettingsService.L(
                    $"Сейчас здесь примерно {FormatBytes(bytes)}: {stats.FileCount:N0} файлов и {stats.FolderCount:N0} папок. Сама папка «Загрузки» останется.",
                    $"There is currently about {FormatBytes(bytes)} here: {stats.FileCount:N0} files and {stats.FolderCount:N0} folders. The Downloads folder itself will remain."),
                11,
                FontWeights.Normal,
                "SecondaryTextBrush");

        summary.Margin =
            new Thickness(
                0, 8, 0, 0);

        summary.TextWrapping =
            TextWrapping.Wrap;

        TextBlock recycleText =
            CreateDialogText(
                SettingsService.L(
                    "В корзину — безопаснее: файлы можно будет восстановить, но место на диске освободится только после очистки Корзины.",
                    "Recycle — safer: files can be restored, but disk space is freed only after the Recycle Bin is emptied."),
                10.5,
                FontWeights.Normal,
                "PrimaryTextBrush");

        recycleText.Margin =
            new Thickness(
                0, 16, 0, 0);

        recycleText.TextWrapping =
            TextWrapping.Wrap;

        TextBlock permanentText =
            CreateDialogText(
                SettingsService.L(
                    "Безвозвратно — место освобождается сразу, восстановить файлы через Корзину будет нельзя.",
                    "Permanently — space is freed immediately, and files cannot be restored from the Recycle Bin."),
                10.5,
                FontWeights.Normal,
                "PrimaryTextBrush");

        permanentText.Margin =
            new Thickness(
                0, 9, 0, 0);

        permanentText.TextWrapping =
            TextWrapping.Wrap;

        content.Children.Add(
            title);

        content.Children.Add(
            summary);

        content.Children.Add(
            recycleText);

        content.Children.Add(
            permanentText);

        root.Children.Add(
            content);

        StackPanel buttons =
            new()
            {
                Orientation =
                    Orientation.Horizontal,

                HorizontalAlignment =
                    HorizontalAlignment.Right,

                Margin =
                    new Thickness(
                        0, 18, 0, 0)
            };

        DownloadsClearChoice choice =
            DownloadsClearChoice.Cancel;

        Button cancel =
            CreateDialogButton(
                SettingsService.L(
                    "Отмена",
                    "Cancel"));

        cancel.MinWidth =
            95;

        cancel.Click +=
            (_, _) =>
            {
                choice =
                    DownloadsClearChoice.Cancel;

                dialog.DialogResult =
                    false;
            };

        Button recycle =
            CreateDialogButton(
                SettingsService.L(
                    "В корзину",
                    "Recycle"));

        recycle.MinWidth =
            105;

        recycle.Margin =
            new Thickness(
                8, 0, 0, 0);

        recycle.Click +=
            (_, _) =>
            {
                choice =
                    DownloadsClearChoice.Recycle;

                dialog.DialogResult =
                    true;
            };

        Button permanent =
            CreateDialogButton(
                SettingsService.L(
                    "Безвозвратно",
                    "Permanently"));

        permanent.MinWidth =
            120;

        permanent.Margin =
            new Thickness(
                8, 0, 0, 0);

        permanent.SetResourceReference(
            Control.BorderBrushProperty,
            "AccentBrush");

        permanent.SetResourceReference(
            Control.ForegroundProperty,
            "AccentBrush");

        permanent.Click +=
            (_, _) =>
            {
                choice =
                    DownloadsClearChoice.Permanent;

                dialog.DialogResult =
                    true;
            };

        buttons.Children.Add(
            cancel);

        buttons.Children.Add(
            recycle);

        buttons.Children.Add(
            permanent);

        Grid.SetRow(
            buttons,
            1);

        root.Children.Add(
            buttons);

        dialog.Content =
            root;

        dialog.ShowDialog();

        return choice;
    }


    private bool ShowConfirmationDialog(
        string title,
        string description,
        string confirmText)
    {
        Window dialog =
            CreateThemedDialogWindow(
                title,
                560,
                300);

        Grid root =
            new()
            {
                Margin =
                    new Thickness(
                        20)
            };

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        TextBlock descriptionText =
            CreateDialogText(
                description,
                11,
                FontWeights.Normal,
                "PrimaryTextBrush");

        descriptionText.TextWrapping =
            TextWrapping.Wrap;

        root.Children.Add(
            descriptionText);

        StackPanel buttons =
            new()
            {
                Orientation =
                    Orientation.Horizontal,

                HorizontalAlignment =
                    HorizontalAlignment.Right,

                Margin =
                    new Thickness(
                        0, 16, 0, 0)
            };

        Button cancel =
            CreateDialogButton(
                SettingsService.L(
                    "Отмена",
                    "Cancel"));

        cancel.MinWidth =
            95;

        cancel.Click +=
            (_, _) =>
            {
                dialog.DialogResult =
                    false;
            };

        Button confirm =
            CreateDialogButton(
                confirmText);

        confirm.MinWidth =
            135;

        confirm.Margin =
            new Thickness(
                8, 0, 0, 0);

        confirm.SetResourceReference(
            Control.BorderBrushProperty,
            "AccentBrush");

        confirm.SetResourceReference(
            Control.ForegroundProperty,
            "AccentBrush");

        confirm.Click +=
            (_, _) =>
            {
                dialog.DialogResult =
                    true;
            };

        buttons.Children.Add(
            cancel);

        buttons.Children.Add(
            confirm);

        Grid.SetRow(
            buttons,
            1);

        root.Children.Add(
            buttons);

        dialog.Content =
            root;

        return dialog.ShowDialog() ==
               true;
    }


    private void ShowInfoDialog(
        string title,
        string description)
    {
        Window dialog =
            CreateThemedDialogWindow(
                title,
                540,
                280);

        Grid root =
            new()
            {
                Margin =
                    new Thickness(
                        20)
            };

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        TextBlock text =
            CreateDialogText(
                description,
                11,
                FontWeights.Normal,
                "PrimaryTextBrush");

        text.TextWrapping =
            TextWrapping.Wrap;

        root.Children.Add(
            text);

        Button close =
            CreateDialogButton(
                SettingsService.L(
                    "Понятно",
                    "OK"));

        close.MinWidth =
            100;

        close.HorizontalAlignment =
            HorizontalAlignment.Right;

        close.Click +=
            (_, _) =>
            {
                dialog.DialogResult =
                    true;
            };

        Grid.SetRow(
            close,
            1);

        root.Children.Add(
            close);

        dialog.Content =
            root;

        dialog.ShowDialog();
    }


    // ShowDialog keeps the owner inactive but continues pumping WPF messages,
    // so the indeterminate bar and counters stay responsive during cleanup.
    private SafeCleanupOutcome ShowSafeCleanupProgressDialog(
        IReadOnlyList<CleanupPlanItem> items)
    {
        Window dialog = CreateThemedDialogWindow(
            SettingsService.L(
                "Thermiqra — Безопасная очистка",
                "Thermiqra — Safe cleanup"),
            650,
            390);
        dialog.ResizeMode = ResizeMode.NoResize;

        Grid root = new() { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        StackPanel body = new();
        TextBlock title = CreateDialogText(
            SettingsService.L("Выполняется безопасная очистка", "Safe cleanup in progress"),
            18, FontWeights.Bold, "PrimaryTextBrush");
        body.Children.Add(title);

        TextBlock description = CreateDialogText(
            SettingsService.L(
                "Удаляются только выбранные временные данные. Занятые и защищённые объекты будут пропущены.",
                "Only the selected temporary data are being deleted. In-use and protected items will be skipped."),
            11, FontWeights.Normal, "SecondaryTextBrush");
        description.TextWrapping = TextWrapping.Wrap;
        description.Margin = new Thickness(0, 9, 0, 14);
        body.Children.Add(description);

        TextBlock areaText = CreateDialogText(
            SettingsService.L("Подготовка...", "Preparing..."),
            12, FontWeights.Bold, "AccentBrush");
        body.Children.Add(areaText);

        ProgressBar bar = new()
        {
            Height = 12,
            IsIndeterminate = true,
            Margin = new Thickness(0, 12, 0, 12),
            IsHitTestVisible = false
        };
        bar.SetResourceReference(Control.ForegroundProperty, "AccentBrush");
        bar.SetResourceReference(Control.BackgroundProperty, "InputBackgroundBrush");
        body.Children.Add(bar);

        TextBlock currentText = CreateDialogText(
            SettingsService.L("Поиск объектов...", "Finding items..."),
            10, FontWeights.Normal, "SecondaryTextBrush");
        currentText.TextTrimming = TextTrimming.CharacterEllipsis;
        currentText.ToolTip = currentText.Text;
        body.Children.Add(currentText);

        TextBlock counters = CreateDialogText(
            SettingsService.L(
                "Удалено файлов: 0   Папок: 0   Пропущено: 0",
                "Deleted files: 0   Folders: 0   Skipped: 0"),
            11, FontWeights.Bold, "PrimaryTextBrush");
        counters.Margin = new Thickness(0, 17, 0, 0);
        counters.TextWrapping = TextWrapping.Wrap;
        body.Children.Add(counters);

        TextBlock note = CreateDialogText(
            SettingsService.L(
                "Процент не показывается: точное число объектов заранее неизвестно.",
                "No percentage is shown because the exact number of items is not known in advance."),
            10, FontWeights.Normal, "SecondaryTextBrush");
        note.Margin = new Thickness(0, 10, 0, 0);
        note.TextWrapping = TextWrapping.Wrap;
        body.Children.Add(note);
        root.Children.Add(body);

        Button stopButton = CreateDialogButton(
            SettingsService.L("Остановить", "Stop"));
        stopButton.MinWidth = 135;
        stopButton.HorizontalAlignment = HorizontalAlignment.Right;
        stopButton.Margin = new Thickness(0, 16, 0, 0);
        Grid.SetRow(stopButton, 1);
        root.Children.Add(stopButton);
        dialog.Content = root;

        using CancellationTokenSource cancellation = new();
        bool finished = false;
        SafeCleanupOutcome? outcome = null;
        Exception? failure = null;

        void RequestStop()
        {
            if (finished || cancellation.IsCancellationRequested)
                return;

            cancellation.Cancel();
            stopButton.IsEnabled = false;
            areaText.Text = SettingsService.L(
                "Останавливаем очистку...", "Stopping cleanup...");
            note.Text = SettingsService.L(
                "Уже удалённые объекты не восстановятся. Ожидаем завершения текущей операции.",
                "Already deleted items cannot be restored. Waiting for the current operation to finish.");
        }

        stopButton.Click += (_, _) => RequestStop();
        dialog.Closing += (_, eventArgs) =>
        {
            if (!finished)
            {
                eventArgs.Cancel = true;
                RequestStop();
            }
        };

        // Created on UI thread: Progress<T> posts updates to WPF Dispatcher.
        IProgress<SafeCleanupProgress> progress =
            new Progress<SafeCleanupProgress>(update =>
            {
                if (finished || cancellation.IsCancellationRequested)
                    return;

                areaText.Text = update.Area;
                currentText.Text = update.CurrentPath;
                currentText.ToolTip = update.CurrentPath;
                counters.Text = SettingsService.L(
                    $"Удалено файлов: {update.DeletedFiles:N0}   Папок: {update.DeletedFolders:N0}   Пропущено: {update.SkippedEntries:N0}",
                    $"Deleted files: {update.DeletedFiles:N0}   Folders: {update.DeletedFolders:N0}   Skipped: {update.SkippedEntries:N0}");
            });

        dialog.Loaded += async (_, _) =>
        {
            try
            {
                outcome = await Task.Run(() =>
                    ExecuteSafeCleanup(items, progress, cancellation.Token));
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                finished = true;
                dialog.DialogResult = true;
            }
        };

        dialog.ShowDialog();
        if (failure != null)
            throw new InvalidOperationException(
                SettingsService.L("Ошибка безопасной очистки.", "Safe cleanup failed."),
                failure);

        return outcome ?? throw new InvalidOperationException(
            SettingsService.L("Очистка не была запущена.", "Cleanup was not started."));
    }

    private void ShowCleanupResultDialog(SafeCleanupOutcome outcome)
    {
        CleanupExecutionResult result = outcome.Result;
        string title = outcome.WasStopped
            ? SettingsService.L("Безопасная очистка остановлена", "Safe cleanup stopped")
            : SettingsService.L("Безопасная очистка завершена", "Safe cleanup complete");

        Window dialog = CreateThemedDialogWindow(
            "Thermiqra — " + title, 630, 490);
        dialog.MinHeight = 400;
        dialog.ResizeMode = ResizeMode.CanResize;

        Grid root = new() { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        StackPanel header = new();
        TextBlock heading = CreateDialogText(
            title, 20, FontWeights.Bold, "PrimaryTextBrush");
        header.Children.Add(heading);

        TextBlock summary = CreateDialogText(
            outcome.WasStopped
                ? SettingsService.L(
                    "Удаление остановлено. Уже удалённые файлы не восстанавливаются.",
                    "Cleanup stopped. Already deleted files cannot be restored.")
                : SettingsService.L(
                    "Результаты обработки выбранных временных данных",
                    "Results for the selected temporary data"),
            11, FontWeights.Normal, "SecondaryTextBrush");
        summary.TextWrapping = TextWrapping.Wrap;
        summary.Margin = new Thickness(0, 7, 0, 16);
        header.Children.Add(summary);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        ScrollViewer scroll = new()
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        StackPanel content = new();

        Border freedCard = new()
        {
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 12)
        };
        freedCard.SetResourceReference(Border.BackgroundProperty, "CardBackgroundBrush");
        freedCard.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");

        StackPanel freedBody = new();
        TextBlock freedTitle = CreateDialogText(
            SettingsService.L("ОСВОБОЖДЕНО МЕСТА", "SPACE FREED"),
            10.5, FontWeights.Bold, "SecondaryTextBrush");
        freedBody.Children.Add(freedTitle);
        TextBlock freedValue = CreateDialogText(
            "≈ " + FormatBytes(result.FreedBytes),
            30, FontWeights.Bold, "AccentBrush");
        freedValue.Margin = new Thickness(0, 5, 0, 0);
        freedBody.Children.Add(freedValue);
        TextBlock freedNote = CreateDialogText(
            SettingsService.L(
                "Примерно, по изменению свободного места на диске.",
                "Approximate, based on the change in free disk space."),
            10, FontWeights.Normal, "SecondaryTextBrush");
        freedNote.TextWrapping = TextWrapping.Wrap;
        freedNote.Margin = new Thickness(0, 5, 0, 0);
        freedBody.Children.Add(freedNote);
        freedCard.Child = freedBody;
        content.Children.Add(freedCard);

        Grid counters = new() { Margin = new Thickness(0, 0, 0, 12) };
        counters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        counters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        counters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Border CreateCountCard(string label, string value, Thickness margin)
        {
            Border tile = new()
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12),
                Margin = margin
            };
            tile.SetResourceReference(Border.BackgroundProperty, "CardBackgroundBrush");
            tile.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

            StackPanel body = new();
            TextBlock number = CreateDialogText(
                value, 21, FontWeights.Bold, "PrimaryTextBrush");
            body.Children.Add(number);
            TextBlock caption = CreateDialogText(
                label, 10.5, FontWeights.Normal, "SecondaryTextBrush");
            caption.Margin = new Thickness(0, 5, 0, 0);
            caption.TextWrapping = TextWrapping.Wrap;
            body.Children.Add(caption);
            tile.Child = body;
            return tile;
        }

        Border deletedFiles = CreateCountCard(
            SettingsService.L("Удалено файлов", "Files deleted"),
            outcome.DeletedFiles.ToString("N0"), new Thickness(0, 0, 5, 0));
        Grid.SetColumn(deletedFiles, 0);
        counters.Children.Add(deletedFiles);

        Border deletedFolders = CreateCountCard(
            SettingsService.L("Удалено папок", "Folders deleted"),
            outcome.DeletedFolders.ToString("N0"), new Thickness(5, 0, 5, 0));
        Grid.SetColumn(deletedFolders, 1);
        counters.Children.Add(deletedFolders);

        Border skipped = CreateCountCard(
            SettingsService.L("Пропущено", "Skipped"),
            result.SkippedEntries.ToString("N0"), new Thickness(5, 0, 0, 0));
        Grid.SetColumn(skipped, 2);
        counters.Children.Add(skipped);
        content.Children.Add(counters);

        if (result.SkippedEntries > 0 || outcome.BlockedBrowserAreas.Count > 0)
        {
            Border noteCard = new()
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(13),
                Margin = new Thickness(0, 0, 0, 8)
            };
            noteCard.SetResourceReference(Border.BackgroundProperty, "InputBackgroundBrush");
            noteCard.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

            StackPanel noteBody = new();
            TextBlock noteTitle = CreateDialogText(
                SettingsService.L("Что не удалось очистить", "Items not cleaned"),
                12, FontWeights.Bold, "PrimaryTextBrush");
            noteBody.Children.Add(noteTitle);
            TextBlock noteText = CreateDialogText(
                SettingsService.L(
                    "Занятые, защищённые или недоступные объекты были пропущены.",
                    "In-use, protected, or inaccessible items were skipped."),
                11, FontWeights.Normal, "SecondaryTextBrush");
            noteText.TextWrapping = TextWrapping.Wrap;
            noteText.Margin = new Thickness(0, 6, 0, 0);
            noteBody.Children.Add(noteText);

            if (outcome.BlockedBrowserAreas.Count > 0)
            {
                TextBlock browserText = CreateDialogText(
                    SettingsService.L(
                        "Не очищены некоторые кэши браузеров или не прошли проверку пути:",
                        "Some browser caches were not cleaned or failed path verification:") +
                    "\n" + string.Join("\n", outcome.BlockedBrowserAreas),
                    11, FontWeights.Normal, "AccentBrush");
                browserText.TextWrapping = TextWrapping.Wrap;
                browserText.Margin = new Thickness(0, 9, 0, 0);
                noteBody.Children.Add(browserText);
            }
            noteCard.Child = noteBody;
            content.Children.Add(noteCard);
        }

        scroll.Content = content;
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        Button close = CreateDialogButton(SettingsService.L("Понятно", "OK"));
        close.MinWidth = 115;
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.Margin = new Thickness(0, 16, 0, 0);
        close.Click += (_, _) => dialog.DialogResult = true;
        Grid.SetRow(close, 2);
        root.Children.Add(close);

        dialog.Content = root;
        dialog.ShowDialog();
    }


    private void ShowGeneralDeleteResult(
        SelectedStorageEntry entry,
        bool success,
        bool permanent)
    {
        if (success)
        {
            ShowInfoDialog(
                permanent
                    ? SettingsService.L(
                        "Удалено",
                        "Deleted")
                    : SettingsService.L(
                        "Перемещено в Корзину",
                        "Moved to Recycle Bin"),
                permanent
                    ? SettingsService.L(
                        $"«{entry.Name}» удалено безвозвратно. Thermiqra сейчас обновит анализ диска.",
                        $"“{entry.Name}” was permanently deleted. Thermiqra will now refresh the drive analysis.")
                    : SettingsService.L(
                        $"«{entry.Name}» перемещено в Корзину. Thermiqra сейчас обновит анализ диска.",
                        $"“{entry.Name}” was moved to the Recycle Bin. Thermiqra will now refresh the drive analysis."));
        }
        else
        {
            ShowInfoDialog(
                SettingsService.L(
                    "Удаление выполнено не полностью",
                    "Deletion was not fully completed"),
                SettingsService.L(
                    $"Windows не смогла полностью удалить:\n\n{entry.Path}\n\nПричиной могут быть занятые, защищённые или системные файлы. Thermiqra обновит анализ, чтобы показать фактический результат.",
                    $"Windows could not fully delete:\n\n{entry.Path}\n\nSome files may be in use, protected, or system-managed. Thermiqra will refresh the analysis to show the actual result."));
        }
    }


    private void ShowDownloadsCleanupResultDialog(
        DownloadsCleanupResult result,
        bool movedToRecycleBin)
    {
        string message;

        if (movedToRecycleBin)
        {
            int remainingFailures =
                Math.Max(
                    0,
                    result.FailedPaths.Count -
                    result.PermanentlyDeletedCount);

            if (result.PermanentDeleteFailures > 0)
            {
                remainingFailures =
                    result.PermanentDeleteFailures;
            }

            message =
                SettingsService.L(
                    $"В корзину перемещено: {result.ProcessedCount:N0}. Безвозвратно удалено после дополнительного подтверждения: {result.PermanentlyDeletedCount:N0}. Не удалось удалить: {remainingFailures:N0}. Чтобы реально освободить место, при необходимости очистите Корзину.",
                    $"Moved to Recycle Bin: {result.ProcessedCount:N0}. Permanently deleted after additional confirmation: {result.PermanentlyDeletedCount:N0}. Could not delete: {remainingFailures:N0}. To actually free disk space, empty the Recycle Bin if needed.");
        }
        else
        {
            message =
                SettingsService.L(
                    $"Безвозвратно удалено: {result.PermanentlyDeletedCount:N0}. Не удалось удалить: {result.PermanentDeleteFailures:N0}.",
                    $"Permanently deleted: {result.PermanentlyDeletedCount:N0}. Could not delete: {result.PermanentDeleteFailures:N0}.");
        }

        ShowInfoDialog(
            SettingsService.L(
                "Очистка «Загрузок» завершена",
                "Downloads cleanup complete"),
            message);
    }


    private Window CreateThemedDialogWindow(
        string title,
        double width,
        double height)
    {
        Window dialog =
            new()
            {
                Owner =
                    this,

                Title =
                    title,

                Width =
                    width,

                Height =
                    height,

                MinWidth =
                    Math.Min(
                        width,
                        480),

                MinHeight =
                    Math.Min(
                        height,
                        240),

                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner,

                ResizeMode =
                    ResizeMode.CanResize,

                ShowInTaskbar =
                    false
            };

        dialog.SetResourceReference(
            Window.BackgroundProperty,
            "WindowBackgroundBrush");

        dialog.SetResourceReference(
            Control.ForegroundProperty,
            "PrimaryTextBrush");

        if (Resources[typeof(Button)] is Style buttonStyle)
        {
            dialog.Resources[typeof(Button)] =
                buttonStyle;
        }

        if (Resources[typeof(ScrollBar)] is Style scrollBarStyle)
        {
            dialog.Resources[typeof(ScrollBar)] =
                scrollBarStyle;
        }

        return dialog;
    }


    private static TextBlock CreateDialogText(
        string text,
        double fontSize,
        FontWeight fontWeight,
        string foregroundResource)
    {
        TextBlock textBlock =
            new()
            {
                Text =
                    text,

                FontSize =
                    fontSize,

                FontWeight =
                    fontWeight
            };

        textBlock.SetResourceReference(
            TextBlock.ForegroundProperty,
            foregroundResource);

        return textBlock;
    }


    private Button CreateDialogButton(
        string content)
    {
        Button button =
            new()
            {
                Content =
                    content,

                Padding =
                    new Thickness(
                        14, 8, 14, 8),

                Cursor =
                    Cursors.Hand
            };

        button.SetResourceReference(
            Control.BackgroundProperty,
            "InputBackgroundBrush");

        button.SetResourceReference(
            Control.ForegroundProperty,
            "PrimaryTextBrush");

        button.SetResourceReference(
            Control.BorderBrushProperty,
            "BorderBrush");

        return button;
    }


}
