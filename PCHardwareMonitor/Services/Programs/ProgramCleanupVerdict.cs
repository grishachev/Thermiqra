namespace PCHardwareMonitor;

internal enum ProgramCleanupVerdict { Clean, Remaining, Limited, ActionsIncomplete, VerificationIncomplete }

internal static class ProgramCleanupVerdicts
{
    internal static ProgramCleanupVerdict Evaluate(bool verified, bool pendingSearch, int remaining,
        int limitations, int failedActions, bool reportSaveFailed)
    {
        if (!verified || pendingSearch || reportSaveFailed) return ProgramCleanupVerdict.VerificationIncomplete;
        if (failedActions > 0) return ProgramCleanupVerdict.ActionsIncomplete;
        if (remaining > 0) return ProgramCleanupVerdict.Remaining;
        return limitations > 0 ? ProgramCleanupVerdict.Limited : ProgramCleanupVerdict.Clean;
    }
}
