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

            CheckBox checkBox =
                new()
                {
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
                    item.Description,
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
                if (!checkBoxes.Any(
                        pair =>
                            pair.CheckBox.IsChecked ==
                            true))
                {
                    return;
                }

                dialog.DialogResult =
                    true;
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


    private void ShowCleanupResultDialog(
        CleanupExecutionResult result)
    {
        string message =
            SettingsService.L(
                $"Очистка завершена. Освобождено примерно {FormatBytes(result.FreedBytes)}. Удалено объектов: {result.DeletedEntries:N0}. Пропущено занятых или защищённых объектов: {result.SkippedEntries:N0}.",
                $"Cleanup complete. Approximately {FormatBytes(result.FreedBytes)} was freed. Deleted items: {result.DeletedEntries:N0}. In-use or protected items skipped: {result.SkippedEntries:N0}.");

        ShowInfoDialog(
            SettingsService.L(
                "Безопасная очистка завершена",
                "Safe cleanup complete"),
            message);
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
