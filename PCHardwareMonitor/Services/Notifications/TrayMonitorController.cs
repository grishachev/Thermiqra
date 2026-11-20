using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace PCHardwareMonitor;

internal static class TrayMonitorPolicy
{
    internal static bool ShouldShow(bool always, bool ownIcon, bool menu,
        bool fullscreen, bool sessionLocked, bool anchorAvailable) =>
        anchorAvailable && !sessionLocked && !menu && !fullscreen && (always || ownIcon);

    internal static System.Drawing.Rectangle Place(System.Drawing.Rectangle anchor, System.Drawing.Rectangle work, int width, int height)
    {
        const int gap = 8;
        width = Math.Min(width, work.Width); height = Math.Min(height, work.Height);
        int x = Math.Clamp(anchor.Right - width, work.Left, work.Right - width);
        int y = anchor.Top - height - gap;
        if (y < work.Top) y = anchor.Bottom + gap;
        return new(x, Math.Clamp(y, work.Top, work.Bottom - height), width, height);
    }
}

internal sealed class TrayMonitorController : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly DispatcherTimer _timer;
    private TrayMonitorWindow? _window;
    private TrayMonitorReading _reading = TrayMonitorReading.Empty;
    private System.Drawing.Rectangle? _fallbackAnchor;
    private DateTime _hoverSince, _suppressUntil;
    private bool _menuOpen, _sessionLocked, _disposed, _loggedError;

    internal TrayMonitorController(Forms.NotifyIcon icon)
    {
        _icon = icon;
        _icon.MouseMove += OnMouseMove;
        _icon.MouseDown += OnMouseDown;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(150) };
        _timer.Tick += Tick;
        _timer.Start();
    }

    internal void UpdateSnapshot(HardwareSnapshot snapshot)
    {
        _reading = TrayMonitorReading.From(snapshot);
        _window?.View.UpdateReading(_reading);
    }

    internal void RefreshConfiguration()
    {
        _window?.View.RefreshAppearance();
        Tick(this, EventArgs.Empty);
    }

    internal void SetMenuOpen(bool open)
    {
        _menuOpen = open;
        _suppressUntil = DateTime.UtcNow.AddMilliseconds(350);
        _window?.Hide();
    }

    private void OnMouseMove(object? sender, Forms.MouseEventArgs e)
    {
        var cursor = Forms.Cursor.Position;
        // Used only if a future WinForms version changes its private icon identity.
        if (_fallbackAnchor is not { } old || !old.Contains(cursor))
            _fallbackAnchor = new(cursor.X - 10, cursor.Y - 10, 20, 20);
    }

    private void OnMouseDown(object? sender, Forms.MouseEventArgs e)
    {
        _suppressUntil = DateTime.UtcNow.AddMilliseconds(750);
        _window?.Hide();
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            _sessionLocked = e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.SessionLogoff or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect;
            if (_sessionLocked) _window?.Hide();
        }));
    }

    private void Tick(object? sender, EventArgs e)
    {
        if (_disposed) return;
        try
        {
            var iconAnchor = TrayMonitorNative.IconRectangle(_icon) ?? _fallbackAnchor;
            var cursor = Forms.Cursor.Position;
            bool ours = iconAnchor?.Contains(cursor) == true;
            if (!ours) _hoverSince = default;
            else if (_hoverSince == default) _hoverSince = DateTime.UtcNow;
            bool menu = _menuOpen || DateTime.UtcNow < _suppressUntil || TrayMonitorNative.MenuActive() || TrayMonitorNative.ShellFlyoutActive();
            bool fullscreen = TrayMonitorNative.FullscreenForeground();
            // A persistent panel must also work when Windows keeps our icon in the
            // collapsed overflow. This fallback is for placement, NEVER hover detection.
            var anchor = iconAnchor ?? (SettingsService.Current.TrayPanelAlwaysVisible ? TrayMonitorNative.TaskbarAnchor() : null);
            if (!menu && anchor.HasValue && (SettingsService.Current.TrayPanelAlwaysVisible || ours))
            {
                var placementAnchor = TrayMonitorNative.PlacementAnchor(anchor.Value);
                var area = _window?.NativeBounds ?? TrayMonitorPolicy.Place(placementAnchor,
                    Forms.Screen.FromRectangle(placementAnchor).WorkingArea, 286,
                    SettingsService.Current.SkinName == "SteamPunk" ? 128 : 104);
                // Custom tray menus need not activate their process or enter the
                // foreground thread's Win32 menu loop (Steam is one example).
                menu = TrayMonitorNative.HasOverlappingPopup(area);
            }
            bool visible = TrayMonitorPolicy.ShouldShow(SettingsService.Current.TrayPanelAlwaysVisible,
                ours && DateTime.UtcNow - _hoverSince >= TimeSpan.FromMilliseconds(300), menu,
                fullscreen, _sessionLocked, anchor.HasValue);
            if (!visible) { _window?.Hide(); return; }
            _window ??= new TrayMonitorWindow();
            _window.View.UpdateReading(_reading);
            _window.ShowAt(TrayMonitorNative.PlacementAnchor(anchor!.Value));
        }
        catch (Exception ex)
        {
            _window?.Hide();
            if (!_loggedError) Trace.WriteLine("Tray monitor presentation failed: " + ex);
            _loggedError = true;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop(); _timer.Tick -= Tick;
        _icon.MouseMove -= OnMouseMove;
        _icon.MouseDown -= OnMouseDown;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        _window?.Close(); _window = null;
    }
}

