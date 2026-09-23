using System.Security.Cryptography;
using Balsm.Infrastructure.Encryption;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Balsm.CareTeam.Tests;

public sealed class CareProviderEncryptionTests
{
    private static CareTeamEncryptionService Service(string? keyBase64 = null)
    {
        var key = keyBase64 ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["CareTeamEncryption:Key"] = key })
            .Build();
        return new CareTeamEncryptionService(config, NullLogger<CareTeamEncryptionService>.Instance);
    }

    [Fact]
    public void Encrypt_ThenDecrypt_RoundTrips()
    {
        var svc = Service();
        var blob = svc.Encrypt("Provider Alpha");
        svc.Decrypt(blob).Should().Be("Provider Alpha");
    }

    [Fact]
    public void Encrypt_SamePlaintextTwice_ProducesDifferentCiphertext()
    {
        var svc = Service();
        svc.Encrypt("Provider Alpha").Should().NotEqual(svc.Encrypt("Provider Alpha"));
    }

    [Fact]
    public void Decrypt_WithDifferentKey_Throws()
    {
        var blob = Service().Encrypt("Provider Alpha");
        var other = Service();
        Action act = () => other.Decrypt(blob);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void EncryptOptional_Null_ReturnsNull()
    {
        Service().EncryptOptional(null).Should().BeNull();
    }

    [Fact]
    public void DecryptOptional_Null_ReturnsNull()
    {
        Service().DecryptOptional(null).Should().BeNull();
    }

    [Fact]
    public void Constructor_WithKeyShorterThan32Bytes_Throws()
    {
        var shortKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        Action act = () => Service(shortKey);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*32 bytes*");
    }

    [Fact]
    public void Encrypt_LongFreeText_RoundTrips()
    {
        var svc = Service();
        var longNote = new string('n', 100_000);
        svc.Decrypt(svc.Encrypt(longNote)).Should().Be(longNote);
    }
}
