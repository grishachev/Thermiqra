using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace PCHardwareMonitor;

public sealed class UpdateInfo
{
    public UpdateInfo(
        Version version,
        Version currentVersion,
        string versionText,
        string currentVersionText,
        string releaseUrl,
        string summary)
    {
        Version = version;
        CurrentVersion = currentVersion;
        VersionText = versionText;
        CurrentVersionText = currentVersionText;
        ReleaseUrl = releaseUrl;
        Summary = summary;
    }

    public Version Version { get; }

    public Version CurrentVersion { get; }

    public string VersionText { get; }

    public string CurrentVersionText { get; }

    public string ReleaseUrl { get; }

    public string Summary { get; }
}

public static class UpdateService
{
    private const string LatestReleaseApiUrl =
        "https://api.github.com/repos/grishachev/Thermiqra/releases/latest";

    private static readonly HttpClient Client =
        CreateClient();

    public static async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        using HttpResponseMessage response =
            await Client
                .GetAsync(LatestReleaseApiUrl)
                .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        string json =
            await response.Content
                .ReadAsStringAsync()
                .ConfigureAwait(false);

        using JsonDocument document =
            JsonDocument.Parse(json);

        JsonElement root =
            document.RootElement;

        string? tagName =
            GetString(
                root,
                "tag_name");

        string? releaseUrl =
            GetString(
                root,
                "html_url");

        if (!TryParseVersion(
                tagName,
                out Version latestVersion) ||
            string.IsNullOrWhiteSpace(
                releaseUrl))
        {
            return null;
        }

        Version currentVersion =
            NormalizeVersion(
                Assembly
                    .GetEntryAssembly()?
                    .GetName()
                    .Version
                ?? new Version(0, 0, 0, 0));

        if (latestVersion.CompareTo(
                currentVersion) <= 0)
        {
            return null;
        }

        string? body =
            GetString(
                root,
                "body");

        return new UpdateInfo(
            latestVersion,
            currentVersion,
            FormatVersion(
                latestVersion),
            FormatVersion(
                currentVersion),
            releaseUrl,
            BuildSummary(body));
    }

    private static HttpClient CreateClient()
    {
        HttpClient client =
            new()
            {
                Timeout =
                    TimeSpan.FromSeconds(10)
            };

        client.DefaultRequestHeaders
            .UserAgent
            .ParseAdd(
                "Thermiqra-UpdateChecker/1.0");

        client.DefaultRequestHeaders
            .Accept
            .ParseAdd(
                "application/vnd.github+json");

        client.DefaultRequestHeaders
            .Add(
                "X-GitHub-Api-Version",
                "2026-03-10");

        return client;
    }

    private static string? GetString(
        JsonElement root,
        string propertyName)
    {
        if (!root.TryGetProperty(
                propertyName,
                out JsonElement element) ||
            element.ValueKind ==
                JsonValueKind.Null)
        {
            return null;
        }

        return element.GetString();
    }

    private static bool TryParseVersion(
        string? tagName,
        out Version version)
    {
        version =
            new Version(0, 0, 0, 0);

        if (string.IsNullOrWhiteSpace(
                tagName))
        {
            return false;
        }

        string value =
            tagName.Trim();

        if (value.StartsWith(
                "v",
                StringComparison.OrdinalIgnoreCase))
        {
            value =
                value.Substring(1);
        }

        int suffixIndex =
            value.IndexOfAny(
                new[] { '-', '+' });

        if (suffixIndex >= 0)
        {
            value =
                value.Substring(
                    0,
                    suffixIndex);
        }

        if (!Version.TryParse(
                value,
                out Version? parsed) ||
            parsed == null)
        {
            return false;
        }

        version =
            NormalizeVersion(parsed);

        return true;
    }

    private static Version NormalizeVersion(
        Version version)
    {
        return new Version(
            Math.Max(
                version.Major,
                0),
            Math.Max(
                version.Minor,
                0),
            Math.Max(
                version.Build,
                0),
            Math.Max(
                version.Revision,
                0));
    }

    private static string FormatVersion(
        Version version)
    {
        if (version.Revision > 0)
        {
            return
                $"{version.Major}." +
                $"{version.Minor}." +
                $"{version.Build}." +
                $"{version.Revision}";
        }

        return
            $"{version.Major}." +
            $"{version.Minor}." +
            $"{version.Build}";
    }

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

        string[] lines =
            normalized.Split('\n');

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
}
