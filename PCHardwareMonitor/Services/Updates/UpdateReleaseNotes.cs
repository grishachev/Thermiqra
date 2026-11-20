using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PCHardwareMonitor;

// Release pages stay English-only. Optional hidden summaries localize the in-app prompt.
internal static class UpdateReleaseNotes
{
    internal static string BuildSummary(string? body, bool russian)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;
        string text = body.Replace("\r", "").Trim();
        string language = russian ? "ru" : "en";
        Match block = Regex.Match(text,
            @"<!--\s*thermiqra-summary:" + language + @"\s*\n(.*?)/thermiqra-summary\s*-->",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        if (!block.Success && russian)
            block = Regex.Match(text, @"<!--\s*thermiqra-summary:en\s*\n(.*?)/thermiqra-summary\s*-->",
                RegexOptions.Singleline | RegexOptions.CultureInvariant);
        text = block.Success ? block.Groups[1].Value : LegacySection(text, language);
        text = Regex.Replace(text, @"<!--[\s\S]*?(?:-->|\z)", "");
        var lines = new List<string>();
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
                line = "• " + line[2..].Trim();
            line = Regex.Replace(line, @"\[([^\]]+)\]\([^)]+\)", "$1");
            line = line.Replace("**", "").Replace("`", "");
            lines.Add(line);
            if (lines.Count == 4) break;
        }
        string summary = string.Join(Environment.NewLine, lines);
        const int maxLength = 500;
        if (summary.Length <= maxLength) return summary;
        int cut = maxLength - 1;
        if (char.IsHighSurrogate(summary[cut - 1])) cut--;
        return summary[..cut].TrimEnd() + "…";
    }

    private static string LegacySection(string text, string language)
    {
        var selected = new List<string>();
        bool found = false, inside = false;
        foreach (string raw in text.Split('\n'))
        {
            string heading = raw.Trim();
            if (heading.StartsWith('#'))
            {
                heading = heading.TrimStart('#').Trim().TrimEnd(':').Trim();
                string? code = heading.ToLowerInvariant() switch
                {
                    "русский" or "russian" or "ru" => "ru",
                    "english" or "en" => "en",
                    _ => null
                };
                if (code != null)
                {
                    if (found && code != language) break;
                    inside = code == language;
                    found |= inside;
                    continue;
                }
            }
            if (inside) selected.Add(raw);
        }
        return found ? string.Join('\n', selected) : text;
    }
}
