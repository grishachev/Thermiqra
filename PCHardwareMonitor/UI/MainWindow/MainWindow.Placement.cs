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


    // ============================================================
    // РАЗМЕР / ПОЛОЖЕНИЕ ОКНА
    // ============================================================

    private void RestoreWindowPlacement()
    {
        try
        {
            if (!System.IO.File.Exists(
                    WindowSettingsPath))
            {
                SetDefaultWindowPlacement();
                return;
            }

            string json =
                System.IO.File.ReadAllText(
                    WindowSettingsPath);

            WindowSettings? settings =
                JsonSerializer.Deserialize<WindowSettings>(
                    json);

            if (settings == null ||
                settings.Width < MinWidth ||
                settings.Height < MinHeight)
            {
                SetDefaultWindowPlacement();
                return;
            }

            Width =
                settings.Width;

            Height =
                settings.Height;

            Left =
                settings.Left;

            Top =
                settings.Top;

            if (!IsWindowVisibleEnough())
            {
                CenterWindowOnPrimaryScreen();
            }

            WindowState =
                settings.IsMaximized
                    ? WindowState.Maximized
                    : WindowState.Normal;
        }
        catch
        {
            SetDefaultWindowPlacement();
        }
    }

    private void SetDefaultWindowPlacement()
    {
        Rect workArea =
            SystemParameters.WorkArea;

        Width =
            Math.Max(
                MinWidth,
                Math.Min(
                    1400,
                    workArea.Width * 0.88));

        Height =
            Math.Max(
                MinHeight,
                Math.Min(
                    820,
                    workArea.Height * 0.88));

        Left =
            workArea.Left +
            (workArea.Width - Width) / 2;

        Top =
            workArea.Top +
            (workArea.Height - Height) / 2;

        WindowState =
            WindowState.Normal;
    }

    private void CenterWindowOnPrimaryScreen()
    {
        Rect workArea =
            SystemParameters.WorkArea;

        Width =
            Math.Min(
                Width,
                workArea.Width * 0.95);

        Height =
            Math.Min(
                Height,
                workArea.Height * 0.95);

        Left =
            workArea.Left +
            (workArea.Width - Width) / 2;

        Top =
            workArea.Top +
            (workArea.Height - Height) / 2;
    }

    private bool IsWindowVisibleEnough()
    {
        Rect windowRect =
            new(
                Left,
                Top,
                Width,
                Height);

        Rect virtualScreen =
            new(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);

        Rect intersection =
            Rect.Intersect(
                windowRect,
                virtualScreen);

        return
            !intersection.IsEmpty &&
            intersection.Width >= 100 &&
            intersection.Height >= 100;
    }

    private void SaveWindowPlacement()
    {
        try
        {
            Rect bounds =
                WindowState ==
                WindowState.Normal

                    ? new Rect(
                        Left,
                        Top,
                        Width,
                        Height)

                    : RestoreBounds;

            WindowSettings settings =
                new()
                {
                    Left =
                        bounds.Left,

                    Top =
                        bounds.Top,

                    Width =
                        bounds.Width,

                    Height =
                        bounds.Height,

                    IsMaximized =
                        WindowState ==
                        WindowState.Maximized
                };

            System.IO.Directory
                .CreateDirectory(
                    SettingsDirectory);

            string json =
                JsonSerializer.Serialize(
                    settings,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

            System.IO.File.WriteAllText(
                WindowSettingsPath,
                json);
        }
        catch
        {
        }
    }
}



public sealed class WindowSettings
{
    public double Left { get; set; }

    public double Top { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    public bool IsMaximized { get; set; }
}