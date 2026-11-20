using System;
using System.IO;
using System.Linq;
using System.Text;

namespace PCHardwareMonitor;

internal static partial class ProgramUserDataLeftoverService
{
    private const string UnrealProfileEvidence = "application-profile:unreal";

    private static (string Path, string Hash)? FindGameProfile(ProgramLeftoverProbe probe, ProgramRemovalBaseline baseline)
    {
        string project = Path.GetFileName(probe.Path);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!Path.GetDirectoryName(probe.Path)!.Equals(local, StringComparison.OrdinalIgnoreCase) ||
            !ProgramGameDataLocations.Aliases(baseline).Contains(project, StringComparer.OrdinalIgnoreCase)) return null;
        string logs = Path.Combine(probe.Path, "Saved", "Logs");
        if (!Directory.Exists(logs)) return null;
        ProgramLeftoverCleanupPolicy.CheckAncestors(logs);
        string expected = ProgramLeftoverPathPolicy.Normalize(Path.Combine(baseline.InstallRoot, project, "Binaries", "Win64"));
        bool confirmed = false;
        foreach (string file in Directory.EnumerateFiles(logs, "*.log").Take(8))
        {
            ProgramLeftoverCleanupPolicy.CheckAncestors(file);
            using FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] bytes = new byte[524288]; int total = 0, read;
            while (total < bytes.Length && (read = stream.Read(bytes, total, bytes.Length - total)) > 0) total += read;
            string text = Encoding.UTF8.GetString(bytes, 0, total);
            foreach (string line in text.Split('\n'))
            {
                const string marker = "LogInit: Base Directory:";
                int offset = line.IndexOf(marker, StringComparison.Ordinal);
                if (offset < 0) continue;
                string path = ProgramLeftoverPathPolicy.Normalize(line[(offset + marker.Length)..].Trim().Replace('/', '\\'));
                if (path.Equals(expected, StringComparison.OrdinalIgnoreCase)) { confirmed = true; break; }
            }
            if (confirmed) break;
        }
        return confirmed ? (UnrealProfileEvidence, ProfileFingerprint(probe.Path)) : null;
    }
}
