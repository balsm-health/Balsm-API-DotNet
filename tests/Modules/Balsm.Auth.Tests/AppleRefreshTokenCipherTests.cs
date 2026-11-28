using System.Security.Cryptography;
using Balsm.Infrastructure.Encryption;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Balsm.Auth.Tests;

public sealed class AppleRefreshTokenCipherTests
{
    private static readonly IConfiguration Config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DobEncryption:Key"] = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()),
        }).Build();

    [Fact]
    public void Cipher_RoundTrips()
    {
        var cipher = new AppleRefreshTokenCipher(Config);

        var blob = cipher.Encrypt("r-apple-refresh");

        blob.Should().NotEqual(System.Text.Encoding.UTF8.GetBytes("r-apple-refresh"));
        cipher.Decrypt(blob).Should().Be("r-apple-refresh");
    }

    [Fact]
    public void Cipher_KeyDiffersFromDobKey()
    {
        var dob = new DobEncryptionService(Config, NullLogger<DobEncryptionService>.Instance);
        var blob = dob.EncryptString("r-apple-refresh");

        var act = () => new AppleRefreshTokenCipher(Config).Decrypt(blob);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Cipher_KeyMissing_ThrowsOnUseNotOnConstruction()
    {
        var cipher = new AppleRefreshTokenCipher(new ConfigurationBuilder().Build());

        var act = () => cipher.Encrypt("r-1");

        act.Should().Throw<InvalidOperationException>();
    }
}