internal sealed class TrayMonitorWindow : Window
{
    internal TrayMonitorView View { get; } = new();
    private IntPtr _handle;
    internal System.Drawing.Rectangle? NativeBounds =>
        _handle != IntPtr.Zero && TrayMonitorNative.TryBounds(_handle, out var bounds) ? bounds : null;

    internal TrayMonitorWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        SizeToContent = SizeToContent.WidthAndHeight;
        IsHitTestVisible = false;
        Content = View;
        SourceInitialized += (_, _) =>
        {
            _handle = new WindowInteropHelper(this).Handle;
            TrayMonitorNative.MakePassive(_handle);
        };
    }

    internal void ShowAt(System.Drawing.Rectangle anchor)
    {
        // Create and position the HWND before showing: no flash at (0,0), focus
        // change, Alt-Tab entry or pointer interception over another application.
        if (_handle == IntPtr.Zero) new WindowInteropHelper(this).EnsureHandle();
        var screen = Forms.Screen.FromRectangle(anchor);
        double scale = TrayMonitorNative.GetDpiForWindow(_handle) / 96d;
        var location = TrayMonitorPolicy.Place(anchor, screen.WorkingArea,
            (int)Math.Ceiling(View.Width * scale), (int)Math.Ceiling(View.PanelHeight * scale));
        if (IsVisible && NativeBounds == location)
        {
            TrayMonitorNative.YieldToTooltips(_handle);
            return;
        }
        bool firstShow = !IsVisible;
        // Moving an existing panel must preserve the Z order. Reasserting
        // HWND_TOPMOST on each timer tick covered/flickered Windows tray tips.
        TrayMonitorNative.Move(_handle, location, false);
        scale = TrayMonitorNative.GetDpiForWindow(_handle) / 96d;
        location = TrayMonitorPolicy.Place(anchor, screen.WorkingArea,
            (int)Math.Ceiling(View.Width * scale), (int)Math.Ceiling(View.PanelHeight * scale));
        if (!IsVisible) Show();
        TrayMonitorNative.Move(_handle, location, true, promote: firstShow);
        TrayMonitorNative.YieldToTooltips(_handle);
    }
}

internal static partial class TrayMonitorNative
{
    // NotifyIcon does not expose its NIF identity. Resolve once; if this changes,
    // hover still works using its own MouseMove event, without guessing other icons.
    private static readonly FieldInfo? Id = typeof(Forms.NotifyIcon).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? Window = typeof(Forms.NotifyIcon).GetField("_window", BindingFlags.Instance | BindingFlags.NonPublic);

