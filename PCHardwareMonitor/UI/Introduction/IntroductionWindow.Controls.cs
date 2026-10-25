using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PCHardwareMonitor;

public sealed partial class IntroductionWindow
{
    internal static Button CreateThemedButton(
        string text,
        bool primary)
    {
        Button button =
            new()
            {
                Content =
                    text,

                Tag =
                    primary
                        ? PrimaryButtonTag
                        : SecondaryButtonTag,

                MinWidth =
                    108,

                Height =
                    40,

                Padding =
                    new Thickness(
                        15,
                        6,
                        15,
                        6),

                FontSize =
                    10,

                FontWeight =
                    FontWeights.Bold,

                Cursor =
                    Cursors.Hand,

                BorderThickness =
                    new Thickness(1),

                Template =
                    CreateButtonTemplate()
            };

        button.MouseEnter +=
            Button_MouseEnter;

        button.MouseLeave +=
            Button_MouseLeave;

        button.PreviewMouseLeftButtonDown +=
            Button_MouseDown;

        button.PreviewMouseLeftButtonUp +=
            Button_MouseUp;

        button.IsEnabledChanged +=
            Button_IsEnabledChanged;

        ApplyButtonVisual(
            button,
            hovered: false,
            pressed: false);

        return button;
    }

    private static ControlTemplate
        CreateButtonTemplate()
    {
        FrameworkElementFactory border =
            new(
                typeof(Border));

        border.Name =
            "ButtonBorder";

        border.SetValue(
            Border.BackgroundProperty,
            new TemplateBindingExtension(
                Control.BackgroundProperty));

        border.SetValue(
            Border.BorderBrushProperty,
            new TemplateBindingExtension(
                Control.BorderBrushProperty));

        border.SetValue(
            Border.BorderThicknessProperty,
            new TemplateBindingExtension(
                Control.BorderThicknessProperty));

        border.SetValue(
            Border.CornerRadiusProperty,
            new CornerRadius(4));

        border.SetValue(
            Border.PaddingProperty,
            new TemplateBindingExtension(
                Control.PaddingProperty));

        FrameworkElementFactory presenter =
            new(
                typeof(ContentPresenter));

        presenter.SetValue(
            FrameworkElement.HorizontalAlignmentProperty,
            HorizontalAlignment.Center);

        presenter.SetValue(
            FrameworkElement.VerticalAlignmentProperty,
            VerticalAlignment.Center);

        border.AppendChild(
            presenter);

        return new ControlTemplate(
            typeof(Button))
        {
            VisualTree =
                border
        };
    }

    private static void Button_MouseEnter(
        object sender,
        MouseEventArgs e)
    {
        if (sender is Button button)
        {
            ApplyButtonVisual(
                button,
                hovered: true,
                pressed: false);
        }
    }

    private static void Button_MouseLeave(
        object sender,
        MouseEventArgs e)
    {
        if (sender is Button button)
        {
            ApplyButtonVisual(
                button,
                hovered: false,
                pressed: false);
        }
    }

    private static void Button_MouseDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is Button button)
        {
            ApplyButtonVisual(
                button,
                hovered: true,
                pressed: true);
        }
    }

    private static void Button_MouseUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is Button button)
        {
            ApplyButtonVisual(
                button,
                hovered:
                    button.IsMouseOver,
                pressed:
                    false);
        }
    }

    private static void Button_IsEnabledChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (sender is Button button)
        {
            ApplyButtonVisual(
                button,
                hovered:
                    false,
                pressed:
                    false);
        }
    }

    private static void ApplyButtonVisual(
        Button button,
        bool hovered,
        bool pressed)
    {
        if (!button.IsEnabled)
        {
            button.SetResourceReference(
                Control.BackgroundProperty,
                "WindowBackgroundBrush");

            button.SetResourceReference(
                Control.BorderBrushProperty,
                "BorderBrush");

            button.SetResourceReference(
                Control.ForegroundProperty,
                "SecondaryTextBrush");

            button.Opacity =
                0.42;

            button.Cursor =
                Cursors.Arrow;

            return;
        }

        button.Opacity =
            pressed
                ? 0.68
                : 1.0;

        button.Cursor =
            Cursors.Hand;

        if (hovered)
        {
            button.SetResourceReference(
                Control.BackgroundProperty,
                "AccentBrush");

            button.SetResourceReference(
                Control.BorderBrushProperty,
                "AccentBrush");

            button.SetResourceReference(
                Control.ForegroundProperty,
                "WindowBackgroundBrush");

            return;
        }

        bool primary =
            string.Equals(
                button.Tag as string,
                PrimaryButtonTag,
                StringComparison.Ordinal);

        button.SetResourceReference(
            Control.BackgroundProperty,
            "CardBackgroundBrush");

        button.SetResourceReference(
            Control.BorderBrushProperty,
            primary
                ? "AccentBrush"
                : "BorderBrush");

        button.SetResourceReference(
            Control.ForegroundProperty,
            primary
                ? "AccentBrush"
                : "PrimaryTextBrush");
    }

    private static Border CreateFrame(
        Thickness padding)
    {
        Border border =
            new()
            {
                Padding =
                    padding,

                BorderThickness =
                    new Thickness(1)
            };

        border.SetResourceReference(
            Border.BackgroundProperty,
            "CardBackgroundBrush");

        border.SetResourceReference(
            Border.BorderBrushProperty,
            "BorderBrush");

        border.SetResourceReference(
            Border.CornerRadiusProperty,
            "PanelCornerRadius");

        return border;
    }
}
