using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace PCHardwareMonitor;

internal static class UpdateIntegrityPolicy
{
    internal static string RequiredHash(string? expected)
    {
        if (expected == null || expected.Length != 64 || expected.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidDataException(SettingsService.L("Обновление не содержит корректной контрольной суммы SHA-256. Автоматическая установка запрещена.",
                "The update has no valid SHA-256 checksum. Automatic installation is blocked."));
        return expected.ToUpperInvariant();
    }

    internal static void RequireSource(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0 ||
            !uri.IsDefaultPort || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.AbsolutePath.StartsWith("/grishachev/Thermiqra/releases/download/", StringComparison.Ordinal))
            throw new InvalidDataException(SettingsService.L("Источник установщика не является официальным релизом Thermiqra.",
                "The installer source is not an official Thermiqra release."));
    }

    internal static void RequireAssetRedirect(Uri? uri)
    {
        if (uri == null || uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0 || !uri.IsDefaultPort ||
            !new[] { "github.com", "release-assets.githubusercontent.com", "objects.githubusercontent.com" }
                .Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException(SettingsService.L("Небезопасное перенаправление загрузки.", "Unsafe download redirect."));
    }

    internal static async Task VerifyAsync(string path, string? expected, CancellationToken token)
    {
        string required = RequiredHash(expected);
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] actual = await SHA256.HashDataAsync(input, token).ConfigureAwait(false);
        if (!CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(required)))
            throw new InvalidDataException(SettingsService.L("Контрольная сумма установщика не совпала. Запуск запрещён.",
                "Installer checksum mismatch. Execution is blocked."));
    }
}
