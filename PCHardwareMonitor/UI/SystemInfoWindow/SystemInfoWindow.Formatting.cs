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
    // ФОРМАТИРОВАНИЕ
    // ============================================================

    private static string ValueOrUnavailable(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? SettingsService.L(
                "Нет данных",
                "No data")
            : value.Trim();
    }


    private static string FormatNumber(
        uint? value)
    {
        return value.HasValue
            ? value.Value.ToString()
            : SettingsService.L(
                "Нет данных",
                "No data");
    }


    private static string FormatFrequency(
        uint? megahertz)
    {
        if (!megahertz.HasValue)
        {
            return SettingsService.L(
                "Нет данных",
                "No data");
        }

        if (megahertz.Value >= 1000)
        {
            return SettingsService.L(
                $"{megahertz.Value / 1000.0:F2} ГГц",
                $"{megahertz.Value / 1000.0:F2} GHz");
        }

        return SettingsService.L(
            $"{megahertz.Value} МГц",
            $"{megahertz.Value} MHz");
    }


    private static string FormatMemorySpeed(
        uint? speed)
    {
        return speed.HasValue
            ? $"{speed.Value} MT/s"
            : SettingsService.L(
                "Нет данных",
                "No data");
    }


    private static string FormatMemoryType(
        ushort? memoryType)
    {
        if (!memoryType.HasValue)
        {
            return SettingsService.L(
                "Нет данных",
                "No data");
        }

        return memoryType.Value switch
        {
            20 => "DDR",
            21 => "DDR2",
            22 => "DDR2 FB-DIMM",
            24 => "DDR3",
            26 => "DDR4",
            27 => "LPDDR",
            28 => "LPDDR2",
            29 => "LPDDR3",
            30 => "LPDDR4",
            34 => "DDR5",
            35 => "LPDDR5",
            _ => SettingsService.L(
                $"Код SMBIOS {memoryType.Value}",
                $"SMBIOS code {memoryType.Value}")
        };
    }


    private static string FormatMemoryFormFactor(
        ushort? formFactor)
    {
        if (!formFactor.HasValue)
        {
            return SettingsService.L(
                "Нет данных",
                "No data");
        }

        return formFactor.Value switch
        {
            8 => "DIMM",
            12 => "SO-DIMM",
            22 => "FB-DIMM",
            _ => SettingsService.L(
                $"Код {formFactor.Value}",
                $"Code {formFactor.Value}")
        };
    }


    private static string FormatMemoryVoltage(
        uint? millivolts)
    {
        if (!millivolts.HasValue)
        {
            return SettingsService.L(
                "Нет данных",
                "No data");
        }

        return SettingsService.L(
            $"{millivolts.Value / 1000.0:F2} В",
            $"{millivolts.Value / 1000.0:F2} V");
    }


    private static string FormatMemoryWidth(
        ushort? bits)
    {
        return bits.HasValue
            ? SettingsService.L(
                $"{bits.Value} бит",
                $"{bits.Value} bits")
            : SettingsService.L(
                "Нет данных",
                "No data");
    }


    private static string FormatCache(
        uint? kilobytes)
    {
        if (!kilobytes.HasValue)
        {
            return SettingsService.L(
                "Нет данных",
                "No data");
        }

        if (kilobytes.Value >= 1024)
        {
            return SettingsService.L(
                $"{kilobytes.Value / 1024.0:F1} МБ",
                $"{kilobytes.Value / 1024.0:F1} MB");
        }

        return SettingsService.L(
            $"{kilobytes.Value} КБ",
            $"{kilobytes.Value} KB");
    }


    private static string FormatDate(
        DateTime? date)
    {
        if (!date.HasValue)
        {
            return SettingsService.L(
                "Нет данных",
                "No data");
        }

        return date.Value.ToString(
            SettingsService.IsRussian
                ? "dd.MM.yyyy"
                : "yyyy-MM-dd");
    }


    private static string FormatBytes(
        ulong? bytes)
    {
        if (!bytes.HasValue ||
            bytes.Value == 0)
        {
            return SettingsService.L(
                "Нет данных",
                "No data");
        }

        double gigabytes =
            bytes.Value /
            1024d /
            1024d /
            1024d;

        if (gigabytes >= 1024)
        {
            return SettingsService.L(
                $"{gigabytes / 1024d:F2} ТБ",
                $"{gigabytes / 1024d:F2} TB");
        }

        return SettingsService.L(
            $"{gigabytes:F1} ГБ",
            $"{gigabytes:F1} GB");
    }



}
