using System.IdentityModel.Tokens.Jwt;
using Balsm.Auth.Application.Commands;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Handlers;
using Balsm.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Balsm.Auth.Tests;

public sealed class RefreshTokenHandlerTests : IDisposable
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid DeviceId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid OtherDeviceId = Guid.Parse("00000000-0000-0000-0000-000000000011");
    private static readonly DateTime FarFuture = new(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LongPast = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private const string Email = "refresh@example.test";

    private readonly AuthTestDatabase _database = new();
    private readonly JwtService _jwt = new(TestSettings.Build());

    public void Dispose() => _database.Dispose();

    private RefreshTokenHandler Handler() => new(_database.Db, _jwt);

    private async Task<string> SeedTokenAsync(DateTime? expiresAt = null, bool revoked = false, bool withIdentity = true)
    {
        var (raw, hash) = _jwt.IssueRefreshToken();
        var token = UserRefreshToken.Create(UserId, hash, DeviceId, expiresAt ?? FarFuture);
        if (revoked) token.Revoke();
        _database.Db.UserRefreshTokens.Add(token);
        if (withIdentity) _database.Db.UserIdentities.Add(UserIdentity.Create(UserId, "email", Email, Email));
        await _database.Db.SaveChangesAsync();
        return raw;
    }

    private static JwtSecurityToken Read(string accessToken) => new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

    [Fact]
    public async Task Handle_ActiveToken_IssuesAccessTokenForTokenOwner()
    {
        var raw = await SeedTokenAsync();

        var result = await Handler().Handle(new RefreshTokenCommand(raw, DeviceId), CancellationToken.None);

        Read(result.AccessToken).Subject.Should().Be(UserId.ToString());
    }

    [Fact]
    public async Task Handle_ActiveToken_EmbedsIdentityEmailInAccessToken()
    {
        var raw = await SeedTokenAsync();

        var result = await Handler().Handle(new RefreshTokenCommand(raw, DeviceId), CancellationToken.None);

        Read(result.AccessToken).Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value.Should().Be(Email);
    }

    [Fact]
    public async Task Handle_OwnerWithoutIdentity_EmbedsEmptyEmail()
    {
        var raw = await SeedTokenAsync(withIdentity: false);

        var result = await Handler().Handle(new RefreshTokenCommand(raw, DeviceId), CancellationToken.None);

        Read(result.AccessToken).Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ActiveToken_RevokesPresentedToken()
    {
        var raw = await SeedTokenAsync();
        var presentedHash = _jwt.HashRefreshToken(raw);

        await Handler().Handle(new RefreshTokenCommand(raw, DeviceId), CancellationToken.None);

        var stored = await _database.Fresh().UserRefreshTokens.SingleAsync(t => t.TokenHash == presentedHash);
        stored.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ActiveToken_StoresRotatedTokenForSameUserAndDevice()
    {
        var raw = await SeedTokenAsync();

        var result = await Handler().Handle(new RefreshTokenCommand(raw, DeviceId), CancellationToken.None);

        var rotatedHash = _jwt.HashRefreshToken(result.RefreshToken);
        var rotated = await _database.Fresh().UserRefreshTokens.SingleAsync(t => t.TokenHash == rotatedHash);
        new { rotated.UserId, rotated.DeviceId, rotated.IsActive }
            .Should().Be(new { UserId, DeviceId, IsActive = true });
    }

    [Fact]
    public async Task Handle_UnknownToken_ThrowsTokenNotFound()
    {
        await SeedTokenAsync();
        var (unknownRaw, _) = _jwt.IssueRefreshToken();

        var act = () => Handler().Handle(new RefreshTokenCommand(unknownRaw, DeviceId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("TokenNotFound");
    }

    [Fact]
    public async Task Handle_TokenPresentedFromOtherDevice_ThrowsTokenNotFound()
    {
        var raw = await SeedTokenAsync();

        var act = () => Handler().Handle(new RefreshTokenCommand(raw, OtherDeviceId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("TokenNotFound");
    }

    [Fact]
    public async Task Handle_RevokedToken_ThrowsTokenRevoked()
    {
        var raw = await SeedTokenAsync(revoked: true);

        var act = () => Handler().Handle(new RefreshTokenCommand(raw, DeviceId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("TokenRevoked");
    }

    [Fact]
    public async Task Handle_ExpiredToken_ThrowsTokenRevoked()
    {
        var raw = await SeedTokenAsync(expiresAt: LongPast);

        var act = () => Handler().Handle(new RefreshTokenCommand(raw, DeviceId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("TokenRevoked");
    }

    [Fact]
    public async Task Handle_TokenReplayedAfterRotation_ThrowsTokenRevoked()
    {
        var raw = await SeedTokenAsync();
        await Handler().Handle(new RefreshTokenCommand(raw, DeviceId), CancellationToken.None);

        var act = () => Handler().Handle(new RefreshTokenCommand(raw, DeviceId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("TokenRevoked");
    }
}
