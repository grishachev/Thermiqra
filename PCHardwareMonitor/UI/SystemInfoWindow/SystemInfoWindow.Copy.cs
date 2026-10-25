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
    private static string BuildCopyText(
        SystemInformationSnapshot snapshot)
    {
        StringBuilder text =
            new();

        text.AppendLine(
            "THERMIQRA — ИНФОРМАЦИЯ О СИСТЕМЕ");

        text.AppendLine(
            "================================");

        text.AppendLine();

        text.AppendLine(
            "[WINDOWS]");

        AppendCopyLine(
            text,
            "Система",
            snapshot.Windows.Name);

        AppendCopyLine(
            text,
            "Редакция",
            snapshot.Windows.Edition);

        AppendCopyLine(
            text,
            "Версия",
            snapshot.Windows.DisplayVersion);

        AppendCopyLine(
            text,
            "Версия ОС",
            snapshot.Windows.Version);

        AppendCopyLine(
            text,
            "Сборка",
            snapshot.Windows.BuildNumber);

        AppendCopyLine(
            text,
            "Архитектура",
            snapshot.Windows.Architecture);

        AppendCopyLine(
            text,
            "Имя компьютера",
            snapshot.Windows.ComputerName);

        text.AppendLine(
            $"Дата установки: " +
            $"{FormatDate(snapshot.Windows.InstallDate)}");

        text.AppendLine();

        text.AppendLine(
            "[ПРОЦЕССОР]");

        AppendCopyLine(
            text,
            "Производитель",
            snapshot.Processor.Manufacturer);

        AppendCopyLine(
            text,
            "Модель",
            snapshot.Processor.Name);

        AppendCopyLine(
            text,
            "Архитектура",
            snapshot.Processor.Architecture);

        text.AppendLine(
            $"Физические ядра: " +
            $"{FormatNumber(snapshot.Processor.PhysicalCores)}");

        text.AppendLine(
            $"Логические потоки: " +
            $"{FormatNumber(snapshot.Processor.LogicalProcessors)}");

        text.AppendLine(
            $"Текущая частота: " +
            $"{FormatFrequency(snapshot.Processor.CurrentClockSpeedMhz)}");

        text.AppendLine(
            $"Максимальная частота: " +
            $"{FormatFrequency(snapshot.Processor.MaxClockSpeedMhz)}");

        AppendCopyLine(
            text,
            "Сокет",
            snapshot.Processor.Socket);

        text.AppendLine(
            $"Кэш L2: " +
            $"{FormatCache(snapshot.Processor.L2CacheKb)}");

        text.AppendLine(
            $"Кэш L3: " +
            $"{FormatCache(snapshot.Processor.L3CacheKb)}");

        text.AppendLine();

        text.AppendLine(
            "[МАТЕРИНСКАЯ ПЛАТА]");

        AppendCopyLine(
            text,
            "Производитель",
            snapshot.Motherboard.Manufacturer);

        AppendCopyLine(
            text,
            "Модель",
            snapshot.Motherboard.Model);

        AppendCopyLine(
            text,
            "Версия платы",
            snapshot.Motherboard.Version);

        AppendCopyLine(
            text,
            "Тип прошивки",
            snapshot.Motherboard.FirmwareType);

        AppendCopyLine(
            text,
            "Версия BIOS",
            snapshot.Motherboard.BiosVersion);

        text.AppendLine(
            $"Дата BIOS: " +
            $"{FormatDate(snapshot.Motherboard.BiosDate)}");

        text.AppendLine();

        text.AppendLine(
            "[ОПЕРАТИВНАЯ ПАМЯТЬ]");

        text.AppendLine(
            $"Установлено: " +
            $"{FormatBytes(snapshot.Memory.TotalBytes)}");

        text.AppendLine(
            $"Обнаружено модулей: " +
            $"{snapshot.Memory.Modules.Count}");

        for (int i = 0;
             i < snapshot.Memory.Modules.Count;
             i++)
        {
            MemoryModuleDetails module =
                snapshot.Memory.Modules[i];

            text.AppendLine();

            text.AppendLine(
                $"Модуль {i + 1}:");

            AppendCopyLine(
                text,
                "  Слот / банк",
                module.Slot);

            text.AppendLine(
                $"  Объём: " +
                $"{FormatBytes(module.CapacityBytes)}");

            AppendCopyLine(
                text,
                "  Производитель",
                module.Manufacturer);

            AppendCopyLine(
                text,
                "  Part Number",
                module.PartNumber);

            AppendCopyLine(
                text,
                "  Серийный номер",
                module.SerialNumber);

            text.AppendLine(
                $"  Частота: " +
                $"{FormatFrequency(module.FrequencyMhz)}");
        }

        text.AppendLine();

        text.AppendLine(
            "[ВИДЕОКАРТЫ]");

        if (snapshot.GraphicsAdapters.Count == 0)
        {
            text.AppendLine(
                "Нет данных");
        }
        else
        {
            for (int i = 0;
                 i < snapshot.GraphicsAdapters.Count;
                 i++)
            {
                GraphicsAdapterDetails adapter =
                    snapshot.GraphicsAdapters[i];

                if (i > 0)
                    text.AppendLine();

                text.AppendLine(
                    $"Видеоадаптер {i + 1}:");

                AppendCopyLine(
                    text,
                    "  Модель",
                    adapter.Name);

                AppendCopyLine(
                    text,
                    "  Производитель",
                    adapter.Manufacturer);

                AppendCopyLine(
                    text,
                    "  Графический процессор",
                    adapter.VideoProcessor);

                text.AppendLine(
                    $"  Видеопамять: " +
                    $"{(adapter.DedicatedMemoryBytes.HasValue
                        ? FormatBytes(adapter.DedicatedMemoryBytes)
                        : "Не определяется надёжно")}");

                AppendCopyLine(
                    text,
                    "  Версия драйвера",
                    adapter.DriverVersion);

                text.AppendLine(
                    $"  Дата драйвера: " +
                    $"{FormatDate(adapter.DriverDate)}");
            }
        }

        text.AppendLine();

        text.AppendLine(
            "[ФИЗИЧЕСКИЕ НАКОПИТЕЛИ]");

        if (snapshot.StorageDevices.Count == 0)
        {
            text.AppendLine(
                "Нет данных");
        }
        else
        {
            for (int i = 0;
                 i < snapshot.StorageDevices.Count;
                 i++)
            {
                StorageDeviceDetails device =
                    snapshot.StorageDevices[i];

                if (i > 0)
                    text.AppendLine();

                text.AppendLine(
                    $"Накопитель {i + 1}:");

                AppendCopyLine(
                    text,
                    "  Модель",
                    device.Model);

                AppendCopyLine(
                    text,
                    "  Производитель",
                    device.Manufacturer);

                AppendCopyLine(
                    text,
                    "  Серийный номер",
                    device.SerialNumber);

                AppendCopyLine(
                    text,
                    "  Прошивка",
                    device.Firmware);

                AppendCopyLine(
                    text,
                    "  Интерфейс",
                    device.InterfaceType);

                AppendCopyLine(
                    text,
                    "  Тип носителя",
                    device.MediaType);

                text.AppendLine(
                    $"  Ёмкость: " +
                    $"{FormatBytes(device.CapacityBytes)}");
            }
        }

        return SettingsService.TranslateText(
            text
                .ToString()
                .TrimEnd());
    }


    private static void AppendCopyLine(
        StringBuilder text,
        string label,
        string? value)
    {
        text.AppendLine(
            $"{label}: " +
            $"{ValueOrUnavailable(value)}");
    }



}
