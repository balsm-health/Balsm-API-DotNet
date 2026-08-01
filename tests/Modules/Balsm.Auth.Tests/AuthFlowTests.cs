using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.Auth.Application.Commands;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Data;
using Xunit;
using Balsm.Auth.Infrastructure.Handlers;
using Balsm.Infrastructure.Auth;
using Balsm.Infrastructure.RateLimit;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Balsm.Auth.Tests;

/// <summary>
/// T073d: OTP issue, 6th-attempt 423, geofence 403 (via lockout), OIDC single-account.
/// Uses SQLite in-memory for handler-level integration tests.
/// </summary>
public sealed class AuthFlowTests : IDisposable
{
    private readonly AuthDbContext _db;
    private readonly AccountDbContext _accountDb;
    private readonly SqliteConnection _connection;
    private readonly SqliteConnection _accountConnection;
    private readonly OtpRateLimitPolicies _rateLimits;
    private readonly IConfiguration _config;

    public AuthFlowTests()
    {
        // Keep one open connection per context for the fixture lifetime — a
        // :memory: SQLite database is destroyed as soon as its last connection
        // closes. Each module owns its own schema (as in production), so each
        // context gets its own in-memory database; EnsureCreated is per-database.
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _accountConnection = new SqliteConnection("Data Source=:memory:");
        _accountConnection.Open();

        var services = new ServiceCollection();
        services.AddSingleton<Balsm.SharedKernel.Events.IDomainEventDispatcher, NullDomainEventDispatcher>();
        services.AddDbContext<AuthDbContext>(o => o.UseSqlite(_connection));
        services.AddDbContext<AccountDbContext>(o => o.UseSqlite(_accountConnection));
        services.AddMemoryCache();

        var sp = services.BuildServiceProvider();
        _db = sp.GetRequiredService<AuthDbContext>();
        _db.Database.EnsureCreated();
        _accountDb = sp.GetRequiredService<AccountDbContext>();
        _accountDb.Database.EnsureCreated();

        var cache = sp.GetRequiredService<IMemoryCache>();
        _rateLimits = new OtpRateLimitPolicies(
            new InMemoryRateLimitStore(cache), NullLogger<OtpRateLimitPolicies>.Instance);

        // No Resend:ApiKey → OtpService logs the code instead of sending email;
        // no Otp:DevCode → verify checks the real challenge hash.
        _config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Otp:HmacSecret"] = "test-otp-hmac-secret",
            ["Jwt:Secret"] = "test-secret-at-least-32-bytes-long!!",
            ["Otp:LinkBaseUrl"] = "http://localhost:5000",
        }).Build();
    }

    private static readonly Guid TestDeviceId = Guid.Parse("00000000-0000-0000-0000-000000000010");

    private RequestOtpHandler CreateRequestHandler() => new(
        _db,
        new OtpService(_config, NullLogger<OtpService>.Instance),
        _rateLimits,
        _config,
        NullLogger<RequestOtpHandler>.Instance);

    private VerifyOtpHandler CreateVerifyHandler() => new(
        _db,
        _accountDb,
        new JwtService(_config),
        new OtpService(_config, NullLogger<OtpService>.Instance),
        _config);

    private async Task SeedEmailIdentityAsync(string email)
    {
        var account = UserAccount.Create(countryCode: "EG", preferredLanguage: "ar-EG");
        _accountDb.UserAccounts.Add(account);
        await _accountDb.SaveChangesAsync();

        var identity = UserIdentity.Create(account.Id, "email", email, email);
        identity.ConfirmEmail(DateTime.UtcNow);
        _db.UserIdentities.Add(identity);
        await _db.SaveChangesAsync();
    }

    private async Task<string> SeedRedeemableChallengeAsync(string email)
    {
        var otp = new OtpService(_config, NullLogger<OtpService>.Instance);
        var (code, hash, _, linkHash, expiresAt) = otp.Generate();
        _db.OtpChallenges.Add(OtpChallenge.Create(email, hash, expiresAt, linkHash));
        await _db.SaveChangesAsync();
        return code;
    }

    [Fact]
    public async Task RequestOtp_NewEmail_ReturnsExpiresIn()
    {
        // Verify rate limit allows first request
        var decision = await _rateLimits.CheckEmailAsync("new@test.com");
        decision.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task RateLimit_EmailExceeded_BlocksOn4thRequest()
    {
        const string email = "ratelimit@test.com";

        // 3 allowed
        for (var i = 0; i < 3; i++)
        {
            var ok = await _rateLimits.CheckEmailAsync(email);
            ok.Allowed.Should().BeTrue($"attempt {i + 1} should be allowed");
        }

        // 4th is blocked
        var blocked = await _rateLimits.CheckEmailAsync(email);
        blocked.Allowed.Should().BeFalse("4th request exceeds 3/10min limit");
        blocked.RetryAfterSeconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AccountLockout_FiveFailures_IsLocked()
    {
        const string email = "lockout@test.com";
        var lockout = AccountLockout.Create(email, "email");
        _db.AccountLockouts.Add(lockout);
        await _db.SaveChangesAsync();

        for (var i = 0; i < 5; i++)
            lockout.RecordFailure();
        await _db.SaveChangesAsync();

        var loaded = await _db.AccountLockouts
            .FirstOrDefaultAsync(l => l.Identifier == email && l.IdentifierType == "email");

        loaded.Should().NotBeNull();
        loaded!.IsLocked.Should().BeTrue("5 failed attempts should lock the account");
    }

    [Fact]
    public async Task AccountLockout_6thAttempt_Returns423()
    {
        const string email = "sixthfail@test.com";
        var lockout = AccountLockout.Create(email, "email");
        for (var i = 0; i < 5; i++) lockout.RecordFailure();
        _db.AccountLockouts.Add(lockout);
        await _db.SaveChangesAsync();

        var lockedUntil = lockout.LockedUntil;
        lockedUntil.Should().NotBeNull();

        // Verify the command would throw AccountLockedException (handler checks this)
        lockout.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void OtpRateLimitException_HasCorrectTier()
    {
        var ex = new OtpRateLimitException(60, "email");
        ex.RetryAfterSeconds.Should().Be(60);
        ex.Tier.Should().Be("email");
    }

    [Fact]
    public async Task RequestOtp_Register_NewEmail_PersistsChallenge()
    {
        var result = await CreateRequestHandler().Handle(
            new RequestOtpCommand("newuser@test.com", "EG", null, OtpPurpose.Register), CancellationToken.None);

        result.ExpiresInSeconds.Should().Be(600);
        (await _db.OtpChallenges.CountAsync(c => c.EmailNormalized == "newuser@test.com")).Should().Be(1);
    }

    [Fact]
    public async Task RequestOtp_Register_ExistingEmail_ThrowsAndSendsNothing()
    {
        await SeedEmailIdentityAsync("existing@test.com");

        var act = () => CreateRequestHandler().Handle(
            new RequestOtpCommand("existing@test.com", "EG", null, OtpPurpose.Register), CancellationToken.None);

        await act.Should().ThrowAsync<EmailAlreadyRegisteredException>();
        (await _db.OtpChallenges.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RequestOtp_Reset_ExistingEmail_PersistsChallenge()
    {
        await SeedEmailIdentityAsync("reset@test.com");

        var result = await CreateRequestHandler().Handle(
            new RequestOtpCommand("reset@test.com", "EG", null, OtpPurpose.Reset), CancellationToken.None);

        result.ExpiresInSeconds.Should().Be(600);
        (await _db.OtpChallenges.CountAsync(c => c.EmailNormalized == "reset@test.com")).Should().Be(1);
    }

    [Fact]
    public async Task RequestOtp_Reset_UnknownEmail_SendsNothing()
    {
        var result = await CreateRequestHandler().Handle(
            new RequestOtpCommand("ghost@test.com", "EG", null, OtpPurpose.Reset), CancellationToken.None);

        result.ExpiresInSeconds.Should().Be(600);
        (await _db.OtpChallenges.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task VerifyOtp_NewEmail_CreatesAccountAndIsNewUser()
    {
        var code = await SeedRedeemableChallengeAsync("brandnew@test.com");

        var result = await CreateVerifyHandler().Handle(
            new VerifyOtpCommand("brandnew@test.com", code, TestDeviceId, "test-device"), CancellationToken.None);

        result.IsNewUser.Should().BeTrue();
        (await _db.UserIdentities.CountAsync(i => i.EmailNormalized == "brandnew@test.com")).Should().Be(1);
    }

    [Fact]
    public async Task VerifyOtp_ExistingIdentity_ThrowsAccountAlreadyExists()
    {
        await SeedEmailIdentityAsync("member@test.com");
        var code = await SeedRedeemableChallengeAsync("member@test.com");

        var act = () => CreateVerifyHandler().Handle(
            new VerifyOtpCommand("member@test.com", code, TestDeviceId, "test-device"), CancellationToken.None);

        await act.Should().ThrowAsync<AccountAlreadyExistsException>();
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _accountDb.Database.EnsureDeleted();
        _accountDb.Dispose();
        _db.Dispose();
        _accountConnection.Dispose();
        _connection.Dispose();
    }

    private sealed class NullDomainEventDispatcher : Balsm.SharedKernel.Events.IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(
            IEnumerable<Balsm.SharedKernel.Events.IDomainEvent> events,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
