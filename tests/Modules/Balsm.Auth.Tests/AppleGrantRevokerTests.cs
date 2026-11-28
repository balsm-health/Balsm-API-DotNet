using System.Security.Cryptography;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Auth.Infrastructure.Services;
using Balsm.Infrastructure.Auth;
using Balsm.Infrastructure.Encryption;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Balsm.Auth.Tests;

/// Purge-time revocation of the Apple grant: the stored refresh token is
/// decrypted, sent to Apple and cleared once Apple confirms.
public sealed class AppleGrantRevokerTests : IDisposable
{
    private readonly AuthDbContext _db;
    private readonly SqliteConnection _connection;
    private readonly AppleRefreshTokenCipher _cipher;

    public AppleGrantRevokerTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var services = new ServiceCollection();
        services.AddSingleton<Balsm.SharedKernel.Events.IDomainEventDispatcher, NullDispatcher>();
        services.AddDbContext<AuthDbContext>(o => o.UseSqlite(_connection));
        _db = services.BuildServiceProvider().GetRequiredService<AuthDbContext>();
        _db.Database.EnsureCreated();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DobEncryption:Key"] = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()),
        }).Build();
        _cipher = new AppleRefreshTokenCipher(config);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private AppleGrantRevoker Revoker(FakeClient client) =>
        new(_db, client, _cipher, NullLogger<AppleGrantRevoker>.Instance);

    private async Task<Guid> AddAppleIdentityAsync(string? refreshToken)
    {
        var userId = Guid.NewGuid();
        var identity = UserIdentity.Create(userId, "apple", "a-sub-" + userId, null);
        if (refreshToken is not null) identity.SetAppleRefreshToken(_cipher.Encrypt(refreshToken));
        _db.UserIdentities.Add(identity);
        await _db.SaveChangesAsync();
        return userId;
    }

    [Fact]
    public async Task RevokeForAccount_NoIdentity_ReturnsNoAppleIdentity()
    {
        var userId = Guid.NewGuid();
        _db.UserIdentities.Add(UserIdentity.Create(userId, "google", "g-sub-1", null));
        await _db.SaveChangesAsync();
        var client = new FakeClient(true, AppleRevokeOutcome.Revoked);

        (await Revoker(client).RevokeForAccountAsync(userId)).Should().Be("no_apple_identity");
        client.Revoked.Should().BeEmpty();
    }

    [Fact]
    public async Task RevokeForAccount_IdentityWithoutToken_ReturnsNoRefreshToken()
    {
        var userId = await AddAppleIdentityAsync(null);
        var client = new FakeClient(true, AppleRevokeOutcome.Revoked);

        (await Revoker(client).RevokeForAccountAsync(userId)).Should().Be("no_refresh_token");
        client.Revoked.Should().BeEmpty();
    }

    [Fact]
    public async Task RevokeForAccount_ClientNotConfigured_ReturnsConfigMissing()
    {
        var userId = await AddAppleIdentityAsync("r-1");
        var client = new FakeClient(false, AppleRevokeOutcome.ConfigMissing);

        (await Revoker(client).RevokeForAccountAsync(userId)).Should().Be("config_missing");
        (await _db.UserIdentities.AsNoTracking().SingleAsync()).AppleRefreshTokenCiphertext.Should().NotBeNull();
    }

    [Fact]
    public async Task RevokeForAccount_Revoked_ClearsStoredToken()
    {
        var userId = await AddAppleIdentityAsync("r-1");
        var client = new FakeClient(true, AppleRevokeOutcome.Revoked);

        (await Revoker(client).RevokeForAccountAsync(userId)).Should().Be("revoked");

        client.Revoked.Should().Equal("r-1");
        (await _db.UserIdentities.AsNoTracking().SingleAsync()).AppleRefreshTokenCiphertext.Should().BeNull();
    }

    [Fact]
    public async Task RevokeForAccount_AppleRejects_ReturnsFailedAndKeepsToken()
    {
        var userId = await AddAppleIdentityAsync("r-1");

        (await Revoker(new FakeClient(true, AppleRevokeOutcome.Failed)).RevokeForAccountAsync(userId))
            .Should().Be("failed");
        (await _db.UserIdentities.AsNoTracking().SingleAsync()).AppleRefreshTokenCiphertext.Should().NotBeNull();
    }

    [Fact]
    public async Task RevokeForAccount_ClientThrows_ReturnsFailed()
    {
        var userId = await AddAppleIdentityAsync("r-1");

        (await Revoker(new FakeClient(true, AppleRevokeOutcome.Revoked, throws: true)).RevokeForAccountAsync(userId))
            .Should().Be("failed");
    }

    [Fact]
    public async Task RevokeForAccount_UndecryptableToken_ReturnsFailed()
    {
        var userId = Guid.NewGuid();
        var identity = UserIdentity.Create(userId, "apple", "a-sub-x", null);
        identity.SetAppleRefreshToken(RandomNumberGenerator.GetBytes(40));
        _db.UserIdentities.Add(identity);
        await _db.SaveChangesAsync();

        (await Revoker(new FakeClient(true, AppleRevokeOutcome.Revoked)).RevokeForAccountAsync(userId))
            .Should().Be("failed");
    }

    private sealed class FakeClient(bool configured, AppleRevokeOutcome outcome, bool throws = false)
        : AppleTokenClient(null!, new ConfigurationBuilder().Build(), NullLogger<AppleTokenClient>.Instance)
    {
        public List<string> Revoked { get; } = [];

        public override bool IsConfigured => configured;

        public override Task<AppleRevokeOutcome> RevokeAsync(string refreshToken, CancellationToken ct)
        {
            Revoked.Add(refreshToken);
            if (throws) throw new HttpRequestException("apple down");
            return Task.FromResult(outcome);
        }
    }

    private sealed class NullDispatcher : Balsm.SharedKernel.Events.IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(IEnumerable<Balsm.SharedKernel.Events.IDomainEvent> events, CancellationToken ct) => Task.CompletedTask;
    }
}
