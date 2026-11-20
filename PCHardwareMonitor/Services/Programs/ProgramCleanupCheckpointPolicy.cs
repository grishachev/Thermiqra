using System;

namespace PCHardwareMonitor;

internal static class ProgramCleanupCheckpointPolicy
{
    internal static bool CanReuse(ProgramRestorePointResult? point, DateTime now) =>
        point is { State: "Created", SequenceNumber: not null } &&
        now >= point.AttemptedUtc && now - point.AttemptedUtc <= TimeSpan.FromMinutes(30);
}
