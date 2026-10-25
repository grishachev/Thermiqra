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


public partial class MainWindow
{
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



}
