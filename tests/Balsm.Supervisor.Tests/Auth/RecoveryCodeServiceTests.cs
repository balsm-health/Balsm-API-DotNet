using Balsm.Supervisor.Auth;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Balsm.Supervisor.Tests.Auth;

public class RecoveryCodeServiceTests
{
    private readonly InMemoryCredentialStore _store = new();
    private readonly RecoveryCodeService _sut;

    public RecoveryCodeServiceTests()
    {
        _sut = new RecoveryCodeService(_store, NullLogger<RecoveryCodeService>.Instance);
    }

    [Fact]
    public async Task GenerateAsync_WithoutCredentials_Throws()
    {
        var act = () => _sut.GenerateAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GenerateAsync_ReturnsUrlSafeCodeAndStoresOnlyItsHash()
    {
        await SeedCredentialsAsync();

        var code = await _sut.GenerateAsync();

        code.Should().HaveLength(32).And.MatchRegex("^[A-Za-z0-9_-]+$");
        var creds = await _store.LoadCredentialsAsync();
        creds!.RecoveryCodeHash.Should().NotBeNullOrEmpty().And.NotBe(code);
        creds.RecoveryCodeCreatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateAsync_ClearsUsedAndRetiredMarkers()
    {
        await SeedCredentialsAsync();
        var creds = (await _store.LoadCredentialsAsync())!;
        creds.RecoveryCodeUsedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        creds.RecoveryCodeRetiredAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await _sut.GenerateAsync();

        creds.RecoveryCodeUsedAt.Should().BeNull();
        creds.RecoveryCodeRetiredAt.Should().BeNull();
    }

    [Fact]
    public async Task ConsumeAsync_WithIssuedCode_SucceedsOnce()
    {
        await SeedCredentialsAsync();
        var code = await _sut.GenerateAsync();

        var first = await _sut.ConsumeAsync(code);
        var second = await _sut.ConsumeAsync(code);

        first.Should().BeTrue();
        second.Should().BeFalse();
    }

    [Fact]
    public async Task ConsumeAsync_WithWrongCode_FailsAndLeavesCodeUsable()
    {
        await SeedCredentialsAsync();
        var code = await _sut.GenerateAsync();

        var wrong = await _sut.ConsumeAsync("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");

        wrong.Should().BeFalse();
        (await _sut.ConsumeAsync(code)).Should().BeTrue();
    }

    [Fact]
    public async Task ConsumeAsync_WithoutCredentials_Fails()
    {
        var result = await _sut.ConsumeAsync("anything");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ConsumeAsync_WhenNoCodeIssued_Fails()
    {
        await SeedCredentialsAsync();

        var result = await _sut.ConsumeAsync("anything");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ConsumeAsync_AfterRetire_Fails()
    {
        await SeedCredentialsAsync();
        var code = await _sut.GenerateAsync();
        await _sut.RetireAsync();

        var result = await _sut.ConsumeAsync(code);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RetireAsync_WithoutCredentials_DoesNothing()
    {
        await _sut.RetireAsync();

        (await _store.HasCredentialsAsync()).Should().BeFalse();
    }

    private Task SeedCredentialsAsync()
        => _store.SaveCredentialsAsync(new AdminCredentials
        {
            Username = "admin",
            PasswordHash = "hash",
            Salt = "salt",
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
}
