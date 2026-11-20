using Xunit;
namespace PCHardwareMonitor.Tests;
public sealed class RemovalDecisionTests
{
    private static ProgramRemovalBaseline Baseline => new("a", DateTime.UtcNow, "registry:HKCU:64:Example", "Example", "1", [], [], InstallRoot: @"C:\Apps\Example");
    private static ProgramLeftoverProbe Scope => new("Directory", @"C:\Apps\Example", ProgramLeftoverGroup.UserData, "", "", EvidenceRoot: @"C:\Apps\Example",
        EvidenceTarget: ProgramRecordedInstallCleanup.DirectoryEvidence, EvidenceFingerprint: "metadata");
    [Fact] public void RegisteredScopeDoesNotClaimEveryFileWasCreatedByInstaller() =>
        Assert.Equal(ProgramRemovalConfidence.ConfirmedScope, ProgramRemovalDecisions.Evaluate(Scope, Baseline, new(true)).Confidence);
    [Theory]
    [InlineData(true,false,false,false)][InlineData(false,true,false,false)][InlineData(false,false,true,false)][InlineData(false,false,false,true)]
    public void LinkOverlapSharedOrChangeBlocks(bool link, bool overlap, bool shared, bool changed) =>
        Assert.False(ProgramRemovalDecisions.Evaluate(Scope, Baseline, new(true, link, overlap, shared, changed)).Allowed);
    [Fact] public void RegistrationReappearingBlocks() => Assert.False(ProgramRemovalDecisions.Evaluate(Scope, Baseline, new(false)).Allowed);
    [Fact] public void VendorParentCannotReplaceProductScope() => Assert.False(ProgramRemovalDecisions.Evaluate(Scope with { Path = @"C:\Apps" }, Baseline, new(true)).Allowed);
    [Fact] public void ObservationCannotAuthorizeDeletion() => Assert.False(ProgramRemovalDecisions.Evaluate(Scope, Baseline with { ObservationOnly = true }, new(true)).Allowed);
    [Fact] public void MissingEvidenceBlocks() => Assert.False(ProgramRemovalDecisions.Evaluate(Scope, Baseline, new(true, RequiredEvidenceAvailable:false)).Allowed);
    [Theory][InlineData(1)][InlineData(2)]
    public void NameMatchOrProtectedGroupIsNotPermission(int group) => Assert.False(ProgramRemovalDecisions.Evaluate(Scope with {Group=(ProgramLeftoverGroup)group}, Baseline, new(true)).Allowed);
    [Fact] public void ServicesAreReadOnly() => Assert.False(ProgramRemovalDecisions.Evaluate(Scope with { Kind="Service" }, Baseline, new(true)).Allowed);
    [Fact] public void WindowsManagedPackageCannotUseManualFileCleanup() => Assert.False(
        ProgramRemovalDecisions.Evaluate(Scope, Baseline with { Identity="msix:ExampleFamily" }, new(true)).Allowed);
    [Fact] public void DriveRootCannotBeACleanupScope() => Assert.False(
        ProgramRemovalDecisions.Evaluate(Scope with { Path=@"C:\", EvidenceTarget="" }, Baseline, new(true)).Allowed);
    [Theory][InlineData(0,false)][InlineData(1,true)][InlineData(2,true)][InlineData(-1,true)]
    public void SharedReferencesAreConservative(int count, bool blocked) => Assert.Equal(blocked, ProgramSharedComponentGuard.IsReferenced(count));
    [Fact] public void UnreadableSharedReferenceBlocks() => Assert.True(ProgramSharedComponentGuard.IsReferenced(null));
}
