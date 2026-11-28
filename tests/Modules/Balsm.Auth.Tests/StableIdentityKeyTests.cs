using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.Auth.Application.Commands;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Auth.Infrastructure.Handlers;
using Balsm.Geofence.Domain;
using Balsm.Infrastructure.Auth;
using Balsm.Infrastructure.RateLimit;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Balsm.Auth.Tests;

/// Every identity is keyed on a stable sign-in id — the provider `sub` for
/// Google/Apple, the row's own id for email — never on the address, which is
/// changeable data. Gmail spellings of one mailbox resolve to one identity, and
/// no account is ever merged or linked across providers by email.
public sealed class StableIdentityKeyTests : IDisposable
{
    private static readonly Guid TestDeviceId = Guid.Parse("00000000-0000-0000-0000-000000000010");

    private readonly AuthDbContext _db;
    private readonly AccountDbContext _accountDb;
    private readonly SqliteConnection _connection;
    private readonly SqliteConnection _accountConnection;
    private readonly OtpRateLimitPolicies _rateLimits;
    private readonly IConfiguration _config;

    public StableIdentityKeyTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _accountConnection = new SqliteConnection("Data Source=:memory:");
        _accountConnection.Open();

        var services = new ServiceCollection();
        services.AddSingleton<Balsm.SharedKernel.Events.IDomainEventDispatcher, NullDispatcher>();
        services.AddDbContext<AuthDbContext>(o => o.UseSqlite(_connection));
        services.AddDbContext<AccountDbContext>(o => o.UseSqlite(_accountConnection));
        services.AddMemoryCache();

        var sp = services.BuildServiceProvider();
        _db = sp.GetRequiredService<AuthDbContext>();
        _db.Database.EnsureCreated();
        _accountDb = sp.GetRequiredService<AccountDbContext>();
        _accountDb.Database.EnsureCreated();

        _rateLimits = new OtpRateLimitPolicies(
            new InMemoryRateLimitStore(sp.GetRequiredService<IMemoryCache>()),
            NullLogger<OtpRateLimitPolicies>.Instance);

