using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

public class ResourceAssociationTests
{
    [Theory]
    [InlineData("\"B:\\Apps\\Acme\\worker.exe\" --start", true)]
    [InlineData("B:\\Apps\\Acme\\worker.exe --start", true)]
    [InlineData("\"b:\\apps\\ACME\\extension.dll\"", true)]
    [InlineData("B:\\Apps\\AcmeOther\\worker.exe", false)]
    [InlineData("C:\\Windows\\rundll32.exe B:\\Apps\\Acme\\extension.dll", false)]
    [InlineData("worker.exe", false)]
    [InlineData("\"B:\\Apps\\Acme\\..\\Other\\worker.exe\"", false)]
    [InlineData("\"B:\\Apps\\Acme\\worker.exe:evil\"", false)]
    [InlineData("\"\\\\server\\Apps\\Acme\\worker.exe\"", false)]
    public void OnlyActualTargetInsideIsolatedScopeMatches(string command, bool matches) =>
        Assert.Equal(matches, ProgramResourceAssociation.TargetsScope(command, @"B:\Apps\Acme"));
}
