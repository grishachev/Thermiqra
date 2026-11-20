using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

[Collection("WpfResources")]
public class TrayMonitorTests
{
    [Fact]
    public void HoverIsDefaultForNewAndExistingSettings()
    {
        Assert.False(new AppSettings().TrayPanelAlwaysVisible);
        Assert.False(System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{\"SkinName\":\"SteamPunk\"}")!.TrayPanelAlwaysVisible);
        Assert.True(System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{\"TrayPanelAlwaysVisible\":true}")!.TrayPanelAlwaysVisible);
    }

    [Fact]
    public void PanelYieldsToOpenMenusFullscreenAndLockedDesktopInEitherMode()
    {
        for (int mask = 0; mask < 64; mask++)
        {
            bool always = (mask & 1) != 0, own = (mask & 2) != 0;
            bool menu = (mask & 4) != 0, fullscreen = (mask & 8) != 0, locked = (mask & 16) != 0, anchor = (mask & 32) != 0;
            bool expected = anchor && (always || own) && !menu && !fullscreen && !locked;
            Assert.Equal(expected, TrayMonitorPolicy.ShouldShow(always, own, menu, fullscreen, locked, anchor));
        }
    }

    [Fact]
    public void PersistentPanelSurvivesPointerLeavingIconAndReturnsWhenMenuCloses()
    {
        // Moving across the taskbar, empty desktop or another icon does not open
        // a surface. Only the hover mode depends on the pointer being on our icon.
        Assert.True(TrayMonitorPolicy.ShouldShow(true, false, false, false, false, true));
        Assert.False(TrayMonitorPolicy.ShouldShow(false, false, false, false, false, true));
        Assert.True(TrayMonitorPolicy.ShouldShow(false, true, false, false, false, true));
        Assert.False(TrayMonitorPolicy.ShouldShow(true, false, true, false, false, true));
        Assert.True(TrayMonitorPolicy.ShouldShow(true, false, false, false, false, true));
    }

    [Fact]
    public void CustomNonActivatingMenusObstructPanelButTooltipsAndOrdinaryWindowsDoNot()
    {
        var panel = new System.Drawing.Rectangle(100, 100, 286, 128);
        var menu = new System.Drawing.Rectangle(350, 60, 220, 300);
        const long customPopup = 0x94000000;
        Assert.True(TrayMonitorNative.IsObstructingPopup(panel, menu, customPopup, "CustomTrayMenu"));
        Assert.True(TrayMonitorNative.IsObstructingPopup(panel, menu, customPopup, "#32768"));
        Assert.False(TrayMonitorNative.IsObstructingPopup(panel, menu, customPopup, "tooltips_class32"));
        Assert.False(TrayMonitorNative.IsObstructingPopup(panel, menu, customPopup, "SysShadow"));
        Assert.False(TrayMonitorNative.IsObstructingPopup(panel, menu, 0x16cf0000, "OrdinaryWindow"));
        Assert.False(TrayMonitorNative.IsObstructingPopup(panel, menu, customPopup | 0x00cc0000, "FramelessMainWindowWithResizeAndSystemMenu"));
        Assert.False(TrayMonitorNative.IsObstructingPopup(panel, menu, 0x50000000, "ChildControl"));
        Assert.False(TrayMonitorNative.IsObstructingPopup(panel, new(800, 60, 220, 300), customPopup, "CustomTrayMenu"));
    }

