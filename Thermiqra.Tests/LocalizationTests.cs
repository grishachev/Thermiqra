using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Xml.Linq;
using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

[Collection("WpfResources")]
public class LocalizationTests
{
    [Fact]
    public void GaugeLabelsUseTheirEnglishAndRussianDictionaryWithoutStartingMonitoring()
    {
        string previous = SettingsService.Current.LanguageName;
        try
        {
            var type = typeof(RingGauge);
            var dictionary = (Dictionary<string, string>)type.GetField("RussianToEnglishText", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            var translate = type.GetMethod("LocalizeExistingText", BindingFlags.Static | BindingFlags.NonPublic)!;
            SettingsService.Current.LanguageName = "English"; SettingsService.ApplyLanguagePreference();
            foreach (var pair in dictionary)
            {
                string result = (string)translate.Invoke(null, new object[] { pair.Key })!;
                Assert.Equal(pair.Value, result); Assert.DoesNotMatch("[А-Яа-яЁё]", result);
            }
            SettingsService.Current.LanguageName = "Russian"; SettingsService.ApplyLanguagePreference();
            foreach (var group in dictionary.GroupBy(p => p.Value).Where(g => g.Count() == 1))
                Assert.Equal(group.Single().Key, translate.Invoke(null, new object[] { group.Key }));
        }
        finally { SettingsService.Current.LanguageName = previous; SettingsService.ApplyLanguagePreference(); }
    }

    [Theory] [InlineData("Russian")][InlineData("English")]
    public void UpdateFailuresAreLocalized(string language)
    {
        string previous = SettingsService.Current.LanguageName;
        try
        {
            SettingsService.Current.LanguageName = language; SettingsService.ApplyLanguagePreference();
            var update = new UpdateInfo(new Version(1, 0, 1), new Version(1, 0, 0), "1.0.1", "1.0.0", "", "", "", "Thermiqra_Setup_1.0.1.exe", new string('0', 64));
            var checksum = Assert.Throws<InvalidDataException>(() => UpdateIntegrityPolicy.RequiredHash(null));
            var signature = Assert.Throws<InvalidDataException>(() => UpdateSignaturePolicy.Verify(Array.Empty<byte>(), update, 1, new string('0', 64)));
            foreach (string message in new[] { checksum.Message, signature.Message })
                if (language == "English") Assert.DoesNotMatch("[А-Яа-яЁё]", message); else Assert.Matches("[А-Яа-яЁё]", message);
        }
        finally { SettingsService.Current.LanguageName = previous; SettingsService.ApplyLanguagePreference(); }
    }

    [Fact]
    public void EveryStaticXamlLabelHasAnEnglishTranslation()
    {
        string previous = SettingsService.Current.LanguageName;
        try
        {
            SettingsService.Current.LanguageName = "English"; SettingsService.ApplyLanguagePreference();
            var missing = new List<string>();
            foreach (string path in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "SourceXaml"), "*.xaml", SearchOption.AllDirectories))
                foreach (var attribute in XDocument.Load(path).Descendants().Attributes().Where(a => a.Name.LocalName is "Text" or "Content" or "Header" or "Title" or "ToolTip"))
                    if (Regex.IsMatch(attribute.Value, "[А-Яа-яЁё]") && Regex.IsMatch(SettingsService.TranslateKnown(attribute.Value), "[А-Яа-яЁё]"))
                        missing.Add(Path.GetFileName(path) + ": " + attribute.Value);
            Assert.Empty(missing);
        }
        finally { SettingsService.Current.LanguageName = previous; SettingsService.ApplyLanguagePreference(); }
    }

    [Theory]
    [InlineData("CyberTech", "English")][InlineData("SteamPunk", "English")][InlineData("FrostCore", "English")][InlineData("MilitaryOps", "English")]
    [InlineData("CyberTech", "Russian")][InlineData("SteamPunk", "Russian")][InlineData("FrostCore", "Russian")][InlineData("MilitaryOps", "Russian")]
    public void ReviewAndResultStatesRenderInBothLanguagesAndAllSkins(string skin, string language)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            string previous = SettingsService.Current.LanguageName;
            try
            {
                SettingsService.Current.LanguageName = language; SettingsService.ApplyLanguagePreference();
                var resources = new ResourceDictionary(); ThemeManager.PopulateAppearance(resources, skin, "SteelBlue");
                foreach (string file in new[] { "ToolTipStyles", "ProgramInputStyles", "ProgramWindowStyles", "SkinWatermark" })
                    resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/Thermiqra;component/UI/Appearance/" + file + ".xaml", UriKind.Relative)));
                var mainSource = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "SourceXaml", "MainWindow.xaml"));
                XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
                var styleXml = new XElement(p + "ResourceDictionary", mainSource.Root!.Element(p + "Window.Resources")!.Elements());
                foreach (var attribute in mainSource.Root.Attributes().Where(a => a.IsNamespaceDeclaration)) styleXml.SetAttributeValue(attribute.Name, attribute.Value);
                resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(styleXml.ToString()));
                var owner = new Window { Resources = resources };
                foreach (string scenario in new[] { "Empty", "ProtectedPackage", "PackageExternal", "Limited", "Items", "Result" })
                {
                    var baseline = new ProgramRemovalBaseline("synthetic-localization", DateTime.UtcNow,
                        scenario is "ProtectedPackage" or "PackageExternal" ? "msix:ExampleVendor.ExampleApplication_family" : "registry:HKCU:64:Example",
                        "Example application", "1.0.0", new(), new(), NativeResult: new(InstalledProgramRemovalState.Removed, 0, "", true));
                    var found = new List<ProgramLeftoverProbe>();
                    if (scenario == "ProtectedPackage") found.Add(new("Directory", @"C:\Program Files\WindowsApps\Example.Package", ProgramLeftoverGroup.Protected,
                        "Каталог пакета обслуживает Windows.", "Windows manages the package folder."));
                    if (scenario == "PackageExternal")
                    {
                        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        found.Add(new("UserData", Path.Combine(root, "Example Vendor", "Example application"), ProgramLeftoverGroup.UserData,
                            "Подтверждённая папка данных продукта.", "Confirmed product data folder.", EvidenceRoot: root,
                            EvidenceTarget: ProgramPackageDataLayout.Evidence, EvidenceFingerprint: "synthetic-fingerprint"));
                    }
                    if (scenario == "Items")
                        foreach (var group in Enum.GetValues<ProgramLeftoverGroup>()) found.Add(new("Directory", @"C:\Example\" + group, group,
                            "Тестовое пояснение.", "Synthetic explanation."));
                    var limits = new List<ProgramReviewLimitation>();
                    if (scenario == "Limited") limits.Add(new("Часть данных недоступна", "Some data is unavailable"));
                    var report = new ProgramLeftoverReport(baseline, DateTime.UtcNow, found, limits, found.Count);
                    UIElement content = scenario == "Result" ? ProgramCleanupPresentation.ResultContent(report,
                        new("synthetic-backup", baseline.Id, baseline.Name, DateTime.UtcNow, new(), baseline.Identity), true, "") :
                        new ProgramLeftoverReviewView(owner, report);
                    var host = new Grid { Width = 900, Height = 740, Resources = resources };
                    host.SetResourceReference(Panel.BackgroundProperty, "WindowBackgroundBrush"); host.Children.Add(content);
                    host.Measure(new Size(900, 740)); host.Arrange(new Rect(0, 0, 900, 740)); host.UpdateLayout();
                    string text = string.Join("\n", VisualText(host));
                    Assert.NotEmpty(text);
                    if (language == "English") Assert.DoesNotMatch("[А-Яа-яЁё]", text);
                    else Assert.Matches("[А-Яа-яЁё]", text);
                    Assert.DoesNotContain("{Binding", text);
                    if (scenario == "Limited") Assert.DoesNotContain(SettingsService.L("Остатки не найдены", "No leftovers found"), string.Join("\n", VisualText(host, visibleOnly: true)));
                    if (scenario == "PackageExternal") Assert.Contains(SettingsService.L("Выбрано: 1", "Selected: 1"), text);
                    string? directory = Environment.GetEnvironmentVariable("THERMIQRA_LOCALIZATION_PREVIEWS");
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                        var bitmap = new RenderTargetBitmap(900, 740, 96, 96, PixelFormats.Pbgra32); bitmap.Render(host);
                        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                        using var file = File.Create(Path.Combine(directory, skin + "-" + language + "-" + scenario + ".png")); png.Save(file);
                    }
                }
                foreach (string window in new[] { "MainWindow", "SettingsWindow", "SystemInfoWindow", "StatisticsWindow", "StorageAnalyzerWindow", "NotificationWindow" })
                {
                    var source = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "SourceXaml", window + ".xaml"));
                    var xml = source.Root!;
                    double width = double.TryParse((string?)xml.Attribute("Width"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w) ? w : 1100;
                    double height = double.TryParse((string?)xml.Attribute("Height"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var h) ? h : 850;
                    double minWidth = double.TryParse((string?)xml.Attribute("MinWidth"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var mw) ? mw : width;
                    xml.Name = p + "Grid";
                    foreach (var attribute in xml.Attributes().Where(a => !a.IsNamespaceDeclaration).ToList()) attribute.Remove();
                    foreach (var element in xml.Elements().Where(e => e.Name.LocalName.StartsWith("Window.")).ToList())
                    {
                        if (element.Name.LocalName == "Window.Resources") element.Name = p + "Grid.Resources";
                        else element.Remove();
                    }
                    var events = new HashSet<string>(new[] { "Click", "Loaded", "Closing", "Closed", "SizeChanged", "SelectionChanged", "ValueChanged", "Checked", "Unchecked", "TextChanged", "PreviewMouseWheel", "MouseLeftButtonDown", "MouseLeftButtonUp", "MouseDoubleClick", "KeyDown", "PreviewKeyDown" });
                    events.UnionWith(typeof(FrameworkElement).Assembly.GetExportedTypes().SelectMany(t => t.GetEvents()).Select(e => e.Name));
                    foreach (var attribute in xml.Descendants().Attributes().ToList())
                    {
                        if (events.Contains(attribute.Name.LocalName)) attribute.Remove();
                        else if (attribute.Name.LocalName is "Text" or "Content" or "Header" or "Title" or "ToolTip") attribute.Value = SettingsService.TranslateKnown(attribute.Value);
                    }
                    foreach (var element in xml.DescendantsAndSelf())
                    {
                        if (element.Name.NamespaceName == "clr-namespace:PCHardwareMonitor")
                            element.Name = XName.Get(element.Name.LocalName, "clr-namespace:PCHardwareMonitor;assembly=Thermiqra");
                        foreach (var attribute in element.Attributes().Where(a => a.IsNamespaceDeclaration && a.Value == "clr-namespace:PCHardwareMonitor"))
                            attribute.Value += ";assembly=Thermiqra";
                    }
                    var windowResources = xml.Element(p + "Grid.Resources");
                    if (windowResources == null) { windowResources = new XElement(p + "Grid.Resources"); xml.AddFirst(windowResources); }
                    var dictionary = windowResources.Element(p + "ResourceDictionary");
                    if (dictionary == null)
                    {
                        dictionary = new XElement(p + "ResourceDictionary", windowResources.Elements().ToList());
                        windowResources.RemoveNodes(); windowResources.Add(dictionary);
                    }
                    var merged = dictionary.Element(p + "ResourceDictionary.MergedDictionaries");
                    if (merged == null) { merged = new XElement(p + "ResourceDictionary.MergedDictionaries"); dictionary.AddFirst(merged); }
                    foreach (string file in new[] { "ToolTipStyles", "ProgramInputStyles", "ProgramWindowStyles", "SkinWatermark" })
                        merged.Add(new XElement(p + "ResourceDictionary", new XAttribute("Source", "/Thermiqra;component/UI/Appearance/" + file + ".xaml")));
                    var surface = (FrameworkElement)XamlReader.Parse(xml.ToString());
                    var host = new Grid { Width = width, Height = height, Resources = resources };
                    host.SetResourceReference(Panel.BackgroundProperty, "WindowBackgroundBrush"); host.Children.Add(surface);
                    host.Measure(new Size(width, height)); host.Arrange(new Rect(0, 0, width, height)); host.UpdateLayout();
                    string text = string.Join("\n", VisualText(host));
                    Assert.NotEmpty(text);
                    if (language == "English") Assert.DoesNotMatch("[А-Яа-яЁё]", text);
                    string? directory = Environment.GetEnvironmentVariable("THERMIQRA_LOCALIZATION_PREVIEWS");
                    if (!string.IsNullOrEmpty(directory))
                    {
                        var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(host);
                        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                        using var file = File.Create(Path.Combine(directory, skin + "-" + language + "-" + window + ".png")); png.Save(file);
                    }
                    host.Width = minWidth; host.Measure(new Size(minWidth, height)); host.Arrange(new Rect(0, 0, minWidth, height)); host.UpdateLayout();
                    if (!string.IsNullOrEmpty(directory) && minWidth != width)
                    {
                        var bitmap = new RenderTargetBitmap((int)minWidth, (int)height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(host);
                        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                        using var file = File.Create(Path.Combine(directory, skin + "-" + language + "-" + window + "-Min.png")); png.Save(file);
                    }
                }
                owner.Close();
            }
            catch (Exception ex) { error = ex; }
            finally { SettingsService.Current.LanguageName = previous; SettingsService.ApplyLanguagePreference(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Offscreen localization check timed out.");
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    private static IEnumerable<string> VisualText(DependencyObject node, bool visibleOnly = false)
    {
        if (visibleOnly && node is UIElement element && element.Visibility != Visibility.Visible) yield break;
        if (node is TextBlock text && text.Text.Length > 0) yield return text.Text;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            foreach (string child in VisualText(VisualTreeHelper.GetChild(node, i), visibleOnly)) yield return child;
    }
}
