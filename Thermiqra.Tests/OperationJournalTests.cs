using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

public class OperationJournalTests
{
    [Fact] public void FailedReplacementKeepsPreviousDurableRecord()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Thermiqra-test-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "attempt.json");
        try
        {
            ProgramOperationJournal.Write(path, new { Stage = "started" });
            string original = File.ReadAllText(path);
            Assert.Throws<IOException>(() => ProgramOperationJournal.Write(path, new { Stage = new string('X', 4 * 1024 * 1024) }));
            Assert.Equal(original, File.ReadAllText(path));
            Assert.Single(Directory.GetFiles(directory));
            ProgramOperationJournal.Write(path, new { Stage = "finished" });
            Assert.Contains("finished", File.ReadAllText(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }
}
