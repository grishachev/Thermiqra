using System;
using System.Collections.Generic;
using System.Linq;

namespace PCHardwareMonitor;

internal static class InstalledProgramChronology
{
    // InstallDate often contains yyyyMMdd only. A same-day Windows key write
    // supplies ordering within that day, not proof of the original install time.
    // Later repairs must not move an old installation to a different day.
    internal static DateTime? Timestamp(InstalledProgramInfo app)
    {
        if (app.InstalledOn is not DateTime date) return null;
        if (date.TimeOfDay != TimeSpan.Zero) return date;
        return app.RegistrationModifiedOn is DateTime modified && modified.Date == date.Date ? modified : date;
    }

    internal static List<InstalledProgramInfo> Sort(IEnumerable<InstalledProgramInfo> apps, bool byName) =>
        (byName ? apps.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase) :
            apps.OrderByDescending(Timestamp).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)).ToList();
}
