using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PCHardwareMonitor;

public partial class StatisticsWindow
{
    private async void ExportCsvButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SaveFileDialog dialog =
            new()
            {
                Title =
                    SettingsService.L(
                        "Экспорт статистики Thermiqra в CSV",
                        "Export Thermiqra statistics to CSV"),

                FileName =
                    $"Thermiqra_Statistics_" +
                    $"{GetPeriodFileName(_currentPeriod)}_" +
                    $"{DateTime.Now:yyyy-MM-dd_HH-mm}.csv",

                DefaultExt =
                    ".csv",

                AddExtension =
                    true,

                Filter =
                    SettingsService.L(
                        "CSV-файл (*.csv)|*.csv|Все файлы (*.*)|*.*",
                        "CSV file (*.csv)|*.csv|All files (*.*)|*.*")
            };

        bool? result =
            dialog.ShowDialog(
                this);

        if (result != true)
            return;

        bool isRussian =
            SettingsService.IsRussian;

        StatisticsPeriod period =
            _currentPeriod;

        ExportCsvButton.IsEnabled =
            false;

        StatusText.Text =
            SettingsService.L(
                "Экспорт статистики...",
                "Exporting statistics...");

        try
        {
            int rowCount =
                await Task.Run(
                    () =>
                        WriteStatisticsCsv(
                            dialog.FileName,
                            period,
                            isRussian));

            StatusText.Text =
                rowCount > 0
                    ? SettingsService.L(
                        $"CSV сохранён. Экспортировано строк: {rowCount:N0}.",
                        $"CSV saved. Exported rows: {rowCount:N0}.")
                    : SettingsService.L(
                        "CSV сохранён, но за выбранный период точек нет.",
                        "CSV saved, but there are no samples for the selected period.");
        }
        catch (Exception ex)
        {
            StatusText.Text =
                SettingsService.L(
                    $"Не удалось экспортировать CSV: {ex.Message}",
                    $"Failed to export CSV: {ex.Message}");

            MessageBox.Show(
                this,
                SettingsService.L(
                    $"Не удалось экспортировать статистику.\n\n{ex.Message}",
                    $"Failed to export statistics.\n\n{ex.Message}"),
                "Thermiqra",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            ExportCsvButton.IsEnabled =
                true;
        }
    }


    private int WriteStatisticsCsv(
        string fileName,
        StatisticsPeriod period,
        bool isRussian)
    {
        CultureInfo valueCulture =
            isRussian
                ? CultureInfo.GetCultureInfo(
                    "ru-RU")
                : CultureInfo.InvariantCulture;

        using StreamWriter writer =
            new(
                fileName,
                append: false,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier:
                    true));

        string[] headers =
            isRussian
                ? new[]
                {
                    "Время",
                    "Категория",
                    "Устройство",
                    "ID устройства",
                    "Показатель",
                    "Значение",
                    "Единица"
                }
                : new[]
                {
                    "Time",
                    "Category",
                    "Device",
                    "Device ID",
                    "Metric",
                    "Value",
                    "Unit"
                };

        writer.WriteLine(
            string.Join(
                ";",
                headers.Select(
                    EscapeCsv)));

        return _statistics.ReadExportRows(
            period,
            row =>
            {
                string[] values =
                {
                    FormatExportDate(
                        row.TimestampUtc,
                        isRussian),

                    GetExportCategory(
                        row.Category,
                        isRussian),

                    row.DeviceName,

                    row.DeviceId,

                    GetExportMetric(
                        row.Metric,
                        isRussian),

                    row.Value.ToString(
                        "0.###",
                        valueCulture),

                    row.Unit
                };

                writer.WriteLine(
                    string.Join(
                        ";",
                        values.Select(
                            EscapeCsv)));
            });
    }


    private static string EscapeCsv(
        string value)
    {
        value ??=
            string.Empty;

        bool needsQuotes =
            value.Contains(';') ||
            value.Contains('"') ||
            value.Contains('\r') ||
            value.Contains('\n');

        if (value.Contains('"'))
        {
            value =
                value.Replace(
                    "\"",
                    "\"\"");
        }

        return needsQuotes
            ? $"\"{value}\""
            : value;
    }


    private static string GetExportCategory(
        string category,
        bool isRussian)
    {
        return category switch
        {
            "CPU" =>
                isRussian
                    ? "Процессор"
                    : "CPU",

            "GPU" =>
                isRussian
                    ? "Видеокарта"
                    : "GPU",

            "RAM" =>
                isRussian
                    ? "Оперативная память"
                    : "RAM",

            "STORAGE" =>
                isRussian
                    ? "Накопитель"
                    : "Storage",

            _ =>
                category
        };
    }


    private static string GetExportMetric(
        string metric,
        bool isRussian)
    {
        return metric switch
        {
            "Temperature" =>
                isRussian
                    ? "Температура"
                    : "Temperature",

            "Load" =>
                isRussian
                    ? "Загрузка"
                    : "Load",

            "HotSpotTemperature" =>
                "Hot Spot",

            "MemoryTemperature" =>
                isRussian
                    ? "Температура памяти GPU"
                    : "GPU memory temperature",

            "MemoryLoad" =>
                isRussian
                    ? "Загрузка памяти GPU"
                    : "GPU memory load",

            _ =>
                metric
        };
    }


    private static string FormatExportDate(
        DateTimeOffset valueUtc,
        bool isRussian)
    {
        DateTimeOffset local =
            valueUtc.ToLocalTime();

        return local.ToString(
            isRussian
                ? "dd.MM.yyyy HH:mm:ss"
                : "yyyy-MM-dd HH:mm:ss");
    }


    private static string GetPeriodFileName(
        StatisticsPeriod period)
    {
        return period switch
        {
            StatisticsPeriod.Today =>
                "Today",

            StatisticsPeriod.Last24Hours =>
                "24h",

            StatisticsPeriod.Last7Days =>
                "7d",

            StatisticsPeriod.Last30Days =>
                "30d",

            _ =>
                "Period"
        };
    }


}
