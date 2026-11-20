using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PCHardwareMonitor;

internal static class ProgramProductIdentity
{
    internal static string Token(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    internal static string RegistrationName(string identity)
    {
        string[] parts = identity.Split(':', 4);
        if (parts.Length != 4 || parts[0] != "registry") return "";
        string name = parts[3];
        if (name.EndsWith("_is1", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        return Guid.TryParse(name, out _) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ? "" : name;
    }
    internal static HashSet<string> Names(ProgramRemovalBaseline baseline) => new(new[] {
        Token(ProgramUserDataLeftoverService.ProductName(baseline.Name)), Token(Path.GetFileName(baseline.InstallRoot)),
        Token(RegistrationName(baseline.Identity)) }.Where(n => n.Length >= 4 && !new[] {
            "microsoft", "windows", "software", "application", "applications", "programs", "common", "settings", "data", "uninstall", "setup", "installer" }.Contains(n)), StringComparer.OrdinalIgnoreCase);
}
