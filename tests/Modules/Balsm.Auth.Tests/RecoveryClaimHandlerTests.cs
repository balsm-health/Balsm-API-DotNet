using System.Security.Cryptography;
using System.Text;
using Balsm.Auth.Application.Commands;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Handlers;
using Balsm.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Balsm.Auth.Tests;

/// <summary>
/// Support-issued recovery tokens: "{unixSeconds}:{HMAC-SHA256(email:unixSeconds)}",
/// valid for one hour. The handler reads the wall clock directly (no clock seam),
/// so "fresh" tokens are stamped with the current time; expired ones use a fixed
/// timestamp far in the past.
/// </summary>
public sealed class RecoveryClaimHandlerTests : IDisposable
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid DeviceId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private const long LongPastUnixSeconds = 1_577_836_800; // 2020-01-01T00:00:00Z
    private const string Email = "recover@example.test";

    private readonly AuthTestDatabase _database = new();
    private readonly JwtService _jwt = new(TestSettings.Build());

    public void Dispose() => _database.Dispose();

    private RecoveryClaimHandler Handler(IConfiguration? config = null) =>
        new(_database.Db, _jwt, config ?? TestSettings.Build(), NullLogger<RecoveryClaimHandler>.Instance);

    private static string SupportToken(
        string email = Email, long? unixSeconds = null, string secret = TestSettings.RecoverySecret)
    {
        var ts = unixSeconds ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{email}:{ts}"));
        return $"{ts}:{Convert.ToHexString(mac)}";
    }

    private static RecoveryClaimCommand Command(string supportToken, string email = Email) =>
        new(email, supportToken, DeviceId, "test-device");

    private async Task SeedIdentityAsync(string provider = "email")
    {
        _database.Db.UserIdentities.Add(UserIdentity.Create(UserId, provider, Email, Email));
        await _database.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_ValidToken_ReturnsSessionForIdentityOwner()
    {
        await SeedIdentityAsync();

        var result = await Handler().Handle(Command(SupportToken()), CancellationToken.None);

        result.UserId.Should().Be(UserId);
    }

    [Fact]
    public async Task Handle_ValidToken_IssuesAccessTokenAcceptedByJwtService()
    {
        await SeedIdentityAsync();

        var result = await Handler().Handle(Command(SupportToken()), CancellationToken.None);

        _jwt.ValidateAccessToken(result.AccessToken).Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ValidToken_StoresRefreshTokenForDevice()
    {
        await SeedIdentityAsync();

        var result = await Handler().Handle(Command(SupportToken()), CancellationToken.None);

        var hash = _jwt.HashRefreshToken(result.RefreshToken);
        var stored = await _database.Fresh().UserRefreshTokens.SingleAsync(t => t.TokenHash == hash);
        new { stored.UserId, stored.DeviceId }.Should().Be(new { UserId, DeviceId });
    }

    [Fact]
    public async Task Handle_EmailWithCaseAndWhitespace_MatchesNormalizedIdentity()
    {
        await SeedIdentityAsync();

        var result = await Handler().Handle(Command(SupportToken(), "  RECOVER@Example.Test "), CancellationToken.None);

        result.UserId.Should().Be(UserId);
    }

    [Fact]
    public async Task Handle_LowercaseHexSignature_IsAccepted()
    {
        await SeedIdentityAsync();

        var result = await Handler().Handle(Command(SupportToken().ToLowerInvariant()), CancellationToken.None);

        result.UserId.Should().Be(UserId);
    }

    [Fact]
    public async Task Handle_SecretNotConfigured_ThrowsInvalidOperation()
    {
        await SeedIdentityAsync();

        var act = () => Handler(TestSettings.Build(recoverySecret: null)).Handle(Command(SupportToken()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Recovery:Secret not configured");
    }

    [Theory]
    [InlineData("no-separator")]
    [InlineData("not-a-number:ABCDEF")]
    [InlineData(":ABCDEF")]
    public async Task Handle_MalformedToken_ThrowsInvalidSupportToken(string supportToken)
    {
        await SeedIdentityAsync();

        var act = () => Handler().Handle(Command(supportToken), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidSupportTokenException>().WithMessage("Support token invalid or expired");
    }

    [Fact]
    public async Task Handle_TokenOlderThanOneHour_ThrowsInvalidSupportToken()
    {
        await SeedIdentityAsync();

        var act = () => Handler().Handle(Command(SupportToken(unixSeconds: LongPastUnixSeconds)), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidSupportTokenException>();
    }

    [Fact]
    public async Task Handle_TokenSignedWithOtherSecret_ThrowsInvalidSupportToken()
    {
        await SeedIdentityAsync();

        var act = () => Handler().Handle(Command(SupportToken(secret: "some-other-secret")), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidSupportTokenException>();
    }

    [Fact]
    public async Task Handle_TokenIssuedForOtherEmail_ThrowsInvalidSupportToken()
    {
        await SeedIdentityAsync();

        var act = () => Handler().Handle(Command(SupportToken(email: "someone-else@example.test")), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidSupportTokenException>();
    }

    [Fact]
    public async Task Handle_TruncatedSignature_ThrowsInvalidSupportToken()
    {
        await SeedIdentityAsync();

        var act = () => Handler().Handle(Command(SupportToken()[..^2]), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidSupportTokenException>();
    }

    [Fact]
    public async Task Handle_NoIdentityForEmail_ThrowsRecoveryIdentityNotFound()
    {
        var act = () => Handler().Handle(Command(SupportToken()), CancellationToken.None);

        await act.Should().ThrowAsync<RecoveryIdentityNotFoundException>()
            .WithMessage("No identity found for recovery claim");
    }

    [Fact]
    public async Task Handle_OnlySocialIdentityForEmail_ThrowsRecoveryIdentityNotFound()
    {
        await SeedIdentityAsync(provider: "google");

        var act = () => Handler().Handle(Command(SupportToken()), CancellationToken.None);

        await act.Should().ThrowAsync<RecoveryIdentityNotFoundException>();
    }
}
