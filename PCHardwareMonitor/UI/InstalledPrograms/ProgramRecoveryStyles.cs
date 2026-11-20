using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace PCHardwareMonitor;

internal static class ProgramRecoveryStyles
{
    internal static void Apply(ComboBox combo)
    {
        combo.FocusVisualStyle = null;
        // Dynamic resources are shared by all four skins; no system-white popup.
        combo.Template = (ControlTemplate)XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="{x:Type ComboBox}">
              <Grid>
                <ToggleButton Focusable="False" ClickMode="Press"
                              IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}">
                  <ToggleButton.Template>
                    <ControlTemplate TargetType="{x:Type ToggleButton}">
                      <Border Background="{DynamicResource CardBackgroundBrush}" BorderBrush="{DynamicResource BorderBrush}"
                              BorderThickness="1" CornerRadius="{DynamicResource MiniCardCornerRadius}" Padding="9,7">
                        <Grid>
                          <Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width="20"/></Grid.ColumnDefinitions>
                          <ContentPresenter Content="{Binding SelectionBoxItem, RelativeSource={RelativeSource AncestorType=ComboBox}}"
                                            TextElement.Foreground="{DynamicResource PrimaryTextBrush}" VerticalAlignment="Center"/>
                          <Path Grid.Column="1" Data="M 0 0 L 5 5 L 10 0" Stroke="{DynamicResource AccentBrush}" StrokeThickness="1.5"
                                HorizontalAlignment="Right" VerticalAlignment="Center"/>
                        </Grid>
                      </Border>
                    </ControlTemplate>
                  </ToggleButton.Template>
                </ToggleButton>
                <Popup Name="PART_Popup" Placement="Bottom" IsOpen="{TemplateBinding IsDropDownOpen}"
                       AllowsTransparency="True" Focusable="False" PopupAnimation="Slide">
                  <Border Background="{DynamicResource CardBackgroundBrush}" BorderBrush="{DynamicResource BorderBrush}"
                          BorderThickness="1" MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}">
                    <ScrollViewer MaxHeight="240"><ItemsPresenter/></ScrollViewer>
                  </Border>
                </Popup>
              </Grid>
              <ControlTemplate.Triggers>
                <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.5"/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
            """);
        Style items = new(typeof(ComboBoxItem));
        items.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("CardBackgroundBrush")));
        items.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("PrimaryTextBrush")));
        items.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(9, 7, 9, 7)));
        items.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
        items.Setters.Add(new Setter(Control.TemplateProperty, (ControlTemplate)XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ComboBoxItem">
                <Border x:Name="ItemBorder" Background="{TemplateBinding Background}"
                        BorderBrush="Transparent" BorderThickness="1" Padding="{TemplateBinding Padding}">
                    <ContentPresenter TextElement.Foreground="{TemplateBinding Foreground}"/>
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsSelected" Value="True">
                        <Setter TargetName="ItemBorder" Property="BorderBrush" Value="{DynamicResource AccentBrush}"/>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
            """)));
        Trigger selected = new() { Property = ComboBoxItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("InputBackgroundBrush")));
        items.Triggers.Add(selected);
        Trigger highlight = new() { Property = ComboBoxItem.IsHighlightedProperty, Value = true };
        highlight.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("AccentBrush")));
        highlight.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("WindowBackgroundBrush")));
        items.Triggers.Add(highlight); combo.ItemContainerStyle = items;
    }
}
