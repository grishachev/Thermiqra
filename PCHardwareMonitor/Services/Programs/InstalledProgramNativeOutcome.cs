using System.Collections.Generic;

namespace PCHardwareMonitor;

internal static class InstalledProgramNativeOutcome
{
    internal static InstalledProgramRemovalResult Classify(string kind, int code, bool? registrationPresent,
        string detail, List<ProgramPendingProcess>? pending = null)
    {
        // Standard Windows reboot statuses also occur in EXE wrappers around MSI.
        // Conservatively retain the reboot hold instead of authorizing cleanup.
        if (code is 3010 or 1641)
            return new(InstalledProgramRemovalState.RebootRequired, code, detail, PendingHelpers: pending);
        if (kind == "MSI" && code == 1602)
            return new(InstalledProgramRemovalState.Cancelled, code, detail, PendingHelpers: pending);
        return new(code != 0 ? InstalledProgramRemovalState.Failed : registrationPresent == false
            ? InstalledProgramRemovalState.Removed : InstalledProgramRemovalState.Unconfirmed, code, detail,
            RegistrationAbsent: registrationPresent == false, PendingHelpers: pending);
    }
}
