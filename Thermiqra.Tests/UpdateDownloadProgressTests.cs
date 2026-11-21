using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

public class UpdateDownloadMeterTests
{
    [Fact]
    public async Task MeasuresIntervalSpeedAndFallsToZeroWhileWaiting()
    {
        TimeSpan now = TimeSpan.Zero;
        await using var meter = new UpdateDownloadReporter(null, null, () => now, heartbeat: false);
        meter.SetStage(UpdateDownloadStage.Downloading, 10 * 1048576);
        now = TimeSpan.FromSeconds(2); meter.Received(4 * 1048576);
        var active = meter.Sample();
        Assert.Equal(2 * 1048576, active.BytesPerSecond); Assert.Equal(40, active.Percent);
        Assert.Equal(TimeSpan.Zero, active.IdleFor);
        now = TimeSpan.FromSeconds(9);
        var idle = meter.Sample();
        Assert.Equal(0, idle.BytesPerSecond); Assert.Equal(TimeSpan.FromSeconds(7), idle.IdleFor);
        Assert.Equal(active.ReceivedBytes, idle.ReceivedBytes);
    }
    [Fact]
    public async Task UnknownSizeDoesNotInventPercentAndCompletePayloadIsNotReadyBeforeVerification()
    {
        await using var meter = new UpdateDownloadReporter(null, null, heartbeat: false);
        meter.SetStage(UpdateDownloadStage.Downloading);
        meter.Received(1048576); Assert.Null(meter.Sample().Percent);
        meter.SetStage(UpdateDownloadStage.Downloading, 1048576);
        Assert.Equal(99, meter.Sample().Percent);
        meter.SetStage(UpdateDownloadStage.Verifying); Assert.Equal(99, meter.Sample().Percent);
        meter.SetStage(UpdateDownloadStage.Ready); Assert.Equal(100, meter.Sample().Percent);
        Assert.Throws<ArgumentOutOfRangeException>(() => meter.Received(0));
    }
    [Fact]
    public async Task HeartbeatContinuesWithoutDataAndStopsWhenDisposed()
    {
        var updates = new ConcurrentQueue<UpdateDownloadProgress>();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var progress = new InlineProgress<UpdateDownloadProgress>(p => { updates.Enqueue(p); if (updates.Count >= 3) ready.TrySetResult(); });
        var meter = new UpdateDownloadReporter(null, progress);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(updates.Last().IdleFor > TimeSpan.Zero);
        await meter.DisposeAsync(); int count = updates.Count;
        await Task.Delay(650); Assert.Equal(count, updates.Count);
    }
    internal sealed class InlineProgress<T>(Action<T> action) : IProgress<T> { public void Report(T value) => action(value); }
}

[Collection("WpfResources")]
public class UpdateDownloadViewTests
{
    [Theory]
    [InlineData("CyberTech", "English")][InlineData("SteamPunk", "English")]
    [InlineData("FrostCore", "English")][InlineData("MilitaryOps", "English")]
    [InlineData("CyberTech", "Russian")][InlineData("SteamPunk", "Russian")]
    [InlineData("FrostCore", "Russian")][InlineData("MilitaryOps", "Russian")]
    public void TransferWaitingUnknownSizeAndVerificationRenderWithThemedControls(string skin, string language)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            string previous = SettingsService.Current.LanguageName;
            try
            {
                SettingsService.Current.LanguageName = language; SettingsService.ApplyLanguagePreference();
                var view = new UpdateDownloadView(null);
                ThemeManager.PopulateAppearance(view.Window.Resources, skin, "SteelBlue");
                var root = (Grid)view.Window.Content; view.Window.Content = null; root.Resources = view.Window.Resources;
                var host = new Border { Width = 480, Height = 244, Background = (Brush)view.Window.FindResource("WindowBackgroundBrush"), Child = root };
                void Render(string scenario)
                {
                    host.Measure(new Size(480, 244)); host.Arrange(new Rect(0, 0, 480, 244)); host.UpdateLayout();
                    Assert.True(view.CancelButton.TranslatePoint(new Point(0, view.CancelButton.ActualHeight), root).Y <= root.ActualHeight + 1);
                    string text = view.Status.Text + view.Amount.Text + view.Speed.Text;
                    if (language == "English") Assert.DoesNotMatch("[А-Яа-яЁё]", text); else Assert.Matches("[А-Яа-яЁё]", text);
                    string? path = Environment.GetEnvironmentVariable("THERMIQRA_TRANSFER_PREVIEWS");
                    if (string.IsNullOrEmpty(path)) return;
                    Directory.CreateDirectory(path);
                    var bitmap = new RenderTargetBitmap(480, 244, 96, 96, PixelFormats.Pbgra32); bitmap.Render(host);
                    var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                    using var output = File.Create(Path.Combine(path, skin + "-" + language + "-" + scenario + ".png")); png.Save(output);
                }
                view.Update(new(UpdateDownloadStage.Downloading, 24 * 1048576, 60 * 1048576, 1.5 * 1048576, TimeSpan.Zero)); Render("Downloading");
                Assert.Contains("40%", view.Amount.Text); Assert.False(view.Bar.IsIndeterminate);
                Assert.Contains(language == "Russian" ? "МБ/с" : "MB/s", view.Speed.Text);
                view.CancelButton.ApplyTemplate(); Assert.NotNull(view.CancelButton.Template.FindName("ThemeButtonBorder", view.CancelButton));
                view.Bar.ApplyTemplate(); Assert.NotNull(view.Bar.Template.FindName("MovingMark", view.Bar));
                view.Update(new(UpdateDownloadStage.Downloading, 24 * 1048576, 60 * 1048576, 0, TimeSpan.FromSeconds(7))); Render("Waiting");
                Assert.Contains("7", view.Status.Text);
                view.Update(new(UpdateDownloadStage.Downloading, 3 * 1048576, null, 8192, TimeSpan.Zero)); Render("UnknownSize");
                Assert.DoesNotContain("%", view.Amount.Text); Assert.True(view.Bar.IsIndeterminate);
                Assert.Contains(language == "Russian" ? "КБ/с" : "KB/s", view.Speed.Text);
                view.Update(new(UpdateDownloadStage.Verifying, 60 * 1048576, 60 * 1048576, 0, TimeSpan.Zero)); Render("Verifying");
                Assert.True(view.Bar.IsIndeterminate);
                view.StartingInstallation(); Render("Installing");
                Assert.False(view.CancelButton.IsEnabled); Assert.Equal(100, view.Bar.Value);
                view.Window.Close();
            }
            catch (Exception ex) { error = ex; }
            finally { SettingsService.Current.LanguageName = previous; SettingsService.ApplyLanguagePreference(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
