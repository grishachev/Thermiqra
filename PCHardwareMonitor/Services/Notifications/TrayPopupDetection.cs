using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace PCHardwareMonitor;

internal readonly record struct TrayPopupTraits(long ExtendedStyle, bool HasOwner, bool OwnerIsTaskbar, double DpiScale = 1);

internal static partial class TrayMonitorNative
{
    internal static bool IsTrayTooltip(Rectangle bounds, string className, TrayPopupTraits traits)
    {
        if (className == "tooltips_class32") return true;
        double scale = Math.Max(1, traits.DpiScale);
        return className == "Xaml_WindowedPopupClass" && traits.OwnerIsTaskbar &&
            (traits.ExtendedStyle & 0x08000000) != 0 && bounds.Height <= 64 * scale && bounds.Width <= 600 * scale;
    }

    private static TrayPopupTraits PopupTraits(IntPtr window)
    {
        long extended = IntPtr.Size == 8 ? GetWindowLongPtr(window, -20).ToInt64() : (uint)GetWindowLong(window, -20);
        IntPtr owner = GetWindow(window, 4);
        bool taskbarOwner = owner != IntPtr.Zero && ClassName(owner) is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
        return new(extended, owner != IntPtr.Zero, taskbarOwner, GetDpiForWindow(window) / 96d);
    }

    internal static bool TryBounds(IntPtr window, out Rectangle bounds)
    {
        bool found = GetWindowRect(window, out var native);
        bounds = native.Rectangle;
        return found;
    }

    internal static bool IsObstructingPopup(Rectangle panel, Rectangle candidate, long style, string className,
        Rectangle? monitorBounds = null, TrayPopupTraits traits = default)
    {
        const long popup = 0x80000000L, child = 0x40000000L, mainWindowChrome = 0x00cc0000L;
        if (candidate.Width <= 0 || candidate.Height <= 0 || !panel.IntersectsWith(candidate)) return false;
        if (className is "SysShadow" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Progman" or "WorkerW") return false;
        if ((style & child) != 0) return false;
        // Windows 11 tray tips are XAML popups, not tooltips_class32. Restrict
        // this exception to small, non-activating popups owned by the taskbar;
        // a full flyout or a third-party menu must still be detected.
        if (IsTrayTooltip(candidate, className, traits)) return false;
        if (className == "#32768") return true;
        // Borderless desktop/recording overlays often have WS_POPUP and remain
        // visible to Win32 even when they draw nothing. A screen-sized surface
        // is not a tray menu. Tolerate one-pixel rounding and monitor edges.
        if (monitorBounds is Rectangle monitor && candidate.Left <= monitor.Left + 4 &&
            candidate.Top <= monitor.Top + 4 && candidate.Right >= monitor.Right - 4 &&
            candidate.Bottom >= monitor.Bottom - 4) return false;
        // Captured Steam menu: SDL_app, style 94CA0000, exstyle 02000188,
        // with an owner. Custom menus can retain caption/system-menu bits.
        // Ownership + TOOLWINDOW identifies the floating surface independently
        // of the application name, window class or foreground menu loop.
        if ((style & popup) != 0 && traits.HasOwner && (traits.ExtendedStyle & 0x80) != 0)
            return true;
        // Custom menus are uncaptioned, non-resizable popup windows. Ordinary
        // application windows underneath the monitor must not dismiss it.
        return (style & popup) != 0 && (style & mainWindowChrome) == 0;
    }

    internal static bool HasOverlappingPopup(Rectangle panel, int? ignoredProcessId = null)
    {
        int ownProcess = ignoredProcessId ?? Environment.ProcessId;
        var monitor = System.Windows.Forms.Screen.FromRectangle(panel).Bounds;
        bool found = false;
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window) || IsIconic(window)) return true;
            GetWindowThreadProcessId(window, out uint pid);
            if (pid == ownProcess || !TryBounds(window, out var bounds) || !panel.IntersectsWith(bounds)) return true;
            long style = IntPtr.Size == 8 ? GetWindowLongPtr(window, -16).ToInt64() : (uint)GetWindowLong(window, -16);
            if (!IsObstructingPopup(panel, bounds, style, ClassName(window), monitor, PopupTraits(window))) return true;
            if (DwmGetWindowAttribute(window, 14, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
            found = true;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    internal static void YieldToTooltips(IntPtr panel)
    {
        if (!TryBounds(panel, out var area)) return;
        bool passedPanel = false;
        IntPtr below = IntPtr.Zero;
        // EnumWindows walks top to bottom. Leave already-higher tips alone;
        // only LOWER our panel if an overlapping topmost tooltip is below it.
        EnumWindows((window, _) =>
        {
            if (window == panel) { passedPanel = true; return true; }
            if (!passedPanel || !IsWindowVisible(window) || !TryBounds(window, out var bounds) || !area.IntersectsWith(bounds)) return true;
            var traits = PopupTraits(window);
            if ((traits.ExtendedStyle & 0x8) == 0 || !IsTrayTooltip(bounds, ClassName(window), traits)) return true;
            if (DwmGetWindowAttribute(window, 14, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
            below = window;
            return true;
        }, IntPtr.Zero);
        if (below != IntPtr.Zero)
            SetWindowPos(panel, below, 0, 0, 0, 0, 0x1u | 0x2u | 0x10u | 0x200u); // NOSIZE | NOMOVE | NOACTIVATE | NOOWNERZORDER
    }

    private delegate bool EnumWindowCallback(IntPtr window, IntPtr context);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr context);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, uint attribute, out int value, int size);
}
