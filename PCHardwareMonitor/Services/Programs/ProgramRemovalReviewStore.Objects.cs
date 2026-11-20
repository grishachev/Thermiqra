using System;
using System.IO;
using System.Text.Json;

namespace PCHardwareMonitor;

internal static partial class ProgramRemovalReviewStore
{
    internal static void SaveObjects(string id, ProgramFootprint footprint)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException(SettingsService.L("Некорректный идентификатор снимка.", "Invalid snapshot identity."));
        Write(Path.Combine(Folder, id + ".objects.json"), footprint);
    }

    internal static ProgramFootprint? ReadObjects(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) return null;
        string path = Path.Combine(Folder, id + ".objects.json");
        if (!File.Exists(path)) return null; // Older reports have no file inventory.
        ProgramLeftoverCleanupPolicy.CheckAncestors(path);
        if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new IOException(SettingsService.L("Превышен предел объёма списка объектов.", "Object inventory exceeds its limit."));
        var value = JsonSerializer.Deserialize<ProgramFootprint>(File.ReadAllText(path));
        if (value == null || !ProgramObservedAssociationStore.ValidFootprint(value))
            throw new IOException(SettingsService.L("Сохранённый снимок объектов повреждён.", "The saved object inventory is damaged."));
        return value;
    }
}
