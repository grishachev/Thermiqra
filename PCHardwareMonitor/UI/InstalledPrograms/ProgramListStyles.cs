using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;

namespace PCHardwareMonitor;

internal static class ProgramListStyles
{
    internal static void Apply(ListView list, string? tooltip = null)
    {
        list.FontSize = 14;
        list.SetResourceReference(Control.BackgroundProperty, "CardBackgroundBrush");
        list.SetResourceReference(Control.ForegroundProperty, "PrimaryTextBrush");
        list.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
        // System templates paint disabled lists and hovered rows with system
        // colours. Own these states so nested modal windows keep the skin.
        list.Template = (ControlTemplate)XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ListView">
              <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1">
                <ScrollViewer x:Name="PART_ScrollViewer" Style="{DynamicResource {x:Static GridView.GridViewScrollViewerStyleKey}}" Focusable="False"
                              CanContentScroll="True" Padding="{TemplateBinding Padding}">
                  <ItemsPresenter/>
                </ScrollViewer>
              </Border>
            </ControlTemplate>
            """);
        if (list.View is GridView view)
        {
            Style header = new(typeof(GridViewColumnHeader));
            header.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("CardBackgroundBrush")));
            header.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("PrimaryTextBrush")));
            header.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("BorderBrush")));
            header.Setters.Add(new Setter(Control.FontSizeProperty, 14d));
            header.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            header.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8)));
            header.Setters.Add(new Setter(Control.TemplateProperty, (ControlTemplate)XamlReader.Parse("""
                <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="GridViewColumnHeader">
                  <Grid>
                    <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="0,0,1,1" Padding="{TemplateBinding Padding}">
                      <ContentPresenter HorizontalAlignment="Left" VerticalAlignment="Center" RecognizesAccessKey="True"/>
                    </Border>
                    <Thumb x:Name="PART_HeaderGripper" Width="8" HorizontalAlignment="Right" Background="Transparent">
                      <Thumb.Template><ControlTemplate TargetType="Thumb"><Border Background="Transparent"/></ControlTemplate></Thumb.Template>
                    </Thumb>
                  </Grid>
                </ControlTemplate>
                """)));
            view.ColumnHeaderContainerStyle = header;
        }
        Style rows = new(typeof(ListViewItem));
        rows.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 9, 6, 9)));
        rows.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("CardBackgroundBrush")));
        rows.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        rows.Setters.Add(new Setter(Control.TemplateProperty, (ControlTemplate)XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ListViewItem">
              <Border Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}"
                      TextElement.Foreground="{TemplateBinding Foreground}">
                <GridViewRowPresenter Content="{TemplateBinding Content}"
                  Columns="{Binding View.Columns, RelativeSource={RelativeSource AncestorType=ListView}}"/>
              </Border>
            </ControlTemplate>
            """)));
        rows.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("PrimaryTextBrush")));
        if (tooltip != null)
        {
            rows.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding(tooltip)));
            rows.Setters.Add(new Setter(ToolTipService.InitialShowDelayProperty, 700));
            rows.Setters.Add(new Setter(ToolTipService.BetweenShowDelayProperty, 0));
        }
        Trigger hover = new() { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("InputBackgroundBrush")));
        rows.Triggers.Add(hover);
        Trigger selected = new() { Property = ListViewItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("AccentBrush")));
        selected.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("WindowBackgroundBrush")));
        rows.Triggers.Add(selected);
        list.ItemContainerStyle = rows;
        ScrollViewer.SetVerticalScrollBarVisibility(list, ScrollBarVisibility.Auto);
    }
}
