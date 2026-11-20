using System;

namespace PCHardwareMonitor;

// A name/publisher match is never a resource association. Only an absolute
// executable/library target strictly inside an isolated recorded install root.
internal static class ProgramResourceAssociation
{
    internal static bool TargetsScope(string command, string installRoot)
    {
        string root = WindowsPathRules.Normalize(installRoot);
        if (root.Length <= 3 || string.IsNullOrWhiteSpace(command)) return false;
        string value = command.Trim();
        string target;
        if (value[0] == '"')
        {
            int end = value.IndexOf('"', 1);
            if (end < 0) return false;
            target = value[1..end];
        }
        else
        {
            // Ambiguous unquoted paths with spaces are deliberately excluded.
            int end = value.IndexOfAny(new[] { ' ', '\t' });
            target = end < 0 ? value : value[..end];
        }
        target = WindowsPathRules.Normalize(target);
        return target.Length > root.Length && WindowsPathRules.Inside(target, root) &&
            (target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
             target.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
    }
}
