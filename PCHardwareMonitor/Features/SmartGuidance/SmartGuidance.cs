using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCHardwareMonitor;

// ================================================================
// SMART GUIDANCE 0.9.0
//
// Законченные сценарии: RAM и накопители.
// Использует уже работающий текущий HardwareSnapshot и локальную
// историю StatisticsService. Отдельный цикл опроса оборудования
// не создаётся.
// ================================================================

public partial class MainWindow
{
    private const string MainClarityCpuTag =
        "THERMIQRA_MAIN_CPU_HUMAN_LABEL";

    private const string MainClarityGpuTag =
        "THERMIQRA_MAIN_GPU_HUMAN_LABEL";

    private const string MainClarityDriveTagPrefix =
        "THERMIQRA_MAIN_DRIVE_HUMAN_LABEL_";

    private const string MainClarityDriveLegendTag =
        "THERMIQRA_MAIN_DRIVE_LEGEND";

    private const string MainClarityGaugeRowSuffix =
        "_ROW";

    private bool _mainClarityHandlersAttached;

    private DispatcherTimer? _mainClarityOneShotTimer;

    private DependencyObject? _mainClarityLastDriveFirstChild;

    private int _mainClarityLastGaugeCount =
        -1;

    private string _mainClarityLastCpuHumanName =
        "";

    private string _mainClarityLastGpuHumanName =
        "";

    private Dictionary<string, MainDriveStorageKind>?
        _mainDriveKindCache;

    private DateTime _mainDriveKindCacheAtUtc =
        DateTime.MinValue;


    static MainWindow()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(
                MainClarityWindow_Loaded));

        EventManager.RegisterClassHandler(
            typeof(StatisticsWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(
                StatisticsWindow_Loaded));
    }


    internal HardwareSnapshot? GetSmartGuidanceSnapshot()
    {
        return _lastSnapshot;
    }


    private enum MainDriveStorageKind
    {
        Unknown,
        Hdd,
        Ssd
    }
}


public partial class SystemInfoWindow
{
    private const string SmartGuidanceTabTag =
        "THERMIQRA_SMART_GUIDANCE";

    private bool _smartGuidanceInitialized;

    private TextBlock? _ramGuidanceStatusText;

    private TextBlock? _ramGuidanceCurrentText;

    private TextBlock? _ramGuidanceHistoryText;

    private TextBlock? _ramGuidanceConfigurationText;

    private TextBlock? _ramGuidancePlacementWarningText;

    private TextBlock? _ramGuidanceMeaningText;

    private TextBlock? _ramGuidanceActionText;

    private TextBlock? _ramGuidanceNoteText;

    private TextBlock? _storageGuidanceStatusText;

    private StackPanel? _storageGuidanceDetailsPanel;

    private TextBlock? _storageGuidanceMeaningText;

    private TextBlock? _storageGuidanceActionText;

    private TextBlock? _upgradeGuidanceStatusText;

    private StackPanel? _upgradeGuidanceDetailsPanel;

    private TextBlock? _upgradeGuidanceNoteText;



    static SystemInfoWindow()
    {
        EventManager.RegisterClassHandler(
            typeof(SystemInfoWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(
                SmartGuidanceWindow_Loaded));
    }



}
