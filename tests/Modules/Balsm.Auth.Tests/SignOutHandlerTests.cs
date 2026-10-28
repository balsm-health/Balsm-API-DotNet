using Balsm.Auth.Application.Commands;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Handlers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Balsm.Auth.Tests;

public sealed class SignOutHandlerTests : IDisposable
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid DeviceId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid OtherDeviceId = Guid.Parse("00000000-0000-0000-0000-000000000011");
    private static readonly DateTime FarFuture = new(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly AuthTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private SignOutHandler Handler() => new(_database.Db);

    private async Task<UserRefreshToken> SeedTokenAsync(string hash, Guid userId, Guid deviceId, bool revoked = false)
    {
        var token = UserRefreshToken.Create(userId, hash, deviceId, FarFuture);
        if (revoked) token.Revoke();
        _database.Db.UserRefreshTokens.Add(token);
        await _database.Db.SaveChangesAsync();
        return token;
    }

    private Task<UserRefreshToken> StoredAsync(string hash) =>
        _database.Fresh().UserRefreshTokens.SingleAsync(t => t.TokenHash == hash);

    [Fact]
    public async Task Handle_ActiveTokensOnDevice_RevokesEveryOne()
    {
        await SeedTokenAsync("hash-a", UserId, DeviceId);
        await SeedTokenAsync("hash-b", UserId, DeviceId);

        await Handler().Handle(new SignOutCommand(UserId, DeviceId), CancellationToken.None);

        (await _database.Fresh().UserRefreshTokens.Where(t => t.DeviceId == DeviceId).ToListAsync())
            .Should().OnlyContain(t => t.RevokedAt != null);
    }

    [Fact]
    public async Task Handle_TokenOnOtherDevice_LeavesItActive()
    {
        await SeedTokenAsync("hash-this-device", UserId, DeviceId);
        await SeedTokenAsync("hash-other-device", UserId, OtherDeviceId);

        await Handler().Handle(new SignOutCommand(UserId, DeviceId), CancellationToken.None);

        (await StoredAsync("hash-other-device")).RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_OtherUsersTokenOnSameDevice_LeavesItActive()
    {
        await SeedTokenAsync("hash-mine", UserId, DeviceId);
        await SeedTokenAsync("hash-theirs", OtherUserId, DeviceId);

        await Handler().Handle(new SignOutCommand(UserId, DeviceId), CancellationToken.None);

        (await StoredAsync("hash-theirs")).RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_AlreadyRevokedToken_KeepsOriginalRevocationTime()
    {
        var revoked = await SeedTokenAsync("hash-revoked", UserId, DeviceId, revoked: true);
        var originalRevokedAt = (await StoredAsync("hash-revoked")).RevokedAt;

        await Handler().Handle(new SignOutCommand(UserId, DeviceId), CancellationToken.None);

        (await StoredAsync(revoked.TokenHash)).RevokedAt.Should().Be(originalRevokedAt);
    }

    [Fact]
    public async Task Handle_NoTokens_CompletesWithoutError()
    {
        var act = () => Handler().Handle(new SignOutCommand(UserId, DeviceId), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
