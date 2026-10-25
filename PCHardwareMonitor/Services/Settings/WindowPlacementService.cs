using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace PCHardwareMonitor;

/// <summary>
/// Saves and restores user-selected geometry for resizable windows.
/// MainWindow retains its own placement behavior. StorageAnalyzerWindow
/// deliberately opens at the main window's current bounds instead.
/// </summary>
internal static class WindowPlacementService
{
    private sealed class AttachmentState
    {
        public bool Restored { get; set; }
        public bool LastNonMinimizedStateWasMaximized { get; set; }
    }

    private static readonly ConditionalWeakTable<Window, AttachmentState>
        AttachedWindows = new();

    private static bool _applicationHookInstalled;

    /// <summary>
    /// Covers windows that do not call SettingsService.ApplyLanguageToWindow.
    /// The normal path attaches in the constructor, before SourceInitialized.
    /// </summary>
    public static void RegisterForApplication()
    {
        if (_applicationHookInstalled)
            return;

        _applicationHookInstalled = true;

        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is Window window)
                    Attach(window);
            }),
            handledEventsToo: true);
    }

    public static void Attach(Window window)
    {
        // Preserve user-approved geometry behavior for these two windows.
        // Generic Window instances are short-lived dialogs, often sharing the
        // same CLR type; they must not inherit one another's saved geometry.
        if (window is MainWindow ||
            window is StorageAnalyzerWindow ||
            window.GetType() == typeof(Window))
        {
            return;
        }

        if (window.ResizeMode != ResizeMode.CanResize &&
            window.ResizeMode != ResizeMode.CanResizeWithGrip)
        {
            return;
        }

        // SystemInfoWindow already calls Attach explicitly. Avoid a second
        // SourceInitialized/Closing subscription from automatic registration.
        if (AttachedWindows.TryGetValue(window, out _))
            return;

        AttachmentState state = new();
        AttachedWindows.Add(window, state);

        string key = window.GetType().Name;

        window.SourceInitialized += (_, _) =>
            Restore(window, key, state);

        window.StateChanged += (_, _) =>
        {
            if (window.WindowState != WindowState.Minimized)
            {
                state.LastNonMinimizedStateWasMaximized =
                    window.WindowState == WindowState.Maximized;
            }
        };

        window.Closing += (_, _) =>
            Save(window, key, state);

        // Fallback for windows that are first discovered through Loaded.
        // Their SourceInitialized has already happened.
        if (PresentationSource.FromVisual(window) != null)
            Restore(window, key, state);
    }

    private static void Restore(Window window, string key, AttachmentState state)
    {
        if (state.Restored)
            return;

        state.Restored = true;

        try
        {
            if (SettingsService.Current.WindowPlacements == null ||
                !SettingsService.Current.WindowPlacements.TryGetValue(
                    key,
                    out WindowPlacementSettings? saved) ||
                saved == null ||
                !HasValidGeometry(saved))
            {
                return;
            }

            Rect bounds = new(
                saved.Left,
                saved.Top,
                Math.Max(window.MinWidth, saved.Width),
                Math.Max(window.MinHeight, saved.Height));

            if (!TryFindVisibleWorkArea(window, bounds, out _))
            {
                // Former monitor may be gone or resolution/DPI may have changed.
                Rect workArea = GetPrimaryWorkArea(window);
                if (workArea.Width <= 0 || workArea.Height <= 0)
                    return;

                bounds.Width = Math.Min(bounds.Width, workArea.Width);
                bounds.Height = Math.Min(bounds.Height, workArea.Height);
                bounds.X = workArea.Left + (workArea.Width - bounds.Width) / 2;
                bounds.Y = workArea.Top + (workArea.Height - bounds.Height) / 2;
            }

            window.WindowStartupLocation = WindowStartupLocation.Manual;
            // A size-to-content Window ignores its saved Width/Height.
            window.SizeToContent = SizeToContent.Manual;
            window.WindowState = WindowState.Normal;
            window.Left = bounds.Left;
            window.Top = bounds.Top;
            window.Width = bounds.Width;
            window.Height = bounds.Height;

            state.LastNonMinimizedStateWasMaximized = saved.IsMaximized;

            if (saved.IsMaximized)
                window.WindowState = WindowState.Maximized;
        }
        catch (Exception ex)
        {
            Trace.WriteLine(
                $"Thermiqra window placement restore ({key}): {ex.Message}");
        }
    }

    private static void Save(Window window, string key, AttachmentState state)
    {
        try
        {
            Rect bounds = window.WindowState == WindowState.Normal
                ? new Rect(window.Left, window.Top,
                    window.ActualWidth, window.ActualHeight)
                : window.RestoreBounds;

            WindowPlacementSettings saved = new()
            {
                Left = bounds.Left,
                Top = bounds.Top,
                Width = bounds.Width,
                Height = bounds.Height,
                IsMaximized = state.LastNonMinimizedStateWasMaximized
            };

            if (!HasValidGeometry(saved))
                return;

            SettingsService.Current.WindowPlacements ??=
                new System.Collections.Generic.Dictionary<string, WindowPlacementSettings>(
                    StringComparer.Ordinal);

            SettingsService.Current.WindowPlacements[key] = saved;
            SettingsService.Save();
        }
        catch (Exception ex)
        {
            Trace.WriteLine(
                $"Thermiqra window placement save ({key}): {ex.Message}");
        }
    }

    private static bool HasValidGeometry(WindowPlacementSettings placement)
    {
        return double.IsFinite(placement.Left) &&
               double.IsFinite(placement.Top) &&
               double.IsFinite(placement.Width) &&
               double.IsFinite(placement.Height) &&
               placement.Width >= 200 &&
               placement.Height >= 150 &&
               placement.Width <= 20000 &&
               placement.Height <= 20000;
    }

    private static bool TryFindVisibleWorkArea(Window window, Rect bounds,
        out Rect workArea)
    {
        foreach (Forms.Screen screen in Forms.Screen.AllScreens)
        {
            Rect area = GetWorkArea(window, screen);
            Rect titleBar = new(
                bounds.Left, bounds.Top, Math.Min(200, bounds.Width), 48);
            Rect visibleTitleBar = Rect.Intersect(area, titleBar);

            if (!visibleTitleBar.IsEmpty &&
                visibleTitleBar.Width >= 80 &&
                visibleTitleBar.Height >= 20)
            {
                workArea = area;
                return true;
            }
        }

        workArea = Rect.Empty;
        return false;
    }

    private static Rect GetPrimaryWorkArea(Window window)
    {
        Forms.Screen? screen = Forms.Screen.PrimaryScreen;
        return screen == null
            ? SystemParameters.WorkArea
            : GetWorkArea(window, screen);
    }

    private static Rect GetWorkArea(Window window, Forms.Screen screen)
    {
        // WinForms uses physical pixels; WPF uses device-independent units.
        Matrix fromDevice = PresentationSource.FromVisual(window)?
            .CompositionTarget?.TransformFromDevice ?? Matrix.Identity;

        System.Drawing.Rectangle area = screen.WorkingArea;
        Point topLeft = fromDevice.Transform(new Point(area.Left, area.Top));
        Point bottomRight = fromDevice.Transform(new Point(area.Right, area.Bottom));
        return new Rect(topLeft, bottomRight);
    }
}
