using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace PCHardwareMonitor;

public partial class SystemInfoWindow : Window
{
    private readonly SystemInfoService _systemInfoService =
        new();

    private SystemInformationSnapshot? _snapshot;


    public SystemInfoWindow()
    {
        InitializeComponent();

        Loaded +=
            SystemInfoWindow_Loaded;
    }


    private void SystemInfoWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        Loaded -=
            SystemInfoWindow_Loaded;

        LoadSystemInformation();
    }


    private void LoadSystemInformation()
    {
        try
        {
            StatusText.Text =
                "Чтение информации о системе...";

            SystemInformationSnapshot snapshot =
                _systemInfoService.GetSnapshot();

            _snapshot =
                snapshot;

            PopulateOverview(snapshot);

            PopulateProcessor(
                snapshot.Processor);

            PopulateMotherboard(
                snapshot.Motherboard);

            PopulateMemory(
                snapshot.Memory);

            PopulateGraphicsAdapters(
                snapshot.GraphicsAdapters);

            PopulateStorageDevices(
                snapshot.StorageDevices);

            PopulateWindows(
                snapshot.Windows);

            string computerName =
                ValueOrUnavailable(
                    snapshot.Windows.ComputerName);

            CyberHeaderComputerNameText.Text =
                computerName;

            SteamHeaderComputerNameText.Text =
                computerName;

            FrostHeaderComputerNameText.Text =
                computerName;

            MilitaryHeaderComputerNameText.Text =
                computerName;

            StatusText.Text =
                $"Данные загружены: " +
                $"{DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusText.Text =
                $"Не удалось получить данные: " +
                $"{ex.Message}";
        }
    }


    // ============================================================
    // ОБЗОР
    // ============================================================

    private static string BuildBoardName(
        MotherboardDetails board)
    {
        List<string> parts =
            new();

        if (!string.IsNullOrWhiteSpace(
                board.Manufacturer))
        {
            parts.Add(
                board.Manufacturer);
        }

        if (!string.IsNullOrWhiteSpace(
                board.Model))
        {
            parts.Add(
                board.Model);
        }

        return parts.Count > 0
            ? string.Join(
                " ",
                parts)
            : "Нет данных";
    }


    private static string BuildWindowsSummary(
        OperatingSystemDetails windows)
    {
        List<string> parts =
            new();

        if (!string.IsNullOrWhiteSpace(
                windows.Name))
        {
            parts.Add(
                windows.Name);
        }

        if (!string.IsNullOrWhiteSpace(
                windows.Edition))
        {
            parts.Add(
                windows.Edition);
        }

        if (!string.IsNullOrWhiteSpace(
                windows.DisplayVersion))
        {
            parts.Add(
                windows.DisplayVersion);
        }

        return parts.Count > 0
            ? string.Join(
                " ",
                parts)
            : "Нет данных";
    }


    private static string BuildGraphicsSummary(
        IReadOnlyList<GraphicsAdapterDetails> adapters)
    {
        if (adapters.Count == 0)
            return "Нет данных";

        List<string> names =
            new();

        foreach (GraphicsAdapterDetails adapter in adapters)
        {
            if (!string.IsNullOrWhiteSpace(
                    adapter.Name))
            {
                names.Add(
                    adapter.Name);
            }
        }

        return names.Count > 0
            ? string.Join(
                Environment.NewLine,
                names)
            : "Нет данных";
    }


    private static string BuildStorageSummary(
        IReadOnlyList<StorageDeviceDetails> devices)
    {
        if (devices.Count == 0)
            return "Нет данных";

        List<string> lines =
            new();

        foreach (StorageDeviceDetails device in devices)
        {
            string name =
                ValueOrUnavailable(
                    device.Model);

            string size =
                FormatBytes(
                    device.CapacityBytes);

            lines.Add(
                $"{name} — {size}");
        }

        return string.Join(
            Environment.NewLine,
            lines);
    }


    private void PopulateOverview(
        SystemInformationSnapshot snapshot)
    {
        OverviewCpuText.Text =
            ValueOrUnavailable(
                snapshot.Processor.Name);

        OverviewBoardText.Text =
            BuildBoardName(
                snapshot.Motherboard);

        OverviewMemoryText.Text =
            FormatBytes(
                snapshot.Memory.TotalBytes);

        OverviewGpuText.Text =
            BuildGraphicsSummary(
                snapshot.GraphicsAdapters);

        OverviewWindowsText.Text =
            BuildWindowsSummary(
                snapshot.Windows);

        OverviewStorageText.Text =
            BuildStorageSummary(
                snapshot.StorageDevices);
    }


    // ============================================================
    // ПРОЦЕССОР
    // ============================================================

    private void PopulateProcessor(
        ProcessorDetails processor)
    {
        CpuManufacturerText.Text =
            ValueOrUnavailable(
                processor.Manufacturer);

        CpuNameText.Text =
            ValueOrUnavailable(
                processor.Name);

        CpuArchitectureText.Text =
            ValueOrUnavailable(
                processor.Architecture);

        CpuCoresText.Text =
            FormatNumber(
                processor.PhysicalCores);

        CpuThreadsText.Text =
            FormatNumber(
                processor.LogicalProcessors);

        CpuCurrentClockText.Text =
            FormatFrequency(
                processor.CurrentClockSpeedMhz);

        CpuMaxClockText.Text =
            FormatFrequency(
                processor.MaxClockSpeedMhz);

        CpuSocketText.Text =
            ValueOrUnavailable(
                processor.Socket);

        CpuCacheText.Text =
            $"L2: {FormatCache(processor.L2CacheKb)}" +
            $"   •   " +
            $"L3: {FormatCache(processor.L3CacheKb)}";
    }


    // ============================================================
    // МАТЕРИНСКАЯ ПЛАТА
    // ============================================================

    private void PopulateMotherboard(
        MotherboardDetails board)
    {
        BoardManufacturerText.Text =
            ValueOrUnavailable(
                board.Manufacturer);

        BoardModelText.Text =
            ValueOrUnavailable(
                board.Model);

        BoardVersionText.Text =
            ValueOrUnavailable(
                board.Version);

        FirmwareTypeText.Text =
            ValueOrUnavailable(
                board.FirmwareType);

        BiosVersionText.Text =
            ValueOrUnavailable(
                board.BiosVersion);

        BiosDateText.Text =
            FormatDate(
                board.BiosDate);
    }


    // ============================================================
    // ПАМЯТЬ
    // ============================================================

    private void PopulateMemory(
        MemoryDetails memory)
    {
        MemoryTotalText.Text =
            FormatBytes(
                memory.TotalBytes);

        MemoryModuleCountText.Text =
            memory.Modules.Count.ToString();

        MemoryModulesPanel
            .Children
            .Clear();

        if (memory.Modules.Count == 0)
        {
            MemoryModulesPanel
                .Children
                .Add(
                    CreateEmptyMessage(
                        "Модули памяти не обнаружены."));
            return;
        }

        for (int i = 0;
             i < memory.Modules.Count;
             i++)
        {
            MemoryModuleDetails module =
                memory.Modules[i];

            Border card =
                CreateInformationCard(
                    $"Модуль {i + 1}",
                    new List<InformationRow>
                    {
                        new(
                            "Слот / банк",
                            ValueOrUnavailable(
                                module.Slot)),

                        new(
                            "Объём",
                            FormatBytes(
                                module.CapacityBytes)),

                        new(
                            "Производитель",
                            ValueOrUnavailable(
                                module.Manufacturer)),

                        new(
                            "Part Number",
                            ValueOrUnavailable(
                                module.PartNumber)),

                        new(
                            "Серийный номер",
                            ValueOrUnavailable(
                                module.SerialNumber)),

                        new(
                            "Частота",
                            FormatFrequency(
                                module.FrequencyMhz))
                    });

            MemoryModulesPanel
                .Children
                .Add(
                    card);
        }
    }


    // ============================================================
    // ВИДЕОКАРТЫ
    // ============================================================

    private void PopulateGraphicsAdapters(
        IReadOnlyList<GraphicsAdapterDetails> adapters)
    {
        GraphicsAdaptersPanel
            .Children
            .Clear();

        if (adapters.Count == 0)
        {
            GraphicsAdaptersPanel
                .Children
                .Add(
                    CreateEmptyMessage(
                        "Видеоадаптеры не обнаружены."));
            return;
        }

        for (int i = 0;
             i < adapters.Count;
             i++)
        {
            GraphicsAdapterDetails adapter =
                adapters[i];

            Border card =
                CreateInformationCard(
                    $"Видеоадаптер {i + 1}",
                    new List<InformationRow>
                    {
                        new(
                            "Модель",
                            ValueOrUnavailable(
                                adapter.Name)),

                        new(
                            "Производитель",
                            ValueOrUnavailable(
                                adapter.Manufacturer)),

                        new(
                            "Графический процессор",
                            ValueOrUnavailable(
                                adapter.VideoProcessor)),

                        new(
                            "Видеопамять",
                            adapter.DedicatedMemoryBytes.HasValue
                                ? FormatBytes(
                                    adapter.DedicatedMemoryBytes)
                                : "Пока не определяется надёжно"),

                        new(
                            "Версия драйвера",
                            ValueOrUnavailable(
                                adapter.DriverVersion)),

                        new(
                            "Дата драйвера",
                            FormatDate(
                                adapter.DriverDate))
                    });

            GraphicsAdaptersPanel
                .Children
                .Add(
                    card);
        }
    }


    // ============================================================
    // НАКОПИТЕЛИ
    // ============================================================

    private void PopulateStorageDevices(
        IReadOnlyList<StorageDeviceDetails> devices)
    {
        StorageDevicesPanel
            .Children
            .Clear();

        if (devices.Count == 0)
        {
            StorageDevicesPanel
                .Children
                .Add(
                    CreateEmptyMessage(
                        "Физические накопители не обнаружены."));
            return;
        }

        for (int i = 0;
             i < devices.Count;
             i++)
        {
            StorageDeviceDetails device =
                devices[i];

            Border card =
                CreateInformationCard(
                    $"Накопитель {i + 1}",
                    new List<InformationRow>
                    {
                        new(
                            "Модель",
                            ValueOrUnavailable(
                                device.Model)),

                        new(
                            "Производитель",
                            ValueOrUnavailable(
                                device.Manufacturer)),

                        new(
                            "Серийный номер",
                            ValueOrUnavailable(
                                device.SerialNumber)),

                        new(
                            "Прошивка",
                            ValueOrUnavailable(
                                device.Firmware)),

                        new(
                            "Интерфейс",
                            ValueOrUnavailable(
                                device.InterfaceType)),

                        new(
                            "Тип носителя",
                            ValueOrUnavailable(
                                device.MediaType)),

                        new(
                            "Ёмкость",
                            FormatBytes(
                                device.CapacityBytes))
                    });

            StorageDevicesPanel
                .Children
                .Add(
                    card);
        }
    }


    // ============================================================
    // WINDOWS
    // ============================================================

    private void PopulateWindows(
        OperatingSystemDetails windows)
    {
        WindowsNameText.Text =
            ValueOrUnavailable(
                windows.Name);

        WindowsEditionText.Text =
            ValueOrUnavailable(
                windows.Edition);

        string versionText =
            ValueOrUnavailable(
                windows.DisplayVersion);

        if (!string.IsNullOrWhiteSpace(
                windows.Version))
        {
            versionText +=
                $"   ({windows.Version})";
        }

        WindowsVersionText.Text =
            versionText;

        WindowsBuildText.Text =
            ValueOrUnavailable(
                windows.BuildNumber);

        WindowsArchitectureText.Text =
            ValueOrUnavailable(
                windows.Architecture);

        ComputerNameText.Text =
            ValueOrUnavailable(
                windows.ComputerName);

        WindowsInstallDateText.Text =
            FormatDate(
                windows.InstallDate);
    }


    // ============================================================
    // ДИНАМИЧЕСКИЕ КАРТОЧКИ
    // ============================================================

    private Border CreateInformationCard(
        string title,
        IReadOnlyList<InformationRow> rows)
    {
        Border card =
            new()
            {
                BorderThickness =
                    new Thickness(1),

                Padding =
                    new Thickness(18),

                Margin =
                    new Thickness(
                        0, 0, 0, 12)
            };

        card.SetResourceReference(
            Border.BackgroundProperty,
            "CardBackgroundBrush");

        card.SetResourceReference(
            Border.BorderBrushProperty,
            "BorderBrush");

        card.SetResourceReference(
            Border.CornerRadiusProperty,
            "PanelCornerRadius");

        StackPanel body =
            new();

        TextBlock heading =
            new()
            {
                Text = title,
                FontSize = 16,
                FontWeight =
                    FontWeights.Bold,
                Margin =
                    new Thickness(
                        0, 0, 0, 12)
            };

        heading.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AccentBrush");

        body.Children.Add(
            heading);

        foreach (InformationRow row in rows)
        {
            Grid line =
                new()
                {
                    Margin =
                        new Thickness(
                            0, 4, 0, 4)
                };

            line.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(210)
                });

            line.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width =
                        new GridLength(
                            1,
                            GridUnitType.Star)
                });

            TextBlock label =
                new()
                {
                    Text =
                        row.Label,

                    FontSize = 11,

                    FontWeight =
                        FontWeights.SemiBold,

                    VerticalAlignment =
                        VerticalAlignment.Center
                };

            label.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            TextBlock value =
                new()
                {
                    Text =
                        row.Value,

                    FontSize = 13,

                    TextWrapping =
                        TextWrapping.Wrap,

                    VerticalAlignment =
                        VerticalAlignment.Center
                };

            value.SetResourceReference(
                TextBlock.ForegroundProperty,
                "PrimaryTextBrush");

            Grid.SetColumn(
                label,
                0);

            Grid.SetColumn(
                value,
                1);

            line.Children.Add(
                label);

            line.Children.Add(
                value);

            body.Children.Add(
                line);
        }

        card.Child =
            body;

        return card;
    }


    private Border CreateEmptyMessage(
        string text)
    {
        Border card =
            new()
            {
                BorderThickness =
                    new Thickness(1),

                Padding =
                    new Thickness(18),

                Margin =
                    new Thickness(
                        0, 0, 0, 12)
            };

        card.SetResourceReference(
            Border.BackgroundProperty,
            "CardBackgroundBrush");

        card.SetResourceReference(
            Border.BorderBrushProperty,
            "BorderBrush");

        card.SetResourceReference(
            Border.CornerRadiusProperty,
            "PanelCornerRadius");

        TextBlock message =
            new()
            {
                Text = text,
                FontSize = 13
            };

        message.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        card.Child =
            message;

        return card;
    }


    // ============================================================
    // ФОРМАТИРОВАНИЕ
    // ============================================================

    private static string ValueOrUnavailable(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Нет данных"
            : value.Trim();
    }


    private static string FormatNumber(
        uint? value)
    {
        return value.HasValue
            ? value.Value.ToString()
            : "Нет данных";
    }


    private static string FormatFrequency(
        uint? megahertz)
    {
        if (!megahertz.HasValue)
            return "Нет данных";

        if (megahertz.Value >= 1000)
        {
            return
                $"{megahertz.Value / 1000.0:F2} ГГц";
        }

        return
            $"{megahertz.Value} МГц";
    }


    private static string FormatCache(
        uint? kilobytes)
    {
        if (!kilobytes.HasValue)
            return "Нет данных";

        if (kilobytes.Value >= 1024)
        {
            return
                $"{kilobytes.Value / 1024.0:F1} МБ";
        }

        return
            $"{kilobytes.Value} КБ";
    }


    private static string FormatDate(
        DateTime? date)
    {
        return date.HasValue
            ? date.Value.ToString(
                "dd.MM.yyyy")
            : "Нет данных";
    }


    private static string FormatBytes(
        ulong? bytes)
    {
        if (!bytes.HasValue ||
            bytes.Value == 0)
        {
            return "Нет данных";
        }

        double gigabytes =
            bytes.Value /
            1024d /
            1024d /
            1024d;

        if (gigabytes >= 1024)
        {
            return
                $"{gigabytes / 1024d:F2} ТБ";
        }

        return
            $"{gigabytes:F1} ГБ";
    }


    // ============================================================
    // КОПИРОВАНИЕ ИНФОРМАЦИИ
    // ============================================================

    private void CopyInformationButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_snapshot == null)
        {
            StatusText.Text =
                "Информация о системе ещё не загружена.";

            return;
        }

        try
        {
            string text =
                BuildCopyText(
                    _snapshot);

            Clipboard.SetText(
                text);

            StatusText.Text =
                "Информация скопирована в буфер обмена.";
        }
        catch (Exception ex)
        {
            StatusText.Text =
                $"Не удалось скопировать данные: " +
                $"{ex.Message}";
        }
    }


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

        return text
            .ToString()
            .TrimEnd();
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


    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }


    private sealed record InformationRow(
        string Label,
        string Value);
}
