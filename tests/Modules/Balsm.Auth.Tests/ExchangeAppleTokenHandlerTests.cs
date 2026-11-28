using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.Auth.Application.Commands;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Auth.Infrastructure.Handlers;
using Balsm.Geofence.Domain;
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

/// Apple sign-in: nonce replay protection and capture of the Apple refresh
/// token (encrypted) so account deletion can revoke it later.
public sealed class ExchangeAppleTokenHandlerTests : IDisposable
{
    private const string RawNonce = "n-1";
    private static readonly Guid TestDeviceId = Guid.Parse("00000000-0000-0000-0000-000000000011");

    private readonly AuthDbContext _db;
    private readonly AccountDbContext _accountDb;
    private readonly SqliteConnection _connection;
    private readonly SqliteConnection _accountConnection;
    private readonly IConfiguration _config;
    private readonly AppleRefreshTokenCipher _cipher;

    public ExchangeAppleTokenHandlerTests()
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

        _config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "test-secret-at-least-32-bytes-long!!",
            ["Apple:ClientId"] = "health.balsm.app",
            ["DobEncryption:Key"] = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()),
        }).Build();
        _cipher = new AppleRefreshTokenCipher(_config);
    }

    public void Dispose()
    {
        _db.Dispose();
        _accountDb.Dispose();
        _connection.Dispose();
        _accountConnection.Dispose();
    }

    // ── Refresh token capture ─────────────────────────────────────────────

    [Fact]
    public async Task Handle_NewUserWithCode_StoresEncryptedRefreshToken()
    {
        var client = new FakeAppleTokenClient(configured: true, result: "r-1");

        await Handler(Payload("a-sub-1"), client).Handle(Command(code: "code-1"), CancellationToken.None);

        var stored = (await _db.UserIdentities.AsNoTracking().SingleAsync()).AppleRefreshTokenCiphertext;
        stored.Should().NotBeNull();
        stored.Should().NotEqual(Encoding.UTF8.GetBytes("r-1"));
        _cipher.Decrypt(stored!).Should().Be("r-1");
        client.ExchangedCodes.Should().Equal("code-1");
    }

    [Fact]
    public async Task Handle_RepeatSignInWithNewCode_ReplacesStoredToken()
    {
        await Handler(Payload("a-sub-1"), new FakeAppleTokenClient(true, "r-1"))
            .Handle(Command(code: "code-1"), CancellationToken.None);

        await Handler(Payload("a-sub-1"), new FakeAppleTokenClient(true, "r-2"))
            .Handle(Command(code: "code-2"), CancellationToken.None);

        var stored = (await _db.UserIdentities.AsNoTracking().SingleAsync()).AppleRefreshTokenCiphertext;
        _cipher.Decrypt(stored!).Should().Be("r-2");
    }

    [Fact]
    public async Task Handle_ExchangeReturnsNull_StillReturnsTokensAndStoresNothing()
    {
        var result = await Handler(Payload("a-sub-1"), new FakeAppleTokenClient(true, null))
            .Handle(Command(code: "code-1"), CancellationToken.None);

        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        (await _db.UserIdentities.AsNoTracking().SingleAsync()).AppleRefreshTokenCiphertext.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ExchangeThrows_StillReturnsTokensAndStoresNothing()
    {
        var result = await Handler(Payload("a-sub-1"), new FakeAppleTokenClient(true, null, throws: true))
            .Handle(Command(code: "code-1"), CancellationToken.None);

        result.AccessToken.Should().NotBeNullOrEmpty();
        (await _db.UserIdentities.AsNoTracking().SingleAsync()).AppleRefreshTokenCiphertext.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ClientNotConfigured_DoesNotCallExchange()
    {
        var client = new FakeAppleTokenClient(configured: false, result: "r-1");

        await Handler(Payload("a-sub-1"), client).Handle(Command(code: "code-1"), CancellationToken.None);

        client.ExchangedCodes.Should().BeEmpty();
        (await _db.UserIdentities.AsNoTracking().SingleAsync()).AppleRefreshTokenCiphertext.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NoAuthorizationCode_DoesNotCallExchange()
    {
        var client = new FakeAppleTokenClient(configured: true, result: "r-1");

        await Handler(Payload("a-sub-1"), client).Handle(Command(code: "  "), CancellationToken.None);

        client.ExchangedCodes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RepeatSignInWithoutEmail_ResolvesExistingIdentityBySubject()
    {
        var first = await Handler(Payload("a-sub-1", "me@icloud.com"), new FakeAppleTokenClient(false, null))
            .Handle(Command(), CancellationToken.None);

        var second = await Handler(Payload("a-sub-1", email: null), new FakeAppleTokenClient(false, null))
            .Handle(Command(), CancellationToken.None);

        second.IsNewUser.Should().BeFalse();
        second.UserId.Should().Be(first.UserId);
    }

    [Fact]
    public async Task Handle_NoStoredEmail_IssuesEmptyEmailClaim()
    {
        var result = await Handler(Payload("a-sub-1", email: null), new FakeAppleTokenClient(false, null))
            .Handle(Command(), CancellationToken.None);

        var claim = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken)
            .Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email);
        claim.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_SignIn_MarksAppleConsent()
    {
        var before = DateTime.UtcNow;
        await Handler(Payload("a-sub-1"), new FakeAppleTokenClient(false, null)).Handle(Command(), CancellationToken.None);
        var first = (await _db.UserIdentities.AsNoTracking().SingleAsync()).AppleConsentAt;

        await Handler(Payload("a-sub-1"), new FakeAppleTokenClient(false, null)).Handle(Command(), CancellationToken.None);
        var second = (await _db.UserIdentities.AsNoTracking().SingleAsync()).AppleConsentAt;

        first.Should().NotBeNull().And.BeOnOrAfter(before);
        second.Should().NotBeNull().And.BeOnOrAfter(first!.Value);
    }

    // ── Nonce ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NonceMissing_Throws401()
    {
        var act = () => Handler(Payload("a-sub-1"), new FakeAppleTokenClient(false, null))
            .Handle(Command(nonce: null), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await _db.UserIdentities.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_NonceHashMismatch_Throws401()
    {
        var act = () => Handler(Payload("a-sub-1"), new FakeAppleTokenClient(false, null))
            .Handle(Command(nonce: "n-2"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_TokenWithoutNonceClaim_Throws401()
    {
        var act = () => Handler(Payload("a-sub-1") with { Nonce = null }, new FakeAppleTokenClient(false, null))
            .Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_NonceMatches_Succeeds()
    {
        var result = await Handler(Payload("a-sub-1"), new FakeAppleTokenClient(false, null))
            .Handle(Command(nonce: RawNonce), CancellationToken.None);

        result.IsNewUser.Should().BeTrue();
    }

    // ── Migration (SQLite set) ────────────────────────────────────────────

    [Fact]
    public async Task Migration_UpAddsColumn_DownDropsIt()
    {
        await using var conn = new SqliteConnection("Data Source=:memory:");
        await conn.OpenAsync();
        await using var db = new AuthDbContext(
            new DbContextOptionsBuilder<AuthDbContext>()
                .UseSqlite(conn, o => o.MigrationsAssembly("Balsm.Auth.Infrastructure.Migrations.Sqlite"))
                .Options,
            new NullDispatcher());

        await db.Database.MigrateAsync("AddAppleRefreshTokenToUserIdentity");
        var afterUp = await ColumnsAsync(conn);
        await db.Database.MigrateAsync("KeyEmailIdentitiesOnStableId");
        var afterDown = await ColumnsAsync(conn);

        afterUp.Should().Contain("apple_refresh_token_ciphertext");
        afterDown.Should().NotContain("apple_refresh_token_ciphertext");
    }

    [Fact]
    public async Task Migration_AppleConsentAt_UpAddsColumn_DownDropsIt()
    {
        await using var conn = new SqliteConnection("Data Source=:memory:");
        await conn.OpenAsync();
        await using var db = new AuthDbContext(
            new DbContextOptionsBuilder<AuthDbContext>()
                .UseSqlite(conn, o => o.MigrationsAssembly("Balsm.Auth.Infrastructure.Migrations.Sqlite"))
                .Options,
            new NullDispatcher());

        await db.Database.MigrateAsync("AddAppleConsentAtToUserIdentity");
        var afterUp = await ColumnsAsync(conn);
        await db.Database.MigrateAsync("AddAppleRefreshTokenToUserIdentity");
        var afterDown = await ColumnsAsync(conn);

        afterUp.Should().Contain("apple_consent_at");
        afterDown.Should().NotContain("apple_consent_at");
    }

    private static async Task<List<string>> ColumnsAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM pragma_table_info('user_identities')";
        await using var reader = await cmd.ExecuteReaderAsync();
        var names = new List<string>();
        while (await reader.ReadAsync()) names.Add(reader.GetString(0));
        return names;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static AppleTokenPayload Payload(string sub, string? email = "me@icloud.com") =>
        new(sub, email, email is not null, false, Sha256Hex(RawNonce));

    private static ExchangeAppleTokenCommand Command(string? code = null, string? nonce = RawNonce) =>
        new("id-token", TestDeviceId, "test", "EG", code, nonce);

    private ExchangeAppleTokenHandler Handler(AppleTokenPayload payload, AppleTokenClient client) => new(
        _db, new AccountProvisioner(_accountDb), new FakeAppleValidator(_config, payload), client, _cipher,
        new AllowAllGeofence(), new JwtService(_config), NullLogger<ExchangeAppleTokenHandler>.Instance);

    private sealed class FakeAppleTokenClient(bool configured, string? result, bool throws = false)
        : AppleTokenClient(null!, new ConfigurationBuilder().Build(), NullLogger<AppleTokenClient>.Instance)
    {
        public List<string> ExchangedCodes { get; } = [];

        public override bool IsConfigured => configured;

        public override Task<string?> ExchangeCodeAsync(string authorizationCode, CancellationToken ct)
        {
            ExchangedCodes.Add(authorizationCode);
            if (throws) throw new HttpRequestException("apple down");
            return Task.FromResult(result);
        }
    }

    private sealed class FakeAppleValidator(IConfiguration config, AppleTokenPayload payload)
        : AppleOidcValidator(config, null!, null!, NullLogger<AppleOidcValidator>.Instance)
    {
        public override Task<AppleTokenPayload?> ValidateAsync(string idToken, CancellationToken ct) =>
            Task.FromResult<AppleTokenPayload?>(payload);
    }

    private sealed class NullDispatcher : Balsm.SharedKernel.Events.IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(
            IEnumerable<Balsm.SharedKernel.Events.IDomainEvent> events, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class AllowAllGeofence : IGeofenceService
    {
        public Task<bool> IsDeniedAsync(string countryCode, CancellationToken ct = default) => Task.FromResult(false);
    }
}

file sealed class AccountProvisioner(AccountDbContext db) : Balsm.SharedKernel.Contracts.IUserAccountProvisioner
{
    public async Task<Guid> ProvisionAsync(string countryCode, string preferredLanguage, CancellationToken ct = default)
    {
        var account = UserAccount.Create(countryCode: countryCode, preferredLanguage: preferredLanguage);
        db.UserAccounts.Add(account);
        await db.SaveChangesAsync(ct);
        return account.Id;
    }

    public Task<Guid> ProvisionWithPreferredIdAsync(
        string countryCode, string preferredLanguage, Guid preferredId, CancellationToken ct = default) =>
        throw new NotSupportedException();
}
