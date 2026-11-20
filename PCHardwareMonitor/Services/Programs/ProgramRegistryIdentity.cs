using System;

namespace PCHardwareMonitor;

internal static class ProgramRegistryIdentity
{
    // HKCU\Software is shared on supported Windows versions, except Classes.
    internal static bool Shared(ProgramLeftoverProbe p) => p.Kind == "Registry" && p.Hive == "HKCU" &&
        p.Path.StartsWith(@"SOFTWARE\", StringComparison.OrdinalIgnoreCase) &&
        !p.Path.Equals(@"SOFTWARE\Classes", StringComparison.OrdinalIgnoreCase) &&
        !p.Path.StartsWith(@"SOFTWARE\Classes\", StringComparison.OrdinalIgnoreCase);
    internal static ProgramLeftoverProbe Canonical(ProgramLeftoverProbe p) => Shared(p) ? p with { View = "64" } : p;
    internal static string Key(ProgramLeftoverProbe p)
    {
        p = Canonical(p);
        return p.Kind + "|" + p.Hive + "|" + p.View + "|" + p.Path;
    }
    internal static string Display(ProgramLeftoverProbe p) => p.Hive + " (" +
        (Shared(p) ? SettingsService.L("общий", "shared") : p.View) + ")\\" + p.Path;
}
