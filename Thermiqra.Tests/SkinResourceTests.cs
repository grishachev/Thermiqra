using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Xml.Linq;
using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

[CollectionDefinition("WpfResources", DisableParallelization = true)]
public class WpfResourceCollection { }

[Collection("WpfResources")]
public class SkinResourceTests
{
    [Theory]
    [InlineData("CyberTech", "MainCyberTechVisibility")]
    [InlineData("SteamPunk", "SteamPunkVisibility")]
    [InlineData("FrostCore", "FrostCoreVisibility")]
    [InlineData("MilitaryOps", "MilitaryOpsVisibility")]
    public void SharedWindowResourcesLoadForEachSkinWithoutOpeningWindows(string skin, string activeKey)
    {
        Exception? error = null;
        Thread thread = new(() =>
        {
            try
            {
                var resources = new ResourceDictionary();
                ThemeManager.PopulateAppearance(resources, skin, "SteelBlue");
                foreach (string file in new[] { "ToolTipStyles", "ProgramInputStyles", "ProgramWindowStyles", "SkinWatermark" })
                    resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(
                        new Uri("/Thermiqra;component/UI/Appearance/" + file + ".xaml", UriKind.Relative)));
                Assert.Equal(Visibility.Visible, resources[activeKey]);
                Assert.Single(new[] { "MainCyberTechVisibility", "SteamPunkVisibility", "FrostCoreVisibility", "MilitaryOpsVisibility" },
                    k => (Visibility)resources[k] == Visibility.Visible);
                Assert.IsAssignableFrom<Brush>(resources["AccentBrush"]);
                Assert.NotEqual(((SolidColorBrush)resources["PrimaryTextBrush"]).Color,
                    ((SolidColorBrush)resources["WindowBackgroundBrush"]).Color);
                var host = new Grid { Resources = resources, Width = 900, Height = 600 };
                var background = (FrameworkElement)((DataTemplate)resources["SkinWatermarkTemplate"]).LoadContent();
                host.Children.Add(background);
                host.Children.Add(ProgramWindowChrome.Header("Example", "Synthetic layout"));
                var capacity = new DriveCapacitySummary { Width = 408 };
                capacity.Update(1024L * 1024 * 1024 * 1024, 256L * 1024 * 1024 * 1024);
                host.Children.Add(capacity);
                host.Measure(new Size(900, 600)); host.Arrange(new Rect(0, 0, 900, 600)); host.UpdateLayout();
                Assert.Equal(900, host.ActualWidth);
                Assert.NotNull(background);
                var source = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "StorageAnalyzerWindow.xaml"));
                XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
                var headerXml = new XElement(source.Root!.Element(presentation + "Grid")!.Elements(presentation + "Grid").First());
                foreach (var attribute in source.Root.Attributes().Where(a => a.IsNamespaceDeclaration))
                    headerXml.SetAttributeValue(attribute.Name, attribute.Value);
                headerXml.SetAttributeValue(XNamespace.Xmlns + "local", "clr-namespace:PCHardwareMonitor;assembly=Thermiqra");
                foreach (var element in headerXml.Descendants().Where(e => e.Name.NamespaceName == "clr-namespace:PCHardwareMonitor"))
                    element.Name = XName.Get(element.Name.LocalName, "clr-namespace:PCHardwareMonitor;assembly=Thermiqra");
                var header = (Grid)XamlReader.Parse(headerXml.ToString());
                header.Resources = resources;
                foreach (string prefix in new[] { "Cyber", "Steam", "Frost", "Military" })
                    ((DriveCapacitySummary)header.FindName(prefix + "Capacity")).Update(1024L * 1024 * 1024 * 1024, 256L * 1024 * 1024 * 1024);
                foreach (double width in new[] { 1240d, 960d })
                {
                    header.Measure(new Size(width, double.PositiveInfinity));
                    Assert.True(header.DesiredSize.Width <= width + 1);
                    Assert.True(header.DesiredSize.Height < 150);
                    header.Arrange(new Rect(0, 0, width, header.DesiredSize.Height)); header.UpdateLayout();
                }
                string? previews = Environment.GetEnvironmentVariable("THERMIQRA_HEADER_PREVIEWS");
                if (!string.IsNullOrEmpty(previews))
                {
                    Directory.CreateDirectory(previews);
                    var bitmap = new RenderTargetBitmap(960, (int)Math.Ceiling(header.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(header);
                    var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(Path.Combine(previews, skin + ".png")); png.Save(file);
                }
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Synthetic resource layout timed out.");
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
