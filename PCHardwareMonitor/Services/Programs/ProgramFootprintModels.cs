using System;
using System.Collections.Generic;

namespace PCHardwareMonitor;

// Observed metadata, not a claim that an installer created or exclusively
// owns these objects. These records never authorize cleanup on their own.
internal sealed record ProgramFootprintFile(string Root, string RelativePath, bool Directory,
    long Length, long LastWriteUtcTicks);
internal sealed record ProgramFootprintRegistry(string Hive, string View, string Path,
    string Fingerprint, int ValueCount, bool Complete = true);
internal sealed record ProgramFootprint(DateTime CapturedUtc, List<ProgramFootprintFile> Files,
    List<ProgramFootprintRegistry> Registry, bool Complete, List<ProgramReviewLimitation> Limitations,
    List<string>? Roots = null, List<string>? RegistryRoots = null);
internal sealed record ProgramFootprintChange(DateTime ObservedUtc, string Kind, string Path);
internal sealed record ProgramMemorySummary(DateTime CapturedUtc, int Files, int RegistryKeys,
    int HistoricalAssociations, bool Complete, DateTime? PreviousSnapshotUtc = null);
internal sealed record ProgramRecordedObjectsResult(int FilesPresent, int FilesAbsent, int FilesUnchecked,
    int RegistryPresent, int RegistryAbsent, int RegistryUnchecked);
