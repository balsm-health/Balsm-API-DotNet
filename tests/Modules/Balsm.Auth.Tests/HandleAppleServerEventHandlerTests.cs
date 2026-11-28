using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.Auth.Application.Commands;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Auth.Infrastructure.Handlers;
using Balsm.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Balsm.Auth.Tests;

/// Apple server-to-server notifications: a revoked consent or deleted Apple
/// account signs the user out everywhere and drops the Apple refresh token;
/// Hide-My-Email toggles refresh the email snapshot. The Balsm account itself
/// is never touched.
public sealed class HandleAppleServerEventHandlerTests : IDisposable
{
    private const string Sub = "001234.apple-sub.0001";
    private static readonly DateTimeOffset EventTime = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly SqliteConnection _accountConnection;
    private readonly AuthDbContext _db;
    private readonly AccountDbContext _accountDb;

    public HandleAppleServerEventHandlerTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _accountConnection = new SqliteConnection("Data Source=:memory:");
        _accountConnection.Open();

        var services = new ServiceCollection();
        services.AddSingleton<Balsm.SharedKernel.Events.IDomainEventDispatcher, NullDispatcher>();
        services.AddDbContext<AuthDbContext>(o => o.UseSqlite(_connection));
        services.AddDbContext<AccountDbContext>(o => o.UseSqlite(_accountConnection));
        var sp = services.BuildServiceProvider();
        _db = sp.GetRequiredService<AuthDbContext>();
        _db.Database.EnsureCreated();
        _accountDb = sp.GetRequiredService<AccountDbContext>();
        _accountDb.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _accountDb.Dispose();
        _connection.Dispose();
        _accountConnection.Dispose();
    }

    [Fact]
    public async Task ConsentRevoked_ClearsAppleTokenAndRevokesAllSessions()
    {
        var userId = await SeedAppleUserAsync(sessions: 2);

        await Handle(Event("consent-revoked"));

        var identity = await _db.UserIdentities.AsNoTracking().SingleAsync();
        identity.AppleRefreshTokenCiphertext.Should().BeNull();
        (await _db.UserRefreshTokens.AsNoTracking().Where(t => t.UserId == userId).ToListAsync())
            .Should().HaveCount(2).And.OnlyContain(t => t.RevokedAt != null);
    }

    [Fact]
    public async Task AccountDelete_SameAsConsentRevoked_AccountRowUntouched()
    {
        var account = UserAccount.Create(countryCode: "EG", preferredLanguage: "en");
        _accountDb.UserAccounts.Add(account);
        await _accountDb.SaveChangesAsync();
        var userId = await SeedAppleUserAsync(sessions: 2, userId: account.Id);

        await Handle(Event("account-delete"));

        (await _db.UserIdentities.AsNoTracking().SingleAsync()).AppleRefreshTokenCiphertext.Should().BeNull();
        (await _db.UserRefreshTokens.AsNoTracking().Where(t => t.UserId == userId).ToListAsync())
            .Should().OnlyContain(t => t.RevokedAt != null);
        var stored = await _accountDb.UserAccounts.AsNoTracking().SingleAsync();
        stored.Id.Should().Be(account.Id);
        stored.DeletionState.Should().Be(account.DeletionState);
    }

    [Fact]
    public async Task EmailDisabled_UpdatesSnapshotOnly()
    {
        var userId = await SeedAppleUserAsync(sessions: 1);

        await Handle(Event("email-disabled", email: "new-relay@privaterelay.appleid.com", isPrivateEmail: true));

        var identity = await _db.UserIdentities.AsNoTracking().SingleAsync();
        identity.EmailNormalized.Should().Be("new-relay@privaterelay.appleid.com");
        identity.AppleRefreshTokenCiphertext.Should().NotBeNull();
        (await _db.UserRefreshTokens.AsNoTracking().Where(t => t.UserId == userId).ToListAsync())
            .Should().OnlyContain(t => t.RevokedAt == null);
    }

    [Fact]
    public async Task EmailEnabled_WithoutEmail_LeavesSnapshot()
    {
        await SeedAppleUserAsync(sessions: 1);

        await Handle(Event("email-enabled", email: null));

        (await _db.UserIdentities.AsNoTracking().SingleAsync()).EmailNormalized
            .Should().Be("relay@privaterelay.appleid.com");
    }

    [Fact]
    public async Task UnknownSubject_NoOp()
    {
        var userId = await SeedAppleUserAsync(sessions: 1);

        await Handle(Event("consent-revoked", sub: "someone-else"));

        (await _db.UserIdentities.AsNoTracking().SingleAsync()).AppleRefreshTokenCiphertext.Should().NotBeNull();
        (await _db.UserRefreshTokens.AsNoTracking().Where(t => t.UserId == userId).ToListAsync())
            .Should().OnlyContain(t => t.RevokedAt == null);
    }

    [Fact]
    public async Task UnknownType_NoOp()
    {
        var userId = await SeedAppleUserAsync(sessions: 1);

        await Handle(Event("something-new", email: "other@example.com"));

        var identity = await _db.UserIdentities.AsNoTracking().SingleAsync();
        identity.AppleRefreshTokenCiphertext.Should().NotBeNull();
        identity.EmailNormalized.Should().Be("relay@privaterelay.appleid.com");
        (await _db.UserRefreshTokens.AsNoTracking().Where(t => t.UserId == userId).ToListAsync())
            .Should().OnlyContain(t => t.RevokedAt == null);
    }

    [Fact]
    public async Task ConsentRevoked_GoogleIdentityWithSameSubject_Untouched()
    {
        var userId = Guid.NewGuid();
        _db.UserIdentities.Add(UserIdentity.Create(userId, "google", Sub, "me@gmail.com"));
        _db.UserRefreshTokens.Add(UserRefreshToken.Create(userId, "hash-g", Guid.NewGuid(), DateTime.UtcNow.AddDays(30)));
        await _db.SaveChangesAsync();

        await Handle(Event("consent-revoked"));

        (await _db.UserRefreshTokens.AsNoTracking().SingleAsync()).RevokedAt.Should().BeNull();
    }

    private async Task<Guid> SeedAppleUserAsync(int sessions, Guid? userId = null)
    {
        var id = userId ?? Guid.NewGuid();
        var identity = UserIdentity.Create(id, "apple", Sub, "relay@privaterelay.appleid.com");
        identity.SetAppleRefreshToken([1, 2, 3]);
        _db.UserIdentities.Add(identity);
        for (var i = 0; i < sessions; i++)
            _db.UserRefreshTokens.Add(UserRefreshToken.Create(id, $"hash-{i}", Guid.NewGuid(), DateTime.UtcNow.AddDays(30)));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return id;
    }

    private Task Handle(AppleServerEvent evt) =>
        new HandleAppleServerEventHandler(_db, NullLogger<HandleAppleServerEventHandler>.Instance)
            .Handle(new HandleAppleServerEventCommand(evt), CancellationToken.None);

    private static AppleServerEvent Event(
        string type, string sub = Sub, string? email = null, bool? isPrivateEmail = null) =>
        new(type, sub, EventTime, email, isPrivateEmail);

    private sealed class NullDispatcher : Balsm.SharedKernel.Events.IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(
            IEnumerable<Balsm.SharedKernel.Events.IDomainEvent> events, CancellationToken ct) => Task.CompletedTask;
    }
}
