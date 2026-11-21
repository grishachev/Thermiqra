using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

[Collection("WpfResources")]
public class UpdateButtonStatesTests
{
    [Theory]
    [InlineData("CyberTech", "English")][InlineData("SteamPunk", "English")]
    [InlineData("FrostCore", "English")][InlineData("MilitaryOps", "English")]
    [InlineData("CyberTech", "Russian")][InlineData("SteamPunk", "Russian")]
    [InlineData("FrostCore", "Russian")][InlineData("MilitaryOps", "Russian")]
    public void UpdateButtonsUseSkinForHoverPressedFocusDisabledAndDefault(string skin, string language)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            string previous = SettingsService.Current.LanguageName;
            try
            {
                SettingsService.Current.LanguageName = language; SettingsService.ApplyLanguagePreference();
                string notes = UpdateReleaseNotes.BuildSummary(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "NextRelease.md")), language == "Russian");
                var update = new UpdateInfo(new Version(1, 1, 2), new Version(1, 1, 1), "1.1.2", "1.1.1", "", notes, null, null, null);
                var dialog = UpdatePromptView.Create(update, null, () => { });
                ThemeManager.PopulateAppearance(dialog.Resources, skin, "SteelBlue");
                var root = (Grid)dialog.Content;
                dialog.Content = null; root.Resources = dialog.Resources;
                var host = new Border { Width = 560, Height = 408, Background = (Brush)dialog.FindResource("WindowBackgroundBrush"), Child = root };
                void Layout() { host.Measure(new Size(560, 408)); host.Arrange(new Rect(0, 0, 560, 408)); host.UpdateLayout(); }
                Layout();
                var buttons = ((WrapPanel)root.Children[3]).Children.Cast<Button>().ToArray();
                Color BrushColor(object value) => Assert.IsType<SolidColorBrush>(value).Color;
                Color accent = BrushColor(dialog.FindResource("AccentBrush"));
                Color dark = BrushColor(dialog.FindResource("WindowBackgroundBrush"));
                string? output = Environment.GetEnvironmentVariable("THERMIQRA_BUTTON_PREVIEWS");
                void Snapshot(string state)
                {
                    if (string.IsNullOrEmpty(output)) return;
                    Directory.CreateDirectory(output);
                    var bitmap = new RenderTargetBitmap(560, 408, 96, 96, PixelFormats.Pbgra32); bitmap.Render(host);
                    var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(Path.Combine(output, skin + "-" + language + "-" + state + ".png")); png.Save(file);
                }
                foreach (var button in buttons)
                {
                    button.ApplyTemplate();
                    var border = Assert.IsType<Border>(button.Template.FindName("ThemeButtonBorder", button));
                    var presenter = Assert.IsType<ContentPresenter>(button.Template.FindName("ThemeButtonContent", button));
                    Assert.NotEqual(Colors.White, BrushColor(border.Background));
                    Force(button, typeof(UIElement), "IsMouseOver", true); Layout();
                    Assert.Equal(accent, BrushColor(border.Background));
                    Assert.Equal(dark, BrushColor(TextElement.GetForeground(presenter)));
                    Assert.True(Contrast(accent, dark) >= 4.5);
                    if (button == buttons[0]) Snapshot("Hover");
                    // Local foreground assignments in future dialogs must not restore white-on-light hover text.
                    button.SetResourceReference(Control.ForegroundProperty, "PrimaryTextBrush"); Layout();
                    Assert.Equal(dark, BrushColor(TextElement.GetForeground(presenter)));
                    Force(button, typeof(ButtonBase), "IsPressed", true); Layout();
                    Assert.Equal(accent, BrushColor(border.Background)); Assert.Equal(0.8, border.Opacity);
                    if (button == buttons[0]) Snapshot("Pressed");
                    Force(button, typeof(ButtonBase), "IsPressed", false);
                    Force(button, typeof(UIElement), "IsMouseOver", false);
                    Force(button, typeof(UIElement), "IsKeyboardFocused", true); Layout();
                    Assert.Equal(accent, BrushColor(border.BorderBrush));
                    Assert.Equal(new Thickness(2), border.BorderThickness);
                    Assert.NotEqual(Colors.White, BrushColor(border.Background));
                    if (button == buttons[0]) Snapshot("Focus");
                    Force(button, typeof(UIElement), "IsKeyboardFocused", false);
                    button.IsEnabled = false; Layout();
                    Assert.Equal(0.5, button.Opacity); Assert.NotEqual(Colors.White, BrushColor(border.Background));
                    if (button == buttons[0]) Snapshot("Disabled");
                    button.IsEnabled = true; Layout();
                }
                Assert.True(buttons[2].IsDefault);
                Snapshot("Normal");
                dialog.Close();
            }
            catch (Exception ex) { error = ex; }
            finally { SettingsService.Current.LanguageName = previous; SettingsService.ApplyLanguagePreference(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    private static void Force(DependencyObject target, Type owner, string property, bool value)
    {
        var field = owner.GetField(property + "PropertyKey", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
        Assert.NotNull(field);
        target.SetValue(Assert.IsType<DependencyPropertyKey>(field.GetValue(null)), value);
    }
    private static double Contrast(Color a, Color b)
    {
        double L(Color c)
        {
            double Linear(byte x) { double s = x / 255d; return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
            return 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
        }
        double x = L(a), y = L(b); return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }
}
