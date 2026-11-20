using System;

namespace PCHardwareMonitor;

internal static class InstalledProgramClassificationService
{
    internal static string GetDesktopComponentReason(string name, string parent)
    {
        if (!string.IsNullOrWhiteSpace(parent))
            return SettingsService.L("Компонент другого продукта", "Component of another product");
        string n = name.Trim();
        if (n.Contains(".ShellEx", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Shell Extension", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("WebView2 Runtime", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Redistributable", StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("Microsoft .NET Runtime", StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("Microsoft Windows Desktop Runtime", StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("Microsoft ASP.NET Core", StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("Microsoft Visual C++", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Hosting Bundle", StringComparison.OrdinalIgnoreCase))
            return SettingsService.L("Среда выполнения или общий компонент", "Runtime or shared component");

        // Deliberately narrow rules. A vendor's name or MSIX alone does NOT
        // mean a package is a Windows component: Microsoft Office and NVIDIA App
        // are ordinary applications; Windows can also install user-requested apps.
        if (n.StartsWith("Microsoft GameInput", StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("Xbox Service", StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("Epic Online Services", StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("Microsoft ODBC Driver", StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("Microsoft SQL Server", StringComparison.OrdinalIgnoreCase) &&
                (n.Contains("LocalDB", StringComparison.OrdinalIgnoreCase) ||
                 n.Contains("Native Client", StringComparison.OrdinalIgnoreCase)) ||
            n.StartsWith("Microsoft Web Deploy", StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("IIS ", StringComparison.OrdinalIgnoreCase) ||
            n.Equals("devtunnel", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("FrameView SDK", StringComparison.OrdinalIgnoreCase))
            return SettingsService.L("Вспомогательный компонент приложения", "Supporting application component");

        if (n.Contains("драйвер", StringComparison.OrdinalIgnoreCase) ||
            n.Contains(" driver", StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("Driver ", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Audio Driver", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Chipset Software", StringComparison.OrdinalIgnoreCase))
            return SettingsService.L("Драйвер устройства", "Device driver");

        if (n.StartsWith("Microsoft .NET SDK", StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("Windows SDK", StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("Microsoft Visual Studio Installer", StringComparison.OrdinalIgnoreCase))
            return SettingsService.L("Инструмент разработчика", "Developer component");

        return "";
    }

    internal static string GetStoreComponentReason(string packageName, bool system,
        bool hasStartApp)
    {
        if (system)
            return SettingsService.L("Компонент Windows", "Windows component");
        if (IsKnownInboxPackage(packageName))
            return SettingsService.L("Встроенное приложение Windows", "Windows inbox application");
        if (!hasStartApp)
            return SettingsService.L("Фоновый пакет без приложения в меню Пуск",
                "Background package without a Start menu application");
        return "";
    }

    private static bool IsKnownInboxPackage(string name)
    {
        // Exact identities preserve the existing inbox-app filter without
        // hiding independently installed products sharing a name prefix.
        // Framework/resource packages are filtered using package metadata;
        // a Microsoft/Windows namespace alone is not proof of system ownership.
        string[] identities =
        {
            "Microsoft.SecHealthUI", "Microsoft.WindowsCalculator",
            "Microsoft.WindowsNotepad", "Microsoft.Windows.Photos",
            "Microsoft.WindowsStore", "Microsoft.WindowsCamera",
            "Microsoft.ZuneVideo", "Microsoft.ZuneMusic", "Microsoft.GetHelp",
            "Microsoft.Getstarted", "Microsoft.Paint", "Microsoft.MSPaint",
            "Microsoft.WindowsTerminal", "Microsoft.XboxGamingOverlay",
            "Microsoft.Xbox.TCUI", "Microsoft.GamingApp", "Microsoft.ScreenSketch",
            "Microsoft.WindowsSoundRecorder", "Microsoft.WindowsAlarms",
            "Microsoft.BingNews", "Microsoft.BingWeather", "Microsoft.WindowsMaps",
            "Microsoft.YourPhone", "Microsoft.People", "Microsoft.MicrosoftOfficeHub",
            "Microsoft.MicrosoftStickyNotes", "Microsoft.PowerAutomateDesktop",
            "Clipchamp.Clipchamp"
        };
        return Array.Exists(identities, identity =>
            name.Equals(identity, StringComparison.OrdinalIgnoreCase));
    }
}
