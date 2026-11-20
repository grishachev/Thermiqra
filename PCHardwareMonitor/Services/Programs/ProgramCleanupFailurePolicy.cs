namespace PCHardwareMonitor;

internal static class ProgramCleanupFailurePolicy
{
    internal static ProgramCleanupEntry Record(ProgramCleanupEntry entry, bool intentPrepared, bool currentlyAbsent, string error)
    {
        // Do not turn a confirmed mutation into "already absent" when saving
        // its outcome fails. A missing target after an unconfirmed operation
        // also does not prove that Thermiqra never touched it.
        if (entry.State is "Recycled" or "Removed") return entry with { Detail = error };
        return entry with { State = !intentPrepared && currentlyAbsent ? "AlreadyAbsent" : "Failed",
            Detail = !intentPrepared && currentlyAbsent ? "" : error };
    }
}
