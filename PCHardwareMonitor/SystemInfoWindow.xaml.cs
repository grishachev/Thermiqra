using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Controls;

namespace PCHardwareMonitor;

public partial class SystemInfoWindow : Window
{

    private readonly SystemInfoService _systemInfoService =
        new();

    private readonly StatisticsService? _statistics;

    private SystemInformationSnapshot? _snapshot;

    public SystemInfoWindow()
        : this(null)
    {
    }


    public SystemInfoWindow(
        StatisticsService? statistics)
    {
        InitializeComponent();

        _statistics =
            statistics;

        SettingsService.ApplyLanguageToWindow(
            this);

        SaveDiagnosticReportButton.Content =
            SettingsService.L(
                "Сохранить отчёт",
                "Save report");

        // Restore this window's last user-selected size and position.
        // Storage Analyzer keeps its separate match-the-main-window behavior.
        WindowPlacementService.Attach(this);

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
                SettingsService.TranslateText(
                    "Чтение информации о системе...");

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
                SettingsService.TranslateText(
                    $"Данные загружены: " +
                    $"{DateTime.Now:HH:mm:ss}");
        }
        catch (Exception ex)
        {
            StatusText.Text =
                SettingsService.TranslateText(
                    $"Не удалось получить данные: " +
                    $"{ex.Message}");
        }
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
