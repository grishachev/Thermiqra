using Thermiqra.RemovalLab;
using Xunit;

namespace Thermiqra.Tests;

public class DisposableGuestTests
{
    [Theory]
    [InlineData(false, true, "Virtual Machine", false)]
    [InlineData(true, false, "Virtual Machine", false)]
    [InlineData(true, true, "System Product Name", false)]
    [InlineData(true, true, "", false)]
    [InlineData(true, true, "Virtual Machine", true)]
    [InlineData(true, true, "VMware Virtual Platform", true)]
    [InlineData(true, true, "VirtualBox", true)]
    public void MutationRunnerRequiresExplicitDisposableGuestAndAdmin(bool optIn, bool admin, string model, bool allowed) =>
        Assert.Equal(allowed, GuestSafety.IsAllowed(optIn, admin, model));
}
