using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace PCHardwareMonitor;

internal static class UpdateSignaturePolicy
{
    internal static void Verify(byte[] signature, UpdateInfo update, long length, string hash)
    {
        try
        {
            UpdateSignatureFormat.Verify(signature, Convert.FromBase64String(UpdateSigningKey.PublicKeyBase64),
                update.VersionText, update.InstallerFileName!, length, hash);
        }
        catch (Exception ex) when (ex is InvalidDataException or CryptographicException or FormatException or JsonException or ArgumentException)
        {
            throw new InvalidDataException(SettingsService.L(
                "Подлинность обновления не подтверждена. Подпись отсутствует, повреждена или не соответствует этому выпуску. Установка остановлена.",
                "The update could not be authenticated. Its signature is missing, damaged or does not match this release. Installation stopped."), ex);
        }
    }
}
