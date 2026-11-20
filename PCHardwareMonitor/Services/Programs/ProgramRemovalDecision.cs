using System;

namespace PCHardwareMonitor;

internal enum ProgramRemovalConfidence { ConfirmedObject, ConfirmedScope, Review, Protected }
internal sealed record ProgramRemovalDecision(bool Allowed, ProgramRemovalConfidence Confidence, string Reason);
internal sealed record ProgramRemovalFacts(bool RegistrationAbsent, bool LinksDetected = false,
    bool OtherApplicationOverlap = false, bool SharedReference = false, bool ChangedSinceReview = false,
    bool RequiredEvidenceAvailable = true);

// One pure admission gate shared by presentation and mutation. OS reads supply
// facts; a folder namespace never claims that an installer created every file.
internal static class ProgramRemovalDecisions
{
    internal static ProgramRemovalDecision Evaluate(ProgramLeftoverProbe probe, ProgramRemovalBaseline baseline, ProgramRemovalFacts facts)
    {
        ProgramRemovalDecision Block(string reason, bool protect = false) => new(false,
            protect ? ProgramRemovalConfidence.Protected : ProgramRemovalConfidence.Review, reason);
        if (probe.Group == ProgramLeftoverGroup.Protected || facts.LinksDetected || facts.OtherApplicationOverlap || facts.SharedReference)
            return Block("protected-or-shared", true);
        if (baseline.Identity.StartsWith("msix:", StringComparison.OrdinalIgnoreCase) && !ProgramPackageDataLayout.ExternalDataScope(probe, baseline))
            return Block("windows-managed-package", true);
        if (!facts.RegistrationAbsent) return Block("registration-present", true);
        if (!facts.RequiredEvidenceAvailable || facts.ChangedSinceReview) return Block("evidence-unavailable-or-changed");
        if (probe.Group is not (ProgramLeftoverGroup.Related or ProgramLeftoverGroup.UserData)) return Block("unconfirmed-association");
        if (baseline.ObservationOnly) return Block("observation-is-not-removal-authority");
        string path = WindowsPathRules.Normalize(probe.Path);
        bool file = probe.Kind is "Directory" or "UserData" or "RecordedFile" or "Shortcut";
        if (file && path.Length == 0) return Block("invalid-file-path", true);
        string root = WindowsPathRules.Normalize(probe.Kind is "Directory" or "UserData" ? probe.Path : probe.EvidenceRoot);
        if (root.Length <= 3) return Block("missing-or-drive-root-scope", true);
        if (!file && probe.Kind != "Registry") return Block("inspection-only-resource", true);
        if (probe.Kind == "Registry" && probe.EvidenceRoot.Length == 0) return Block("missing-registry-evidence");
        if (probe.Kind is "UserData" or "RecordedFile" && probe.EvidenceFingerprint.Length == 0) return Block("missing-fingerprint");
        if (probe.Kind == "Directory" && probe.EvidenceTarget == ProgramRecordedInstallCleanup.DirectoryEvidence &&
            (!path.Equals(WindowsPathRules.Normalize(baseline.InstallRoot), StringComparison.OrdinalIgnoreCase) || probe.EvidenceFingerprint.Length == 0))
            return Block("outside-registered-install-scope", true);
        return new(true, probe.Kind is "Directory" or "UserData" ? ProgramRemovalConfidence.ConfirmedScope : ProgramRemovalConfidence.ConfirmedObject,
            probe.Kind is "Directory" or "UserData" ? "explicit-application-data-scope" : "validated-object");
    }
}