        _config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Otp:HmacSecret"] = "test-otp-hmac-secret",
            ["Jwt:Secret"] = "test-secret-at-least-32-bytes-long!!",
            ["Otp:LinkBaseUrl"] = "http://localhost:5000",
            ["Google:ClientId"] = "test-google-client",
            ["Apple:ClientId"] = "health.balsm.app",
        }).Build();
    }

    public void Dispose()
    {
        _db.Dispose();
        _accountDb.Dispose();
        _connection.Dispose();
        _accountConnection.Dispose();
    }

    // ── Email identities ──────────────────────────────────────────────────

    [Fact]
    public async Task PasswordSignIn_WithDottedGmailVariant_FindsSameIdentity()
    {
        var userId = await SeedEmailIdentityAsync("hossameldinmi@gmail.com", password: "correct horse");
        var handler = new PasswordSignInHandler(_db, new PasswordHasher(), new JwtService(_config));

        var result = await handler.Handle(
            new PasswordSignInCommand("hossamEldin.M.I@gmail.com", "correct horse", TestDeviceId, "test"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task PasswordSignIn_WrongPasswordsAcrossGmailVariants_ShareOneLock()
    {
        await SeedEmailIdentityAsync("hossameldinmi@gmail.com", password: "correct horse");
        var handler = new PasswordSignInHandler(_db, new PasswordHasher(), new JwtService(_config));
        for (var i = 0; i < 5; i++)
        {
            await handler.Handle(
                new PasswordSignInCommand($"hossam.eldin.mi+try{i}@gmail.com", "wrong", TestDeviceId, "test"),
                CancellationToken.None);
        }

        var right = () => handler.Handle(
            new PasswordSignInCommand("hossameldinmi@gmail.com", "correct horse", TestDeviceId, "test"),
            CancellationToken.None);

        await right.Should().ThrowAsync<AccountLockedException>();
    }

    [Fact]
    public async Task RequestOtp_SignUpWithDottedVariantOfExisting_ReportsExists()
    {
        var userId = await SeedEmailIdentityAsync("hossameldinmi@gmail.com");
        const string variant = "Hossam.Eldin.MI+balsm@googlemail.com";

        // Reset sends nothing for an address it does not know; a challenge
        // means the variant was recognised as the existing account.
        await RequestHandler().Handle(
            new RequestOtpCommand(variant, "EG", null, OtpPurpose.Reset, "127.0.0.1", "en"), CancellationToken.None);
        var recognised = await _db.OtpChallenges.AnyAsync(c => c.EmailNormalized == variant.ToLowerInvariant());
        var code = await SeedChallengeAsync(variant);
        var signIn = await VerifyHandler().Handle(
            new VerifyOtpCommand(variant, code, TestDeviceId, "test"), CancellationToken.None);

        recognised.Should().BeTrue();
        signIn.IsNewUser.Should().BeFalse();
        signIn.UserId.Should().Be(userId);
        (await _db.UserIdentities.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task VerifyOtp_NewEmail_KeysIdentityOnItsOwnId()
    {
        var code = await SeedChallengeAsync("First.Last@Balsm.Health");

        await VerifyHandler().Handle(
            new VerifyOtpCommand("First.Last@Balsm.Health", code, TestDeviceId, "test"), CancellationToken.None);

        var identity = await _db.UserIdentities.SingleAsync();
        identity.ProviderSubject.Should().Be(identity.Id.ToString());
        identity.EmailNormalized.Should().Be("first.last@balsm.health");
        identity.EmailCanonical.Should().Be("first.last@balsm.health");
    }

    [Fact]
    public async Task CreateEmail_SecondIdentityForSameGmailMailbox_RejectedByDatabase()
    {
        await SeedEmailIdentityAsync("hossameldinmi@gmail.com");
        _db.UserIdentities.Add(UserIdentity.CreateEmail(Guid.NewGuid(), "Hossam.Eldin.MI@gmail.com"));

        var act = () => _db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public void CreateEmail_KeepsAddressAsTyped_AndSubjectIsRowId()
    {
        var identity = UserIdentity.CreateEmail(Guid.NewGuid(), " Hossam.Eldin.MI+x@Gmail.com ");

        identity.Provider.Should().Be("email");
        identity.ProviderSubject.Should().Be(identity.Id.ToString());
        identity.EmailNormalized.Should().Be("hossam.eldin.mi+x@gmail.com");
        identity.EmailCanonical.Should().Be("hossameldinmi@gmail.com");
    }

    [Fact]
    public void ChangeEmail_KeepsProviderSubjectAndUserId_ClearsConfirmation()
    {
        var userId = Guid.NewGuid();
        var identity = UserIdentity.CreateEmail(userId, "old@balsm.health");
        identity.ConfirmEmail(DateTime.UtcNow);
        var subject = identity.ProviderSubject;

        identity.ChangeEmail("New.Address@Gmail.com");

        identity.ProviderSubject.Should().Be(subject);
        identity.UserId.Should().Be(userId);
        identity.EmailNormalized.Should().Be("new.address@gmail.com");
        identity.EmailCanonical.Should().Be("newaddress@gmail.com");
        identity.EmailConfirmedAt.Should().BeNull();
    }

    [Fact]
    public void RefreshProviderEmail_Blank_KeepsStoredEmail()
    {
        var identity = UserIdentity.Create(Guid.NewGuid(), "apple", "apple-sub", "kept@icloud.com");

        identity.RefreshProviderEmail("  ");
        identity.RefreshProviderEmail(null);

        identity.EmailNormalized.Should().Be("kept@icloud.com");
        identity.EmailCanonical.Should().Be("kept@icloud.com");
    }

    [Fact]
    public void RefreshProviderEmail_OnEmailIdentity_Throws()
    {
        var identity = UserIdentity.CreateEmail(Guid.NewGuid(), "a@balsm.health");

        var act = () => identity.RefreshProviderEmail("b@balsm.health");

        act.Should().Throw<InvalidOperationException>();
    }

    // ── Social identities ─────────────────────────────────────────────────

    [Fact]
    public async Task GoogleSignIn_EmailChangedAtGoogle_SameUserAndSnapshotUpdated()
    {
        var first = await GoogleHandler(new GoogleTokenPayload("g-sub-1", "old@gmail.com", true))
            .Handle(GoogleCommand(), CancellationToken.None);

        var second = await GoogleHandler(new GoogleTokenPayload("g-sub-1", "New.Name@gmail.com", true))
            .Handle(GoogleCommand(), CancellationToken.None);

        second.IsNewUser.Should().BeFalse();
        second.UserId.Should().Be(first.UserId);
        var identity = await _db.UserIdentities.AsNoTracking().SingleAsync();
        identity.EmailNormalized.Should().Be("new.name@gmail.com");
        identity.EmailCanonical.Should().Be("newname@gmail.com");
    }

    [Fact]
    public async Task GoogleSignIn_TokenWithoutEmail_Succeeds()
    {
        var result = await GoogleHandler(new GoogleTokenPayload("g-sub-2", null, false))
            .Handle(GoogleCommand(), CancellationToken.None);

        result.IsNewUser.Should().BeTrue();
        var identity = await _db.UserIdentities.AsNoTracking().SingleAsync();
        identity.ProviderSubject.Should().Be("g-sub-2");
        identity.EmailNormalized.Should().BeNull();
    }

    [Fact]
    public async Task AppleRepeatSignInWithoutEmail_KeepsStoredEmail()
    {
        var first = await AppleHandler(new AppleTokenPayload("a-sub-1", "me@icloud.com", true, false))
            .Handle(AppleCommand(), CancellationToken.None);

        var second = await AppleHandler(new AppleTokenPayload("a-sub-1", null, false, false))
            .Handle(AppleCommand(), CancellationToken.None);

        second.IsNewUser.Should().BeFalse();
        second.UserId.Should().Be(first.UserId);
        var identity = await _db.UserIdentities.AsNoTracking().SingleAsync();
        identity.EmailNormalized.Should().Be("me@icloud.com");
    }

    [Fact]
    public async Task AppleSignIn_TokenWithoutEmail_StoresNoSyntheticAddress()
    {
        await AppleHandler(new AppleTokenPayload("a-sub-2", null, false, false))
            .Handle(AppleCommand(), CancellationToken.None);

        var identity = await _db.UserIdentities.AsNoTracking().SingleAsync();
        identity.EmailNormalized.Should().BeNull();
        identity.EmailCanonical.Should().BeNull();
    }

    [Fact]
    public async Task SocialSignIn_EmailMatchesExistingEmailAccount_CreatesSeparateAccount()
    {
        var emailUserId = await SeedEmailIdentityAsync("hossameldinmi@gmail.com");

        var google = await GoogleHandler(new GoogleTokenPayload("g-sub-3", "hossameldinmi@gmail.com", true))
            .Handle(GoogleCommand(), CancellationToken.None);
        var apple = await AppleHandler(new AppleTokenPayload("a-sub-3", "hossam.eldin.mi@gmail.com", true, false))
            .Handle(AppleCommand(), CancellationToken.None);

        google.IsNewUser.Should().BeTrue();
        apple.IsNewUser.Should().BeTrue();
        new[] { emailUserId, google.UserId, apple.UserId }.Should().OnlyHaveUniqueItems();
        (await _db.UserIdentities.CountAsync()).Should().Be(3);
    }

    // ── Migration (SQLite set) ────────────────────────────────────────────

    private const string PreviousMigration = "AddAttemptsToOtpChallenge";
    private const string KeyMigration = "KeyEmailIdentitiesOnStableId";

    [Fact]
    public async Task Migration_Up_KeysEmailRowsOnIdAndFillsCanonical()
    {
        await using var conn = new SqliteConnection("Data Source=:memory:");
        await conn.OpenAsync();
        await using var db = MigrationContext(conn);
        await db.Database.MigrateAsync(PreviousMigration);
        var a = await InsertLegacyEmailRowAsync(conn, "hossam.eldin.mi@gmail.com");
        var b = await InsertLegacyEmailRowAsync(conn, "first.last@balsm.health");

        await db.Database.MigrateAsync(KeyMigration);

        var rows = await ReadRowsAsync(conn);
        rows.Should().BeEquivalentTo(new[]
        {
            (Id: a.ToString(), Subject: a.ToString(), Canonical: (string?)"hossameldinmi@gmail.com"),
            (Id: b.ToString(), Subject: b.ToString(), Canonical: (string?)"first.last@balsm.health"),
        });
    }

    [Fact]
    public async Task Migration_Up_CollidingCanonicalRows_AbortsNamingBothIds()
    {
        await using var conn = new SqliteConnection("Data Source=:memory:");
        await conn.OpenAsync();
        await using var db = MigrationContext(conn);
        await db.Database.MigrateAsync(PreviousMigration);
        var a = await InsertLegacyEmailRowAsync(conn, "hossam.eldin.mi@gmail.com");
        var b = await InsertLegacyEmailRowAsync(conn, "hossameldinmi+balsm@googlemail.com");

        var act = () => db.Database.MigrateAsync(KeyMigration);

        var thrown = await act.Should().ThrowAsync<SqliteException>();
        thrown.Which.Message.Should().ContainAll(a.ToString().ToUpperInvariant(), b.ToString().ToUpperInvariant());
    }

    [Fact]
    public async Task Migration_Down_RestoresEmailAsSubject()
    {
        await using var conn = new SqliteConnection("Data Source=:memory:");
        await conn.OpenAsync();
        await using var db = MigrationContext(conn);
        await db.Database.MigrateAsync(PreviousMigration);
        var a = await InsertLegacyEmailRowAsync(conn, "hossam.eldin.mi@gmail.com");
        await db.Database.MigrateAsync(KeyMigration);

        await db.Database.MigrateAsync(PreviousMigration);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT provider_subject FROM user_identities WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", a.ToString().ToUpperInvariant());
        (await cmd.ExecuteScalarAsync()).Should().Be("hossam.eldin.mi@gmail.com");
    }

    private static AuthDbContext MigrationContext(SqliteConnection conn) => new(
        new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlite(conn, o => o.MigrationsAssembly("Balsm.Auth.Infrastructure.Migrations.Sqlite"))
            .Options,
        new NullDispatcher());

    private static async Task<Guid> InsertLegacyEmailRowAsync(SqliteConnection conn, string email)
    {
        var id = Guid.NewGuid();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO user_identities (id, user_id, provider, provider_subject, email_normalized, created_at)
            VALUES ($id, $user, 'email', $email, $email, '2026-01-01 00:00:00')
            """;
        cmd.Parameters.AddWithValue("$id", id.ToString().ToUpperInvariant());
        cmd.Parameters.AddWithValue("$user", Guid.NewGuid().ToString().ToUpperInvariant());
        cmd.Parameters.AddWithValue("$email", email);
        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    private static async Task<List<(string Id, string Subject, string? Canonical)>> ReadRowsAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, provider_subject, email_canonical FROM user_identities";
        await using var reader = await cmd.ExecuteReaderAsync();
        var rows = new List<(string, string, string?)>();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetString(0).ToLowerInvariant(), reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }
        return rows;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private RequestOtpHandler RequestHandler() => new(
        _db, new OtpService(_config, NullLogger<OtpService>.Instance), _rateLimits, _config,
        NullLogger<RequestOtpHandler>.Instance);

    private VerifyOtpHandler VerifyHandler() => new(
        _db, new AccountProvisioner(_accountDb), new JwtService(_config),
        new OtpService(_config, NullLogger<OtpService>.Instance),
        new DevOtpCodePolicy(_config,
            new Microsoft.Extensions.Hosting.Internal.HostingEnvironment { EnvironmentName = "Development" },
            NullLogger<DevOtpCodePolicy>.Instance));

    private ExchangeGoogleTokenHandler GoogleHandler(GoogleTokenPayload payload) => new(
        _db, new AccountProvisioner(_accountDb), new FakeGoogleValidator(_config, payload),
        new AllowAllGeofence(), new JwtService(_config));

    private ExchangeAppleTokenHandler AppleHandler(AppleTokenPayload payload) => new(
        _db, new AccountProvisioner(_accountDb), new FakeAppleValidator(_config, payload),
        new AllowAllGeofence(), new JwtService(_config));

    private static ExchangeGoogleTokenCommand GoogleCommand() => new("id-token", TestDeviceId, "test", "EG");

    private static ExchangeAppleTokenCommand AppleCommand() => new("id-token", TestDeviceId, "test", "EG");

    private async Task<Guid> SeedEmailIdentityAsync(string email, string? password = null)
    {
        var account = UserAccount.Create(countryCode: "EG", preferredLanguage: "ar-EG");
        _accountDb.UserAccounts.Add(account);
        await _accountDb.SaveChangesAsync();

        var identity = UserIdentity.CreateEmail(account.Id, email);
        identity.ConfirmEmail(DateTime.UtcNow);
        if (password is not null) identity.SetPasswordHash(new PasswordHasher().Hash(password));
        _db.UserIdentities.Add(identity);
        await _db.SaveChangesAsync();
        return account.Id;
    }

    private async Task<string> SeedChallengeAsync(string email)
    {
        var otp = new OtpService(_config, NullLogger<OtpService>.Instance);
        var (code, hash, _, linkHash, expiresAt) = otp.Generate();
        _db.OtpChallenges.Add(OtpChallenge.Create(email.Trim().ToLowerInvariant(), hash, expiresAt, linkHash));
        await _db.SaveChangesAsync();
        return code;
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

    private sealed class FakeGoogleValidator(IConfiguration config, GoogleTokenPayload payload)
        : GoogleOidcValidator(config, NullLogger<GoogleOidcValidator>.Instance)
    {
        public override Task<GoogleTokenPayload?> ValidateAsync(string idToken, CancellationToken ct) =>
            Task.FromResult<GoogleTokenPayload?>(payload);
    }

    private sealed class FakeAppleValidator(IConfiguration config, AppleTokenPayload payload)
        : AppleOidcValidator(config, null!, null!, NullLogger<AppleOidcValidator>.Instance)
    {
        public override Task<AppleTokenPayload?> ValidateAsync(string idToken, CancellationToken ct) =>
            Task.FromResult<AppleTokenPayload?>(payload);
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
        new Balsm.Account.Infrastructure.Services.UserAccountProvisioner(db)
            .ProvisionWithPreferredIdAsync(countryCode, preferredLanguage, preferredId, ct);
}
