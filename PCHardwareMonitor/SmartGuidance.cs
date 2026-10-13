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


    private static void MainClarityWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MainWindow window)
            return;

        if (!window._mainClarityHandlersAttached)
        {
            window._mainClarityHandlersAttached =
                true;

            window.IsEnabledChanged +=
                MainClarityWindow_IsEnabledChanged;

            window.IsVisibleChanged +=
                MainClarityWindow_IsVisibleChanged;

            window.MainGaugesPanel.LayoutUpdated +=
                window.MainClarityGaugesPanel_LayoutUpdated;

            window.DrivesPanel.LayoutUpdated +=
                window.MainClarityDrivesPanel_LayoutUpdated;
        }

        window.ApplyMainScreenClarity();
        window.ScheduleMainScreenClarityRefresh();
    }


    private static void MainClarityWindow_IsEnabledChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (sender is MainWindow window &&
            window.IsEnabled)
        {
            window.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(
                    window.ApplyMainScreenClarity));
        }
    }


    private static void MainClarityWindow_IsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (sender is MainWindow window &&
            window.IsVisible)
        {
            window.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(
                    window.ApplyMainScreenClarity));
        }
    }


    private void MainClarityGaugesPanel_LayoutUpdated(
        object? sender,
        EventArgs e)
    {
        int currentCount =
            MainGaugesPanel.Children.Count;

        string cpuHumanName =
            SettingsService.L(
                "процессор",
                "processor");

        string gpuHumanName =
            SettingsService.L(
                "видеокарта",
                "graphics card");

        if (currentCount ==
                _mainClarityLastGaugeCount &&
            string.Equals(
                cpuHumanName,
                _mainClarityLastCpuHumanName,
                StringComparison.Ordinal) &&
            string.Equals(
                gpuHumanName,
                _mainClarityLastGpuHumanName,
                StringComparison.Ordinal))
        {
            return;
        }

        _mainClarityLastGaugeCount =
            currentCount;

        _mainClarityLastCpuHumanName =
            cpuHumanName;

        _mainClarityLastGpuHumanName =
            gpuHumanName;

        ApplyGaugeHumanLabel(
            _cpuGauge,
            "CPU",
            cpuHumanName,
            MainClarityCpuTag);

        foreach (RingGauge gauge in
                 _gpuGauges)
        {
            ApplyGaugeHumanLabel(
                gauge,
                "GPU",
                gpuHumanName,
                MainClarityGpuTag);
        }
    }


    private void MainClarityDrivesPanel_LayoutUpdated(
        object? sender,
        EventArgs e)
    {
        if (DrivesPanel.Children.Count == 0)
        {
            _mainClarityLastDriveFirstChild =
                null;

            return;
        }

        DependencyObject firstChild =
            DrivesPanel.Children[0];

        if (ReferenceEquals(
                firstChild,
                _mainClarityLastDriveFirstChild))
        {
            return;
        }

        _mainClarityLastDriveFirstChild =
            firstChild;

        ApplyMainDriveClarity();
    }


    private void ScheduleMainScreenClarityRefresh()
    {
        _mainClarityOneShotTimer?.Stop();

        _mainClarityOneShotTimer =
            new DispatcherTimer
            {
                Interval =
                    TimeSpan.FromMilliseconds(
                        1400)
            };

        _mainClarityOneShotTimer.Tick +=
            MainClarityOneShotTimer_Tick;

        _mainClarityOneShotTimer.Start();
    }


    private void MainClarityOneShotTimer_Tick(
        object? sender,
        EventArgs e)
    {
        _mainClarityOneShotTimer?.Stop();
        _mainClarityOneShotTimer =
            null;

        ApplyMainScreenClarity();
    }


    private void ApplyMainScreenClarity()
    {
        string cpuHumanName =
            SettingsService.L(
                "процессор",
                "processor");

        string gpuHumanName =
            SettingsService.L(
                "видеокарта",
                "graphics card");

        ApplyGaugeHumanLabel(
            _cpuGauge,
            "CPU",
            cpuHumanName,
            MainClarityCpuTag);

        foreach (RingGauge gauge in
                 _gpuGauges)
        {
            ApplyGaugeHumanLabel(
                gauge,
                "GPU",
                gpuHumanName,
                MainClarityGpuTag);
        }

        _mainClarityLastGaugeCount =
            MainGaugesPanel.Children.Count;

        _mainClarityLastCpuHumanName =
            cpuHumanName;

        _mainClarityLastGpuHumanName =
            gpuHumanName;

        ApplyMainDriveClarity();
    }


    private static void ApplyGaugeHumanLabel(
        DependencyObject root,
        string technicalName,
        string humanName,
        string tag)
    {
        string rowTag =
            tag +
            MainClarityGaugeRowSuffix;

        foreach (TextBlock titleText in
                 FindMainClarityTextBlocks(
                     root)
                 .Where(
                     textBlock =>
                         textBlock.FontSize >= 20 &&
                         string.Equals(
                             textBlock.Text,
                             technicalName,
                             StringComparison.OrdinalIgnoreCase))
                 .ToList())
        {
            if (VisualTreeHelper.GetParent(
                    titleText)
                is not StackPanel parent)
            {
                continue;
            }

            if (string.Equals(
                    parent.Tag as string,
                    rowTag,
                    StringComparison.Ordinal))
            {
                TextBlock? existingLabel =
                    parent.Children
                        .OfType<TextBlock>()
                        .FirstOrDefault(
                            child =>
                                string.Equals(
                                    child.Tag as string,
                                    tag,
                                    StringComparison.Ordinal));

                if (existingLabel != null)
                {
                    existingLabel.Text =
                        $"— {humanName}";
                }

                continue;
            }

            int index =
                parent.Children.IndexOf(
                    titleText);

            if (index < 0)
                continue;

            parent.Children.RemoveAt(
                index);

            StackPanel row =
                new()
                {
                    Orientation =
                        Orientation.Horizontal,

                    VerticalAlignment =
                        VerticalAlignment.Center,

                    Tag =
                        rowTag
                };

            row.Children.Add(
                titleText);

            TextBlock humanLabel =
                new()
                {
                    Text =
                        $"— {humanName}",

                    Tag =
                        tag,

                    FontSize =
                        Math.Max(
                            11,
                            titleText.FontSize *
                            0.48),

                    FontWeight =
                        FontWeights.SemiBold,

                    FontFamily =
                        titleText.FontFamily,

                    VerticalAlignment =
                        VerticalAlignment.Center,

                    Margin =
                        new Thickness(
                            9,
                            2,
                            0,
                            0)
                };

            humanLabel.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            row.Children.Add(
                humanLabel);

            parent.Children.Insert(
                index,
                row);
        }
    }


    private void ApplyMainDriveClarity()
    {
        EnsureMainDriveLegend();

        HardwareSnapshot? snapshot =
            _lastSnapshot;

        if (snapshot == null ||
            snapshot.Drives.Count == 0 ||
            DrivesPanel.Children.Count == 0)
        {
            return;
        }

        _mainClarityLastDriveFirstChild =
            DrivesPanel.Children[0];

        Dictionary<string, MainDriveStorageKind>
            driveKinds =
                GetMainDriveKindMap();

        int count =
            Math.Min(
                snapshot.Drives.Count,
                DrivesPanel.Children.Count);

        for (int i = 0;
             i < count;
             i++)
        {
            LogicalDriveInfo drive =
                snapshot.Drives[i];

            string driveName =
                NormalizeMainDriveName(
                    drive.Name);

            if (!driveKinds.TryGetValue(
                    driveName,
                    out MainDriveStorageKind kind) ||
                kind ==
                    MainDriveStorageKind.Unknown)
            {
                continue;
            }

            DependencyObject card =
                DrivesPanel.Children[i];

            string visibleName =
                string.IsNullOrWhiteSpace(
                    drive.VolumeLabel)
                    ? drive.Name
                    : $"{drive.Name} {drive.VolumeLabel}";

            string tag =
                MainClarityDriveTagPrefix +
                driveName;

            TextBlock? target =
                FindMainClarityTextBlocks(
                        card)
                    .FirstOrDefault(
                        textBlock =>
                            string.Equals(
                                textBlock.Tag as string,
                                tag,
                                StringComparison.Ordinal) ||
                            string.Equals(
                                textBlock.Text,
                                visibleName,
                                StringComparison.OrdinalIgnoreCase));

            if (target == null)
                continue;

            string shortType =
                kind ==
                    MainDriveStorageKind.Ssd
                    ? "SSD"
                    : "HDD";

            InsertDriveHumanLabel(
                target,
                shortType,
                tag);
        }
    }


    private static void InsertDriveHumanLabel(
        TextBlock target,
        string shortType,
        string tag)
    {
        string baseText;

        if (target.Tag is string existingTag &&
            string.Equals(
                existingTag,
                tag,
                StringComparison.Ordinal))
        {
            baseText =
                target.Inlines
                    .OfType<Run>()
                    .FirstOrDefault()?
                    .Text ??
                target.Text;
        }
        else
        {
            baseText =
                target.Text;
        }

        target.Tag =
            tag;

        target.Inlines.Clear();

        Run mainRun =
            new(
                baseText);

        target.Inlines.Add(
            mainRun);

        if (ContainsDriveTypeToken(
                baseText,
                shortType))
        {
            return;
        }

        Run typeRun =
            new(
                $" {shortType}");

        target.Inlines.Add(
            typeRun);
    }


    private static bool ContainsDriveTypeToken(
        string text,
        string token)
    {
        if (string.IsNullOrWhiteSpace(
                text) ||
            string.IsNullOrWhiteSpace(
                token))
        {
            return false;
        }

        string[] parts =
            text.Split(
                new[]
                {
                    ' ',
                    '\\',
                    '/',
                    ':',
                    '-',
                    '_',
                    '(',
                    ')',
                    '[',
                    ']'
                },
                StringSplitOptions.RemoveEmptyEntries);

        return parts.Any(
            part =>
                string.Equals(
                    part,
                    token,
                    StringComparison.OrdinalIgnoreCase));
    }


    private void EnsureMainDriveLegend()
    {
        if (DrivesPanel.Parent is not StackPanel parent)
            return;

        TextBlock? existing =
            parent.Children
                .OfType<TextBlock>()
                .FirstOrDefault(
                    child =>
                        string.Equals(
                            child.Tag as string,
                            MainClarityDriveLegendTag,
                            StringComparison.Ordinal));

        string legendText =
            SettingsService.L(
                "SSD — быстрый накопитель   •   HDD — обычный жёсткий диск",
                "SSD — fast storage   •   HDD — hard disk drive");

        if (existing != null)
        {
            existing.Text =
                legendText;

            return;
        }

        int drivesIndex =
            parent.Children.IndexOf(
                DrivesPanel);

        if (drivesIndex < 0)
            return;

        TextBlock legend =
            new()
            {
                Tag =
                    MainClarityDriveLegendTag,

                Text =
                    legendText,

                FontSize =
                    10,

                FontWeight =
                    FontWeights.SemiBold,

                Margin =
                    new Thickness(
                        4,
                        0,
                        4,
                        7),

                TextWrapping =
                    TextWrapping.Wrap
            };

        legend.SetResourceReference(
            TextBlock.ForegroundProperty,
            "SecondaryTextBrush");

        parent.Children.Insert(
            drivesIndex,
            legend);
    }


    private Dictionary<string, MainDriveStorageKind>
        GetMainDriveKindMap()
    {
        if (_mainDriveKindCache != null &&
            DateTime.UtcNow -
                _mainDriveKindCacheAtUtc <
            TimeSpan.FromMinutes(
                1))
        {
            return _mainDriveKindCache;
        }

        _mainDriveKindCache =
            ReadMainDriveKindMap();

        _mainDriveKindCacheAtUtc =
            DateTime.UtcNow;

        return _mainDriveKindCache;
    }


    private static Dictionary<string, MainDriveStorageKind>
        ReadMainDriveKindMap()
    {
        Dictionary<string, MainDriveStorageKind> result =
            new(
                StringComparer.OrdinalIgnoreCase);

        Dictionary<int, MainDriveStorageKind>
            physicalKinds =
                ReadMainPhysicalStorageKinds();

        try
        {
            using ManagementObjectSearcher searcher =
                new(
                    "SELECT Antecedent, Dependent " +
                    "FROM Win32_LogicalDiskToPartition");

            using ManagementObjectCollection associations =
                searcher.Get();

            foreach (ManagementObject association in
                     associations)
            {
                string dependent =
                    association[
                        "Dependent"]?
                    .ToString() ??
                    string.Empty;

                string? logicalDrive =
                    ExtractLogicalDriveFromWmiPath(
                        dependent);

                if (string.IsNullOrWhiteSpace(
                        logicalDrive))
                {
                    continue;
                }

                string? antecedent =
                    association[
                        "Antecedent"]?
                    .ToString();

                if (string.IsNullOrWhiteSpace(
                        antecedent))
                {
                    continue;
                }

                using ManagementObject partition =
                    new(
                        new ManagementPath(
                            antecedent));

                partition.Get();

                if (!int.TryParse(
                        partition["DiskIndex"]?
                            .ToString(),
                        out int diskIndex))
                {
                    continue;
                }

                if (physicalKinds.TryGetValue(
                        diskIndex,
                        out MainDriveStorageKind kind))
                {
                    result[
                        NormalizeMainDriveName(
                            logicalDrive)] =
                        kind;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Не удалось сопоставить логические и физические диски: {ex}");
        }

        return result;
    }


    private static Dictionary<int, MainDriveStorageKind>
        ReadMainPhysicalStorageKinds()
    {
        Dictionary<int, MainDriveStorageKind> result =
            new();

        try
        {
            ManagementScope scope =
                new(
                    @"\\.\root\Microsoft\Windows\Storage");

            scope.Connect();

            ObjectQuery query =
                new(
                    "SELECT DeviceId, MediaType " +
                    "FROM MSFT_PhysicalDisk");

            using ManagementObjectSearcher searcher =
                new(
                    scope,
                    query);

            using ManagementObjectCollection items =
                searcher.Get();

            foreach (ManagementObject item in
                     items)
            {
                if (!int.TryParse(
                        item["DeviceId"]?
                            .ToString(),
                        out int deviceId))
                {
                    continue;
                }

                int mediaType =
                    item["MediaType"] == null
                        ? 0
                        : Convert.ToInt32(
                            item["MediaType"]);

                result[deviceId] =
                    mediaType switch
                    {
                        3 =>
                            MainDriveStorageKind.Hdd,

                        4 =>
                            MainDriveStorageKind.Ssd,

                        _ =>
                            MainDriveStorageKind.Unknown
                    };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Не удалось определить тип диска для главного окна: {ex}");
        }

        return result;
    }


    private static string?
        ExtractLogicalDriveFromWmiPath(
            string path)
    {
        const string marker =
            "DeviceID=\"";

        int start =
            path.IndexOf(
                marker,
                StringComparison.OrdinalIgnoreCase);

        if (start < 0)
            return null;

        start +=
            marker.Length;

        int end =
            path.IndexOf(
                '"',
                start);

        if (end <= start)
            return null;

        return path.Substring(
            start,
            end - start);
    }


    private static string NormalizeMainDriveName(
        string? driveName)
    {
        if (string.IsNullOrWhiteSpace(
                driveName))
        {
            return string.Empty;
        }

        return driveName
            .Trim()
            .TrimEnd('\\');
    }


    private static IEnumerable<TextBlock>
        FindMainClarityTextBlocks(
            DependencyObject root)
    {
        int childCount =
            VisualTreeHelper.GetChildrenCount(
                root);

        for (int i = 0;
             i < childCount;
             i++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(
                    root,
                    i);

            if (child is TextBlock textBlock)
            {
                yield return
                    textBlock;
            }

            foreach (TextBlock nested in
                     FindMainClarityTextBlocks(
                         child))
            {
                yield return
                    nested;
            }
        }
    }


    private static void StatisticsWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not StatisticsWindow window)
            return;

        window.Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(
                () =>
                    ApplyStatisticsScrollBarTheme(
                        window)));
    }


    private static void ApplyStatisticsScrollBarTheme(
        StatisticsWindow window)
    {
        if (window.FindName(
                "StatisticsScrollViewer")
            is not ScrollViewer scrollViewer)
        {
            return;
        }

        const string markerKey =
            "ThermiqraStatisticsScrollThemeLoaded";

        if (scrollViewer.Resources.Contains(
                markerKey))
        {
            return;
        }

        const string resourcesXaml =
            """
            <ResourceDictionary
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:primitives="clr-namespace:System.Windows.Controls.Primitives;assembly=PresentationFramework">

                <Style x:Key="ThermiqraStatisticsScrollThumbStyle"
                       TargetType="{x:Type primitives:Thumb}">
                    <Setter Property="Background"
                            Value="{DynamicResource BorderBrush}"/>
                    <Setter Property="Template">
                        <Setter.Value>
                            <ControlTemplate TargetType="{x:Type primitives:Thumb}">
                                <Border x:Name="ThumbBorder"
                                        Background="{TemplateBinding Background}"
                                        CornerRadius="2"
                                        Margin="2,0"/>
                                <ControlTemplate.Triggers>
                                    <Trigger Property="IsMouseOver"
                                             Value="True">
                                        <Setter TargetName="ThumbBorder"
                                                Property="Background"
                                                Value="{DynamicResource AccentBrush}"/>
                                    </Trigger>
                                    <Trigger Property="IsDragging"
                                             Value="True">
                                        <Setter TargetName="ThumbBorder"
                                                Property="Background"
                                                Value="{DynamicResource AccentBrush}"/>
                                    </Trigger>
                                </ControlTemplate.Triggers>
                            </ControlTemplate>
                        </Setter.Value>
                    </Setter>
                </Style>

                <Style x:Key="ThermiqraStatisticsInvisibleScrollButtonStyle"
                       TargetType="{x:Type RepeatButton}">
                    <Setter Property="Focusable"
                            Value="False"/>
                    <Setter Property="IsTabStop"
                            Value="False"/>
                    <Setter Property="Opacity"
                            Value="0"/>
                    <Setter Property="Template">
                        <Setter.Value>
                            <ControlTemplate TargetType="{x:Type RepeatButton}">
                                <Border Background="Transparent"/>
                            </ControlTemplate>
                        </Setter.Value>
                    </Setter>
                </Style>

                <Style TargetType="{x:Type ScrollBar}">
                    <Setter Property="Width"
                            Value="11"/>
                    <Setter Property="Background"
                            Value="Transparent"/>
                    <Setter Property="Template">
                        <Setter.Value>
                            <ControlTemplate TargetType="{x:Type ScrollBar}">
                                <Grid Background="Transparent">
                                    <primitives:Track x:Name="PART_Track"
                                                      IsDirectionReversed="True">
                                        <primitives:Track.DecreaseRepeatButton>
                                            <RepeatButton Style="{StaticResource ThermiqraStatisticsInvisibleScrollButtonStyle}"
                                                          Command="ScrollBar.PageUpCommand"/>
                                        </primitives:Track.DecreaseRepeatButton>

                                        <primitives:Track.Thumb>
                                            <primitives:Thumb Style="{StaticResource ThermiqraStatisticsScrollThumbStyle}"
                                                              MinHeight="34"/>
                                        </primitives:Track.Thumb>

                                        <primitives:Track.IncreaseRepeatButton>
                                            <RepeatButton Style="{StaticResource ThermiqraStatisticsInvisibleScrollButtonStyle}"
                                                          Command="ScrollBar.PageDownCommand"/>
                                        </primitives:Track.IncreaseRepeatButton>
                                    </primitives:Track>
                                </Grid>
                            </ControlTemplate>
                        </Setter.Value>
                    </Setter>
                </Style>
            </ResourceDictionary>
            """;

        try
        {
            if (XamlReader.Parse(
                    resourcesXaml)
                is ResourceDictionary dictionary)
            {
                scrollViewer.Resources
                    .MergedDictionaries
                    .Add(
                        dictionary);

                scrollViewer.Resources[
                    markerKey] =
                        true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Не удалось применить стиль полосы прокрутки статистики: {ex}");
        }
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


    private static void SmartGuidanceWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not SystemInfoWindow window)
            return;

        // Вкладку создаём сразу при открытии окна, до тяжёлого чтения
        // системной информации. Поэтому она появляется одновременно
        // со всеми штатными вкладками и больше не «догружается» позже.
        window.EnsureSmartGuidanceTab();

        // Сам анализ обновляем после штатного LoadSystemInformation(),
        // чтобы использовать уже полученные данные и не создавать
        // отдельный опрос оборудования.
        window.Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(
                () =>
                {
                    window.RefreshRamSmartGuidance();
                    window.RefreshStorageSmartGuidance();
                    window.RefreshUpgradeGuidance();
                }));
    }


    // ============================================================
    // ВКЛАДКА «АНАЛИЗ»
    // ============================================================

    private void EnsureSmartGuidanceTab()
    {
        if (_smartGuidanceInitialized)
            return;

        TabControl? tabControl =
            FindVisualChild<TabControl>(
                this);

        if (tabControl == null)
            return;

        foreach (object item in
                 tabControl.Items)
        {
            if (item is TabItem existingTab &&
                string.Equals(
                    existingTab.Tag as string,
                    SmartGuidanceTabTag,
                    StringComparison.Ordinal))
            {
                _smartGuidanceInitialized =
                    true;

                return;
            }
        }

        TabItem analysisTab =
            new()
            {
                Header =
                    SettingsService.L(
                        "АНАЛИЗ",
                        "ANALYSIS"),

                Tag =
                    SmartGuidanceTabTag
            };

        ScrollViewer scrollViewer =
            new()
            {
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Auto,

                HorizontalScrollBarVisibility =
                    ScrollBarVisibility.Disabled
            };

        StackPanel rootPanel =
            new()
            {
                Margin =
                    new Thickness(
                        0,
                        18,
                        6,
                        0)
            };

        TextBlock title =
            new()
            {
                Text =
                    SettingsService.L(
                        "Анализ системы",
                        "System analysis")
            };

        if (TryFindResource(
                "SectionTitleStyle")
            is Style sectionTitleStyle)
        {
            title.Style =
                sectionTitleStyle;
        }

        rootPanel.Children.Add(
            title);

        Border ramCard =
            new();

        if (TryFindResource(
                "InfoCardStyle")
            is Style infoCardStyle)
        {
            ramCard.Style =
                infoCardStyle;
        }
        else
        {
            ramCard.Padding =
                new Thickness(18);

            ramCard.Margin =
                new Thickness(
                    0,
                    0,
                    0,
                    12);

            ramCard.BorderThickness =
                new Thickness(1);

            ramCard.SetResourceReference(
                Border.BackgroundProperty,
                "CardBackgroundBrush");

            ramCard.SetResourceReference(
                Border.BorderBrushProperty,
                "BorderBrush");
        }

        StackPanel cardPanel =
            new();

        TextBlock ramTitle =
            CreateGuidanceText(
                SettingsService.L(
                    "ОПЕРАТИВНАЯ ПАМЯТЬ",
                    "SYSTEM MEMORY"),
                15,
                FontWeights.Bold,
                "PrimaryTextBrush");

        cardPanel.Children.Add(
            ramTitle);

        _ramGuidanceStatusText =
            CreateGuidanceText(
                SettingsService.L(
                    "Анализ данных...",
                    "Analyzing data..."),
                20,
                FontWeights.Bold,
                "AccentBrush");

        _ramGuidanceStatusText.Margin =
            new Thickness(
                0,
                10,
                0,
                0);

        cardPanel.Children.Add(
            _ramGuidanceStatusText);

        _ramGuidanceCurrentText =
            CreateGuidanceValueText();

        _ramGuidanceCurrentText.Margin =
            new Thickness(
                0,
                10,
                0,
                0);

        cardPanel.Children.Add(
            _ramGuidanceCurrentText);

        _ramGuidanceHistoryText =
            CreateGuidanceValueText();

        _ramGuidanceHistoryText.Margin =
            new Thickness(
                0,
                7,
                0,
                0);

        cardPanel.Children.Add(
            _ramGuidanceHistoryText);

        _ramGuidanceConfigurationText =
            CreateGuidanceText(
                string.Empty,
                11,
                FontWeights.Normal,
                "SecondaryTextBrush");

        _ramGuidanceConfigurationText.Margin =
            new Thickness(
                0,
                7,
                0,
                0);

        cardPanel.Children.Add(
            _ramGuidanceConfigurationText);

        _ramGuidancePlacementWarningText =
            CreateGuidanceText(
                string.Empty,
                12,
                FontWeights.SemiBold,
                "AccentBrush");

        _ramGuidancePlacementWarningText.Margin =
            new Thickness(
                0,
                12,
                0,
                0);

        _ramGuidancePlacementWarningText.Visibility =
            Visibility.Collapsed;

        cardPanel.Children.Add(
            _ramGuidancePlacementWarningText);

        Border separator =
            new()
            {
                Height = 1,
                Margin =
                    new Thickness(
                        0,
                        16,
                        0,
                        14)
            };

        separator.SetResourceReference(
            Border.BackgroundProperty,
            "BorderBrush");

        cardPanel.Children.Add(
            separator);

        TextBlock meaningLabel =
            CreateGuidanceLabelText(
                SettingsService.L(
                    "ЧТО ЭТО ОЗНАЧАЕТ",
                    "WHAT THIS MEANS"));

        cardPanel.Children.Add(
            meaningLabel);

        _ramGuidanceMeaningText =
            CreateGuidanceValueText();

        _ramGuidanceMeaningText.Margin =
            new Thickness(
                0,
                5,
                0,
                0);

        cardPanel.Children.Add(
            _ramGuidanceMeaningText);

        TextBlock actionLabel =
            CreateGuidanceLabelText(
                SettingsService.L(
                    "ЧТО ИМЕЕТ СМЫСЛ СДЕЛАТЬ",
                    "REASONABLE NEXT STEP"));

        actionLabel.Margin =
            new Thickness(
                0,
                16,
                0,
                0);

        cardPanel.Children.Add(
            actionLabel);

        _ramGuidanceActionText =
            CreateGuidanceValueText();

        _ramGuidanceActionText.Margin =
            new Thickness(
                0,
                5,
                0,
                0);

        cardPanel.Children.Add(
            _ramGuidanceActionText);

        _ramGuidanceNoteText =
            CreateGuidanceText(
                SettingsService.L(
                    "Вывод основан на текущей загрузке и локальной истории. " +
                    "Один высокий пик сам по себе не считается признаком нехватки памяти.",
                    "The conclusion is based on current load and local history. " +
                    "A single high peak by itself is not treated as evidence of insufficient memory."),
                10,
                FontWeights.Normal,
                "SecondaryTextBrush");

        _ramGuidanceNoteText.Margin =
            new Thickness(
                0,
                16,
                0,
                0);

        cardPanel.Children.Add(
            _ramGuidanceNoteText);

        ramCard.Child =
            cardPanel;

        rootPanel.Children.Add(
            ramCard);

        Border storageCard =
            new();

        if (TryFindResource(
                "InfoCardStyle")
            is Style storageInfoCardStyle)
        {
            storageCard.Style =
                storageInfoCardStyle;
        }
        else
        {
            storageCard.Padding =
                new Thickness(18);

            storageCard.Margin =
                new Thickness(
                    0,
                    0,
                    0,
                    12);

            storageCard.BorderThickness =
                new Thickness(1);

            storageCard.SetResourceReference(
                Border.BackgroundProperty,
                "CardBackgroundBrush");

            storageCard.SetResourceReference(
                Border.BorderBrushProperty,
                "BorderBrush");
        }

        StackPanel storageCardPanel =
            new();

        TextBlock storageTitle =
            CreateGuidanceText(
                SettingsService.L(
                    "НАКОПИТЕЛИ",
                    "STORAGE"),
                15,
                FontWeights.Bold,
                "PrimaryTextBrush");

        storageCardPanel.Children.Add(
            storageTitle);

        _storageGuidanceStatusText =
            CreateGuidanceText(
                SettingsService.L(
                    "Анализ данных...",
                    "Analyzing data..."),
                20,
                FontWeights.Bold,
                "AccentBrush");

        _storageGuidanceStatusText.Margin =
            new Thickness(
                0,
                10,
                0,
                0);

        storageCardPanel.Children.Add(
            _storageGuidanceStatusText);

        _storageGuidanceDetailsPanel =
            new StackPanel
            {
                Margin =
                    new Thickness(
                        0,
                        10,
                        0,
                        0)
            };

        storageCardPanel.Children.Add(
            _storageGuidanceDetailsPanel);

        Border storageSeparator =
            new()
            {
                Height = 1,
                Margin =
                    new Thickness(
                        0,
                        16,
                        0,
                        14)
            };

        storageSeparator.SetResourceReference(
            Border.BackgroundProperty,
            "BorderBrush");

        storageCardPanel.Children.Add(
            storageSeparator);

        TextBlock storageMeaningLabel =
            CreateGuidanceLabelText(
                SettingsService.L(
                    "ЧТО ЭТО ОЗНАЧАЕТ",
                    "WHAT THIS MEANS"));

        storageCardPanel.Children.Add(
            storageMeaningLabel);

        _storageGuidanceMeaningText =
            CreateGuidanceValueText();

        _storageGuidanceMeaningText.Margin =
            new Thickness(
                0,
                5,
                0,
                0);

        storageCardPanel.Children.Add(
            _storageGuidanceMeaningText);

        TextBlock storageActionLabel =
            CreateGuidanceLabelText(
                SettingsService.L(
                    "ЧТО ИМЕЕТ СМЫСЛ СДЕЛАТЬ",
                    "REASONABLE NEXT STEP"));

        storageActionLabel.Margin =
            new Thickness(
                0,
                16,
                0,
                0);

        storageCardPanel.Children.Add(
            storageActionLabel);

        _storageGuidanceActionText =
            CreateGuidanceValueText();

        _storageGuidanceActionText.Margin =
            new Thickness(
                0,
                5,
                0,
                0);

        storageCardPanel.Children.Add(
            _storageGuidanceActionText);

        storageCard.Child =
            storageCardPanel;

        rootPanel.Children.Add(
            storageCard);

        Border upgradeCard =
            new();

        if (TryFindResource(
                "InfoCardStyle")
            is Style upgradeInfoCardStyle)
        {
            upgradeCard.Style =
                upgradeInfoCardStyle;
        }
        else
        {
            upgradeCard.Padding =
                new Thickness(18);

            upgradeCard.Margin =
                new Thickness(
                    0,
                    0,
                    0,
                    12);

            upgradeCard.BorderThickness =
                new Thickness(1);

            upgradeCard.SetResourceReference(
                Border.BackgroundProperty,
                "CardBackgroundBrush");

            upgradeCard.SetResourceReference(
                Border.BorderBrushProperty,
                "BorderBrush");
        }

        StackPanel upgradeCardPanel =
            new();

        TextBlock upgradeTitle =
            CreateGuidanceText(
                SettingsService.L(
                    "ЧТО МОЖНО УЛУЧШИТЬ",
                    "WHAT COULD BE IMPROVED"),
                15,
                FontWeights.Bold,
                "PrimaryTextBrush");

        upgradeCardPanel.Children.Add(
            upgradeTitle);


        _upgradeGuidanceStatusText =
            CreateGuidanceText(
                SettingsService.L(
                    "Анализ данных...",
                    "Analyzing data..."),
                20,
                FontWeights.Bold,
                "AccentBrush");

        _upgradeGuidanceStatusText.Margin =
            new Thickness(
                0,
                10,
                0,
                0);

        upgradeCardPanel.Children.Add(
            _upgradeGuidanceStatusText);

        _upgradeGuidanceDetailsPanel =
            new StackPanel
            {
                Margin =
                    new Thickness(
                        0,
                        12,
                        0,
                        0)
            };

        upgradeCardPanel.Children.Add(
            _upgradeGuidanceDetailsPanel);

        _upgradeGuidanceNoteText =
            CreateGuidanceText(
                string.Empty,
                11,
                FontWeights.Normal,
                "SecondaryTextBrush");

        _upgradeGuidanceNoteText.Margin =
            new Thickness(
                0,
                14,
                0,
                0);

        _upgradeGuidanceNoteText.Visibility =
            Visibility.Collapsed;

        upgradeCardPanel.Children.Add(
            _upgradeGuidanceNoteText);

        upgradeCard.Child =
            upgradeCardPanel;

        rootPanel.Children.Add(
            upgradeCard);

        scrollViewer.Content =
            rootPanel;

        analysisTab.Content =
            scrollViewer;

        // Технические разделы идут единым блоком:
        // ОБЗОР -> ПРОЦЕССОР -> ПЛАТА -> ПАМЯТЬ -> ВИДЕОКАРТА
        // -> НАКОПИТЕЛИ -> WINDOWS.
        // АНАЛИЗ завершает окно и интерпретирует уже показанные данные.
        tabControl.Items.Add(
            analysisTab);

        _smartGuidanceInitialized =
            true;
    }


    private TextBlock CreateGuidanceValueText()
    {
        TextBlock textBlock =
            new();

        if (TryFindResource(
                "InfoValueStyle")
            is Style infoValueStyle)
        {
            textBlock.Style =
                infoValueStyle;
        }
        else
        {
            textBlock.FontSize =
                13;

            textBlock.TextWrapping =
                TextWrapping.Wrap;

            textBlock.SetResourceReference(
                TextBlock.ForegroundProperty,
                "PrimaryTextBrush");
        }

        return textBlock;
    }


    private TextBlock CreateGuidanceLabelText(
        string text)
    {
        TextBlock textBlock =
            new()
            {
                Text = text
            };

        if (TryFindResource(
                "InfoLabelStyle")
            is Style infoLabelStyle)
        {
            textBlock.Style =
                infoLabelStyle;
        }
        else
        {
            textBlock.FontSize =
                11;

            textBlock.FontWeight =
                FontWeights.SemiBold;

            textBlock.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");
        }

        return textBlock;
    }


    private static TextBlock CreateGuidanceText(
        string text,
        double fontSize,
        FontWeight fontWeight,
        string foregroundResource)
    {
        TextBlock textBlock =
            new()
            {
                Text = text,
                FontSize = fontSize,
                FontWeight = fontWeight,
                TextWrapping =
                    TextWrapping.Wrap
            };

        textBlock.SetResourceReference(
            TextBlock.ForegroundProperty,
            foregroundResource);

        return textBlock;
    }


    private static T? FindVisualChild<T>(
        DependencyObject parent)
        where T : DependencyObject
    {
        int childCount =
            VisualTreeHelper.GetChildrenCount(
                parent);

        for (int i = 0;
             i < childCount;
             i++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(
                    parent,
                    i);

            if (child is T result)
                return result;

            T? nested =
                FindVisualChild<T>(
                    child);

            if (nested != null)
                return nested;
        }

        return null;
    }


    // ============================================================
    // АНАЛИЗ RAM
    // ============================================================

    private void RefreshRamSmartGuidance()
    {
        if (!_smartGuidanceInitialized ||
            _ramGuidanceStatusText == null ||
            _ramGuidanceCurrentText == null ||
            _ramGuidanceHistoryText == null ||
            _ramGuidanceConfigurationText == null ||
            _ramGuidancePlacementWarningText == null ||
            _ramGuidanceMeaningText == null ||
            _ramGuidanceActionText == null)
        {
            return;
        }

        HardwareSnapshot? currentSnapshot =
            (Owner as MainWindow)?
                .GetSmartGuidanceSnapshot();

        RamHistoryEvidence? history =
            ReadRamHistoryEvidence();

        double? currentLoad =
            GetCurrentRamLoad(
                currentSnapshot);

        _ramGuidanceCurrentText.Text =
            BuildCurrentRamText(
                currentSnapshot,
                currentLoad);

        _ramGuidanceHistoryText.Text =
            BuildRamHistoryText(
                history);

        _ramGuidanceConfigurationText.Text =
            BuildRamConfigurationText();

        ApplyRamPlacementGuidance();

        ApplyRamGuidanceConclusion(
            currentLoad,
            history);
    }


    private RamHistoryEvidence? ReadRamHistoryEvidence()
    {
        if (_statistics == null)
            return null;

        try
        {
            StatisticsSummary last24Hours =
                _statistics.GetSummary(
                    StatisticsPeriod.Last24Hours);

            StatisticsSummary last7Days =
                _statistics.GetSummary(
                    StatisticsPeriod.Last7Days);

            StatisticsChartSeries daySeries =
                _statistics.GetChartSeries(
                    StatisticsPeriod.Last24Hours,
                    StatisticsChartMetric.RamLoad,
                    maxPoints: 1200);

            StatisticsChartSeries weekSeries =
                _statistics.GetChartSeries(
                    StatisticsPeriod.Last7Days,
                    StatisticsChartMetric.RamLoad,
                    maxPoints: 1200);

            List<StatisticsChartPoint> dayPoints =
                daySeries.Points;

            List<StatisticsChartPoint> weekPoints =
                weekSeries.Points;

            int dayHighCount =
                dayPoints.Count(
                    point =>
                        point.Value >= 85);

            int weekHighCount =
                weekPoints.Count(
                    point =>
                        point.Value >= 85);

            int weekDaysWithSamples =
                weekPoints
                    .Select(
                        point =>
                            point.TimestampUtc
                                .ToLocalTime()
                                .Date)
                    .Distinct()
                    .Count();

            int weekDaysWithHighLoad =
                weekPoints
                    .Where(
                        point =>
                            point.Value >= 85)
                    .Select(
                        point =>
                            point.TimestampUtc
                                .ToLocalTime()
                                .Date)
                    .Distinct()
                    .Count();

            return new RamHistoryEvidence
            {
                Last24Hours =
                    last24Hours,

                Last7Days =
                    last7Days,

                DaySeriesPointCount =
                    dayPoints.Count,

                DayHighPointCount =
                    dayHighCount,

                WeekSeriesPointCount =
                    weekPoints.Count,

                WeekHighPointCount =
                    weekHighCount,

                WeekDaysWithSamples =
                    weekDaysWithSamples,

                WeekDaysWithHighLoad =
                    weekDaysWithHighLoad
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Не удалось получить данные Smart Guidance для RAM: {ex}");

            return null;
        }
    }


    private void ApplyRamGuidanceConclusion(
        double? currentLoad,
        RamHistoryEvidence? history)
    {
        if (_ramGuidanceStatusText == null ||
            _ramGuidanceMeaningText == null ||
            _ramGuidanceActionText == null)
        {
            return;
        }

        if (!currentLoad.HasValue)
        {
            SetRamGuidanceText(
                SettingsService.L(
                    "Недостаточно текущих данных",
                    "Not enough current data"),

                SettingsService.L(
                    "Thermiqra пока не получила надёжное текущее значение загрузки оперативной памяти (RAM), " +
                    "поэтому не делает вывод о её достаточности.",
                    "Thermiqra has not yet received a reliable current system memory (RAM) load value, " +
                    "so it does not make a conclusion about memory sufficiency."),

                SettingsService.L(
                    "Продолжайте пользоваться компьютером и откройте анализ позже. " +
                    "История будет уточнять вывод по мере накопления данных.",
                    "Continue using the computer and open the analysis again later. " +
                    "The conclusion will become more reliable as history accumulates."));

            return;
        }

        bool enoughDayHistory =
            history != null &&
            history.Last24Hours.SampleCount >= 30 &&
            history.DaySeriesPointCount >= 30;

        bool enoughWeekHistory =
            history != null &&
            history.Last7Days.SampleCount >= 120 &&
            history.WeekSeriesPointCount >= 60 &&
            history.WeekDaysWithSamples >= 2;

        double dayHighRatio =
            history != null &&
            history.DaySeriesPointCount > 0
                ? history.DayHighPointCount /
                  (double)history.DaySeriesPointCount
                : 0;

        double weekHighRatio =
            history != null &&
            history.WeekSeriesPointCount > 0
                ? history.WeekHighPointCount /
                  (double)history.WeekSeriesPointCount
                : 0;

        bool repeatedHighLoad24Hours =
            enoughDayHistory &&
            history!.DayHighPointCount >= 20 &&
            dayHighRatio >= 0.10;

        bool repeatedHighLoad7Days =
            enoughWeekHistory &&
            history!.WeekHighPointCount >= 60 &&
            history.WeekDaysWithHighLoad >= 2 &&
            weekHighRatio >= 0.08;

        bool repeatedHighLoad =
            repeatedHighLoad24Hours ||
            repeatedHighLoad7Days;

        bool enoughHistory =
            enoughDayHistory ||
            enoughWeekHistory;

        bool isolatedHighPeak =
            !repeatedHighLoad &&
            history != null &&
            (
                (history.Last24Hours.RamLoad.Maximum.HasValue &&
                 history.Last24Hours.RamLoad.Maximum.Value >= 90) ||
                (history.Last7Days.RamLoad.Maximum.HasValue &&
                 history.Last7Days.RamLoad.Maximum.Value >= 90)
            );

        double load =
            currentLoad.Value;

        if (repeatedHighLoad)
        {
            string status =
                load >= 90
                    ? SettingsService.L(
                        "Оперативная память (RAM) часто почти заполнена",
                        "System memory (RAM) is often nearly full")
                    : SettingsService.L(
                        "Оперативная память (RAM) часто была почти заполнена",
                        "System memory (RAM) has often been nearly full");

            SetRamGuidanceText(
                status,

                SettingsService.L(
                    "В сохранённой истории оперативная память неоднократно была загружена на 85 % и выше. " +
                    "Это не означает неисправность, но может указывать на нехватку свободной памяти " +
                    "при ваших обычных нагрузках.",
                    "Saved history shows system memory (RAM) repeatedly reaching 85% load or higher. " +
                    "This does not indicate a hardware fault, but it may point to limited free memory " +
                    "under your usual workloads."),

                SettingsService.L(
                    "Сначала проверьте в Диспетчере задач программы, которые занимают больше всего памяти. " +
                    "Если такая картина регулярно повторяется во время обычной работы или игр, " +
                    "увеличение объёма оперативной памяти (RAM) может иметь смысл.",
                    "First check Task Manager for the applications using the most memory. " +
                    "If this pattern regularly repeats during normal work or gaming, " +
                    "increasing system memory (RAM) capacity may be worth considering."));

            return;
        }

        if (load >= 90)
        {
            SetRamGuidanceText(
                SettingsService.L(
                    "Память почти полностью занята сейчас",
                    "Memory is almost fully used right now"),

                enoughHistory
                    ? SettingsService.L(
                        "Текущая загрузка очень высокая, но история пока не показывает устойчивого повторения. " +
                        "Одного такого эпизода недостаточно, чтобы делать вывод о нехватке оперативной памяти (RAM).",
                        "Current load is very high, but history does not yet show a persistent recurring pattern. " +
                        "One episode is not enough to conclude that system memory (RAM) capacity is insufficient.")
                    : SettingsService.L(
                        "Текущая загрузка очень высокая, но накопленной истории пока недостаточно " +
                        "для уверенного вывода о нехватке оперативной памяти (RAM).",
                        "Current load is very high, but there is not enough accumulated history yet " +
                        "to make a reliable conclusion about insufficient system memory (RAM)."),

                SettingsService.L(
                    "Проверьте, какие программы сейчас используют больше всего памяти. " +
                    "Не меняйте конфигурацию только из-за одного пика — сначала посмотрите, повторяется ли ситуация.",
                    "Check which applications are using the most memory right now. " +
                    "Do not change the configuration because of a single peak — first see whether the situation repeats."));

            return;
        }

        if (load >= 85)
        {
            SetRamGuidanceText(
                SettingsService.L(
                    "Оперативная память (RAM) сильно загружена сейчас",
                    "System memory (RAM) is heavily loaded right now"),

                enoughHistory
                    ? SettingsService.L(
                        "Загрузка высокая, но по накопленной истории устойчивого повторения пока не видно. " +
                        "Это ещё не основание считать объём памяти недостаточным.",
                        "Load is high, but accumulated history does not yet show a persistent recurring pattern. " +
                        "This is not enough to conclude that system memory (RAM) capacity is insufficient.")
                    : SettingsService.L(
                        "Загрузка высокая. Истории пока мало, поэтому Thermiqra не делает " +
                        "долгосрочный вывод по одному текущему значению.",
                        "Load is high. There is not enough history yet, so Thermiqra does not make " +
                        "a long-term conclusion from one current value."),

                SettingsService.L(
                    "Если компьютер работает нормально, срочных действий не требуется. " +
                    "При повторении высокой загрузки проверьте самые требовательные программы и историю загрузки оперативной памяти (RAM).",
                    "If the computer is working normally, no urgent action is required. " +
                    "If high load keeps returning, check the most memory-intensive applications and system memory (RAM) history."));

            return;
        }

        if (isolatedHighPeak)
        {
            SetRamGuidanceText(
                SettingsService.L(
                    "Сейчас загрузка оперативной памяти (RAM) нормальная",
                    "System memory (RAM) load is normal right now"),

                SettingsService.L(
                    "В истории был высокий пик, но данных о повторяющейся высокой загрузке недостаточно. " +
                    "Один пик не считается основанием для рекомендации апгрейда.",
                    "History contains a high peak, but there is not enough evidence of recurring high load. " +
                    "A single peak is not a basis for recommending an upgrade."),

                SettingsService.L(
                    "Ничего менять по одному пику не нужно. " +
                    "Наблюдайте за историей при обычной работе и играх.",
                    "There is no need to change anything because of one peak. " +
                    "Watch the history during normal work and gaming."));

            return;
        }

        if (!enoughHistory)
        {
            SetRamGuidanceText(
                load >= 70
                    ? SettingsService.L(
                        "Оперативная память (RAM) заметно загружена, но ситуация спокойная",
                        "System memory (RAM) is noticeably used, but the situation is calm")
                    : SettingsService.L(
                        "Сейчас загрузка оперативной памяти (RAM) нормальная",
                        "System memory (RAM) load is normal right now"),

                SettingsService.L(
                    "Текущее значение не выглядит проблемным, но истории пока недостаточно " +
                    "для долгосрочного вывода.",
                    "The current value does not look problematic, but there is not enough history yet " +
                    "for a long-term conclusion."),

                SettingsService.L(
                    "Срочных действий не требуется. " +
                    "Thermiqra уточнит картину по мере накопления истории.",
                    "No urgent action is required. " +
                    "Thermiqra will refine the picture as more history is collected."));

            return;
        }

        SetRamGuidanceText(
            SettingsService.L(
                "Признаков нехватки оперативной памяти (RAM) не видно",
                "No signs of insufficient system memory (RAM) are visible"),

            SettingsService.L(
                "Текущая загрузка и накопленная история не показывают " +
                "повторяющейся высокой занятости оперативной памяти.",
                "Current load and accumulated history do not show " +
                "recurring high system memory (RAM) usage."),

            SettingsService.L(
                "Ничего менять не требуется. " +
                "Продолжайте использовать компьютер как обычно.",
                "No changes are required. " +
                "Continue using the computer normally."));
    }


    private void SetRamGuidanceText(
        string status,
        string meaning,
        string action)
    {
        if (_ramGuidanceStatusText != null)
        {
            _ramGuidanceStatusText.Text =
                status;
        }

        if (_ramGuidanceMeaningText != null)
        {
            _ramGuidanceMeaningText.Text =
                meaning;
        }

        if (_ramGuidanceActionText != null)
        {
            _ramGuidanceActionText.Text =
                action;
        }
    }


    private static double? GetCurrentRamLoad(
        HardwareSnapshot? snapshot)
    {
        if (snapshot == null)
            return null;

        if (snapshot.Memory.Load.HasValue)
        {
            return Math.Clamp(
                snapshot.Memory.Load.Value,
                0,
                100);
        }

        if (snapshot.Memory.UsedGb.HasValue &&
            snapshot.Memory.AvailableGb.HasValue)
        {
            double total =
                snapshot.Memory.UsedGb.Value +
                snapshot.Memory.AvailableGb.Value;

            if (total > 0)
            {
                return Math.Clamp(
                    snapshot.Memory.UsedGb.Value /
                    total *
                    100.0,
                    0,
                    100);
            }
        }

        return null;
    }


    private static string BuildCurrentRamText(
        HardwareSnapshot? snapshot,
        double? currentLoad)
    {
        if (snapshot == null ||
            !currentLoad.HasValue)
        {
            return SettingsService.L(
                "Загрузка оперативной памяти (RAM) сейчас: нет надёжных данных.",
                "System memory (RAM) usage now: no reliable data.");
        }

        string result =
            SettingsService.L(
                $"Загрузка оперативной памяти (RAM) сейчас: {currentLoad.Value:F0} %",
                $"System memory (RAM) usage now: {currentLoad.Value:F0}%");

        if (snapshot.Memory.UsedGb.HasValue &&
            snapshot.Memory.TotalGb.HasValue)
        {
            result +=
                SettingsService.L(
                    $"   •   занято {snapshot.Memory.UsedGb.Value:F1} ГБ " +
                    $"из {snapshot.Memory.TotalGb.Value:F1} ГБ",
                    $"   •   used {snapshot.Memory.UsedGb.Value:F1} GB " +
                    $"of {snapshot.Memory.TotalGb.Value:F1} GB");
        }
        else if (snapshot.Memory.UsedGb.HasValue)
        {
            result +=
                SettingsService.L(
                    $"   •   занято {snapshot.Memory.UsedGb.Value:F1} ГБ",
                    $"   •   used {snapshot.Memory.UsedGb.Value:F1} GB");
        }

        if (snapshot.Memory.AvailableGb.HasValue)
        {
            result +=
                SettingsService.L(
                    $"   •   доступно {snapshot.Memory.AvailableGb.Value:F1} ГБ",
                    $"   •   available {snapshot.Memory.AvailableGb.Value:F1} GB");
        }

        return result;
    }

    private string BuildRamConfigurationText()
    {
        if (_snapshot == null)
        {
            return SettingsService.L(
                "Оперативная память: данные пока недоступны.",
                "System memory: data is not available yet.");
        }

        MemoryDetails memory =
            _snapshot.Memory;

        return SettingsService.L(
            $"Установлено: {FormatBytes(memory.TotalBytes)}" +
            $"   •   модулей памяти: {memory.Modules.Count}",
            $"Installed: {FormatBytes(memory.TotalBytes)}" +
            $"   •   memory modules: {memory.Modules.Count}");
    }


    private void ApplyRamPlacementGuidance()
    {
        if (_ramGuidancePlacementWarningText == null)
            return;

        string? warning =
            BuildRamPlacementWarning();

        if (string.IsNullOrWhiteSpace(
                warning))
        {
            _ramGuidancePlacementWarningText.Text =
                string.Empty;

            _ramGuidancePlacementWarningText.Visibility =
                Visibility.Collapsed;

            return;
        }

        _ramGuidancePlacementWarningText.Text =
            warning;

        _ramGuidancePlacementWarningText.Visibility =
            Visibility.Visible;
    }


    private string? BuildRamPlacementWarning()
    {
        if (_snapshot == null)
            return null;

        MemoryDetails memory =
            _snapshot.Memory;

        // Срабатываем только в достаточно понятном случае:
        // 4 физических слота, установлено ровно 2 модуля,
        // а названия обоих слотов надёжно указывают на один канал.
        // Если BIOS/WMI отдаёт неоднозначные BANK/DIMM номера,
        // рекомендацию не показываем.
        if (memory.PhysicalSlotCount != 4 ||
            memory.Modules.Count != 2)
        {
            return null;
        }

        string? firstChannel =
            TryGetReliableMemoryChannel(
                memory.Modules[0].Slot);

        string? secondChannel =
            TryGetReliableMemoryChannel(
                memory.Modules[1].Slot);

        if (string.IsNullOrWhiteSpace(
                firstChannel) ||
            string.IsNullOrWhiteSpace(
                secondChannel) ||
            !string.Equals(
                firstChannel,
                secondChannel,
                StringComparison.Ordinal))
        {
            return null;
        }

        return SettingsService.L(
            "СТОИТ ПРОВЕРИТЬ УСТАНОВКУ ПАМЯТИ\n" +
            "Thermiqra видит признаки того, что два модуля оперативной памяти " +
            "могут быть установлены рядом. На платах с четырьмя разъёмами пару " +
            "модулей обычно устанавливают через один свободный разъём. " +
            "Это позволяет памяти работать эффективнее и может увеличить её пропускную способность.\n" +
            "Если модули действительно стоят рядом, при возможности переставьте один из них " +
            "через разъём. Перед перестановкой полностью выключите компьютер и отключите питание.",
            "CHECK THE MEMORY INSTALLATION\n" +
            "Thermiqra sees signs that the two system memory (RAM) modules may be installed next to each other. " +
            "On motherboards with four memory slots, a pair of modules is usually installed " +
            "with one empty slot between them. This can let memory work more efficiently " +
            "and may increase memory bandwidth.\n" +
            "If the modules are physically next to each other, move one module over by one slot " +
            "when possible. Fully shut down the computer and disconnect power before moving memory.");
    }


    private static string? TryGetReliableMemoryChannel(
        string? slot)
    {
        if (string.IsNullOrWhiteSpace(
                slot))
        {
            return null;
        }

        string normalized =
            new(
                slot
                    .Where(
                        char.IsLetterOrDigit)
                    .Select(
                        char.ToUpperInvariant)
                    .ToArray());

        if (normalized.Contains(
                "CHANNELA",
                StringComparison.Ordinal))
        {
            return "A";
        }

        if (normalized.Contains(
                "CHANNELB",
                StringComparison.Ordinal))
        {
            return "B";
        }

        bool looksLikeDimmName =
            normalized.Contains(
                "DIMM",
                StringComparison.Ordinal) ||
            normalized.Contains(
                "DDR",
                StringComparison.Ordinal) ||
            normalized.Length <= 3;

        if (!looksLikeDimmName)
            return null;

        if (normalized.EndsWith(
                "A1",
                StringComparison.Ordinal) ||
            normalized.EndsWith(
                "A2",
                StringComparison.Ordinal))
        {
            return "A";
        }

        if (normalized.EndsWith(
                "B1",
                StringComparison.Ordinal) ||
            normalized.EndsWith(
                "B2",
                StringComparison.Ordinal))
        {
            return "B";
        }

        return null;
    }

    private static string BuildRamHistoryText(
        RamHistoryEvidence? history)
    {
        if (history == null)
        {
            return SettingsService.L(
                "История загрузки памяти сейчас недоступна.",
                "Memory usage history is currently unavailable.");
        }

        bool useDay =
            history.Last24Hours.SampleCount > 0;

        StatisticsSummary summary =
            useDay
                ? history.Last24Hours
                : history.Last7Days;

        if (summary.SampleCount <= 0)
        {
            return SettingsService.L(
                "История загрузки оперативной памяти пока не накоплена.",
                "System memory (RAM) usage history has not been accumulated yet.");
        }

        string periodNameRu =
            useDay
                ? "За последние 24 часа"
                : "За последние 7 дней";

        string periodNameEn =
            useDay
                ? "Over the last 24 hours"
                : "Over the last 7 days";

        string average =
            summary.RamLoad.Average.HasValue
                ? $"{summary.RamLoad.Average.Value:F0} %"
                : "—";

        string maximum =
            summary.RamLoad.Maximum.HasValue
                ? $"{summary.RamLoad.Maximum.Value:F0} %"
                : "—";

        int highPoints =
            useDay
                ? history.DayHighPointCount
                : history.WeekHighPointCount;

        int sampledPoints =
            useDay
                ? history.DaySeriesPointCount
                : history.WeekSeriesPointCount;

        double highRatio =
            sampledPoints > 0
                ? highPoints /
                  (double)sampledPoints
                : 0;

        bool repeatedHighLoad =
            useDay
                ? highPoints >= 20 &&
                  highRatio >= 0.10
                : highPoints >= 60 &&
                  history.WeekDaysWithHighLoad >= 2 &&
                  highRatio >= 0.08;

        string assessmentRu;
        string assessmentEn;

        if (highPoints == 0)
        {
            assessmentRu =
                "Высокой загрузки памяти не наблюдалось.";

            assessmentEn =
                "No high memory usage was observed.";
        }
        else if (repeatedHighLoad)
        {
            assessmentRu =
                "Высокая загрузка памяти повторялась.";

            assessmentEn =
                "High memory usage occurred repeatedly.";
        }
        else
        {
            assessmentRu =
                "Были отдельные высокие пики, но устойчивой высокой загрузки не видно.";

            assessmentEn =
                "There were isolated high peaks, but no sustained high memory usage is visible.";
        }

        return SettingsService.L(
            $"{periodNameRu}: средняя загрузка оперативной памяти — {average}, " +
            $"максимальная — {maximum}. {assessmentRu}",
            $"{periodNameEn}: average system memory (RAM) usage was {average}, " +
            $"maximum was {maximum}. {assessmentEn}");
    }

    // ============================================================
    // АНАЛИЗ НАКОПИТЕЛЕЙ
    // ============================================================

    private void RefreshStorageSmartGuidance()
    {
        if (!_smartGuidanceInitialized ||
            _storageGuidanceStatusText == null ||
            _storageGuidanceDetailsPanel == null ||
            _storageGuidanceMeaningText == null ||
            _storageGuidanceActionText == null)
        {
            return;
        }

        HardwareSnapshot? snapshot =
            (Owner as MainWindow)?
                .GetSmartGuidanceSnapshot();

        _storageGuidanceDetailsPanel
            .Children
            .Clear();

        if (snapshot == null)
        {
            SetStorageGuidanceText(
                SettingsService.L(
                    "Недостаточно текущих данных",
                    "Not enough current data"),

                SettingsService.L(
                    "Thermiqra пока не получила свежие данные о накопителях.",
                    "Thermiqra has not yet received fresh storage data."),

                SettingsService.L(
                    "Откройте анализ позже, когда мониторинг успеет получить данные.",
                    "Open the analysis again after monitoring has had time to collect data."));

            AddStorageDetailLine(
                SettingsService.L(
                    "Температура и свободное место сейчас недоступны.",
                    "Temperature and free-space data are currently unavailable."));

            return;
        }

        List<StorageGuidanceDeviceEvidence> devices =
            snapshot.StorageDevices
                .Select(
                    BuildStorageDeviceEvidence)
                .ToList();

        List<StorageGuidanceDeviceEvidence>
            temperatureAttentionDevices =
                devices
                    .Where(
                        device =>
                            device.Level ==
                                StorageGuidanceLevel.Warm ||
                            device.Level ==
                                StorageGuidanceLevel.Warning ||
                            device.Level ==
                                StorageGuidanceLevel.Critical)
                    .ToList();

        if (temperatureAttentionDevices.Count == 0)
        {
            bool hasAnyTemperature =
                devices.Any(
                    device =>
                        device.CurrentTemperature.HasValue ||
                        device.HistoryMaximum.HasValue);

            AddStorageDetailLine(
                hasAnyTemperature
                    ? SettingsService.L(
                        "Температура накопителей: всё в норме.",
                        "Storage temperatures: everything looks normal.")
                    : SettingsService.L(
                        "Температура накопителей: данные доступны не для всех устройств.",
                        "Storage temperatures: data is not available for every device."));
        }
        else
        {
            foreach (StorageGuidanceDeviceEvidence device
                     in temperatureAttentionDevices)
            {
                AddStorageDetailLine(
                    BuildStorageDeviceLine(
                        device));
            }
        }

        List<DriveSpaceGuidance> driveSpaces =
            snapshot.Drives
                .Where(
                    drive =>
                        drive.TotalBytes > 0)
                .Select(
                    BuildDriveSpaceGuidance)
                .ToList();

        List<DriveSpaceGuidance> lowSpaceDrives =
            driveSpaces
                .Where(
                    drive =>
                        drive.Level !=
                        StorageGuidanceLevel.Normal)
                .ToList();

        if (lowSpaceDrives.Count == 0)
        {
            AddStorageDetailLine(
                SettingsService.L(
                    "Свободное место: всё в норме.",
                    "Free space: everything looks normal."));
        }
        else
        {
            foreach (DriveSpaceGuidance drive
                     in lowSpaceDrives)
            {
                AddStorageDetailLine(
                    BuildDriveSpaceLine(
                        drive));
            }
        }

        StorageGuidanceLevel temperatureLevel =
            devices.Count == 0
                ? StorageGuidanceLevel.Unknown
                : devices.Max(
                    device =>
                        device.Level);

        StorageGuidanceLevel spaceLevel =
            lowSpaceDrives.Count == 0
                ? StorageGuidanceLevel.Normal
                : lowSpaceDrives.Max(
                    drive =>
                        drive.Level);

        StorageGuidanceLevel overallLevel =
            (StorageGuidanceLevel)
            Math.Max(
                (int)temperatureLevel,
                (int)spaceLevel);

        bool hasTemperatureData =
            devices.Any(
                device =>
                    device.CurrentTemperature.HasValue ||
                    device.HistoryMaximum.HasValue);

        ApplyStorageGuidanceConclusion(
            overallLevel,
            temperatureLevel,
            spaceLevel,
            hasTemperatureData);
    }


    private StorageGuidanceDeviceEvidence
        BuildStorageDeviceEvidence(
            StorageDeviceInfo storage)
    {
        StorageGuidanceThresholds thresholds =
            GetStorageGuidanceThresholds(
                storage);

        double? historyAverage =
            null;

        double? historyMaximum =
            null;

        string? historyPeriod =
            null;

        bool repeatedWarmHistory =
            false;

        if (_statistics != null)
        {
            try
            {
                string deviceId =
                    string.IsNullOrWhiteSpace(
                        storage.Id)
                        ? storage.Name
                        : storage.Id;

                StatisticsSummary daySummary =
                    _statistics.GetSummary(
                        StatisticsPeriod.Last24Hours);

                StatisticsSummary weekSummary =
                    _statistics.GetSummary(
                        StatisticsPeriod.Last7Days);

                StorageStatisticsSummary? dayDevice =
                    FindStorageSummary(
                        daySummary,
                        deviceId,
                        storage.Name);

                StorageStatisticsSummary? weekDevice =
                    FindStorageSummary(
                        weekSummary,
                        deviceId,
                        storage.Name);

                StatisticsChartSeries daySeries =
                    _statistics.GetChartSeries(
                        StatisticsPeriod.Last24Hours,
                        StatisticsChartMetric.StorageTemperature,
                        deviceId,
                        maxPoints: 1200);

                StatisticsChartSeries weekSeries =
                    _statistics.GetChartSeries(
                        StatisticsPeriod.Last7Days,
                        StatisticsChartMetric.StorageTemperature,
                        deviceId,
                        maxPoints: 1200);

                if (dayDevice?.Temperature.Maximum.HasValue ==
                        true ||
                    daySeries.Points.Count > 0)
                {
                    historyAverage =
                        dayDevice?
                            .Temperature
                            .Average;

                    historyMaximum =
                        dayDevice?
                            .Temperature
                            .Maximum;

                    historyPeriod =
                        SettingsService.L(
                            "24 часа",
                            "24 hours");

                    repeatedWarmHistory =
                        HasRepeatedStorageHeat(
                            daySeries.Points,
                            thresholds.Warning);
                }
                else if (weekDevice?.Temperature.Maximum.HasValue ==
                             true ||
                         weekSeries.Points.Count > 0)
                {
                    historyAverage =
                        weekDevice?
                            .Temperature
                            .Average;

                    historyMaximum =
                        weekDevice?
                            .Temperature
                            .Maximum;

                    historyPeriod =
                        SettingsService.L(
                            "7 дней",
                            "7 days");

                    repeatedWarmHistory =
                        HasRepeatedStorageHeat(
                            weekSeries.Points,
                            thresholds.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Не удалось получить данные Smart Guidance для накопителя {storage.Name}: {ex}");
            }
        }

        StorageGuidanceLevel level =
            GetStorageTemperatureLevel(
                storage.Temperature,
                historyMaximum,
                repeatedWarmHistory,
                thresholds);

        return new StorageGuidanceDeviceEvidence
        {
            Name =
                string.IsNullOrWhiteSpace(
                    storage.Name)
                    ? SettingsService.L(
                        "Накопитель",
                        "Storage device")
                    : storage.Name,

            HumanType =
                GetStorageHumanType(
                    storage),

            CurrentTemperature =
                storage.Temperature,

            HistoryAverage =
                historyAverage,

            HistoryMaximum =
                historyMaximum,

            HistoryPeriod =
                historyPeriod,

            Level =
                level
        };
    }


    private static StorageStatisticsSummary?
        FindStorageSummary(
            StatisticsSummary summary,
            string deviceId,
            string deviceName)
    {
        return summary.StorageDevices
            .FirstOrDefault(
                item =>
                    string.Equals(
                        item.DeviceId,
                        deviceId,
                        StringComparison.OrdinalIgnoreCase)) ??
            summary.StorageDevices
                .FirstOrDefault(
                    item =>
                        string.Equals(
                            item.DeviceName,
                            deviceName,
                            StringComparison.OrdinalIgnoreCase));
    }


    private static bool HasRepeatedStorageHeat(
        IReadOnlyList<StatisticsChartPoint> points,
        double warningTemperature)
    {
        if (points.Count < 20)
            return false;

        int highCount =
            points.Count(
                point =>
                    point.Value >=
                    warningTemperature);

        if (highCount < 5)
            return false;

        double ratio =
            highCount /
            (double)points.Count;

        return ratio >= 0.10;
    }


    private static StorageGuidanceLevel
        GetStorageTemperatureLevel(
            double? currentTemperature,
            double? historyMaximum,
            bool repeatedWarmHistory,
            StorageGuidanceThresholds thresholds)
    {
        if (currentTemperature.HasValue)
        {
            double current =
                currentTemperature.Value;

            if (current >=
                thresholds.Critical)
            {
                return StorageGuidanceLevel.Critical;
            }

            if (current >=
                thresholds.Warning)
            {
                return StorageGuidanceLevel.Warning;
            }

            if (current >=
                thresholds.Warning - 5)
            {
                return StorageGuidanceLevel.Warm;
            }
        }

        if (repeatedWarmHistory)
        {
            return StorageGuidanceLevel.Warm;
        }

        if (historyMaximum.HasValue &&
            historyMaximum.Value >=
                thresholds.Warning - 5)
        {
            return StorageGuidanceLevel.Warm;
        }

        if (!currentTemperature.HasValue &&
            !historyMaximum.HasValue)
        {
            return StorageGuidanceLevel.Unknown;
        }

        return StorageGuidanceLevel.Normal;
    }


    private static StorageGuidanceThresholds
        GetStorageGuidanceThresholds(
            StorageDeviceInfo storage)
    {
        AppSettings settings =
            SettingsService.Current;

        if (!settings
                .AutomaticStorageTemperatureThresholdsEnabled)
        {
            return new StorageGuidanceThresholds(
                settings.StorageWarningTemperature,
                settings.StorageCriticalTemperature);
        }

        string identifier =
            storage.Id ??
            string.Empty;

        if (identifier.Contains(
                "nvme",
                StringComparison.OrdinalIgnoreCase))
        {
            return new StorageGuidanceThresholds(
                70,
                80);
        }

        if (identifier.Contains(
                "ssd",
                StringComparison.OrdinalIgnoreCase))
        {
            return new StorageGuidanceThresholds(
                60,
                70);
        }

        if (identifier.Contains(
                "hdd",
                StringComparison.OrdinalIgnoreCase))
        {
            return new StorageGuidanceThresholds(
                55,
                60);
        }

        return new StorageGuidanceThresholds(
            65,
            80);
    }


    private static string GetStorageHumanType(
        StorageDeviceInfo storage)
    {
        string identifier =
            storage.Id ??
            string.Empty;

        if (identifier.Contains(
                "nvme",
                StringComparison.OrdinalIgnoreCase))
        {
            return SettingsService.L(
                "Твердотельный накопитель (SSD, NVMe)",
                "Solid-state drive (SSD, NVMe)");
        }

        if (identifier.Contains(
                "ssd",
                StringComparison.OrdinalIgnoreCase))
        {
            return SettingsService.L(
                "Твердотельный накопитель (SSD)",
                "Solid-state drive (SSD)");
        }

        if (identifier.Contains(
                "hdd",
                StringComparison.OrdinalIgnoreCase))
        {
            return SettingsService.L(
                "Обычный жёсткий диск (HDD)",
                "Hard disk drive (HDD)");
        }

        return SettingsService.L(
            "Накопитель данных",
            "Storage device");
    }


    private static string BuildStorageDeviceLine(
        StorageGuidanceDeviceEvidence device)
    {
        List<string> parts =
            new();

        if (device.CurrentTemperature.HasValue)
        {
            parts.Add(
                SettingsService.L(
                    $"сейчас {device.CurrentTemperature.Value:F0} °C",
                    $"now {device.CurrentTemperature.Value:F0} °C"));
        }

        if (device.HistoryMaximum.HasValue)
        {
            string period =
                string.IsNullOrWhiteSpace(
                    device.HistoryPeriod)
                    ? SettingsService.L(
                        "в истории",
                        "in history")
                    : SettingsService.L(
                        $"за {device.HistoryPeriod}",
                        $"over {device.HistoryPeriod}");

            parts.Add(
                SettingsService.L(
                    $"максимум {period} — {device.HistoryMaximum.Value:F0} °C",
                    $"maximum {period} — {device.HistoryMaximum.Value:F0} °C"));
        }

        string state =
            device.Level switch
            {
                StorageGuidanceLevel.Critical =>
                    SettingsService.L(
                        "температура слишком высокая",
                        "temperature is too high"),

                StorageGuidanceLevel.Warning =>
                    SettingsService.L(
                        "накопитель заметно нагрет",
                        "the drive is running hot"),

                StorageGuidanceLevel.Warm =>
                    SettingsService.L(
                        "нагрев заметный, но температура остаётся в допустимом диапазоне",
                        "heat is noticeable, but temperature remains within an acceptable range"),

                StorageGuidanceLevel.Normal =>
                    SettingsService.L(
                        "температура нормальная",
                        "temperature is normal"),

                _ =>
                    SettingsService.L(
                        "температура не определяется",
                        "temperature is not available")
            };

        string readings =
            parts.Count > 0
                ? string.Join(
                    "; ",
                    parts)
                : SettingsService.L(
                    "нет данных о температуре",
                    "no temperature data");

        return
            $"{device.HumanType}: {device.Name} — {readings}. {state}.";
    }


    private static DriveSpaceGuidance
        BuildDriveSpaceGuidance(
            LogicalDriveInfo drive)
    {
        double usedPercent =
            drive.TotalBytes > 0
                ? (drive.TotalBytes -
                   drive.FreeBytes) /
                  (double)drive.TotalBytes *
                  100.0
                : 0;

        AppSettings settings =
            SettingsService.Current;

        StorageGuidanceLevel level =
            usedPercent >=
                settings.DiskCriticalPercent
                ? StorageGuidanceLevel.Critical
                : usedPercent >=
                    settings.DiskWarningPercent
                    ? StorageGuidanceLevel.Warning
                    : StorageGuidanceLevel.Normal;

        return new DriveSpaceGuidance
        {
            Name =
                FormatDriveName(
                    drive.Name),

            FreeBytes =
                drive.FreeBytes,

            FreePercent =
                Math.Clamp(
                    100.0 - usedPercent,
                    0,
                    100),

            Level =
                level
        };
    }


    private static string BuildDriveSpaceLine(
        DriveSpaceGuidance drive)
    {
        string state =
            drive.Level ==
                StorageGuidanceLevel.Critical
                ? SettingsService.L(
                    "свободного места почти не осталось",
                    "almost no free space remains")
                : SettingsService.L(
                    "свободного места осталось мало",
                    "free space is getting low");

        return SettingsService.L(
            $"Диск {drive.Name}: свободно {drive.FreePercent:F0} % " +
            $"({FormatStorageBytes(drive.FreeBytes)}). {state}.",
            $"Drive {drive.Name}: {drive.FreePercent:F0}% free " +
            $"({FormatStorageBytes(drive.FreeBytes)}). {state}.");
    }


    private static string FormatDriveName(
        string name)
    {
        if (string.IsNullOrWhiteSpace(
                name))
        {
            return "—";
        }

        return name
            .Trim()
            .TrimEnd(
                '\\',
                '/');
    }


    private static string FormatStorageBytes(
        long bytes)
    {
        if (bytes <= 0)
        {
            return SettingsService.L(
                "0 ГБ",
                "0 GB");
        }

        double gigabytes =
            bytes /
            1024d /
            1024d /
            1024d;

        if (gigabytes >= 1024)
        {
            return SettingsService.L(
                $"{gigabytes / 1024d:F1} ТБ",
                $"{gigabytes / 1024d:F1} TB");
        }

        return SettingsService.L(
            $"{gigabytes:F1} ГБ",
            $"{gigabytes:F1} GB");
    }


    private void ApplyStorageGuidanceConclusion(
        StorageGuidanceLevel overallLevel,
        StorageGuidanceLevel temperatureLevel,
        StorageGuidanceLevel spaceLevel,
        bool hasTemperatureData)
    {
        bool criticalTemperature =
            temperatureLevel ==
            StorageGuidanceLevel.Critical;

        bool warningTemperature =
            temperatureLevel ==
            StorageGuidanceLevel.Warning;

        bool warmTemperature =
            temperatureLevel ==
            StorageGuidanceLevel.Warm;

        bool criticalSpace =
            spaceLevel ==
            StorageGuidanceLevel.Critical;

        bool warningSpace =
            spaceLevel ==
            StorageGuidanceLevel.Warning;

        if (criticalTemperature &&
            criticalSpace)
        {
            SetStorageGuidanceText(
                SettingsService.L(
                    "Накопители требуют внимания",
                    "Storage needs attention"),

                SettingsService.L(
                    "Одновременно обнаружены слишком высокая температура накопителя " +
                    "и очень малый запас свободного места на одном из дисков.",
                    "Both an excessively high storage temperature and critically low free space " +
                    "have been detected."),

                SettingsService.L(
                    "Сначала снизьте нагрузку и проверьте охлаждение накопителя. " +
                    "Затем освободите место на переполненном диске. " +
                    "На главном экране Thermiqra рядом с диском можно нажать «Анализ».",
                    "First reduce load and check the drive cooling. " +
                    "Then free space on the nearly full drive. " +
                    "On the Thermiqra main screen, use “Analyze” next to the drive."));

            return;
        }

        if (criticalTemperature)
        {
            SetStorageGuidanceText(
                SettingsService.L(
                    "Температура одного из накопителей слишком высокая",
                    "One storage device is too hot"),

                SettingsService.L(
                    "Температура достигла уровня, при котором длительная работа нежелательна. " +
                    "Thermiqra учитывает тип накопителя, а не применяет один порог ко всем устройствам.",
                    "Temperature has reached a level where prolonged operation is undesirable. " +
                    "Thermiqra accounts for the storage type instead of using one threshold for every device."),

                SettingsService.L(
                    "Снизьте нагрузку и проверьте охлаждение в области накопителя: " +
                    "пыль, обдув и, если установлен радиатор, его прилегание. " +
                    "Если температура не снижается, не оставляйте накопитель под длительной высокой нагрузкой.",
                    "Reduce load and check cooling around the drive: " +
                    "dust, airflow and, if a heatsink is installed, its contact. " +
                    "If temperature does not drop, avoid prolonged heavy load on the drive."));

            return;
        }

        if (criticalSpace)
        {
            SetStorageGuidanceText(
                SettingsService.L(
                    "На одном из дисков почти не осталось свободного места",
                    "One drive is almost out of free space"),

                SettingsService.L(
                    "Когда диск почти заполнен, Windows и программы могут работать хуже, " +
                    "а обновлениям и временным файлам может не хватать места.",
                    "When a drive is nearly full, Windows and applications can work less smoothly, " +
                    "and updates or temporary files may run out of room."),

                SettingsService.L(
                    "Освободите место. На главном экране Thermiqra рядом с нужным диском " +
                    "нажмите «Анализ», чтобы увидеть, какие папки и файлы занимают больше всего места.",
                    "Free some space. On the Thermiqra main screen, use “Analyze” next to that drive " +
                    "to see which folders and files occupy the most space."));

            return;
        }

        if (warningTemperature)
        {
            SetStorageGuidanceText(
                SettingsService.L(
                    "Один из накопителей заметно нагрет",
                    "One storage device is running hot"),

                SettingsService.L(
                    "Температура высокая для этого типа накопителя, но это ещё не означает неисправность. " +
                    "Важно, держится ли такой нагрев долго и повторяется ли он под обычной нагрузкой.",
                    "Temperature is high for this type of storage device, but this does not by itself indicate a fault. " +
                    "What matters is whether this heat lasts and keeps returning under normal use."),

                SettingsService.L(
                    "Если температура долго держится высокой, проверьте пыль и обдув в корпусе. " +
                    "Если накопитель установлен под радиатором, убедитесь, что радиатор установлен нормально.",
                    "If the temperature stays high for a long time, check dust and airflow in the case. " +
                    "If the drive uses a heatsink, make sure the heatsink is properly installed."));

            return;
        }

        if (warningSpace)
        {
            SetStorageGuidanceText(
                SettingsService.L(
                    "На одном из дисков мало свободного места",
                    "One drive is low on free space"),

                SettingsService.L(
                    "Диск уже близок к заполнению. Это не авария, но запас места лучше увеличить " +
                    "до того, как он закончится.",
                    "The drive is getting close to full. This is not an emergency, " +
                    "but it is better to increase the free-space margin before it runs out."),

                SettingsService.L(
                    "На главном экране Thermiqra рядом с этим диском нажмите «Анализ», " +
                    "чтобы быстро увидеть самые крупные папки и файлы.",
                    "On the Thermiqra main screen, use “Analyze” next to that drive " +
                    "to quickly see the largest folders and files."));

            return;
        }

        if (warmTemperature)
        {
            SetStorageGuidanceText(
                SettingsService.L(
                    "Накопитель нагревался заметно, но остаётся в допустимом диапазоне",
                    "A storage device ran noticeably warm but remains in an acceptable range"),

                SettingsService.L(
                    "Для этого типа накопителя зафиксированный нагрев ещё находится в допустимом рабочем диапазоне. " +
                    "Сам по себе такой максимум не является критическим состоянием.",
                    "For this type of storage device, the observed heat is still within an acceptable operating range. " +
                    "That maximum by itself is not a critical condition."),

                SettingsService.L(
                    "Срочных действий не требуется. Если температура часто держится близко к верхней границе " +
                    "во время длительной нагрузки, проверьте радиатор и обдув.",
                    "No urgent action is required. If temperature often stays close to the upper end " +
                    "during sustained load, check the heatsink and airflow."));

            return;
        }

        if (!hasTemperatureData)
        {
            SetStorageGuidanceText(
                SettingsService.L(
                    "Свободное место в норме",
                    "Free space looks normal"),

                SettingsService.L(
                    "Проблем со свободным местом не видно. Температуру некоторых накопителей " +
                    "оборудование может не предоставлять.",
                    "No free-space issue is visible. Some storage devices may not expose temperature data."),

                SettingsService.L(
                    "Ничего делать не требуется.",
                    "No action is required."));

            return;
        }

        SetStorageGuidanceText(
            SettingsService.L(
                "С накопителями всё выглядит нормально",
                "Storage looks normal"),

            SettingsService.L(
                "Температуры накопителей и запас свободного места сейчас не требуют внимания.",
                "Storage temperatures and available free space do not currently require attention."),

            SettingsService.L(
                "Ничего делать не требуется.",
                "No action is required."));
    }


    // ============================================================
    // ЧТО МОЖНО УЛУЧШИТЬ
    // ============================================================

    private void RefreshUpgradeGuidance()
    {
        if (!_smartGuidanceInitialized ||
            _upgradeGuidanceStatusText == null ||
            _upgradeGuidanceDetailsPanel == null ||
            _upgradeGuidanceNoteText == null)
        {
            return;
        }

        _upgradeGuidanceDetailsPanel
            .Children
            .Clear();

        _upgradeGuidanceNoteText.Text =
            string.Empty;

        _upgradeGuidanceNoteText.Visibility =
            Visibility.Collapsed;

        HardwareSnapshot? snapshot =
            (Owner as MainWindow)?
                .GetSmartGuidanceSnapshot();

        if (snapshot == null)
        {
            _upgradeGuidanceStatusText.Text =
                SettingsService.L(
                    "Нужно немного времени",
                    "A little more time is needed");

            TextBlock waitingText =
                CreateGuidanceValueText();

            waitingText.Text =
                SettingsService.L(
                    "Thermiqra ещё собирает данные для рекомендации.",
                    "Thermiqra is still collecting data for a recommendation.");

            _upgradeGuidanceDetailsPanel
                .Children
                .Add(
                    waitingText);

            return;
        }

        int recommendationCount =
            0;

        if (HasRecurringHighRamLoadForUpgrade())
        {
            AddUpgradeRecommendation(
                SettingsService.L(
                    "Оперативной памяти может не хватать",
                    "System memory may be insufficient"),

                SettingsService.L(
                    "Высокая загрузка оперативной памяти (RAM) повторяется. " +
                    "Сначала посмотрите в Диспетчере задач, какие программы используют больше всего памяти. " +
                    "Если это происходит регулярно при вашей обычной работе, увеличение объёма оперативной памяти может помочь.",
                    "High system-memory (RAM) usage keeps recurring. " +
                    "First check Task Manager to see which applications use the most memory. " +
                    "If this happens regularly during your normal work, adding more memory may help."));

            recommendationCount++;
        }

        CpuGpuUpgradeResult cpuGpuResult =
            GetCpuGpuUpgradeResult();

        if (cpuGpuResult.Outcome ==
            CpuGpuUpgradeOutcome.Cpu)
        {
            AddUpgradeRecommendation(
                SettingsService.L(
                    "Процессор может быть главным ограничением",
                    "The processor may be the main limitation"),

                BuildCpuUpgradeRecommendationText(
                    cpuGpuResult));

            recommendationCount++;
        }
        else if (cpuGpuResult.Outcome ==
                 CpuGpuUpgradeOutcome.CpuCooling)
        {
            AddUpgradeRecommendation(
                SettingsService.L(
                    "Сначала стоит проверить охлаждение процессора",
                    "Check processor cooling first"),

                BuildCpuCoolingRecommendationText(
                    cpuGpuResult));

            recommendationCount++;
        }
        else if (cpuGpuResult.Outcome ==
                 CpuGpuUpgradeOutcome.GpuCooling)
        {
            AddUpgradeRecommendation(
                SettingsService.L(
                    "Сначала стоит проверить охлаждение видеокарты",
                    "Check graphics-card cooling first"),

                BuildGpuCoolingRecommendationText(
                    cpuGpuResult));

            recommendationCount++;
        }

        LogicalDriveInfo? systemDrive =
            FindWindowsLogicalDrive(
                snapshot);

        SystemDriveHardwareEvidence
            hardwareEvidence =
                ReadSystemDriveHardwareEvidence(
                    snapshot);

        if (hardwareEvidence.Kind ==
            PhysicalStorageKind.Hdd)
        {
            if (hardwareEvidence.HasOtherSsd)
            {
                AddUpgradeRecommendation(
                    SettingsService.L(
                        "Windows работает с обычного жёсткого диска (HDD)",
                        "Windows is running from a hard disk drive (HDD)"),

                    SettingsService.L(
                        "В компьютере уже есть более быстрый твердотельный накопитель (SSD). " +
                        "Если на нём достаточно места, перенос Windows на него обычно ускоряет включение компьютера и запуск программ.",
                        "A faster solid-state drive (SSD) is already installed in the computer. " +
                        "If it has enough space, moving Windows to it usually speeds up computer startup and application launch."));
            }
            else
            {
                AddUpgradeRecommendation(
                    SettingsService.L(
                        "Windows работает с обычного жёсткого диска (HDD)",
                        "Windows is running from a hard disk drive (HDD)"),

                    SettingsService.L(
                        "Для Windows обычно быстрее твердотельный накопитель (SSD). " +
                        "Он может заметно ускорить включение компьютера и запуск программ. " +
                        "Если текущая скорость вас устраивает, срочности нет.",
                        "A solid-state drive (SSD) is usually faster for Windows. " +
                        "It can noticeably speed up computer startup and application launch. " +
                        "If you are happy with the current speed, there is no urgency."));
            }

            recommendationCount++;
        }

        if (systemDrive != null &&
            ShouldSuggestLargerSystemDrive(
                systemDrive))
        {
            double totalGb =
                systemDrive.TotalBytes /
                1024d /
                1024d /
                1024d;

            double freeGb =
                systemDrive.FreeBytes /
                1024d /
                1024d /
                1024d;

            AddUpgradeRecommendation(
                SettingsService.L(
                    "На диске Windows становится мало места",
                    "The Windows drive is running low on space"),

                SettingsService.L(
                    $"Свободно примерно {freeGb:F0} ГБ из {totalGb:F0} ГБ. " +
                    "Сначала откройте «Анализ» этого диска и посмотрите, что занимает место. " +
                    "Если после очистки свободное место быстро снова заканчивается, может быть удобнее более вместительный диск для Windows.",
                    $"About {freeGb:F0} GB is free out of {totalGb:F0} GB. " +
                    "First open Analyze for this drive and see what is using the space. " +
                    "If free space quickly runs low again after cleanup, a larger Windows drive may be more convenient."));

            recommendationCount++;
        }

        if (recommendationCount == 0)
        {
            _upgradeGuidanceStatusText.Text =
                SettingsService.L(
                    "Сейчас явных рекомендаций нет",
                    "No clear recommendations right now");

            TextBlock summaryText =
                CreateGuidanceValueText();

            summaryText.Text =
                cpuGpuResult.HasEnoughHistory
                    ? SettingsService.L(
                        "По имеющимся данным явного слабого места в памяти, диске, процессоре или видеокарте не видно.",
                        "The available data does not show a clear weak point in memory, storage, processor, or graphics card.")
                    : SettingsService.L(
                        "По памяти и диску явных проблем не видно. Для оценки процессора и видеокарты Thermiqra сейчас накапливает новую историю с учётом стресс-тестов. Старые данные остаются в статистике, но не используются для советов по апгрейду.",
                        "No clear memory or storage issue is visible. Thermiqra is now building a new processor and graphics-card history that accounts for stress tests. Older data remains in Statistics but is not used for upgrade advice.");

            _upgradeGuidanceDetailsPanel
                .Children
                .Add(
                    summaryText);
        }
        else
        {
            _upgradeGuidanceStatusText.Text =
                recommendationCount == 1
                    ? SettingsService.L(
                        "Есть одна рекомендация",
                        "There is one recommendation")
                    : SettingsService.L(
                        "Есть несколько рекомендаций",
                        "There are several recommendations");
        }
    }


    private void AddUpgradeRecommendation(
        string title,
        string text)
    {
        if (_upgradeGuidanceDetailsPanel == null)
            return;

        StackPanel block =
            new();

        if (_upgradeGuidanceDetailsPanel
                .Children
                .Count > 0)
        {
            block.Margin =
                new Thickness(
                    0,
                    14,
                    0,
                    0);
        }

        TextBlock titleText =
            CreateGuidanceText(
                title,
                13,
                FontWeights.SemiBold,
                "AccentBrush");

        TextBlock valueText =
            CreateGuidanceValueText();

        valueText.Text =
            text;

        valueText.Margin =
            new Thickness(
                0,
                5,
                0,
                0);

        block.Children.Add(
            titleText);

        block.Children.Add(
            valueText);

        _upgradeGuidanceDetailsPanel
            .Children
            .Add(
                block);
    }


    private bool HasRecurringHighRamLoadForUpgrade()
    {
        if (_statistics == null)
            return false;

        try
        {
            StatisticsChartSeries daySeries =
                _statistics.GetAdvisorRamLoadSeries(
                    StatisticsPeriod.Last24Hours,
                    1200);

            StatisticsChartSeries weekSeries =
                _statistics.GetAdvisorRamLoadSeries(
                    StatisticsPeriod.Last7Days,
                    1200);

            List<StatisticsChartPoint> dayPoints =
                daySeries.Points;

            List<StatisticsChartPoint> weekPoints =
                weekSeries.Points;

            int dayHighCount =
                dayPoints.Count(
                    point =>
                        point.Value >= 85);

            int weekHighCount =
                weekPoints.Count(
                    point =>
                        point.Value >= 85);

            int weekDaysWithSamples =
                weekPoints
                    .Select(
                        point =>
                            point.TimestampUtc
                                .ToLocalTime()
                                .Date)
                    .Distinct()
                    .Count();

            int weekDaysWithHighLoad =
                weekPoints
                    .Where(
                        point =>
                            point.Value >= 85)
                    .Select(
                        point =>
                            point.TimestampUtc
                                .ToLocalTime()
                                .Date)
                    .Distinct()
                    .Count();

            bool enoughDayHistory =
                dayPoints.Count >= 120;

            bool enoughWeekHistory =
                weekPoints.Count >= 240 &&
                weekDaysWithSamples >= 2;

            double dayHighRatio =
                dayPoints.Count > 0
                    ? dayHighCount /
                      (double)dayPoints.Count
                    : 0;

            double weekHighRatio =
                weekPoints.Count > 0
                    ? weekHighCount /
                      (double)weekPoints.Count
                    : 0;

            bool repeatedHighLoad24Hours =
                enoughDayHistory &&
                dayHighCount >= 20 &&
                dayHighRatio >= 0.10;

            bool repeatedHighLoad7Days =
                enoughWeekHistory &&
                weekHighCount >= 60 &&
                weekDaysWithHighLoad >= 2 &&
                weekHighRatio >= 0.08;

            return
                repeatedHighLoad24Hours ||
                repeatedHighLoad7Days;
        }
        catch
        {
            return false;
        }
    }


    private CpuGpuUpgradeResult GetCpuGpuUpgradeResult()
    {

        if (_statistics == null)
        {
            return new CpuGpuUpgradeResult();
        }

        try
        {
            AdvisorPerformanceHistory history =
                _statistics.GetAdvisorPerformanceHistory(
                    StatisticsPeriod.Last7Days);

            AdvisorGpuPerformanceSeries? activeGpu =
                history.Gpus
                    .Where(
                        gpu =>
                            gpu.Points.Count > 0)
                    .OrderByDescending(
                        gpu =>
                            gpu.Points.Count(
                                point =>
                                    point.GpuLoad >= 40))
                    .ThenByDescending(
                        gpu =>
                            gpu.Points.Average(
                                point =>
                                    point.GpuLoad))
                    .FirstOrDefault();

            if (activeGpu == null)
            {
                return new CpuGpuUpgradeResult();
            }

            List<AdvisorPerformancePoint> points =
                activeGpu.Points
                    .OrderBy(
                        point =>
                            point.TimestampUtc)
                    .ToList();

            int daysWithSamples =
                points
                    .Select(
                        point =>
                            point.TimestampUtc
                                .ToLocalTime()
                                .Date)
                    .Distinct()
                    .Count();

            bool enoughHistory =
                points.Count >= 180 &&
                daysWithSamples >= 2;

            if (!enoughHistory)
            {
                return new CpuGpuUpgradeResult
                {
                    HasEnoughHistory =
                        false
                };
            }

            List<AdvisorPerformancePoint>
                cpuLimitedPoints =
                    points
                        .Where(
                            point =>
                                point.CpuLoad >= 90 &&
                                point.GpuLoad <= 70)
                        .ToList();

            List<AdvisorPerformancePoint>
                gpuLimitedPoints =
                    points
                        .Where(
                            point =>
                                point.GpuLoad >= 95 &&
                                point.CpuLoad <= 80)
                        .ToList();

            List<AdvisorPerformancePoint>
                bothHighPoints =
                    points
                        .Where(
                            point =>
                                point.CpuLoad >= 90 &&
                                point.GpuLoad >= 90)
                        .ToList();

            bool recurringCpuPattern =
                IsRecurringPerformancePattern(
                    points,
                    point =>
                        point.CpuLoad >= 90 &&
                        point.GpuLoad <= 70,
                    cpuLimitedPoints);

            bool recurringGpuPattern =
                IsRecurringPerformancePattern(
                    points,
                    point =>
                        point.GpuLoad >= 95 &&
                        point.CpuLoad <= 80,
                    gpuLimitedPoints);

            bool recurringBothHigh =
                IsRecurringPerformancePattern(
                    points,
                    point =>
                        point.CpuLoad >= 90 &&
                        point.GpuLoad >= 90,
                    bothHighPoints);

            if (recurringBothHigh ||
                (recurringCpuPattern &&
                 recurringGpuPattern))
            {
                return new CpuGpuUpgradeResult
                {
                    Outcome =
                        CpuGpuUpgradeOutcome.Ambiguous,

                    HasEnoughHistory =
                        true
                };
            }

            AppSettings settings =
                SettingsService.Current;

            if (recurringCpuPattern)
            {
                bool thermalConcern =
                    HasThermalConcern(
                        cpuLimitedPoints,
                        point =>
                            point.CpuTemperature,
                        settings.CpuWarningTemperature);

                AdvisorEvidenceSpan evidenceSpan =
                    _statistics.GetAdvisorEvidenceSpan(
                        activeGpu.DeviceId,
                        thermalConcern
                            ? AdvisorEvidenceKind.CpuCooling
                            : AdvisorEvidenceKind.CpuLimited,
                        settings.CpuWarningTemperature);

                return new CpuGpuUpgradeResult
                {
                    Outcome =
                        thermalConcern
                            ? CpuGpuUpgradeOutcome.CpuCooling
                            : CpuGpuUpgradeOutcome.Cpu,

                    HasEnoughHistory =
                        true,

                    EvidenceSpanDays =
                        evidenceSpan.HasEvidence
                            ? evidenceSpan.CalendarSpanDays
                            : GetFallbackEvidenceSpanDays(
                                cpuLimitedPoints),

                    CpuPeakLoad =
                        cpuLimitedPoints.Max(
                            point =>
                                point.CpuLoad),

                    CpuPeakTemperature =
                        GetMaximumTemperature(
                            cpuLimitedPoints,
                            point =>
                                point.CpuTemperature),

                    GpuAverageLoadDuringCpuPattern =
                        cpuLimitedPoints.Average(
                            point =>
                                point.GpuLoad)
                };
            }

            if (recurringGpuPattern)
            {
                bool thermalConcern =
                    HasThermalConcern(
                        gpuLimitedPoints,
                        point =>
                            point.GpuTemperature,
                        settings.GpuWarningTemperature);

                AdvisorEvidenceSpan evidenceSpan =
                    thermalConcern
                        ? _statistics.GetAdvisorEvidenceSpan(
                            activeGpu.DeviceId,
                            AdvisorEvidenceKind.GpuCooling,
                            settings.GpuWarningTemperature)
                        : new AdvisorEvidenceSpan();

                return new CpuGpuUpgradeResult
                {
                    Outcome =
                        thermalConcern
                            ? CpuGpuUpgradeOutcome.GpuCooling
                            : CpuGpuUpgradeOutcome.None,

                    HasEnoughHistory =
                        true,

                    EvidenceSpanDays =
                        thermalConcern &&
                        evidenceSpan.HasEvidence
                            ? evidenceSpan.CalendarSpanDays
                            : GetFallbackEvidenceSpanDays(
                                gpuLimitedPoints),

                    GpuPeakLoad =
                        gpuLimitedPoints.Max(
                            point =>
                                point.GpuLoad),

                    GpuPeakTemperature =
                        GetMaximumTemperature(
                            gpuLimitedPoints,
                            point =>
                                point.GpuTemperature),

                    CpuAverageLoadDuringGpuPattern =
                        gpuLimitedPoints.Average(
                            point =>
                                point.CpuLoad)
                };
            }

            return new CpuGpuUpgradeResult
            {
                Outcome =
                    CpuGpuUpgradeOutcome.None,

                HasEnoughHistory =
                    true
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Не удалось оценить CPU/GPU для Upgrade Advisor: {ex}");

            return new CpuGpuUpgradeResult();
        }
    }


    private static bool IsRecurringPerformancePattern(
        IReadOnlyList<AdvisorPerformancePoint> allPoints,
        Func<AdvisorPerformancePoint, bool> predicate,
        IReadOnlyList<AdvisorPerformancePoint> matchingPoints)
    {
        if (matchingPoints.Count < 30)
            return false;

        int daysWithPattern =
            matchingPoints
                .Select(
                    point =>
                        point.TimestampUtc
                            .ToLocalTime()
                            .Date)
                .Distinct()
                .Count();

        if (daysWithPattern < 2)
            return false;

        int sustainedEpisodes =
            CountSustainedEpisodes(
                allPoints,
                predicate,
                5);

        return sustainedEpisodes >= 2;
    }


    private static int CountSustainedEpisodes(
        IReadOnlyList<AdvisorPerformancePoint> points,
        Func<AdvisorPerformancePoint, bool> predicate,
        int minimumPoints)
    {
        int episodeCount =
            0;

        int currentCount =
            0;

        DateTimeOffset? previousTimestamp =
            null;

        foreach (AdvisorPerformancePoint point in points)
        {
            bool closeToPrevious =
                !previousTimestamp.HasValue ||
                point.TimestampUtc -
                previousTimestamp.Value <=
                    TimeSpan.FromMinutes(2.5);

            if (predicate(
                    point))
            {
                if (!closeToPrevious)
                {
                    if (currentCount >=
                        minimumPoints)
                    {
                        episodeCount++;
                    }

                    currentCount =
                        0;
                }

                currentCount++;
            }
            else
            {
                if (currentCount >=
                    minimumPoints)
                {
                    episodeCount++;
                }

                currentCount =
                    0;
            }

            previousTimestamp =
                point.TimestampUtc;
        }

        if (currentCount >=
            minimumPoints)
        {
            episodeCount++;
        }

        return episodeCount;
    }


    private static int GetFallbackEvidenceSpanDays(
        IReadOnlyList<AdvisorPerformancePoint> points)
    {
        if (points.Count == 0)
            return 1;

        DateTime firstDate =
            points
                .Min(
                    point =>
                        point.TimestampUtc)
                .ToLocalTime()
                .Date;

        DateTime lastDate =
            points
                .Max(
                    point =>
                        point.TimestampUtc)
                .ToLocalTime()
                .Date;

        return Math.Max(
            1,
            (lastDate -
             firstDate).Days +
            1);
    }


    private static double? GetMaximumTemperature(
        IReadOnlyList<AdvisorPerformancePoint> points,
        Func<AdvisorPerformancePoint, double?> selector)
    {
        List<double> values =
            points
                .Select(
                    selector)
                .Where(
                    value =>
                        value.HasValue)
                .Select(
                    value =>
                        value!.Value)
                .ToList();

        return values.Count == 0
            ? null
            : values.Max();
    }


    private static string BuildCpuUpgradeRecommendationText(
        CpuGpuUpgradeResult result)
    {
        string span =
            FormatObservedSpan(
                result.EvidenceSpanDays);

        string loadText =
            result.CpuPeakLoad.HasValue
                ? $"{result.CpuPeakLoad.Value:F0}%"
                : "—";

        string gpuText =
            result.GpuAverageLoadDuringCpuPattern.HasValue
                ? $"{result.GpuAverageLoadDuringCpuPattern.Value:F0}%"
                : "—";

        string temperatureText =
            result.CpuPeakTemperature.HasValue
                ? $"{result.CpuPeakTemperature.Value:F0} °C"
                : "—";

        return SettingsService.L(
            $"На протяжении {span} повторяются длительные периоды высокой загрузки процессора " +
            $"(до {loadText}); видеокарта в такие моменты используется примерно на {gpuText}. " +
            $"Температура процессора доходила до {temperatureText}, признаков устойчивого перегрева не видно. " +
            "Если именно в эти моменты компьютер работает медленно, более производительный процессор может дать заметный эффект.",

            $"Over {span}, there have been repeated sustained periods of high processor load " +
            $"(up to {loadText}); the graphics card was used at about {gpuText} during those periods. " +
            $"Processor temperature reached {temperatureText}, with no sign of persistent overheating. " +
            "If the computer feels slow specifically at those times, a faster processor may make a noticeable difference.");
    }


    private static string BuildCpuCoolingRecommendationText(
        CpuGpuUpgradeResult result)
    {
        string span =
            FormatObservedSpan(
                result.EvidenceSpanDays);

        string loadText =
            result.CpuPeakLoad.HasValue
                ? $"{result.CpuPeakLoad.Value:F0}%"
                : "—";

        string temperatureText =
            result.CpuPeakTemperature.HasValue
                ? $"{result.CpuPeakTemperature.Value:F0} °C"
                : "—";

        return SettingsService.L(
            $"На протяжении {span} высокая загрузка процессора повторяется вместе с повышенной температурой " +
            $"(нагрузка до {loadText}, температура до {temperatureText}). " +
            "До вывода о необходимости замены процессора сначала стоит проверить охлаждение: " +
            "чистоту системы, работу вентиляторов, установку кулера и состояние термопасты.",

            $"Over {span}, high processor load has repeatedly occurred together with elevated temperature " +
            $"(load up to {loadText}, temperature up to {temperatureText}). " +
            "Before concluding that the processor needs replacing, check cooling first: " +
            "dust buildup, fan operation, cooler installation, and thermal paste.");
    }


    private static string BuildGpuCoolingRecommendationText(
        CpuGpuUpgradeResult result)
    {
        string span =
            FormatObservedSpan(
                result.EvidenceSpanDays);

        string loadText =
            result.GpuPeakLoad.HasValue
                ? $"{result.GpuPeakLoad.Value:F0}%"
                : "—";

        string temperatureText =
            result.GpuPeakTemperature.HasValue
                ? $"{result.GpuPeakTemperature.Value:F0} °C"
                : "—";

        return SettingsService.L(
            $"На протяжении {span} высокая загрузка видеокарты повторяется вместе с повышенной температурой " +
            $"(нагрузка до {loadText}, температура до {temperatureText}). " +
            "Сама по себе загрузка видеокарты около 100% нормальна для тяжёлых графических задач, " +
            "но при повышенной температуре сначала стоит проверить вентиляторы и приток воздуха в корпусе.",

            $"Over {span}, high graphics-card load has repeatedly occurred together with elevated temperature " +
            $"(load up to {loadText}, temperature up to {temperatureText}). " +
            "Graphics-card load near 100% is normal by itself in demanding graphics workloads, " +
            "but with elevated temperature it is worth checking the fans and case airflow first.");
    }


    private static string FormatObservedSpan(
        int days)
    {
        days =
            Math.Max(
                1,
                days);

        if (SettingsService.IsRussian)
        {
            int lastTwo =
                days % 100;

            int last =
                days % 10;

            string word =
                lastTwo >= 11 &&
                lastTwo <= 14
                    ? "дней"
                    : last == 1
                        ? "день"
                        : last >= 2 &&
                          last <= 4
                            ? "дня"
                            : "дней";

            return
                $"{days} {word}";
        }

        return days == 1
            ? "1 day"
            : $"{days} days";
    }


    private static bool HasThermalConcern(
        IReadOnlyList<AdvisorPerformancePoint> points,
        Func<AdvisorPerformancePoint, double?> temperatureSelector,
        double warningTemperature)
    {
        List<double> temperatures =
            points
                .Select(
                    temperatureSelector)
                .Where(
                    value =>
                        value.HasValue)
                .Select(
                    value =>
                        value!.Value)
                .ToList();

        if (temperatures.Count < 8)
            return false;

        int hotCount =
            temperatures.Count(
                value =>
                    value >=
                    warningTemperature);

        return
            hotCount >= 8 &&
            hotCount /
            (double)temperatures.Count >=
                0.30;
    }




    private enum CpuGpuUpgradeOutcome
    {
        None,
        Cpu,
        CpuCooling,
        GpuCooling,
        Ambiguous
    }


    private sealed class CpuGpuUpgradeResult
    {
        public CpuGpuUpgradeOutcome Outcome { get; init; } =
            CpuGpuUpgradeOutcome.None;

        public bool HasEnoughHistory { get; init; }

        public int EvidenceSpanDays { get; init; } =
            1;

        public double? CpuPeakLoad { get; init; }

        public double? CpuPeakTemperature { get; init; }

        public double? GpuAverageLoadDuringCpuPattern { get; init; }

        public double? GpuPeakLoad { get; init; }

        public double? GpuPeakTemperature { get; init; }

        public double? CpuAverageLoadDuringGpuPattern { get; init; }
    }


    private static LogicalDriveInfo?
        FindWindowsLogicalDrive(
            HardwareSnapshot snapshot)
    {
        string windowsPath =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);

        string? root =
            Path.GetPathRoot(
                windowsPath);

        if (string.IsNullOrWhiteSpace(
                root))
        {
            return null;
        }

        string normalizedRoot =
            FormatDriveName(
                root);

        return snapshot.Drives
            .FirstOrDefault(
                drive =>
                    string.Equals(
                        FormatDriveName(
                            drive.Name),
                        normalizedRoot,
                        StringComparison.OrdinalIgnoreCase));
    }


    private static bool ShouldSuggestLargerSystemDrive(
        LogicalDriveInfo drive)
    {
        if (drive.TotalBytes <= 0)
            return false;

        double totalGb =
            drive.TotalBytes /
            1024d /
            1024d /
            1024d;

        double freeGb =
            drive.FreeBytes /
            1024d /
            1024d /
            1024d;

        double freePercent =
            drive.FreeBytes /
            (double)drive.TotalBytes *
            100.0;

        if (freeGb <= 12 ||
            freePercent <= 5)
        {
            return true;
        }

        if (totalGb <= 160 &&
            (freeGb <= 40 ||
             freePercent <= 30))
        {
            return true;
        }

        if (totalGb <= 320 &&
            (freeGb <= 32 ||
             freePercent <= 15))
        {
            return true;
        }

        return false;
    }


    private static SystemDriveHardwareEvidence
        ReadSystemDriveHardwareEvidence(
            HardwareSnapshot snapshot)
    {
        int? systemDiskIndex =
            TryGetWindowsPhysicalDiskIndex();

        if (!systemDiskIndex.HasValue)
        {
            return new SystemDriveHardwareEvidence
            {
                Kind =
                    PhysicalStorageKind.Unknown,

                HasOtherSsd =
                    snapshot.StorageDevices.Any(
                        IsStorageDeviceSsd)
            };
        }

        Dictionary<int, PhysicalStorageKind>
            physicalKinds =
                ReadPhysicalStorageKinds();

        PhysicalStorageKind kind =
            physicalKinds.TryGetValue(
                systemDiskIndex.Value,
                out PhysicalStorageKind detectedKind)
                    ? detectedKind
                    : PhysicalStorageKind.Unknown;

        string? systemModel =
            ReadWin32DiskModel(
                systemDiskIndex.Value);

        if (kind ==
            PhysicalStorageKind.Unknown)
        {
            StorageDeviceInfo? matchingStorage =
                snapshot.StorageDevices
                    .FirstOrDefault(
                        storage =>
                            !string.IsNullOrWhiteSpace(
                                systemModel) &&
                            (
                                storage.Name.Contains(
                                    systemModel,
                                    StringComparison.OrdinalIgnoreCase) ||
                                systemModel.Contains(
                                    storage.Name,
                                    StringComparison.OrdinalIgnoreCase)
                            ));

            if (matchingStorage != null)
            {
                kind =
                    GetPhysicalStorageKind(
                        matchingStorage);
            }
            else if (!string.IsNullOrWhiteSpace(
                         systemModel))
            {
                if (systemModel.Contains(
                        "NVME",
                        StringComparison.OrdinalIgnoreCase) ||
                    systemModel.Contains(
                        "SSD",
                        StringComparison.OrdinalIgnoreCase))
                {
                    kind =
                        PhysicalStorageKind.Ssd;
                }
            }
        }

        bool hasOtherSsd =
            physicalKinds.Any(
                pair =>
                    pair.Key !=
                        systemDiskIndex.Value &&
                    pair.Value ==
                        PhysicalStorageKind.Ssd);

        if (!hasOtherSsd &&
            kind ==
                PhysicalStorageKind.Hdd)
        {
            hasOtherSsd =
                snapshot.StorageDevices.Any(
                    IsStorageDeviceSsd);
        }

        return new SystemDriveHardwareEvidence
        {
            Kind =
                kind,

            HasOtherSsd =
                hasOtherSsd
        };
    }


    private static int?
        TryGetWindowsPhysicalDiskIndex()
    {
        try
        {
            string windowsPath =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.Windows);

            string? root =
                Path.GetPathRoot(
                    windowsPath);

            if (string.IsNullOrWhiteSpace(
                    root))
            {
                return null;
            }

            string logicalId =
                FormatDriveName(
                    root);

            using ManagementObjectSearcher searcher =
                new(
                    "SELECT Antecedent, Dependent " +
                    "FROM Win32_LogicalDiskToPartition");

            using ManagementObjectCollection results =
                searcher.Get();

            foreach (ManagementObject association in
                     results)
            {
                string dependent =
                    association[
                        "Dependent"]?
                    .ToString() ??
                    string.Empty;

                if (!dependent.Contains(
                        $"DeviceID=\"{logicalId}\"",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? antecedent =
                    association[
                        "Antecedent"]?
                    .ToString();

                if (string.IsNullOrWhiteSpace(
                        antecedent))
                {
                    continue;
                }

                using ManagementObject partition =
                    new(
                        new ManagementPath(
                            antecedent));

                partition.Get();

                object? diskIndexValue =
                    partition[
                        "DiskIndex"];

                if (diskIndexValue != null &&
                    int.TryParse(
                        diskIndexValue.ToString(),
                        out int diskIndex))
                {
                    return diskIndex;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Не удалось определить физический диск Windows: {ex}");
        }

        return null;
    }


    private static Dictionary<int, PhysicalStorageKind>
        ReadPhysicalStorageKinds()
    {
        Dictionary<int, PhysicalStorageKind> result =
            new();

        try
        {
            ManagementScope scope =
                new(
                    @"\\.\root\Microsoft\Windows\Storage");

            scope.Connect();

            ObjectQuery query =
                new(
                    "SELECT DeviceId, MediaType " +
                    "FROM MSFT_PhysicalDisk");

            using ManagementObjectSearcher searcher =
                new(
                    scope,
                    query);

            using ManagementObjectCollection items =
                searcher.Get();

            foreach (ManagementObject item in
                     items)
            {
                if (!int.TryParse(
                        item["DeviceId"]?
                            .ToString(),
                        out int deviceId))
                {
                    continue;
                }

                int mediaType =
                    item["MediaType"] == null
                        ? 0
                        : Convert.ToInt32(
                            item["MediaType"]);

                PhysicalStorageKind kind =
                    mediaType switch
                    {
                        3 =>
                            PhysicalStorageKind.Hdd,

                        4 =>
                            PhysicalStorageKind.Ssd,

                        _ =>
                            PhysicalStorageKind.Unknown
                    };

                result[deviceId] =
                    kind;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Не удалось определить типы физических накопителей: {ex}");
        }

        return result;
    }


    private static string?
        ReadWin32DiskModel(
            int diskIndex)
    {
        try
        {
            using ManagementObjectSearcher searcher =
                new(
                    "SELECT Model " +
                    "FROM Win32_DiskDrive " +
                    $"WHERE Index = {diskIndex}");

            using ManagementObjectCollection results =
                searcher.Get();

            foreach (ManagementObject item in
                     results)
            {
                return item["Model"]?
                    .ToString()?
                    .Trim();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Не удалось получить модель системного диска: {ex}");
        }

        return null;
    }


    private static bool IsStorageDeviceSsd(
        StorageDeviceInfo storage)
    {
        return GetPhysicalStorageKind(
                   storage) ==
               PhysicalStorageKind.Ssd;
    }


    private static PhysicalStorageKind
        GetPhysicalStorageKind(
            StorageDeviceInfo storage)
    {
        string identifier =
            storage.Id ??
            string.Empty;

        if (identifier.Contains(
                "nvme",
                StringComparison.OrdinalIgnoreCase) ||
            identifier.Contains(
                "ssd",
                StringComparison.OrdinalIgnoreCase))
        {
            return PhysicalStorageKind.Ssd;
        }

        if (identifier.Contains(
                "hdd",
                StringComparison.OrdinalIgnoreCase))
        {
            return PhysicalStorageKind.Hdd;
        }

        return PhysicalStorageKind.Unknown;
    }


    private void SetStorageGuidanceText(
        string status,
        string meaning,
        string action)
    {
        if (_storageGuidanceStatusText != null)
        {
            _storageGuidanceStatusText.Text =
                status;
        }

        if (_storageGuidanceMeaningText != null)
        {
            _storageGuidanceMeaningText.Text =
                meaning;
        }

        if (_storageGuidanceActionText != null)
        {
            _storageGuidanceActionText.Text =
                action;
        }
    }


    private void AddStorageDetailLine(
        string text)
    {
        if (_storageGuidanceDetailsPanel == null)
            return;

        TextBlock line =
            CreateGuidanceValueText();

        line.Text =
            text;

        if (_storageGuidanceDetailsPanel
                .Children
                .Count > 0)
        {
            line.Margin =
                new Thickness(
                    0,
                    6,
                    0,
                    0);
        }

        _storageGuidanceDetailsPanel
            .Children
            .Add(
                line);
    }


    private sealed class SystemDriveHardwareEvidence
    {
        public PhysicalStorageKind Kind { get; init; }

        public bool HasOtherSsd { get; init; }
    }


    private enum PhysicalStorageKind
    {
        Unknown = 0,
        Hdd = 1,
        Ssd = 2
    }


    private readonly record struct
        StorageGuidanceThresholds(
            double Warning,
            double Critical);


    private sealed class StorageGuidanceDeviceEvidence
    {
        public string Name { get; init; } = "";

        public string HumanType { get; init; } = "";

        public double? CurrentTemperature { get; init; }

        public double? HistoryAverage { get; init; }

        public double? HistoryMaximum { get; init; }

        public string? HistoryPeriod { get; init; }

        public StorageGuidanceLevel Level { get; init; }
    }


    private sealed class DriveSpaceGuidance
    {
        public string Name { get; init; } = "";

        public long FreeBytes { get; init; }

        public double FreePercent { get; init; }

        public StorageGuidanceLevel Level { get; init; }
    }


    private enum StorageGuidanceLevel
    {
        Unknown = -1,
        Normal = 0,
        Warm = 1,
        Warning = 2,
        Critical = 3
    }


    private sealed class RamHistoryEvidence
    {
        public StatisticsSummary Last24Hours { get; init; } =
            new();

        public StatisticsSummary Last7Days { get; init; } =
            new();

        public int DaySeriesPointCount { get; init; }

        public int DayHighPointCount { get; init; }

        public int WeekSeriesPointCount { get; init; }

        public int WeekHighPointCount { get; init; }

        public int WeekDaysWithSamples { get; init; }

        public int WeekDaysWithHighLoad { get; init; }
    }
}
