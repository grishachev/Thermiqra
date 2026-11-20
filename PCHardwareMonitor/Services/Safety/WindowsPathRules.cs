using System;
using System.IO;
using System.Linq;

namespace PCHardwareMonitor;

// Lexical checks only: no filesystem, registry, environment or link traversal.
internal static class WindowsPathRules
{
    internal static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        string path = raw.Trim();
        if (path.StartsWith('"') || path.EndsWith('"'))
        {
            if (path.Length < 2 || !path.StartsWith('"') || !path.EndsWith('"')) return "";
            path = path[1..^1];
        }
        path = path.Replace('/', '\\');
        if (path.Length < 3 || !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] != '\\' ||
            path.AsSpan(2).Contains(':') || path.IndexOfAny(['\0', '*', '?', '<', '>', '|', '"']) >= 0) return "";
        foreach (string part in path[3..].Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part is "." or "..") continue;
            string stem = part.Split('.')[0];
            if (part.EndsWith('.') || part.EndsWith(' ') || part.Any(char.IsControl) ||
                new[] { "CON", "PRN", "AUX", "NUL", "CLOCK$" }.Contains(stem, StringComparer.OrdinalIgnoreCase) ||
                (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                    stem[3] is >= '1' and <= '9')) return "";
        }
        try { string full = Path.GetFullPath(path); return full.Length == 3 ? full : full.TrimEnd('\\'); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException) { return ""; }
    }

    internal static bool Inside(string path, string parent) => parent.Length > 0 &&
        (path.Equals(parent, StringComparison.OrdinalIgnoreCase) || path.StartsWith(parent.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));
}
