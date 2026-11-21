// Summary parser copied verbatim from published tag v1.0.0 (0af199a).
// Keep this compatibility fixture independent of the current parser.
using PCHardwareMonitor;
namespace Thermiqra.Tests;
internal static class Published100ReleaseNotes
{
    internal static string Build(string body) => BuildSummary(body);
    private static string BuildSummary(
        string? body)
    {
        if (string.IsNullOrWhiteSpace(
                body))
        {
            return string.Empty;
        }

        string normalized =
            body.Replace(
                    "\r",
                    string.Empty)
                .Trim();

        string selectedText =
            ExtractLanguageSection(
                normalized);

        string[] lines =
            selectedText.Split('\n');

        List<string> summaryLines =
            new();

        foreach (string rawLine
                 in lines)
        {
            string line =
                rawLine.Trim();

            if (string.IsNullOrWhiteSpace(
                    line) ||
                line.StartsWith(
                    "#",
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith(
                    "- ",
                    StringComparison.Ordinal) ||
                line.StartsWith(
                    "* ",
                    StringComparison.Ordinal))
            {
                line =
                    "• " +
                    line.Substring(2)
                        .Trim();
            }

            summaryLines.Add(line);

            if (summaryLines.Count >= 4)
                break;
        }

        string summary =
            string.Join(
                Environment.NewLine,
                summaryLines);

        const int maxLength = 500;

        if (summary.Length >
            maxLength)
        {
            summary =
                summary.Substring(
                    0,
                    maxLength)
                    .TrimEnd() +
                "…";
        }

        return summary;
    }


    private static string ExtractLanguageSection(
        string text)
    {
        string targetLanguage =
            SettingsService.IsRussian
                ? "ru"
                : "en";

        string[] lines =
            text.Split('\n');

        List<string> selectedLines =
            new();

        bool targetSectionFound =
            false;

        bool insideTargetSection =
            false;

        foreach (string rawLine
                 in lines)
        {
            string line =
                rawLine.Trim();

            if (TryGetLanguageSectionCode(
                    line,
                    out string languageCode))
            {
                if (targetSectionFound &&
                    !string.Equals(
                        languageCode,
                        targetLanguage,
                        StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                insideTargetSection =
                    string.Equals(
                        languageCode,
                        targetLanguage,
                        StringComparison.OrdinalIgnoreCase);

                if (insideTargetSection)
                {
                    targetSectionFound =
                        true;
                }

                continue;
            }

            if (insideTargetSection)
            {
                selectedLines.Add(
                    rawLine);
            }
        }

        if (!targetSectionFound)
        {
            // Совместимость со старыми Release Notes:
            // если языковых секций нет, используем прежнюю логику.
            return text;
        }

        return string.Join(
                Environment.NewLine,
                selectedLines)
            .Trim();
    }


    private static bool TryGetLanguageSectionCode(
        string line,
        out string languageCode)
    {
        languageCode =
            string.Empty;

        if (string.IsNullOrWhiteSpace(
                line) ||
            !line.StartsWith(
                "#",
                StringComparison.Ordinal))
        {
            return false;
        }

        string heading =
            line.TrimStart('#')
                .Trim()
                .TrimEnd(':')
                .Trim();

        if (string.Equals(
                heading,
                "Русский",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                heading,
                "Russian",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                heading,
                "RU",
                StringComparison.OrdinalIgnoreCase))
        {
            languageCode =
                "ru";

            return true;
        }

        if (string.Equals(
                heading,
                "English",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                heading,
                "EN",
                StringComparison.OrdinalIgnoreCase))
        {
            languageCode =
                "en";

            return true;
        }

        return false;
    }

}
