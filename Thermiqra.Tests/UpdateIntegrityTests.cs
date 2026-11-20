using System.Security.Cryptography;
using Xunit;
namespace PCHardwareMonitor.Tests;
public sealed class UpdateIntegrityTests
{
    [Theory]
    [InlineData(null)][InlineData("")][InlineData("sha256:abc")][InlineData("0")]
    [InlineData("000000000000000000000000000000000000000000000000000000000000000Z")]
    public void MissingOrMalformedHashBlocks(string? hash) => Assert.Throws<InvalidDataException>(() => UpdateIntegrityPolicy.RequiredHash(hash));

    [Theory]
    [InlineData("http://github.com/grishachev/Thermiqra/releases/download/v1/a.exe")]
    [InlineData("https://github.com.evil.test/grishachev/Thermiqra/releases/download/v1/a.exe")]
    [InlineData("https://github.com/elsewhere/Thermiqra/releases/download/v1/a.exe")]
    [InlineData("https://user@github.com/grishachev/Thermiqra/releases/download/v1/a.exe")]
    public void WrongSourceBlocks(string url) => Assert.Throws<InvalidDataException>(() => UpdateIntegrityPolicy.RequireSource(url));

    [Theory]
    [InlineData("https://release-assets.githubusercontent.com/a", true)]
    [InlineData("https://objects.githubusercontent.com/a", true)]
    [InlineData("https://release-assets.githubusercontent.com.evil.test/a", false)]
    [InlineData("http://objects.githubusercontent.com/a", false)]
    [InlineData("https://user@objects.githubusercontent.com/a", false)]
    public void RedirectMustStayOnGithubHttpsAssetHosts(string url, bool allowed)
    {
        if (allowed) UpdateIntegrityPolicy.RequireAssetRedirect(new Uri(url));
        else Assert.Throws<InvalidDataException>(() => UpdateIntegrityPolicy.RequireAssetRedirect(new Uri(url)));
    }

    [Fact]
    public async Task FileMustMatchAndTamperingIsDetected()
    {
        string path = Path.Combine(Path.GetTempPath(), "Thermiqra-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            await File.WriteAllBytesAsync(path, [1, 2, 3]);
            string expected = Convert.ToHexString(SHA256.HashData([1, 2, 3]));
            await UpdateIntegrityPolicy.VerifyAsync(path, expected, default);
            await File.WriteAllBytesAsync(path, [1, 2, 4]);
            await Assert.ThrowsAsync<InvalidDataException>(() => UpdateIntegrityPolicy.VerifyAsync(path, expected, default));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ApplicationBinaryCanPassIntegrityWithoutPublisherCertificate()
    {
        string path = typeof(UpdateService).Assembly.Location;
        string expected = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path)));
        await UpdateIntegrityPolicy.VerifyAsync(path, expected, default);
    }
}
