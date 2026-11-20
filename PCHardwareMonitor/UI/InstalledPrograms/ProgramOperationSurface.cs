using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace PCHardwareMonitor;

// Decisions and results replace content in the current operation window.
internal sealed class ProgramOperationSurface : ContentControl
{
    private TaskCompletionSource<bool>? _decision;
    internal bool HasDecision => _decision != null;

    internal ProgramOperationSurface()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch; Focusable = false;
    }

    internal void CancelDecision() => _decision?.TrySetResult(false);

    internal WorkingState Working(string title, string text) => new(this, title, text);

    internal void Present(UIElement content)
    {
        Grid background = ThemedBackground();
        background.Children.Add(content);
        Content = background;
    }

    private static Grid ThemedBackground()
    {
        Grid grid = new() { ClipToBounds = true };
        ContentControl watermark = new() { IsHitTestVisible = false, Focusable = false, ClipToBounds = true,
            HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        watermark.SetResourceReference(ContentControl.ContentTemplateProperty, "SkinWatermarkTemplate");
        Grid.SetRowSpan(watermark, 3); grid.Children.Add(watermark);
        return grid;
    }

    internal sealed class WorkingState : IDisposable
    {
        private readonly ProgramOperationSurface _owner;
        private readonly object _previous;
        private readonly StackPanel _panel = new() { Margin = new Thickness(14) };
        private readonly Grid _background = ThemedBackground();
        private readonly TextBlock _message = new() { FontSize = 16, LineHeight = 25, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 16, 0, 0) };
        private bool _disposed;
        internal WorkingState(ProgramOperationSurface owner, string title, string text)
        {
            _owner = owner; _previous = owner.Content;
            _panel.Children.Add(ProgramWindowChrome.Header(title, ""));
            _panel.Children.Add(BusyBar());
            _message.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryTextBrush");
            _panel.Children.Add(_message); Update(text);
            _background.Children.Add(_panel); owner.Content = _background;
        }
        internal void Update(string text) { if (!_disposed) _message.Text = text; }
        internal void Phase(string title, string text)
        {
            if (_disposed) return;
            _panel.Children.RemoveAt(0); _panel.Children.Insert(0, ProgramWindowChrome.Header(title, "")); Update(text);
        }
        public void Dispose()
        {
            _disposed = true;
            if (ReferenceEquals(_owner.Content, _background)) _owner.Content = _previous;
        }
    }

    internal Task<bool> AskAsync(string title, string text, string accept, string? cancel = null)
    {
        TextBlock message = new() { Text = text, FontSize = 14, LineHeight = 22, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(14) };
        message.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryTextBrush");
        return AskAsync(title, message, accept, cancel);
    }

    internal async Task<bool> AskAsync(string title, UIElement body, string accept, string? cancel = null)
    {
        if (_decision != null) throw new InvalidOperationException(SettingsService.L("Ожидается ответ на предыдущий запрос.", "A decision is already pending."));
        object previous = Content;
        var decision = _decision = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Grid panel = ThemedBackground(); panel.Margin = new Thickness(4);
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var heading = ProgramWindowChrome.Header(title, ""); panel.Children.Add(heading);
        ScrollViewer scroll = new() { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); panel.Children.Add(scroll);
        WrapPanel buttons = new() { HorizontalAlignment = HorizontalAlignment.Right };
        Button Make(string label) => new() { Content = label, FontSize = 14, MinHeight = 38,
            Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(8, 0, 0, 0) };
        if (cancel != null)
        {
            Button back = Make(cancel); back.Click += (_, _) => decision.TrySetResult(false); buttons.Children.Add(back);
        }
        Button proceed = Make(accept); proceed.Click += (_, _) => decision.TrySetResult(true); buttons.Children.Add(proceed);
        var footer = ProgramWindowChrome.Footer(buttons); Grid.SetRow(footer, 2); panel.Children.Add(footer);
        Content = panel;
        try { return await decision.Task; }
        finally { _decision = null; Content = previous; }
    }

    internal static ProgressBar BusyBar()
    {
        ProgressBar progress = new() { IsIndeterminate = true, Height = 7, Margin = new Thickness(0, 8, 0, 12) };
        progress.Template = (ControlTemplate)XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ProgressBar">
                <Grid ClipToBounds="True" Background="{DynamicResource GaugeTrackBrush}">
                    <Border x:Name="Sweep" Width="100" HorizontalAlignment="Left" Background="{DynamicResource AccentBrush}">
                        <Border.RenderTransform><TranslateTransform X="-100"/></Border.RenderTransform>
                    </Border>
                </Grid>
                <ControlTemplate.Triggers>
                    <EventTrigger RoutedEvent="FrameworkElement.Loaded">
                        <BeginStoryboard><Storyboard RepeatBehavior="Forever">
                            <DoubleAnimation Storyboard.TargetName="Sweep"
                                Storyboard.TargetProperty="(UIElement.RenderTransform).(TranslateTransform.X)"
                                From="-100" To="1100" Duration="0:0:2.2"/>
                        </Storyboard></BeginStoryboard>
                    </EventTrigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
            """);
        return progress;
    }
}
