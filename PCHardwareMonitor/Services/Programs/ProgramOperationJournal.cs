using System;
using System.IO;
using System.Text.Json;

namespace PCHardwareMonitor;

internal static class ProgramOperationJournal
{
    internal static void Write<T>(string path, T payload)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? throw new IOException(SettingsService.L("Некорректный каталог журнала операций.", "Invalid journal directory."));
        ProgramLeftoverCleanupPolicy.CheckAncestors(directory);
        Directory.CreateDirectory(directory);
        ProgramLeftoverCleanupPolicy.CheckAncestors(directory);
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException(SettingsService.L("Путь журнала операций является ссылкой.", "Journal path is a link."));
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream output = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(output, payload);
                if (output.Length > 4 * 1024 * 1024) throw new IOException(SettingsService.L("Превышен предел размера журнала операций.", "Journal size limit exceeded."));
                output.Flush(true);
            }
            ProgramLeftoverCleanupPolicy.CheckAncestors(directory);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