    [Fact]
    public void NativeEnumerationDetectsPopupWithoutActivationAndRecoversAfterItCloses()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var area = new System.Drawing.Rectangle(-30000, -30000, 286, 128);
                using (var popup = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Synthetic tray popup")
                {
                    PositionX = area.X, PositionY = area.Y, Width = 100, Height = 100,
                    WindowStyle = unchecked((int)0x90000000), ExtendedWindowStyle = 0x08000080
                }))
                {
                    Assert.True(TrayMonitorNative.HasOverlappingPopup(area, -1));
                    Assert.False(TrayMonitorNative.HasOverlappingPopup(area)); // Own windows are excluded.
                    Assert.False(TrayMonitorNative.HasOverlappingPopup(new(-29000, -29000, 100, 100), -1));
                }
                Assert.False(TrayMonitorNative.HasOverlappingPopup(area, -1));
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    [Fact]
    public void DesktopAndScreenSizedBackgroundOverlayCannotSuppressPanel()
    {
        // Actual native classes, styles and bounds observed when the panel
        // disappeared on the user's 3440x1440 desktop. No application-name rule.
        var screen = new System.Drawing.Rectangle(0, 0, 3440, 1440);
        var panel = new System.Drawing.Rectangle(2900, 1256, 286, 128);
        Assert.False(TrayMonitorNative.IsObstructingPopup(panel, screen, 0x96000000, "Progman", screen));
        Assert.False(TrayMonitorNative.IsObstructingPopup(panel, screen, 0x96000000, "WorkerW", screen));
        Assert.False(TrayMonitorNative.IsObstructingPopup(panel, new(0, 0, 3439, 1440), 0x94000000, "CEF-OSC-WIDGET", screen));
        Assert.False(TrayMonitorNative.IsObstructingPopup(panel, screen, 0x94000000, "AnotherRecordingOverlay", screen));
        Assert.True(TrayMonitorNative.IsObstructingPopup(panel, new(3100, 950, 220, 434), 0x94000000, "CustomTrayMenu", screen));
        var leftScreen = new System.Drawing.Rectangle(-1920, -1080, 1920, 1080);
        var leftPanel = new System.Drawing.Rectangle(-400, -200, 286, 128);
        Assert.False(TrayMonitorNative.IsObstructingPopup(leftPanel, new(-1920, -1080, 1919, 1080), 0x94000000, "BackgroundOverlay", leftScreen));
    }

    [Fact]
    public void CapturedSteamMenuAndWindows11TrayTooltipAreDistinguished()
    {
        var screen = new System.Drawing.Rectangle(0, 0, 3440, 1440);
        var panel = new System.Drawing.Rectangle(3000, 1256, 400, 128);
        // Read from the real open Steam menu and real explorer tray tooltip.
        Assert.True(TrayMonitorNative.IsObstructingPopup(panel, new(3218, 969, 203, 456), 0x94ca0000,
            "SDL_app", screen, new(0x02000188, true, false)));
        Assert.True(TrayMonitorNative.IsObstructingPopup(panel, new(3218, 969, 203, 456), 0x94ca0000,
            "AnotherCustomMenu", screen, new(0x02000188, true, false)));
        Assert.False(TrayMonitorNative.IsObstructingPopup(panel, new(3177, 1347, 62, 41), 0x94000000,
            "Xaml_WindowedPopupClass", screen, new(0x08200008, true, true)));
        // XAML flyouts do not inherit the exception for a small tray tip.
        Assert.True(TrayMonitorNative.IsObstructingPopup(panel, new(3100, 1000, 300, 384), 0x94000000,
            "Xaml_WindowedPopupClass", screen, new(0x08200008, true, true)));
        Assert.True(TrayMonitorNative.IsObstructingPopup(panel, new(3177, 1347, 62, 41), 0x94000000,
            "Xaml_WindowedPopupClass", screen, new(0x08200008, true, false)));
    }

    [Fact]
    public void NativeOwnedCaptionedToolPopupIsDetectedUntilClosed()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var owner = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Hidden popup owner")
                { PositionX = -30000, PositionY = -30000, Width = 100, Height = 100, WindowStyle = unchecked((int)0x80000000), ExtendedWindowStyle = 0x08000080 });
                var area = new System.Drawing.Rectangle(-30000, -30000, 286, 128);
                using (var popup = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Captioned owned tray menu")
                {
                    ParentWindow = owner.Handle, PositionX = area.X, PositionY = area.Y, Width = 100, Height = 100,
                    WindowStyle = unchecked((int)0x94ca0000), ExtendedWindowStyle = 0x0a000188
                }))
                    Assert.True(TrayMonitorNative.HasOverlappingPopup(area, -1));
                Assert.False(TrayMonitorNative.HasOverlappingPopup(area, -1));
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    [Theory]
    [InlineData(0, 0, 1920, 1040, 1750, 1040)]
    [InlineData(-1920, 0, 1920, 1040, -1900, 1040)]
    [InlineData(0, -1080, 1920, 1040, 1750, -1080)]
    [InlineData(48, 0, 1872, 1080, 0, 1000)]
    [InlineData(0, 48, 1920, 1032, 1800, 0)]
    public void PlacementStaysInMonitorWorkingAreaForEveryTaskbarEdgeAndScale(int x, int y, int w, int h, int ax, int ay)
    {
        var work = new System.Drawing.Rectangle(x, y, w, h);
        var anchor = new System.Drawing.Rectangle(ax, ay, 24, 24);
        foreach (double scale in new[] { 1d, 1.25, 1.5, 2d, 3d })
        {
            var result = TrayMonitorPolicy.Place(anchor, work, (int)(286 * scale), (int)(128 * scale));
            Assert.True(work.Contains(result));
            Assert.False(result.IntersectsWith(anchor));
        }
    }

    [Fact]
    public void MultiGpuMetricsBelongToSameAdapterAndInvalidReadingsStayUnknown()
    {
        var snapshot = new HardwareSnapshot();
        snapshot.Cpu.Temperature = float.NaN; snapshot.Cpu.Load = 101;
        snapshot.Gpus.Add(new() { Temperature = 32, Load = 95 });
        snapshot.Gpus.Add(new() { Temperature = 46, Load = 12 });
        snapshot.Gpus.Add(new() { Temperature = float.PositiveInfinity, Load = 80 });
        snapshot.Memory.Load = -2;
        var reading = TrayMonitorReading.From(snapshot);
        Assert.Equal(46f, reading.GpuTemperature); Assert.Equal(12f, reading.GpuLoad);
        Assert.Null(reading.CpuTemperature); Assert.Null(reading.CpuLoad); Assert.Null(reading.MemoryLoad);
        Assert.Equal(TrayMonitorReading.Empty, TrayMonitorReading.From(new HardwareSnapshot()));
    }

    [Fact]
    public void WindowsFormsTrayIdentityIsAvailableWithoutRuntimeGuessing()
    {
        foreach (string name in new[] { "Id", "Window" })
            Assert.IsAssignableFrom<FieldInfo>(typeof(TrayMonitorNative).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null));
    }

    [Fact]
    public void NativePanelCannotActivateInterceptClicksOrAppearInTaskbar()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            TrayMonitorWindow? window = null;
            try
            {
                window = new TrayMonitorWindow();
                var hwnd = new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
                long style = GetWindowLongPtr(hwnd, -20).ToInt64();
                Assert.Equal(0x080000a0L, style & 0x080000a0L);
                Assert.False(window.ShowActivated); Assert.False(window.ShowInTaskbar);
                Assert.False(window.IsHitTestVisible); Assert.False(window.IsVisible);
                Assert.True(TrayMonitorNative.GetDpiForWindow(hwnd) >= 96);
            }
            catch (Exception ex) { error = ex; }
            finally { window?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [Fact]
    public void RepeatedPositionUpdatesDoNotCoverTooltipAndPanelYieldsWithoutHiding()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            IntPtr tip = IntPtr.Zero;
            try
            {
                var rect = new System.Drawing.Rectangle(-30000, -30000, 286, 128);
                using var panel = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Synthetic monitoring panel")
                { PositionX = rect.X, PositionY = rect.Y, Width = rect.Width, Height = rect.Height,
                    WindowStyle = unchecked((int)0x90000000), ExtendedWindowStyle = 0x080000a0 });
                TrayMonitorNative.Move(panel.Handle, rect, true, promote: true);
                InitCommonControls();
                tip = CreateWindowEx(0x080000a8, "tooltips_class32", "Synthetic tray tooltip", 0x90000000,
                    rect.X + 20, rect.Y + 40, 120, 30, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                Assert.NotEqual(IntPtr.Zero, tip);
                var tipRect = new System.Drawing.Rectangle(rect.X + 20, rect.Y + 40, 120, 30);
                TrayMonitorNative.Move(tip, tipRect, true, promote: true);
                Assert.True(IsWindowVisible(tip));
                Assert.True(Above(tip, panel.Handle));
                for (int i = 0; i < 10; i++)
                {
                    TrayMonitorNative.Move(panel.Handle, rect, true);
                    Assert.True(Above(tip, panel.Handle));
                }
                // Also cover a tooltip that was already present when the panel
                // reappears after a menu: lower the panel, never dismiss either.
                TrayMonitorNative.Move(panel.Handle, rect, true, promote: true);
                Assert.True(Above(panel.Handle, tip));
                TrayMonitorNative.YieldToTooltips(panel.Handle);
                Assert.True(Above(tip, panel.Handle));
                Assert.True(IsWindowVisible(panel.Handle)); Assert.True(IsWindowVisible(tip));
                TrayMonitorNative.YieldToTooltips(panel.Handle);
                Assert.True(Above(tip, panel.Handle));
                Assert.True(TrayMonitorNative.TryBounds(panel.Handle, out var actual));
                Assert.Equal(rect, actual);
            }
            catch (Exception ex) { error = ex; }
            finally { if (tip != IntPtr.Zero) DestroyWindow(tip); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    private static bool Above(IntPtr first, IntPtr second)
    {
        IntPtr current = second;
        for (int i = 0; i < 2000 && current != IntPtr.Zero; i++)
        {
            current = GetZWindow(current, 3); // GW_HWNDPREV
            if (current == first) return true;
        }
        return false;
    }

    [System.Runtime.InteropServices.DllImport("comctl32.dll")] private static extern void InitCommonControls();
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(uint extended, string className, string text, uint style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr window);
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindow")] private static extern IntPtr GetZWindow(IntPtr window, uint command);

    [Theory]
    [InlineData("SteamPunk", "Russian")][InlineData("CyberTech", "Russian")]
    [InlineData("FrostCore", "Russian")][InlineData("MilitaryOps", "Russian")]
    [InlineData("SteamPunk", "English")][InlineData("CyberTech", "English")]
    [InlineData("FrostCore", "English")][InlineData("MilitaryOps", "English")]
    public void RealPanelRendersAllSkinsLanguagesMissingAndWideReadingsAtMultipleDpi(string skin, string language)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            var settings = SettingsService.Current;
            var oldSkin = settings.SkinName; var oldLanguage = settings.LanguageName;
            try
            {
                settings.SkinName = skin; settings.LanguageName = language; SettingsService.ApplyLanguagePreference();
                var view = new TrayMonitorView();
                Assert.Equal(skin, view.Skin);
                foreach (var scenario in new[] { "Normal", "Missing", "Wide" })
                {
                    view.UpdateReading(scenario == "Missing" ? TrayMonitorReading.Empty : scenario == "Wide"
                        ? new(-12, 100, 105, 100, 100, 127.9f, 128) : new(46, 12, 39, 4, 49, 7.8f, 16));
                    view.Measure(new Size(view.Width, view.Height));
                    view.Arrange(new Rect(0, 0, view.Width, view.Height)); view.UpdateLayout();
                    Assert.Equal(286, view.ActualWidth);
                    Assert.InRange(view.ActualHeight, 100, 128);
                    if (language == "English") Assert.All(view.DetailLabels(), text => Assert.DoesNotMatch("[А-Яа-яЁё]", text));
                    foreach (int dpi in new[] { 96, 120, 144, 192 })
                    {
                        var image = new RenderTargetBitmap((int)Math.Ceiling(view.Width * dpi / 96), (int)Math.Ceiling(view.Height * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
                        image.Render(view);
                        byte[] pixels = new byte[image.PixelWidth * image.PixelHeight * 4]; image.CopyPixels(pixels, image.PixelWidth * 4, 0);
                        Assert.Contains(pixels.Where((_, i) => i % 4 == 3), alpha => alpha > 0);
                        string? directory = Environment.GetEnvironmentVariable("THERMIQRA_TRAY_PREVIEWS");
                        if (!string.IsNullOrEmpty(directory))
                        {
                            Directory.CreateDirectory(directory);
                            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                            using var file = File.Create(Path.Combine(directory, $"{skin}-{language}-{scenario}-{dpi}.png")); encoder.Save(file);
                        }
                    }
                }
                // Configuration changes use the existing view, including missing readings.
                settings.SkinName = "FrostCore"; view.RefreshAppearance(); Assert.Equal("FrostCore", view.Skin);
            }
            catch (Exception ex) { error = ex; }
            finally { settings.SkinName = oldSkin; settings.LanguageName = oldLanguage; SettingsService.ApplyLanguagePreference(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
