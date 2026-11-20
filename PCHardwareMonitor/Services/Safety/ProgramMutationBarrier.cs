using System;

namespace PCHardwareMonitor;

// The durable intent must exist before the last live validation and mutation.
// Failure at either prerequisite propagates; mutation is never attempted.
internal static class ProgramMutationBarrier
{
    internal static void Execute(Action persistIntent, Action validateCurrent, Action mutate)
    {
        persistIntent();
        validateCurrent();
        mutate();
    }
}
