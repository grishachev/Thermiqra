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


    private async Task CheckHardwareChangesAsync()
    {
        if (_isHardwareChangeCheckRunning ||
            _isExiting)
        {
            return;
        }

        _isHardwareChangeCheckRunning =
            true;

        try
        {
            HardwareChangeResult result =
                await Task.Run(
                    () =>
                        new HardwareChangeService()
                            .CheckAndUpdate());

            if (!result.HasChanges ||
                _isExiting)
            {
                return;
            }

            const int maxVisibleChanges = 4;

            List<string> visibleChanges =
                result.Changes
                    .Take(
                        maxVisibleChanges)
                    .ToList();

            string message =
                string.Join(
                    Environment.NewLine,
                    visibleChanges
                        .Select(
                            change =>
                                $"• {change}"));

            int hiddenCount =
                result.Changes.Count -
                visibleChanges.Count;

            if (hiddenCount > 0)
            {
                message +=
                    Environment.NewLine +
                    SettingsService.L(
                        $"• И ещё изменений: {hiddenCount}",
                        $"• And {hiddenCount} more change(s)");
            }

            _notifications.Show(
                NotificationType.Info,
                SettingsService.L(
                    "Thermiqra — изменилось оборудование",
                    "Thermiqra — hardware changed"),
                message,
                ShowMainWindow);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Не удалось проверить изменения оборудования: {ex}");
        }
        finally
        {
            _isHardwareChangeCheckRunning =
                false;
        }
    }

    // ============================================================
    // МОНИТОРИНГ
    // ============================================================

    private void UpdateMonitor()
    {
        try
        {
            HardwareSnapshot snapshot =
                _monitor.GetSnapshot();

            _lastSnapshot =
                snapshot;

            _tray.UpdateTooltip(
                snapshot.Cpu.Temperature,
                GetTrayGpuTemperature(
                    snapshot));

            _cpuDetailsWindow?
                .UpdateSnapshot(
                    snapshot);

            try
            {
                _statistics?.SaveSnapshot(
                    snapshot);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Не удалось сохранить статистику: {ex}");
            }

            UpdateCpu(snapshot);

            UpdateGpus(snapshot);

            UpdateMemory(snapshot);

            UpdateDrives(snapshot);

            UpdateStorageTemperatures(
                snapshot);

            _alerts.Evaluate(
                snapshot);

            string statusText =
                SettingsService.L(
                    "Обновлено: ",
                    "Updated: ") +
                $"{DateTime.Now:HH:mm:ss}";

            StatusText.Text =
                statusText;

            SteamStatusText.Text =
                statusText;

            FrostStatusText.Text =
                statusText;

            MilitaryStatusText.Text =
                statusText;

            _livePulseBright =
                !_livePulseBright;

            double pulseOpacity =
                _livePulseBright
                    ? 1.0
                    : 0.35;

            LivePulseDot.Opacity =
                pulseOpacity;

            SteamLivePulseDot.Opacity =
                pulseOpacity;

            FrostLivePulseDot.Opacity =
                pulseOpacity;

            MilitaryLivePulseDot.Opacity =
                pulseOpacity;
        }
        catch (Exception ex)
        {
            string errorText =
                SettingsService.L(
                    $"Ошибка: {ex.Message}",
                    $"Error: {ex.Message}");

            StatusText.Text =
                errorText;

            SteamStatusText.Text =
                errorText;

            FrostStatusText.Text =
                errorText;

            MilitaryStatusText.Text =
                errorText;
        }
    }

    // ============================================================
    // CPU
    // ============================================================

    private void UpdateCpu(
        HardwareSnapshot snapshot)
    {
        _cpuGauge.Update(
            snapshot.Cpu.Temperature,
            snapshot.Cpu.Load,
            snapshot.Cpu.Name,
            "");
    }

    // ============================================================
    // GPU
    // ============================================================

    private void UpdateGpus(
        HardwareSnapshot snapshot)
    {
        EnsureGpuGauges(
            snapshot.Gpus.Count);

        for (int i = 0;
             i < snapshot.Gpus.Count;
             i++)
        {
            GpuInfo gpu =
                snapshot.Gpus[i];

            _gpuGauges[i]
                .Update(
                    gpu.Temperature,
                    gpu.Load,
                    gpu.Name,
                    BuildGpuDetails(gpu));
        }
    }

    private void EnsureGpuGauges(
        int requiredCount)
    {
        while (_gpuGauges.Count <
               requiredCount)
        {
            RingGauge gauge =
                new(
                    "GPU",
                    TemperatureType.Gpu);

            _gpuGauges.Add(
                gauge);

            MainGaugesPanel
                .Children
                .Add(
                    gauge);

            SettingsService.ApplyLanguageToElement(
                gauge);
        }

        while (_gpuGauges.Count >
               requiredCount)
        {
            RingGauge gauge =
                _gpuGauges[
                    _gpuGauges.Count - 1];

            MainGaugesPanel
                .Children
                .Remove(
                    gauge);

            _gpuGauges.RemoveAt(
                _gpuGauges.Count - 1);
        }
    }

    private static float? GetTrayGpuTemperature(
        HardwareSnapshot snapshot)
    {
        float? highestTemperature =
            null;

        foreach (GpuInfo gpu
                 in snapshot.Gpus)
        {
            if (!gpu.Temperature.HasValue)
                continue;

            if (!highestTemperature.HasValue ||
                gpu.Temperature.Value >
                highestTemperature.Value)
            {
                highestTemperature =
                    gpu.Temperature.Value;
            }
        }

        return highestTemperature;
    }


    private static string BuildGpuDetails(
        GpuInfo gpu)
    {
        List<string> details =
            new();

        if (gpu.HotSpotTemperature.HasValue)
        {
            details.Add(
                $"Hot Spot " +
                $"{gpu.HotSpotTemperature.Value:F0}°");
        }

        if (gpu.MemoryTemperature.HasValue)
        {
            details.Add(
                $"VRAM " +
                $"{gpu.MemoryTemperature.Value:F0}°");
        }

        return string.Join(
            "   •   ",
            details);
    }

    // ============================================================
    // RAM
    // ============================================================

    private void UpdateMemory(
        HardwareSnapshot snapshot)
    {
        float? used =
            snapshot.Memory.UsedGb;

        float? total =
            snapshot.Memory.TotalGb;

        string memoryText;

        if (used.HasValue &&
            total.HasValue)
        {
            memoryText =
                SettingsService.L(
                    $"{used.Value:F1} ГБ ",
                    $"{used.Value:F1} GB ") +
                SettingsService.L(
                    $"из {total.Value:F1} ГБ",
                    $"of {total.Value:F1} GB");
        }
        else
        {
            memoryText =
                SettingsService.L(
                    "Нет данных",
                    "No data");
        }

        RamText.Text =
            memoryText;

        SteamRamText.Text =
            memoryText;

        FrostRamText.Text =
            memoryText;

        MilitaryRamText.Text =
            memoryText;

        double ramLoad =
            ClampPercent(
                snapshot.Memory.Load);

        RamBar.Value =
            ramLoad;

        SteamRamBar.Value =
            ramLoad;

        FrostRamBar.Value =
            ramLoad;

        Brush ramBrush;

        if (ramLoad >= 90)
        {
            ramBrush =
                System.Windows.Media.Brushes.Red;
        }
        else if (ramLoad >= 85)
        {
            ramBrush =
                System.Windows.Media.Brushes.Orange;
        }
        else if (ramLoad >= 70)
        {
            ramBrush =
                System.Windows.Media.Brushes.Gold;
        }
        else
        {
            ramBrush =
                UiBrushes.Theme(
                    "AccentBrush");
        }

        RamBar.Foreground =
            ramBrush;

        SteamRamBar.Foreground =
            ramBrush;

        FrostRamBar.Foreground =
            ramBrush;

        FrostRamFill.Background =
            ramBrush;

        FrostRamFill.Height =
            78.0 * ramLoad / 100.0;

        Border[] militaryRamSegments =
        {
            MilitaryRamSegment0,
            MilitaryRamSegment1,
            MilitaryRamSegment2,
            MilitaryRamSegment3,
            MilitaryRamSegment4,
            MilitaryRamSegment5,
            MilitaryRamSegment6,
            MilitaryRamSegment7,
            MilitaryRamSegment8,
            MilitaryRamSegment9
        };

        int activeMilitarySegments =
            ramLoad <= 0
                ? 0
                : Math.Clamp(
                    (int)Math.Ceiling(
                        ramLoad / 10.0),
                    0,
                    10);

        for (int i = 0;
             i < militaryRamSegments.Length;
             i++)
        {
            if (i < activeMilitarySegments)
            {
                militaryRamSegments[i].Background =
                    ramBrush;
            }
            else
            {
                militaryRamSegments[i]
                    .SetResourceReference(
                        Border.BackgroundProperty,
                        "MilitaryGridBrush");
            }
        }

        RamPercentText.Text =
            SettingsService.L(
                "Использовано: ",
                "Used: ") +
            $"{FormatPercent(snapshot.Memory.Load)}";

        SteamRamPercentText.Text =
            $"{ramLoad:F0} %";

        FrostRamPercentText.Text =
            $"{ramLoad:F0} %";

        MilitaryRamPercentText.Text =
            $"{ramLoad:F0} %";
    }
}
