using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace PCHardwareMonitor;

internal sealed record UpdateSignedPayload(int Schema, string Product, string Version, string FileName, long Length, string Sha256);
internal sealed record UpdateSignatureEnvelope(string Algorithm, string KeyId, string Payload, string Signature);

internal static class UpdateSignatureFormat
{
    internal const int MaximumEnvelopeBytes = 16384;
    internal const string Algorithm = "ECDSA-P256-SHA256";
    internal static string KeyId(byte[] publicKey) => Convert.ToHexString(SHA256.HashData(publicKey))[..24];

    internal static byte[] Sign(UpdateSignedPayload payload, ECDsa key)
    {
        byte[] data = JsonSerializer.SerializeToUtf8Bytes(payload);
        byte[] signature = key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return JsonSerializer.SerializeToUtf8Bytes(new UpdateSignatureEnvelope(Algorithm, KeyId(key.ExportSubjectPublicKeyInfo()),
            Convert.ToBase64String(data), Convert.ToBase64String(signature)));
    }

    internal static void Verify(byte[] envelopeBytes, byte[] publicKey, string version, string fileName, long length, string sha256)
    {
        if (envelopeBytes.Length == 0 || envelopeBytes.Length > MaximumEnvelopeBytes) throw new InvalidDataException("Invalid signature size.");
        using var document = JsonDocument.Parse(envelopeBytes);
        RequireProperties(document.RootElement, new[] { "Algorithm", "KeyId", "Payload", "Signature" });
        var envelope = JsonSerializer.Deserialize<UpdateSignatureEnvelope>(envelopeBytes) ?? throw new InvalidDataException("Missing signature.");
        if (envelope.Algorithm != Algorithm || envelope.KeyId != KeyId(publicKey) || envelope.Payload == null || envelope.Signature == null)
            throw new InvalidDataException("Unknown signing key or algorithm.");
        byte[] data = Convert.FromBase64String(envelope.Payload);
        byte[] signature = Convert.FromBase64String(envelope.Signature);
        if (data.Length > 4096 || signature.Length != 64) throw new InvalidDataException("Invalid signature encoding.");
        using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(publicKey, out int consumed);
        if (consumed != publicKey.Length || key.KeySize != 256 ||
            !key.VerifyData(data, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            throw new InvalidDataException("Invalid update signature.");
        using var payloadDocument = JsonDocument.Parse(data);
        RequireProperties(payloadDocument.RootElement, new[] { "Schema", "Product", "Version", "FileName", "Length", "Sha256" });
        var payload = JsonSerializer.Deserialize<UpdateSignedPayload>(data) ?? throw new InvalidDataException("Missing payload.");
        if (payload.Schema != 1 || payload.Product != "Thermiqra" || payload.Version != version ||
            payload.FileName != fileName || payload.Length != length || length <= 0 ||
            payload.Sha256 == null || payload.Sha256.Length != 64 || !payload.Sha256.All(Uri.IsHexDigit) ||
            !payload.Sha256.Equals(sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update identity mismatch.");
    }

    private static void RequireProperties(JsonElement element, string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Expected signature object.");
        var properties = element.EnumerateObject().Select(p => p.Name).ToArray();
        if (properties.Length != names.Length || properties.Distinct(StringComparer.Ordinal).Count() != names.Length ||
            properties.Except(names, StringComparer.Ordinal).Any()) throw new InvalidDataException("Unexpected signature fields.");
    }
}
