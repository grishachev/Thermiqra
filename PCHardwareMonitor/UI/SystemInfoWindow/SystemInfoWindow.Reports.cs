using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Controls;

namespace PCHardwareMonitor;

public partial class SystemInfoWindow
{
    // ============================================================
    // ДИАГНОСТИЧЕСКИЙ ОТЧЁТ
    // ============================================================

    private void SaveDiagnosticReportButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_snapshot == null)
        {
            StatusText.Text =
                SettingsService.L(
                    "Информация о системе ещё не загружена.",
                    "System information has not been loaded yet.");

            return;
        }

        try
        {
            SaveFileDialog dialog =
                new()
                {
                    Title =
                        SettingsService.L(
                            "Сохранить диагностический отчёт Thermiqra",
                            "Save Thermiqra diagnostic report"),

                    FileName =
                        $"Thermiqra_Diagnostic_" +
                        $"{DateTime.Now:yyyy-MM-dd_HH-mm}.txt",

                    DefaultExt =
                        ".txt",

                    AddExtension =
                        true,

                    Filter =
                        SettingsService.L(
                            "Текстовый файл (*.txt)|*.txt|Все файлы (*.*)|*.*",
                            "Text file (*.txt)|*.txt|All files (*.*)|*.*")
                };

            bool? result =
                dialog.ShowDialog(
                    this);

            if (result != true)
                return;

            string report =
                BuildDiagnosticReport(
                    _snapshot);

            File.WriteAllText(
                dialog.FileName,
                report,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier:
                    true));

            StatusText.Text =
                SettingsService.L(
                    $"Диагностический отчёт сохранён: {dialog.FileName}",
                    $"Diagnostic report saved: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            StatusText.Text =
                SettingsService.L(
                    $"Не удалось сохранить отчёт: {ex.Message}",
                    $"Failed to save the report: {ex.Message}");

            MessageBox.Show(
                this,
                SettingsService.L(
                    $"Не удалось сохранить диагностический отчёт.\n\n{ex.Message}",
                    $"Failed to save the diagnostic report.\n\n{ex.Message}"),
                "Thermiqra",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }


    private string BuildDiagnosticReport(
        SystemInformationSnapshot systemSnapshot)
    {
        StringBuilder text =
            new();

        text.AppendLine(
            "THERMIQRA — " +
            SettingsService.L(
                "ДИАГНОСТИЧЕСКИЙ ОТЧЁТ",
                "DIAGNOSTIC REPORT"));

        text.AppendLine(
            new string(
                '=',
                62));

        text.AppendLine();

        text.AppendLine(
            $"{SettingsService.L("Создан", "Generated")}: " +
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        text.AppendLine(
            $"{SettingsService.L("Версия Thermiqra", "Thermiqra version")}: " +
            $"{GetThermiqraVersion()}");

        text.AppendLine(
            SettingsService.L(
                "Отчёт создан локально. Thermiqra не отправляет эти данные автоматически.",
                "This report was created locally. Thermiqra does not send this data automatically."));

        text.AppendLine();

        text.AppendLine(
            BuildCopyText(
                systemSnapshot));

        text.AppendLine();
        text.AppendLine();

        AppendCurrentMonitoringSection(
            text);

        text.AppendLine();

        AppendSpdXmpSection(
            text);

        text.AppendLine();

        AppendHistorySection(
            text);

        return text
            .ToString()
            .TrimEnd();
    }


    private static void AppendCurrentMonitoringSection(
        StringBuilder text)
    {
        text.AppendLine(
            $"[{SettingsService.L("ТЕКУЩИЙ МОНИТОРИНГ", "CURRENT MONITORING")}]");

        HardwareSnapshot? snapshot =
            HardwareMonitorService.LastSnapshot;

        if (snapshot == null)
        {
            text.AppendLine(
                SettingsService.L(
                    "Текущий снимок датчиков пока недоступен.",
                    "The current sensor snapshot is not available yet."));

            return;
        }

        text.AppendLine();

        text.AppendLine(
            $"CPU: {snapshot.Cpu.Name}");

        text.AppendLine(
            $"  {SettingsService.L("Температура", "Temperature")}: " +
            $"{FormatLiveTemperature(snapshot.Cpu.Temperature)}");

        text.AppendLine(
            $"  {SettingsService.L("Загрузка", "Load")}: " +
            $"{FormatLivePercent(snapshot.Cpu.Load)}");

        text.AppendLine();

        text.AppendLine(
            $"GPU:");

        if (snapshot.Gpus.Count == 0)
        {
            text.AppendLine(
                $"  {SettingsService.L("Нет данных", "No data")}");
        }
        else
        {
            for (int i = 0;
                 i < snapshot.Gpus.Count;
                 i++)
            {
                GpuInfo gpu =
                    snapshot.Gpus[i];

                text.AppendLine(
                    $"  GPU {i + 1}: {gpu.Name}");

                text.AppendLine(
                    $"    {SettingsService.L("Температура", "Temperature")}: " +
                    $"{FormatLiveTemperature(gpu.Temperature)}");

                text.AppendLine(
                    $"    Hot Spot: " +
                    $"{FormatLiveTemperature(gpu.HotSpotTemperature)}");

                text.AppendLine(
                    $"    {SettingsService.L("Температура VRAM", "VRAM temperature")}: " +
                    $"{FormatLiveTemperature(gpu.MemoryTemperature)}");

                text.AppendLine(
                    $"    {SettingsService.L("Загрузка", "Load")}: " +
                    $"{FormatLivePercent(gpu.Load)}");

                text.AppendLine(
                    $"    {SettingsService.L("Загрузка памяти GPU", "GPU memory load")}: " +
                    $"{FormatLivePercent(gpu.MemoryLoad)}");
            }
        }

        text.AppendLine();

        text.AppendLine(
            $"RAM:");

        text.AppendLine(
            $"  {SettingsService.L("Загрузка", "Load")}: " +
            $"{FormatLivePercent(snapshot.Memory.Load)}");

        text.AppendLine(
            $"  {SettingsService.L("Используется", "Used")}: " +
            $"{FormatLiveGigabytes(snapshot.Memory.UsedGb)}");

        text.AppendLine(
            $"  {SettingsService.L("Доступно", "Available")}: " +
            $"{FormatLiveGigabytes(snapshot.Memory.AvailableGb)}");

        text.AppendLine(
            $"  {SettingsService.L("Всего", "Total")}: " +
            $"{FormatLiveGigabytes(snapshot.Memory.TotalGb)}");

        text.AppendLine();

        text.AppendLine(
            $"[{SettingsService.L("ТЕМПЕРАТУРЫ НАКОПИТЕЛЕЙ", "STORAGE TEMPERATURES")}]");

        if (snapshot.StorageDevices.Count == 0)
        {
            text.AppendLine(
                SettingsService.L(
                    "Нет данных",
                    "No data"));
        }
        else
        {
            foreach (StorageDeviceInfo storage
                     in snapshot.StorageDevices)
            {
                text.AppendLine(
                    $"{storage.Name}: " +
                    $"{FormatLiveTemperature(storage.Temperature)}");
            }
        }

        text.AppendLine();

        text.AppendLine(
            $"[{SettingsService.L("ЛОГИЧЕСКИЕ ДИСКИ", "LOGICAL DRIVES")}]");

        if (snapshot.Drives.Count == 0)
        {
            text.AppendLine(
                SettingsService.L(
                    "Нет данных",
                    "No data"));
        }
        else
        {
            foreach (LogicalDriveInfo drive
                     in snapshot.Drives)
            {
                long usedBytes =
                    Math.Max(
                        0,
                        drive.TotalBytes -
                        drive.FreeBytes);

                double usedPercent =
                    drive.TotalBytes > 0
                        ? usedBytes * 100.0 /
                          drive.TotalBytes
                        : 0;

                string label =
                    string.IsNullOrWhiteSpace(
                        drive.VolumeLabel)
                        ? string.Empty
                        : $" ({drive.VolumeLabel})";

                text.AppendLine(
                    $"{drive.Name}{label}");

                text.AppendLine(
                    $"  {SettingsService.L("Всего", "Total")}: " +
                    $"{FormatLongBytes(drive.TotalBytes)}");

                text.AppendLine(
                    $"  {SettingsService.L("Занято", "Used")}: " +
                    $"{FormatLongBytes(usedBytes)} " +
                    $"({usedPercent:F1} %)");

                text.AppendLine(
                    $"  {SettingsService.L("Свободно", "Free")}: " +
                    $"{FormatLongBytes(drive.FreeBytes)}");
            }
        }
    }


    private static void AppendSpdXmpSection(
        StringBuilder text)
    {
        text.AppendLine(
            $"[{SettingsService.L("SPD / JEDEC / XMP", "SPD / JEDEC / XMP")}]");

        HardwareSnapshot? snapshot =
            HardwareMonitorService.LastSnapshot;

        if (snapshot == null ||
            snapshot.MemorySpdModules.Count == 0)
        {
            text.AppendLine(
                SettingsService.L(
                    "Расширенные SPD/XMP-данные пока недоступны.",
                    "Extended SPD/XMP data is not available yet."));

            return;
        }

        for (int i = 0;
             i < snapshot.MemorySpdModules.Count;
             i++)
        {
            MemorySpdModuleInfo module =
                snapshot.MemorySpdModules[i];

            if (i > 0)
                text.AppendLine();

            string moduleName =
                string.IsNullOrWhiteSpace(
                    module.Name)
                    ? SettingsService.L(
                        $"Модуль {i + 1}",
                        $"Module {i + 1}")
                    : module.Name;

            text.AppendLine(
                $"{SettingsService.L("Модуль", "Module")} {i + 1}: " +
                $"{moduleName}");

            text.AppendLine(
                $"  {SettingsService.L("Ёмкость", "Capacity")}: " +
                $"{FormatSpdCapacity(module.CapacityGb)}");

            if (module.Jedec != null)
            {
                text.AppendLine(
                    $"  JEDEC: " +
                    $"{BuildJedecSummary(module.Jedec)}");

                if (module.Jedec.SupportedCasLatencies.Count > 0)
                {
                    text.AppendLine(
                        $"  JEDEC CL: " +
                        $"{string.Join(", ", module.Jedec.SupportedCasLatencies)}");
                }
            }
            else
            {
                text.AppendLine(
                    $"  JEDEC: " +
                    $"{SettingsService.L("Нет данных", "No data")}");
            }

            if (module.XmpProfiles.Count == 0)
            {
                text.AppendLine(
                    $"  XMP: " +
                    $"{SettingsService.L("Профили не обнаружены", "No profiles detected")}");
            }
            else
            {
                foreach (MemoryXmpProfileInfo profile
                         in module.XmpProfiles)
                {
                    text.AppendLine(
                        $"  {module.XmpVersion ?? "XMP"} " +
                        $"{SettingsService.L("профиль", "profile")} " +
                        $"{profile.ProfileNumber}: " +
                        $"{BuildXmpProfileSummary(profile)}");
                }
            }

            if (module.Timings.Count > 0)
            {
                text.AppendLine(
                    $"  {SettingsService.L("SPD-тайминги", "SPD timings")}:");

                foreach (MemorySpdTimingInfo timing
                         in module.Timings)
                {
                    text.AppendLine(
                        $"    {timing.Name}: " +
                        $"{timing.ValueNanoseconds:0.###} ns");
                }
            }
        }
    }



}
