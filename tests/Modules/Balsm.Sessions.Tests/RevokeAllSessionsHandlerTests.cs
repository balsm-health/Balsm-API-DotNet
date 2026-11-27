using Balsm.Sessions.Application.Commands;
using Balsm.Sessions.Domain.Entities;
using Balsm.Sessions.Infrastructure.Handlers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Balsm.Sessions.Tests;

public sealed class RevokeAllSessionsHandlerTests : IDisposable
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid RefreshTokenId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime EarlierRevokedAt = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly SessionsTestDb _db = new();

    public void Dispose() => _db.Dispose();

    private async Task SeedAsync(Guid sessionId, Guid userId, Guid deviceId, DateTime? revokedAt = null)
    {
        await using var context = _db.CreateContext();
        var entry = context.ActiveSessions.Add(
            ActiveSession.Create(userId, deviceId, "Test Device", "tablet", RefreshTokenId));
        entry.Property(s => s.Id).CurrentValue = sessionId;
        entry.Property(s => s.RevokedAt).CurrentValue = revokedAt;
        await context.SaveChangesAsync();
    }

    private async Task RevokeAllAsync(Guid userId)
    {
        await using var context = _db.CreateContext();
        await new RevokeAllSessionsHandler(context)
            .Handle(new RevokeAllSessionsCommand(userId), CancellationToken.None);
    }

    private async Task<List<ActiveSession>> SessionsOfAsync(Guid userId)
    {
        await using var context = _db.CreateContext();
        return await context.ActiveSessions.AsNoTracking().Where(s => s.UserId == userId).ToListAsync();
    }

    [Fact]
    public async Task Handle_WhenUserHasActiveSessions_RevokesAllOfThem()
    {
        await SeedAsync(Guid.Parse("a0000000-0000-0000-0000-000000000001"), UserId,
            Guid.Parse("d0000000-0000-0000-0000-000000000001"));
        await SeedAsync(Guid.Parse("a0000000-0000-0000-0000-000000000002"), UserId,
            Guid.Parse("d0000000-0000-0000-0000-000000000002"));

        await RevokeAllAsync(UserId);

        (await SessionsOfAsync(UserId)).Should().HaveCount(2).And.OnlyContain(s => !s.IsActive);
    }

    [Fact]
    public async Task Handle_WhenSessionAlreadyRevoked_KeepsOriginalRevokedAt()
    {
        await SeedAsync(Guid.Parse("a0000000-0000-0000-0000-000000000003"), UserId,
            Guid.Parse("d0000000-0000-0000-0000-000000000003"), EarlierRevokedAt);

        await RevokeAllAsync(UserId);

        (await SessionsOfAsync(UserId)).Single().RevokedAt.Should().Be(EarlierRevokedAt);
    }

    [Fact]
    public async Task Handle_WhenOtherUserHasSessions_LeavesThemActive()
    {
        await SeedAsync(Guid.Parse("a0000000-0000-0000-0000-000000000004"), OtherUserId,
            Guid.Parse("d0000000-0000-0000-0000-000000000004"));

        await RevokeAllAsync(UserId);

        (await SessionsOfAsync(OtherUserId)).Should().ContainSingle().Which.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenUserHasNoSessions_CompletesWithoutError()
    {
        var act = () => RevokeAllAsync(UserId);

        await act.Should().NotThrowAsync();
    }
}
