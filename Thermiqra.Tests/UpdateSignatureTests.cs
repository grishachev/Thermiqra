using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

public class UpdateSignatureTests
{
    [Fact]
    public void RealSignedOldReleasePassesEmbeddedKeyButCannotBeReplayedAsNewVersion()
    {
        byte[] signature = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Update-1.0.0.signature.json"));
        const string hash = "232FB99A4BB6F5352D0F89DF8B4714A64ACA80DDE32A54529AC1A1AF4611CABC";
        var update = new UpdateInfo(new Version(1, 0, 0), new Version(0, 9, 0), "1.0.0", "0.9.0", "", "", "", "Thermiqra_Setup_1.0.0.exe", hash);
        UpdateSignaturePolicy.Verify(signature, update, 60175052, hash);
        var replay = new UpdateInfo(new Version(1, 0, 1), new Version(1, 0, 0), "1.0.1", "1.0.0", "", "", "", "Thermiqra_Setup_1.0.1.exe", hash);
        Assert.Throws<InvalidDataException>(() => UpdateSignaturePolicy.Verify(signature, replay, 60175052, hash));
    }

    private static readonly string Hash = Convert.ToHexString(SHA256.HashData(new byte[] { 1, 2, 3 }));
    private static UpdateSignedPayload Payload => new(1, "Thermiqra", "1.0.1", "Thermiqra_Setup_1.0.1.exe", 3, Hash);
    private static void Verify(byte[] bytes, byte[] key, string version = "1.0.1", string file = "Thermiqra_Setup_1.0.1.exe", long size = 3, string? hash = null) =>
        UpdateSignatureFormat.Verify(bytes, key, version, file, size, hash ?? Hash);

    [Fact] public void EmbeddedPublicKeyIsValidP256()
    {
        byte[] publicKey = Convert.FromBase64String(UpdateSigningKey.PublicKeyBase64);
        using var key = ECDsa.Create(); key.ImportSubjectPublicKeyInfo(publicKey, out int read);
        Assert.Equal(publicKey.Length, read); Assert.Equal(256, key.KeySize);
        Assert.Throws<CryptographicException>(() => key.ExportPkcs8PrivateKey());
    }

    [Fact] public void AuthenticSignatureWorksWithoutWindowsCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Verify(UpdateSignatureFormat.Sign(Payload, key), key.ExportSubjectPublicKeyInfo());
    }

    [Theory]
    [InlineData("version")][InlineData("file")][InlineData("length")][InlineData("hash")][InlineData("key")][InlineData("payload")][InlineData("signature")]
    public void SubstitutionAndReplayAreRejected(string mutation)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] publicKey = key.ExportSubjectPublicKeyInfo();
        byte[] bytes = UpdateSignatureFormat.Sign(Payload, key);
        if (mutation == "key")
        {
            using var attacker = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var forged = JsonSerializer.Deserialize<UpdateSignatureEnvelope>(UpdateSignatureFormat.Sign(Payload, attacker))!;
            bytes = JsonSerializer.SerializeToUtf8Bytes(forged with { KeyId = UpdateSignatureFormat.KeyId(publicKey) });
        }
        if (mutation is "payload" or "signature")
        {
            var envelope = JsonSerializer.Deserialize<UpdateSignatureEnvelope>(bytes)!;
            if (mutation == "payload") envelope = envelope with { Payload = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(Payload with { Length = 4 })) };
            else { byte[] signature = Convert.FromBase64String(envelope.Signature); signature[0] ^= 1; envelope = envelope with { Signature = Convert.ToBase64String(signature) }; }
            bytes = JsonSerializer.SerializeToUtf8Bytes(envelope);
        }
        Assert.Throws<InvalidDataException>(() => Verify(bytes, publicKey, mutation == "version" ? "1.0.2" : "1.0.1",
            mutation == "file" ? "Thermiqra_Setup_1.0.2.exe" : Payload.FileName,
            mutation == "length" ? 4 : 3, mutation == "hash" ? new string('0', 64) : Hash));
    }

    [Theory]
    [InlineData("")][InlineData("{}")] [InlineData("null")][InlineData("{\"Algorithm\":\"ECDSA-P256-SHA256\",\"Algorithm\":\"x\"}")]
    public void MissingAndMalformedSignaturesAreRejected(string input)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Assert.Throws<InvalidDataException>(() => Verify(Encoding.UTF8.GetBytes(input), key.ExportSubjectPublicKeyInfo()));
    }

    [Fact] public void OversizedSignatureIsRejected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Assert.Throws<InvalidDataException>(() => Verify(new byte[UpdateSignatureFormat.MaximumEnvelopeBytes + 1], key.ExportSubjectPublicKeyInfo()));
    }

    [Theory] [InlineData("product")][InlineData("schema")][InlineData("zero-length")][InlineData("invalid-hash")]
    public void EvenCorrectlySignedInvalidMetadataIsRejected(string field)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payload = field switch { "product" => Payload with { Product = "Another application" },
            "schema" => Payload with { Schema = 2 }, "zero-length" => Payload with { Length = 0 }, _ => Payload with { Sha256 = "invalid" } };
        Assert.Throws<InvalidDataException>(() => Verify(UpdateSignatureFormat.Sign(payload, key), key.ExportSubjectPublicKeyInfo(), size: payload.Length, hash: payload.Sha256));
    }
}
