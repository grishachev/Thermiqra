using System;
using System.Collections.Generic;

namespace PCHardwareMonitor;

internal sealed record ProgramReviewLimitation(string ReasonRu, string ReasonEn, string Detail = "")
{
    public string DisplayText => SettingsService.L(ReasonRu, ReasonEn) + (Detail.Length > 0 ? ": " + Detail : "");
}

internal enum ProgramLeftoverGroup { Related, NeedsReview, Protected, UserData }
internal sealed record ProgramLeftoverProbe(string Kind, string Path, ProgramLeftoverGroup Group,
    string ReasonRu, string ReasonEn, string Hive = "", string View = "", string EvidenceRoot = "",
    string EvidenceTarget = "", string EvidenceFingerprint = "");
internal sealed record ProgramRemovalBaseline(string Id, DateTime CapturedUtc, string Identity,
    string Name, string Version, List<ProgramLeftoverProbe> Probes, List<ProgramReviewLimitation> Limitations,
    string Publisher = "", string InstallRoot = "", bool ObservationOnly = false,
    ProgramRestorePointResult? RestorePoint = null, ProgramMemorySummary? Memory = null,
    List<string>? GameProjects = null, InstalledProgramRemovalResult? NativeResult = null);
internal sealed record ProgramLeftoverReport(ProgramRemovalBaseline Baseline, DateTime CheckedUtc,
    List<ProgramLeftoverProbe> Found, List<ProgramReviewLimitation> Limitations, int CheckedObjects,
    List<ProgramLeftoverCheck>? Checks = null, string AnalyzerPath = "", ProgramRecordedObjectsResult? RecordedObjects = null,
    bool SearchPending = false);
internal sealed record ProgramLeftoverCheck(string Kind, string Path, string Outcome, string Detail);

/// <summary>Path association is evidence for review, never proof of exclusive ownership.</summary>
internal static class ProgramLeftoverPathPolicy
{
    internal static bool Inside(string path, string parent) => parent.Length > 0 &&
        (path.Equals(parent, StringComparison.OrdinalIgnoreCase) ||
         path.StartsWith(parent.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));

    internal static string Normalize(string raw)
    {
        return WindowsPathRules.Normalize(Environment.ExpandEnvironmentVariables(raw));
    }

    internal static bool UserContent(string path)
    {
        foreach (Environment.SpecialFolder folder in new[] {
            Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.DesktopDirectory,
            Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.MyMusic,
            Environment.SpecialFolder.MyVideos })
        {
            string root = Environment.GetFolderPath(folder);
            if (root.Length > 0 && Inside(path, Normalize(root))) return true;
        }
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (string folder in new[] { "Downloads", "Saved Games", "Games", "OneDrive", "YandexDisk" })
            if (Inside(path, Normalize(System.IO.Path.Combine(profile, folder)))) return true;
        foreach (string variable in new[] { "OneDrive", "OneDriveConsumer", "OneDriveCommercial" })
        {
            string? root = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(root) && Inside(path, Normalize(root))) return true;
        }
        return false;
    }
}
