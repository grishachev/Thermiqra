using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

[Collection("WpfResources")]
public class UpdateReleaseNotesTests
{
    [Theory]
    [InlineData("Russian")]
    [InlineData("English")]
    public void Published100ParserReadsLocalizedHighlightsFromHiddenReleaseSections(string language)
    {
        string previous = SettingsService.Current.LanguageName;
        try
        {
            SettingsService.Current.LanguageName = language;
            SettingsService.ApplyLanguagePreference();
            string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "NextRelease.md"));
            string legacy = Published100ReleaseNotes.Build(body);
            Assert.Equal(UpdateReleaseNotes.BuildSummary(body, language == "Russian"), legacy);
            Assert.Equal(4, legacy.Split(Environment.NewLine).Length);
            Assert.True(legacy.Length <= 500);
            Assert.DoesNotContain("<!--", legacy);
            if (language == "Russian") Assert.Matches("[А-Яа-яЁё]", legacy);
            else Assert.DoesNotMatch("[А-Яа-яЁё]", legacy);
        }
        finally { SettingsService.Current.LanguageName = previous; SettingsService.ApplyLanguagePreference(); }
    }

    [Fact]
    public void PreparedReleaseIsEnglishOnGitHubAndCompactInBothAppLanguages()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "NextRelease.md"));
        string visible = System.Text.RegularExpressions.Regex.Replace(body, @"<!--[\s\S]*?-->", "");
        Assert.DoesNotMatch("[А-Яа-яЁё]", visible);
        foreach (bool russian in new[] { false, true })
        {
            string summary = UpdateReleaseNotes.BuildSummary(body, russian);
            Assert.InRange(summary.Length, 1, 500);
            Assert.Equal(4, summary.Split(Environment.NewLine).Length);
            Assert.DoesNotContain("<!--", summary);
            if (russian) Assert.Matches("[А-Яа-яЁё]", summary);
            else Assert.DoesNotMatch("[А-Яа-яЁё]", summary);
        }
        // Published clients cannot parse the new metadata: their first four lines must still be useful.
        string[] legacyLines = body.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).Take(4).ToArray();
        Assert.All(legacyLines, l => Assert.StartsWith("- ", l));
        Assert.True(string.Join(Environment.NewLine, legacyLines).Length < 500);
    }

    [Theory]
    [InlineData(false, "English entry")]
    [InlineData(true, "Русская запись")]
    public void LegacyBilingualReleasesKeepTheirSelectedLanguage(bool russian, string expected)
    {
        string body = "## Русский\n- Русская запись\n## English\n- English entry";
        Assert.Equal("• " + expected, UpdateReleaseNotes.BuildSummary(body, russian));
    }

    [Fact]
    public void MissingTranslationFallsBackToEnglishWithoutLeakingMetadataOrMarkdown()
    {
        string body = "# Release\n- **Visible** [details](https://example.test)\n<!-- internal metadata -->\n- `Second`";
        Assert.Equal("• Visible details" + Environment.NewLine + "• Second", UpdateReleaseNotes.BuildSummary(body, true));
        Assert.Equal("• Only English", UpdateReleaseNotes.BuildSummary("<!-- thermiqra-summary:en\n- Only English\n/thermiqra-summary -->", true));
        Assert.Equal("Visible", UpdateReleaseNotes.BuildSummary("Visible\n<!-- unclosed metadata", false));
        Assert.Empty(UpdateReleaseNotes.BuildSummary(null, false));
    }

    [Fact]
    public void OversizedNotesAreBoundedWithoutSplittingSurrogatePair()
    {
        string summary = UpdateReleaseNotes.BuildSummary(new string('a', 498) + "😀tail", false);
        Assert.True(summary.Length <= 500);
        Assert.EndsWith("…", summary);
        Assert.False(char.IsHighSurrogate(summary[^2]));
    }
}

[Collection("WpfResources")]
public class UpdatePromptLayoutTests
{
    [Theory]
    [InlineData("CyberTech", "English")][InlineData("SteamPunk", "English")]
    [InlineData("FrostCore", "English")][InlineData("MilitaryOps", "English")]
    [InlineData("CyberTech", "Russian")][InlineData("SteamPunk", "Russian")]
    [InlineData("FrostCore", "Russian")][InlineData("MilitaryOps", "Russian")]
    public void LongNotesScrollAndActionsStayVisibleWithoutStartingApplication(string skin, string language)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            string old = SettingsService.Current.LanguageName;
            try
            {
                SettingsService.Current.LanguageName = language; SettingsService.ApplyLanguagePreference();
                string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "NextRelease.md"));
                string summary = UpdateReleaseNotes.BuildSummary(body, language == "Russian");
                var update = new UpdateInfo(new Version(1, 0, 1), new Version(1, 0, 0), "1.0.1", "1.0.0", "https://example.test", summary, null, null, null);
                int opened = 0;
                Window dialog = UpdatePromptView.Create(update, null, () => opened++);
                ThemeManager.PopulateAppearance(dialog.Resources, skin, "SteelBlue");
                var host = (Grid)dialog.Content;
                dialog.Content = null;
                host.Resources = dialog.Resources;
                double width = dialog.Width, height = dialog.Height - 32;
                var backdrop = new Border { Width = width, Height = height, Background = (Brush)dialog.FindResource("WindowBackgroundBrush"), Child = host };
                backdrop.Measure(new Size(width, height)); backdrop.Arrange(new Rect(0, 0, width, height)); backdrop.UpdateLayout();
                var buttons = (WrapPanel)host.Children[3];
                Assert.Equal(3, buttons.Children.Count);
                Assert.True(((Button)buttons.Children[2]).IsDefault);
                Assert.True(((Button)buttons.Children[1]).IsCancel);
                ((Button)buttons.Children[0]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(1, opened);
                Assert.True(buttons.TranslatePoint(new Point(0, buttons.ActualHeight), host).Y <= host.ActualHeight + 1);
                string? output = Environment.GetEnvironmentVariable("THERMIQRA_UPDATE_PREVIEWS");
                if (!string.IsNullOrEmpty(output))
                {
                    Directory.CreateDirectory(output);
                    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(height), 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(backdrop);
                    var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(Path.Combine(output, skin + "-" + language + ".png")); png.Save(file);
                }
                var scroll = (ScrollViewer)host.Children[2];
                var content = (StackPanel)scroll.Content;
                ((TextBlock)content.Children[1]).Text = string.Join('\n', Enumerable.Repeat(summary, 30));
                backdrop.Measure(new Size(width, height)); backdrop.Arrange(new Rect(0, 0, width, height)); backdrop.UpdateLayout();
                Assert.True(scroll.ScrollableHeight > 0);
                Assert.True(buttons.TranslatePoint(new Point(0, buttons.ActualHeight), host).Y <= host.ActualHeight + 1);
                dialog.Close();
            }
            catch (Exception ex) { error = ex; }
            finally { SettingsService.Current.LanguageName = old; SettingsService.ApplyLanguagePreference(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