    internal static System.Drawing.Rectangle? IconRectangle(Forms.NotifyIcon icon)
    {
        if (Id?.GetValue(icon) is not object id || Window?.GetValue(icon) is not Forms.NativeWindow window || window.Handle == IntPtr.Zero)
            return null;
        var identity = new IconIdentity { Size = (uint)Marshal.SizeOf<IconIdentity>(), Handle = window.Handle, Id = Convert.ToUInt32(id) };
        return Shell_NotifyIconGetRect(ref identity, out var r) == 0 && r.Right > r.Left && r.Bottom > r.Top ? r.Rectangle : null;
    }

    internal static bool MenuActive()
    {
        var info = new GuiThreadInfo { Size = (uint)Marshal.SizeOf<GuiThreadInfo>() };
        return GetGUIThreadInfo(0, ref info) && (info.Flags & 0x1c) != 0;
    }

    internal static System.Drawing.Rectangle? TaskbarAnchor()
    {
        IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
        if (taskbar == IntPtr.Zero || !GetWindowRect(taskbar, out var rect)) return null;
        return rect.Rectangle;
    }

    internal static System.Drawing.Rectangle PlacementAnchor(System.Drawing.Rectangle icon)
    {
        var point = new NativePoint { X = icon.Left + icon.Width / 2, Y = icon.Top + icon.Height / 2 };
        IntPtr root = GetAncestor(WindowFromPoint(point), 2);
        // Do not cover the other rows of icons inside the expanded overflow panel.
        return ClassName(root) is "TopLevelWindowForOverflowXamlIsland" or "NotifyIconOverflowWindow"
            && GetWindowRect(root, out var rect) ? rect.Rectangle : icon;
    }

    internal static bool ShellFlyoutActive()
    {
        IntPtr foreground = GetForegroundWindow();
        string name = ClassName(foreground);
        if (name == "ControlCenterWindow") return true;
        if (name != "Windows.UI.Core.CoreWindow" && name != "XamlExplorerHostIslandWindow") return false;
        GetWindowThreadProcessId(foreground, out uint pid);
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName is "ShellExperienceHost" or "StartMenuExperienceHost" or "SearchHost";
        }
        catch (ArgumentException) { return false; } // The flyout closed between calls.
        catch (System.ComponentModel.Win32Exception) { return true; } // Yield if the shell cannot be inspected.
    }

    internal static bool FullscreenForeground()
    {
        IntPtr foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || ClassName(foreground) is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;
        if (!GetWindowRect(foreground, out var r)) return false;
        var bounds = Forms.Screen.FromHandle(foreground).Bounds;
        return r.Left <= bounds.Left && r.Top <= bounds.Top && r.Right >= bounds.Right && r.Bottom >= bounds.Bottom;
    }

    private static string ClassName(IntPtr handle)
    {
        var text = new StringBuilder(256); GetClassName(handle, text, text.Capacity); return text.ToString();
    }

    internal static void MakePassive(IntPtr hwnd)
    {
        long style = IntPtr.Size == 8 ? GetWindowLongPtr(hwnd, -20).ToInt64() : GetWindowLong(hwnd, -20);
        // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT
        style |= 0x08000000 | 0x80 | 0x20;
        if (IntPtr.Size == 8) SetWindowLongPtr(hwnd, -20, new IntPtr(style)); else SetWindowLong(hwnd, -20, (int)style);
    }

    internal static void Move(IntPtr hwnd, System.Drawing.Rectangle rectangle, bool show, bool promote = false) =>
        SetWindowPos(hwnd, promote ? new IntPtr(-1) : IntPtr.Zero, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height,
            0x10u | 0x200u | (show ? 0x40u : 0u) | (promote ? 0u : 0x4u)); // NOACTIVATE | NOOWNERZORDER | [SHOWWINDOW] | [NOZORDER]

    [StructLayout(LayoutKind.Sequential)] private struct IconIdentity { public uint Size; public IntPtr Handle; public uint Id; public Guid Guid; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
        public readonly System.Drawing.Rectangle Rectangle => System.Drawing.Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }
    [StructLayout(LayoutKind.Sequential)] private struct GuiThreadInfo
    {
        public uint Size, Flags;
        public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret;
        public NativeRect CaretRect;
    }
    [DllImport("shell32.dll")] private static extern int Shell_NotifyIconGetRect(ref IconIdentity identity, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint id, ref GuiThreadInfo info);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int max);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string className, string? name);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}
