using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
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
        string summary,
        string? installerDownloadUrl,
        string? installerFileName,
        string? installerSha256)
    {
        Version = version;
        CurrentVersion = currentVersion;
        VersionText = versionText;
        CurrentVersionText = currentVersionText;
        ReleaseUrl = releaseUrl;
        Summary = summary;
        InstallerDownloadUrl = installerDownloadUrl;
        InstallerFileName = installerFileName;
        InstallerSha256 = installerSha256;
    }

    public Version Version { get; }

    public Version CurrentVersion { get; }

    public string VersionText { get; }

    public string CurrentVersionText { get; }

    public string ReleaseUrl { get; }

    public string Summary { get; }

    public string? InstallerDownloadUrl { get; }

    public string? InstallerFileName { get; }

    public string? InstallerSha256 { get; }

    public bool HasInstaller =>
        !string.IsNullOrWhiteSpace(
            InstallerDownloadUrl) &&
        !string.IsNullOrWhiteSpace(
            InstallerFileName);
}

public static class UpdateService
{
    private const string LatestReleaseApiUrl =
        "https://api.github.com/repos/grishachev/Thermiqra/releases/latest";

    private const string InstallerFilePrefix =
        "Thermiqra_Setup_";

    private static readonly HttpClient Client =
        CreateClient();

