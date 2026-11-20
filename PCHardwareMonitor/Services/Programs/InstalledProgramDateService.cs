using System;
using System.IO;
using System.Globalization;

namespace PCHardwareMonitor;

/// <summary>
/// Date derived from a verified application directory is a filesystem date,
/// never proof of the original installation date. Callers must retain its source.
/// </summary>
internal static class InstalledProgramDateService
{
    internal static (DateTime? Date, string Source) TryReadFolderDate(
        string location, string kind)
    {
        if (string.IsNullOrWhiteSpace(location))
            return (null, "");

        try
        {
            if (!Path.IsPathFullyQualified(location) || !Directory.Exists(location))
                return (null, "");

            DirectoryInfo directory = new(location);
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                return (null, "");

            // The top of a shared hierarchy is never a specific program.
            if (directory.Parent == null ||
                InstalledProgramIconService.IsWindowsSystemDirectory(location))
                return (null, "");

            DateTime date = directory.CreationTime;
            if (date.Year < 2000 || date > DateTime.Now.AddMinutes(1))
                return (null, "");

            string source = kind == "MSIX"
                ? SettingsService.L(
                    "Дата создания каталога пакета; могла измениться при обновлении",
                    "Package folder creation date; may have changed on update")
                : SettingsService.L(
                    "Дата создания каталога программы; не подтверждает первоначальную установку",
                    "Application folder creation date; does not prove the original installation date");
            return (date, source);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            return (null, "");
        }
    }

    internal static string FormatDate(DateTime date)
    {
        return SettingsService.L(
            date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}
