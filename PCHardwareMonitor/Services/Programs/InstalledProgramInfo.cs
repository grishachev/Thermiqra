using System;
using System.Windows.Media;

namespace PCHardwareMonitor;

internal sealed record InstalledProgramInfo(
    string Name, string Publisher, string Version, string Kind,
    string SizeText, string InstallLocation, string Source,
    string InstallDateText = "—", DateTime? InstalledOn = null,
    ImageSource? AppIcon = null, bool IsAuxiliary = false,
    string SizeSource = "", string DateSource = "", string ComponentReason = "",
    string Identity = "", DateTime? FirstObservedOn = null,
    DateTime? LastVersionChangeOn = null, string RegisteredLocation = "", DateTime? SizeMeasuredUtc = null,
    bool SizeFromCache = false, DateTime? CachedInventoryUtc = null,
    string LocationEvidence = "", long DiscoveryDurationMs = 0, DateTime? RegistrationModifiedOn = null)
{
    public string LocationExplanation => !string.IsNullOrWhiteSpace(InstallLocation)
        ? LocationText
        : !string.IsNullOrWhiteSpace(RegisteredLocation)
            ? SettingsService.L("Каталог из регистрации Windows не найден или не подтверждён: ",
                "Windows registered directory was not found or confirmed: ") + RegisteredLocation
            : SettingsService.L("Windows не сообщает подтверждённый каталог программы. Компоненты могут находиться в нескольких папках.",
                "Windows does not report a confirmed application directory. Components may be spread across multiple folders.");

    public bool HasIcon => AppIcon != null;

    public string LocationText =>
        string.IsNullOrWhiteSpace(InstallLocation) ? "—" : InstallLocation +
        (InstalledProgramLocationService.IsBroadContainerDirectory(InstallLocation)
            ? SettingsService.L(" (общий каталог)", " (shared directory)") : "");
}
