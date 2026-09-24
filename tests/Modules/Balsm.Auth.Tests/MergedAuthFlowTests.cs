using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.Auth.Application.Commands;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Auth.Infrastructure.Handlers;
using Balsm.Infrastructure.Auth;
using Balsm.Infrastructure.RateLimit;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Balsm.Auth.Tests;

/// <summary>
/// One entry for everyone: email + password, falling back to an emailed code.
///
/// The client cannot be told whether an account exists — it is what the flow
/// hides — so neither endpoint may answer that question, and verify has to
/// sign an existing user in rather than refuse them.
/// </summary>
public sealed class MergedAuthFlowTests : IDisposable
{
    private readonly AuthDbContext _db;
    private readonly AccountDbContext _accountDb;
    private readonly SqliteConnection _connection;
    private readonly SqliteConnection _accountConnection;
    private readonly OtpRateLimitPolicies _rateLimits;
    private readonly IConfiguration _config;

    public MergedAuthFlowTests()
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

        _config = Config();
    }

    private static IConfiguration Config(string? devCode = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Otp:HmacSecret"] = "test-otp-hmac-secret",
            ["Jwt:Secret"] = "test-secret-at-least-32-bytes-long!!",
            ["Otp:LinkBaseUrl"] = "http://localhost:5000",
            ["Otp:DevCode"] = devCode,
        }).Build();

    private static readonly Guid TestDeviceId = Guid.Parse("00000000-0000-0000-0000-000000000010");

    private RequestOtpHandler RequestHandler() => new(
        _db, new OtpService(_config, NullLogger<OtpService>.Instance), _rateLimits, _config,
        NullLogger<RequestOtpHandler>.Instance);

    private VerifyOtpHandler VerifyHandler(IConfiguration? config = null, string environment = "Development") => new(
        _db, new AccountProvisioner(_accountDb), new JwtService(config ?? _config),
        new OtpService(config ?? _config, NullLogger<OtpService>.Instance), config ?? _config,
        new FakeEnvironment(environment));

    private async Task<Guid> SeedIdentityAsync(string email, string? password = null)
    {
        var account = UserAccount.Create(countryCode: "EG", preferredLanguage: "ar-EG");
        _accountDb.UserAccounts.Add(account);
        await _accountDb.SaveChangesAsync();

        var identity = UserIdentity.Create(account.Id, "email", email, email);
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
        _db.OtpChallenges.Add(OtpChallenge.Create(email, hash, expiresAt, linkHash));
        await _db.SaveChangesAsync();
        return code;
    }

    private async Task<UserRefreshToken> SeedRefreshTokenAsync(Guid userId)
    {
        var token = UserRefreshToken.Create(userId, "hash-of-an-older-session", TestDeviceId, DateTime.UtcNow.AddDays(30));
        _db.UserRefreshTokens.Add(token);
        await _db.SaveChangesAsync();
        return token;
    }

    // ── The code request answers the same way either way ──────────────────

    [Fact]
    public async Task RequestOtp_Continue_IssuesAChallenge_ForAnUnknownEmail()
    {
        await RequestHandler().Handle(
            new RequestOtpCommand("stranger@test.com", "EG", null, OtpPurpose.Continue, "1.1.1.1"),
            CancellationToken.None);

        (await _db.OtpChallenges.CountAsync(c => c.EmailNormalized == "stranger@test.com"))
            .Should().Be(1);
    }

    [Fact]
    public async Task RequestOtp_Continue_IssuesAChallenge_ForAKnownEmail()
    {
        // The old Register purpose threw EmailAlreadyRegistered here, which told
        // any anonymous caller that this address has a Balsm account.
        await SeedIdentityAsync("known@test.com");

        await RequestHandler().Handle(
            new RequestOtpCommand("known@test.com", "EG", null, OtpPurpose.Continue, "1.1.1.1"),
            CancellationToken.None);

        (await _db.OtpChallenges.CountAsync(c => c.EmailNormalized == "known@test.com"))
            .Should().Be(1);
    }

    // ── Verify signs in or creates, and says which ────────────────────────

    [Fact]
    public async Task VerifyOtp_ExistingIdentity_SignsThemIn()
    {
        var userId = await SeedIdentityAsync("returning@test.com", password: "correct-horse");
        var code = await SeedChallengeAsync("returning@test.com");

        var result = await VerifyHandler().Handle(
            new VerifyOtpCommand("returning@test.com", code, TestDeviceId, "iPhone"),
            CancellationToken.None);

        result.IsNewUser.Should().BeFalse();
        result.UserId.Should().Be(userId, "the existing account is signed in, not duplicated");
        (await _db.UserIdentities.CountAsync(i => i.EmailNormalized == "returning@test.com")).Should().Be(1);
    }

    [Fact]
    public async Task VerifyOtp_ExistingIdentity_LeavesThePasswordAlone()
    {
        // Verifying a code proves the mailbox, not an intent to change
        // credentials. A typo at the password field must not cost someone the
        // password they actually have.
        await SeedIdentityAsync("returning@test.com", password: "correct-horse");
        var before = (await _db.UserIdentities.SingleAsync()).PasswordHash;
        var code = await SeedChallengeAsync("returning@test.com");

        await VerifyHandler().Handle(
            new VerifyOtpCommand("returning@test.com", code, TestDeviceId, "iPhone"),
            CancellationToken.None);

        (await _db.UserIdentities.SingleAsync()).PasswordHash.Should().Be(before);
    }

    [Fact]
    public async Task VerifyOtp_UnknownEmail_CreatesTheAccount()
    {
        var code = await SeedChallengeAsync("newcomer@test.com");

        var result = await VerifyHandler().Handle(
            new VerifyOtpCommand("newcomer@test.com", code, TestDeviceId, "iPhone"),
            CancellationToken.None);

        result.IsNewUser.Should().BeTrue();
        (await _db.UserIdentities.CountAsync(i => i.EmailNormalized == "newcomer@test.com")).Should().Be(1);
    }

    // ── The dev bypass is a development bypass ────────────────────────────

    [Fact]
    public async Task VerifyOtp_DevCode_IsRefusedInProduction()
    {
        // A fixed always-valid code creates accounts and issues tokens for any
        // address. Configuration discipline is not a control; refuse it here.
        var config = Config(devCode: "000000");
        await SeedChallengeAsync("victim@test.com");

        var verify = () => VerifyHandler(config, environment: "Production").Handle(
            new VerifyOtpCommand("victim@test.com", "000000", TestDeviceId, "iPhone"),
            CancellationToken.None);

        await verify.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task VerifyOtp_DevCode_StillWorksOutsideProduction()
    {
        var config = Config(devCode: "000000");

        var result = await VerifyHandler(config, environment: "Staging").Handle(
            new VerifyOtpCommand("tester@test.com", "000000", TestDeviceId, "iPhone"),
            CancellationToken.None);

        result.IsNewUser.Should().BeTrue();
    }

    // ── A new password ends every other session ───────────────────────────

    [Fact]
    public async Task SetPassword_RevokesRefreshTokensFromOtherSessions()
    {
        // Someone changes their password because they think a session is not
        // theirs. A refresh token that outlives the change keeps the intruder in
        // for up to 30 days.
        var userId = await SeedIdentityAsync("owner@test.com", password: "old-password");
        var stolen = await SeedRefreshTokenAsync(userId);

        await new SetPasswordHandler(_db, new PasswordHasher()).Handle(
            new SetPasswordCommand(userId, "a-new-password"), CancellationToken.None);

        (await _db.UserRefreshTokens.SingleAsync(t => t.Id == stolen.Id)).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ResetPassword_RevokesRefreshTokensFromOtherSessions()
    {
        var userId = await SeedIdentityAsync("forgetful@test.com", password: "old-password");
        var stolen = await SeedRefreshTokenAsync(userId);
        var code = await SeedChallengeAsync("forgetful@test.com");

        await new ResetPasswordHandler(
                _db, new PasswordHasher(), new OtpService(_config, NullLogger<OtpService>.Instance), _config)
            .Handle(new ResetPasswordCommand("forgetful@test.com", code, "a-new-password"), CancellationToken.None);

        (await _db.UserRefreshTokens.SingleAsync(t => t.Id == stolen.Id)).IsActive.Should().BeFalse();
    }

    public void Dispose()
    {
        _db.Dispose();
        _accountDb.Dispose();
        _connection.Dispose();
        _accountConnection.Dispose();
    }

    private sealed class NullDispatcher : Balsm.SharedKernel.Events.IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(
            IEnumerable<Balsm.SharedKernel.Events.IDomainEvent> events, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Balsm.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
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
}
