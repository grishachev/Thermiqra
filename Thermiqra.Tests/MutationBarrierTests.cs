using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

public class MutationBarrierTests
{
    [Theory] [InlineData(true)] [InlineData(false)]
    public void FailedPrerequisiteNeverMutates(bool savingFails)
    {
        bool mutated = false;
        Assert.Throws<IOException>(() => ProgramMutationBarrier.Execute(
            () => { if (savingFails) throw new IOException("Disk full"); },
            () => throw new IOException("Evidence changed"),
            () => mutated = true));
        Assert.False(mutated);
    }

    [Fact] public void SuccessfulSequencePersistsThenValidatesThenMutates()
    {
        List<string> events = new();
        ProgramMutationBarrier.Execute(() => events.Add("saved"), () => events.Add("validated"), () => events.Add("mutated"));
        Assert.Equal(new[] { "saved", "validated", "mutated" }, events);
    }
}
