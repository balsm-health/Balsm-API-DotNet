using Balsm.Auth.Application.Commands;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Handlers;
using Balsm.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Balsm.Auth.Tests;

public sealed class VerifyLinkHandlerTests : IDisposable
{
    private static readonly Guid ProvisionedUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ExistingUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid DeviceId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly DateTime FarFuture = new(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LongPast = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private const string Email = "link@example.test";
    private const string LinkToken = "test-link-token-0001";

    private readonly AuthTestDatabase _database = new();
    private readonly FakeAccountProvisioner _provisioner = new(ProvisionedUserId);
    private readonly OtpService _otp = new(TestSettings.Build(), NullLogger<OtpService>.Instance);

    public void Dispose() => _database.Dispose();

    private VerifyLinkHandler Handler() =>
        new(_database.Db, _provisioner, new JwtService(TestSettings.Build()), _otp);

    private static VerifyLinkCommand Command(string token = LinkToken) => new(token, DeviceId, "test-device");

    private async Task<OtpChallenge> SeedChallengeAsync(DateTime? expiresAt = null)
    {
        var challenge = OtpChallenge.Create(Email, "unused-code-hash", expiresAt ?? FarFuture, _otp.HashToken(LinkToken));
        _database.Db.OtpChallenges.Add(challenge);
        await _database.Db.SaveChangesAsync();
        return challenge;
    }

    private async Task SeedEmailIdentityAsync()
    {
        _database.Db.UserIdentities.Add(UserIdentity.Create(ExistingUserId, "email", Email, Email));
        await _database.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_ValidLinkForNewEmail_ReturnsProvisionedNewUser()
    {
        await SeedChallengeAsync();

        var result = await Handler().Handle(Command(), CancellationToken.None);

        new { result.UserId, result.IsNewUser }.Should().Be(new { UserId = ProvisionedUserId, IsNewUser = true });
    }

    [Fact]
    public async Task Handle_ValidLinkForNewEmail_ProvisionsAccountWithEgyptDefaults()
    {
        await SeedChallengeAsync();

        await Handler().Handle(Command(), CancellationToken.None);

        _provisioner.Calls.Should().Equal(("EG", "ar-EG"));
    }

    [Fact]
    public async Task Handle_ValidLinkForNewEmail_CreatesConfirmedEmailIdentity()
    {
        await SeedChallengeAsync();

        await Handler().Handle(Command(), CancellationToken.None);

        var identity = await _database.Fresh().UserIdentities.SingleAsync();
        new { identity.UserId, identity.Provider, identity.EmailNormalized, Confirmed = identity.EmailConfirmedAt != null }
            .Should().Be(new { UserId = ProvisionedUserId, Provider = "email", EmailNormalized = (string?)Email, Confirmed = true });
    }

    [Fact]
    public async Task Handle_ValidLinkForExistingEmail_SignsInExistingUser()
    {
        await SeedEmailIdentityAsync();
        await SeedChallengeAsync();

        var result = await Handler().Handle(Command(), CancellationToken.None);

        new { result.UserId, result.IsNewUser }.Should().Be(new { UserId = ExistingUserId, IsNewUser = false });
    }

    [Fact]
    public async Task Handle_ValidLinkForExistingEmail_DoesNotProvisionAccount()
    {
        await SeedEmailIdentityAsync();
        await SeedChallengeAsync();

        await Handler().Handle(Command(), CancellationToken.None);

        _provisioner.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ValidLink_ConsumesChallenge()
    {
        var challenge = await SeedChallengeAsync();

        await Handler().Handle(Command(), CancellationToken.None);

        (await _database.Fresh().OtpChallenges.SingleAsync(c => c.Id == challenge.Id)).ConsumedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ValidLink_StoresRefreshTokenForDevice()
    {
        await SeedChallengeAsync();
        var jwt = new JwtService(TestSettings.Build());

        var result = await Handler().Handle(Command(), CancellationToken.None);

        var hash = jwt.HashRefreshToken(result.RefreshToken);
        var stored = await _database.Fresh().UserRefreshTokens.SingleAsync(t => t.TokenHash == hash);
        new { stored.UserId, stored.DeviceId }.Should().Be(new { UserId = ProvisionedUserId, DeviceId });
    }

    [Fact]
    public async Task Handle_UnknownToken_ThrowsUnauthorized()
    {
        await SeedChallengeAsync();

        var act = () => Handler().Handle(Command("test-link-token-unknown"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_LinkUsedTwice_ThrowsUnauthorized()
    {
        await SeedChallengeAsync();
        await Handler().Handle(Command(), CancellationToken.None);

        var act = () => Handler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_ExpiredLink_ThrowsUnauthorized()
    {
        await SeedChallengeAsync(expiresAt: LongPast);

        var act = () => Handler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_ChallengeWithAttemptsSpent_ThrowsUnauthorized()
    {
        var challenge = await SeedChallengeAsync();
        await _database.Db.OtpChallenges.Where(c => c.Id == challenge.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Attempts, OtpChallenge.MaxAttempts));
        _database.Db.ChangeTracker.Clear();

        var act = () => Handler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
