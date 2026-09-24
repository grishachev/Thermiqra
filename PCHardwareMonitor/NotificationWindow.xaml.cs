using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCHardwareMonitor;

public partial class NotificationWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;

    private static readonly IntPtr HwndTopmost =
        new(-1);

    private static readonly IntPtr HwndNoTopmost =
        new(-2);

    private readonly NotificationType _type;
    private readonly bool _topmostWithoutActivation;
    private readonly Action? _clickAction;
    private readonly DispatcherTimer _closeTimer;

    public NotificationWindow(
        NotificationType type,
        string title,
        string message,
        bool topmostWithoutActivation,
        Action? clickAction)
    {
        InitializeComponent();

        _type =
            type;

        _topmostWithoutActivation =
            topmostWithoutActivation;

        _clickAction =
            clickAction;

        string typeText =
            GetTypeText(
                type);

        Brush severityBrush =
            CreateSeverityBrush(
                type);

        ApplyText(
            title,
            message,
            typeText);

        ApplySeverityBrush(
            severityBrush);

        _closeTimer =
            new DispatcherTimer
            {
                Interval =
                    GetDisplayDuration(
                        type)
            };

        _closeTimer.Tick +=
            CloseTimer_Tick;

        Loaded +=
            NotificationWindow_Loaded;

        Closed +=
            NotificationWindow_Closed;
    }


    public void SetPosition(
        double left,
        double top)
    {
        Left =
            left;

        Top =
            top;
    }


    protected override void OnSourceInitialized(
        EventArgs e)
    {
        base.OnSourceInitialized(
            e);

        IntPtr hwnd =
            new WindowInteropHelper(
                this)
                .Handle;

        IntPtr currentStyle =
            GetWindowLongPtr(
                hwnd,
                GwlExStyle);

        long updatedStyle =
            currentStyle.ToInt64() |
            WsExToolWindow |
            WsExNoActivate;

        SetWindowLongPtr(
            hwnd,
            GwlExStyle,
            new IntPtr(
                updatedStyle));

        SetWindowPos(
            hwnd,
            _topmostWithoutActivation
                ? HwndTopmost
                : HwndNoTopmost,
            0,
            0,
            0,
            0,
            SwpNoMove |
            SwpNoSize |
            SwpNoActivate);
    }


    private void NotificationWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        _closeTimer.Start();
    }


    private void NotificationWindow_Closed(
        object? sender,
        EventArgs e)
    {
        _closeTimer.Stop();

        _closeTimer.Tick -=
            CloseTimer_Tick;
    }


    private void CloseTimer_Tick(
        object? sender,
        EventArgs e)
    {
        _closeTimer.Stop();

        Close();
    }


    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled =
            true;

        Close();
    }


    private void Root_MouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (FindButtonAncestor(
                e.OriginalSource as DependencyObject) != null)
        {
            return;
        }

        try
        {
            _clickAction?.Invoke();
        }
        finally
        {
            Close();
        }
    }


    private static Button? FindButtonAncestor(
        DependencyObject? element)
    {
        while (element != null)
        {
            if (element is Button button)
            {
                return button;
            }

            element =
                VisualTreeHelper.GetParent(
                    element);
        }

        return null;
    }


    private void ApplyText(
        string title,
        string message,
        string typeText)
    {
        CyberTitleText.Text =
            title;

        SteamTitleText.Text =
            title;

        FrostTitleText.Text =
            title;

        MilitaryTitleText.Text =
            title;


        CyberMessageText.Text =
            message;

        SteamMessageText.Text =
            message;

        FrostMessageText.Text =
            message;

        MilitaryMessageText.Text =
            message;


        CyberTypeText.Text =
            typeText;

        SteamTypeText.Text =
            typeText;

        FrostTypeText.Text =
            typeText;

        MilitaryTypeText.Text =
            typeText;
    }


    private void ApplySeverityBrush(
        Brush severityBrush)
    {
        CyberSeverityBar.Background =
            severityBrush;

        CyberSeverityBadge.BorderBrush =
            severityBrush;

        CyberTypeText.Foreground =
            severityBrush;


        SteamSeverityBadge.BorderBrush =
            severityBrush;

        SteamTypeText.Foreground =
            severityBrush;


        FrostSeverityBadge.BorderBrush =
            severityBrush;

        FrostSeverityBar.Background =
            severityBrush;

        FrostTypeText.Foreground =
            severityBrush;


        MilitarySeverityBadge.BorderBrush =
            severityBrush;

        MilitarySeverityBar.Background =
            severityBrush;

        MilitaryTypeText.Foreground =
            severityBrush;
    }


    private static string GetTypeText(
        NotificationType type)
    {
        return type switch
        {
            NotificationType.Critical =>
                SettingsService.L(
                    "КРИТИЧЕСКОЕ",
                    "CRITICAL"),

            NotificationType.Info =>
                SettingsService.L(
                    "ИНФОРМАЦИЯ",
                    "INFO"),

            _ =>
                SettingsService.L(
                    "ПРЕДУПРЕЖДЕНИЕ",
                    "WARNING")
        };
    }


    private static Brush CreateSeverityBrush(
        NotificationType type)
    {
        string color =
            type switch
            {
                NotificationType.Critical =>
                    "#FF5A5F",

                NotificationType.Info =>
                    "#58C7F3",

                _ =>
                    "#F2C94C"
            };

        return new SolidColorBrush(
            (Color)
            ColorConverter.ConvertFromString(
                color));
    }


    private static TimeSpan GetDisplayDuration(
        NotificationType type)
    {
        return type switch
        {
            NotificationType.Critical =>
                TimeSpan.FromSeconds(
                    10),

            NotificationType.Info =>
                TimeSpan.FromSeconds(
                    7),

            _ =>
                TimeSpan.FromSeconds(
                    8)
        };
    }


    private static IntPtr GetWindowLongPtr(
        IntPtr hwnd,
        int index)
    {
        if (IntPtr.Size ==
            8)
        {
            return GetWindowLongPtr64(
                hwnd,
                index);
        }

        return new IntPtr(
            GetWindowLong32(
                hwnd,
                index));
    }


    private static IntPtr SetWindowLongPtr(
        IntPtr hwnd,
        int index,
        IntPtr newValue)
    {
        if (IntPtr.Size ==
            8)
        {
            return SetWindowLongPtr64(
                hwnd,
                index,
                newValue);
        }

        return new IntPtr(
            SetWindowLong32(
                hwnd,
                index,
                newValue.ToInt32()));
    }


    [DllImport(
        "user32.dll",
        EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(
        IntPtr hwnd,
        int index);


    [DllImport(
        "user32.dll",
        EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(
        IntPtr hwnd,
        int index,
        int newValue);


    [DllImport(
        "user32.dll",
        EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(
        IntPtr hwnd,
        int index);


    [DllImport(
        "user32.dll",
        EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(
        IntPtr hwnd,
        int index,
        IntPtr newValue);


    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr hwndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);
}
