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
    private sealed record VerifiedUpdate(UpdateInfo Release, string Hash, byte[] Signature);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, VerifiedUpdate> VerifiedDownloads = new(StringComparer.OrdinalIgnoreCase);
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
                SettingsService.L("В выпуске отсутствует установщик Thermiqra.", "The release does not contain a Thermiqra installer."));
        }

        string updateDirectory =
            GetUpdateDirectory();

        string requiredHash = UpdateIntegrityPolicy.RequiredHash(update.InstallerSha256);
        UpdateIntegrityPolicy.RequireSource(update.InstallerDownloadUrl);
        byte[] signature = await DownloadSignatureAsync(update.InstallerDownloadUrl + ".signature.json", cancellationToken).ConfigureAwait(false);
        RequireSafeCache(updateDirectory);

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
                SettingsService.L("Некорректное имя файла установщика обновления.", "The update installer file name is invalid."));
        }

        string installerPath =
            Path.Combine(
                updateDirectory,
                safeFileName);

        string partialPath = installerPath + "." + Guid.NewGuid().ToString("N") + ".download";

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

            UpdateIntegrityPolicy.RequireAssetRedirect(response.RequestMessage?.RequestUri);
            long? totalLength =
                response.Content
                    .Headers
                    .ContentLength;

            await using Stream input =
                await response.Content
                    .ReadAsStreamAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            await using (
                FileStream output =
                    new(
                        partialPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        81920,
                        useAsync: true))
            {
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
                    if (received > 512L * 1024 * 1024)
                        throw new InvalidDataException(SettingsService.L("Установщик превышает допустимый размер загрузки.", "Installer exceeds the download size limit."));

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
            }

            // Важно: поток записи уже закрыт до проверки SHA-256.
            // Иначе Windows не даст VerifySha256Async открыть
            // .download-файл из-за FileShare.None.
            await VerifySha256Async(
                    partialPath,
                    update.InstallerSha256,
                    cancellationToken)
                .ConfigureAwait(false);

            UpdateSignaturePolicy.Verify(signature, update, new FileInfo(partialPath).Length, requiredHash);

            File.Move(
                partialPath,
                installerPath,
                true);

            VerifiedDownloads[Path.GetFullPath(installerPath)] = new(update, requiredHash, signature);

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

    public static Task StartInstallerAsync(string installerPath) => Task.Run(() => StartInstaller(installerPath));

    private static void StartInstaller(string installerPath)
    {
        if (string.IsNullOrWhiteSpace(
                installerPath) ||
            !File.Exists(
                installerPath))
        {
            throw new FileNotFoundException(
                SettingsService.L("Установщик обновления Thermiqra не найден.", "The Thermiqra update installer was not found."),
                installerPath);
        }

        string fullPath = Path.GetFullPath(installerPath);
        if (!VerifiedDownloads.TryGetValue(fullPath, out VerifiedUpdate? verified))
            throw new InvalidDataException(SettingsService.L("Установщик не прошёл проверку в текущем сеансе. Повторите загрузку.",
                "The installer was not verified in this session. Download it again."));
        RequireSafeCache(Path.GetDirectoryName(fullPath)!);
        // Keep writes/deletion locked until Windows has opened the executable.
        using FileStream executionLock = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        UpdateIntegrityPolicy.VerifyAsync(fullPath, verified.Hash, default).GetAwaiter().GetResult();
        UpdateSignaturePolicy.Verify(verified.Signature, verified.Release, executionLock.Length, verified.Hash);
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
                SettingsService.L("Не удалось запустить установщик обновления Thermiqra.", "The Thermiqra update installer could not be started."));
        }
    }

    private static async Task<byte[]> DownloadSignatureAsync(string url, CancellationToken token)
    {
        UpdateIntegrityPolicy.RequireSource(url);
        try
        {
            using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            UpdateIntegrityPolicy.RequireAssetRedirect(response.RequestMessage?.RequestUri);
            if (response.Content.Headers.ContentLength > UpdateSignatureFormat.MaximumEnvelopeBytes)
                throw new InvalidDataException(SettingsService.L("Файл подписи обновления слишком большой.", "The update signature file is too large."));
            await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var output = new MemoryStream();
            byte[] buffer = new byte[4096];
            int count;
            while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
            {
                if (output.Length + count > UpdateSignatureFormat.MaximumEnvelopeBytes)
                    throw new InvalidDataException(SettingsService.L("Файл подписи обновления слишком большой.", "The update signature file is too large."));
                output.Write(buffer, 0, count);
            }
            return output.ToArray();
        }
        catch (HttpRequestException ex)
        {
            throw new IOException(SettingsService.L("Не удалось получить подпись обновления из официального релиза. Установка остановлена.",
                "Could not retrieve the update signature from the official release. Installation stopped."), ex);
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
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
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

            RequireSafeCache(updateDirectory);
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
        catch (Exception ex)
        {
            DiagnosticLog.Failure("Update.Cache", ex);
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
        catch (Exception ex)
        {
            DiagnosticLog.Failure("Update.CacheDelete", ex);
            // Файл может быть занят установщиком.
            // Следующий запуск попробует удалить его ещё раз.
        }
    }

    private static Task VerifySha256Async(string filePath, string? expectedSha256, CancellationToken cancellationToken) =>
        UpdateIntegrityPolicy.VerifyAsync(filePath, expectedSha256, cancellationToken);

    private static void RequireSafeCache(string path)
    {
        string expected = Path.GetFullPath(GetUpdateDirectory());
        if (!Path.GetFullPath(path).Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException(SettingsService.L("Некорректный путь к каталогу обновлений.", "Unexpected update cache path."));
        for (DirectoryInfo? current = new(path); current != null; current = current.Parent)
            try
            {
                if ((File.GetAttributes(current.FullName) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException(SettingsService.L("Каталог обновлений содержит ссылку. Автоматическая установка запрещена.",
                        "The update cache contains a link. Automatic installation is blocked."));
            }
            catch (DirectoryNotFoundException) { }
            catch (FileNotFoundException) { }
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

    private static string BuildSummary(string? body) =>
        UpdateReleaseNotes.BuildSummary(body, SettingsService.IsRussian);
}