    public static async Task<UpdateInfo?> CheckForUpdateAsync(
        CancellationToken cancellationToken = default)
    {
        CleanupInstallerCache();

        using HttpResponseMessage response =
            await Client
                .GetAsync(
                    LatestReleaseApiUrl,
                    cancellationToken)
                .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        string json =
            await response.Content
                .ReadAsStringAsync(
                    cancellationToken)
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

        string expectedInstallerName =
            $"{InstallerFilePrefix}" +
            $"{FormatVersion(latestVersion)}.exe";

        string? installerDownloadUrl = null;
        string? installerFileName = null;
        string? installerSha256 = null;

        if (root.TryGetProperty(
                "assets",
                out JsonElement assets) &&
            assets.ValueKind ==
                JsonValueKind.Array)
        {
            foreach (JsonElement asset
                     in assets.EnumerateArray())
            {
                string? assetName =
                    GetString(
                        asset,
                        "name");

                if (!string.Equals(
                        assetName,
                        expectedInstallerName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? assetUrl =
                    GetString(
                        asset,
                        "browser_download_url");

                if (string.IsNullOrWhiteSpace(
                        assetUrl))
                {
                    continue;
                }

                installerDownloadUrl =
                    assetUrl;

                installerFileName =
                    assetName;

                string? digest =
                    GetString(
                        asset,
                        "digest");

                if (!string.IsNullOrWhiteSpace(
                        digest) &&
                    digest.StartsWith(
                        "sha256:",
                        StringComparison.OrdinalIgnoreCase))
                {
                    installerSha256 =
                        digest.Substring(
                            "sha256:".Length)
                            .Trim();
                }

                break;
            }
        }

        return new UpdateInfo(
            latestVersion,
            currentVersion,
            FormatVersion(
                latestVersion),
            FormatVersion(
                currentVersion),
            releaseUrl,
            BuildSummary(body),
            installerDownloadUrl,
            installerFileName,
            installerSha256);
    }

    public static async Task<string> DownloadInstallerAsync(
        UpdateInfo update,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!update.HasInstaller ||
            string.IsNullOrWhiteSpace(
                update.InstallerDownloadUrl) ||
            string.IsNullOrWhiteSpace(
                update.InstallerFileName))
        {
            throw new InvalidOperationException(
                "The release does not contain a Thermiqra installer.");
        }

        string updateDirectory =
            GetUpdateDirectory();

        Directory.CreateDirectory(
            updateDirectory);

        string safeFileName =
            Path.GetFileName(
                update.InstallerFileName);

        if (string.IsNullOrWhiteSpace(
                safeFileName) ||
            !safeFileName.EndsWith(
                ".exe",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The update installer file name is invalid.");
        }

        string installerPath =
            Path.Combine(
                updateDirectory,
                safeFileName);

        string partialPath =
            installerPath +
            ".download";

        TryDeleteFile(
            partialPath);

        TryDeleteFile(
            installerPath);

        try
        {
            using HttpResponseMessage response =
                await Client
                    .GetAsync(
                        update.InstallerDownloadUrl,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken)
                    .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            long? totalLength =
                response.Content
                    .Headers
                    .ContentLength;

            await using Stream input =
                await response.Content
                    .ReadAsStreamAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            await using FileStream output =
                new(
                    partialPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    useAsync: true);

            byte[] buffer =
                new byte[81920];

            long received = 0;
            int lastPercent = -1;

            while (true)
            {
                int read =
                    await input.ReadAsync(
                            buffer.AsMemory(
                                0,
                                buffer.Length),
                            cancellationToken)
                        .ConfigureAwait(false);

                if (read <= 0)
                    break;

                await output.WriteAsync(
                        buffer.AsMemory(
                            0,
                            read),
                        cancellationToken)
                    .ConfigureAwait(false);

                received +=
                    read;

                if (totalLength.HasValue &&
                    totalLength.Value > 0)
                {
                    int percent =
                        (int)Math.Clamp(
                            received * 100 /
                            totalLength.Value,
                            0,
                            99);

                    if (percent !=
                        lastPercent)
                    {
                        lastPercent =
                            percent;

                        progress?.Report(
                            percent);
                    }
                }
            }

            await output
                .FlushAsync(
                    cancellationToken)
                .ConfigureAwait(false);

            await VerifySha256Async(
                    partialPath,
                    update.InstallerSha256,
                    cancellationToken)
                .ConfigureAwait(false);

            File.Move(
                partialPath,
                installerPath,
                true);

            progress?.Report(100);

            return installerPath;
        }
        catch
        {
            TryDeleteFile(
                partialPath);

            throw;
        }
    }

    public static void StartInstaller(
        string installerPath)
    {
        if (string.IsNullOrWhiteSpace(
                installerPath) ||
            !File.Exists(
                installerPath))
        {
            throw new FileNotFoundException(
                "The Thermiqra update installer was not found.",
                installerPath);
        }

        ProcessStartInfo startInfo =
            new()
            {
                FileName =
                    installerPath,

                Arguments =
                    "/SP- /VERYSILENT /SUPPRESSMSGBOXES /NORESTART",

                WorkingDirectory =
                    Path.GetDirectoryName(
                        installerPath)
                    ?? Environment.CurrentDirectory,

                UseShellExecute =
                    true
            };

        Process? process =
            Process.Start(
                startInfo);

        if (process == null)
        {
            throw new InvalidOperationException(
                "The Thermiqra update installer could not be started.");
        }
    }

    private static HttpClient CreateClient()
    {
        HttpClient client =
            new()
            {
                Timeout =
                    TimeSpan.FromMinutes(10)
            };

        client.DefaultRequestHeaders
            .UserAgent
            .ParseAdd(
                "Thermiqra-UpdateChecker/1.1");

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

    private static string GetUpdateDirectory()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "Thermiqra",
            "Updates");
    }

    private static void CleanupInstallerCache()
    {
        try
        {
            string updateDirectory =
                GetUpdateDirectory();

            if (!Directory.Exists(
                    updateDirectory))
            {
                return;
            }

            foreach (string file
                     in Directory.EnumerateFiles(
                         updateDirectory,
                         $"{InstallerFilePrefix}*.exe"))
            {
                TryDeleteFile(file);
            }

            foreach (string file
                     in Directory.EnumerateFiles(
                         updateDirectory,
                         "*.download"))
            {
                TryDeleteFile(file);
            }
        }
        catch
        {
            // Кэш обновлений не должен мешать запуску Thermiqra.
        }
    }

    private static void TryDeleteFile(
        string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Файл может быть занят установщиком.
            // Следующий запуск попробует удалить его ещё раз.
        }
    }

    private static async Task VerifySha256Async(
        string filePath,
        string? expectedSha256,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                expectedSha256))
        {
            return;
        }

        await using FileStream stream =
            File.OpenRead(
                filePath);

        using SHA256 sha256 =
            SHA256.Create();

        byte[] hash =
            await sha256
                .ComputeHashAsync(
                    stream,
                    cancellationToken)
                .ConfigureAwait(false);

        string actual =
            Convert.ToHexString(hash);

        if (!string.Equals(
                actual,
                expectedSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The downloaded Thermiqra installer failed the SHA-256 integrity check.");
        }
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