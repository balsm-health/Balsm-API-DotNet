using Balsm.Sessions.Application.Queries;
using Balsm.Sessions.Domain.Entities;
using Balsm.Sessions.Infrastructure.Handlers;
using FluentAssertions;
using Xunit;

namespace Balsm.Sessions.Tests;

public sealed class ListSessionsHandlerTests : IDisposable
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid DeviceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RefreshTokenId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OlderSessionId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid NewerSessionId = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    private static readonly DateTime FirstSeenAt = new(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime OlderActivityAt = new(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime NewerActivityAt = new(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc);

    private readonly SessionsTestDb _db = new();

    public void Dispose() => _db.Dispose();

    private async Task SeedAsync(
        Guid sessionId, Guid userId, string label, DateTime lastActivityAt, bool revoked = false)
    {
        await using var context = _db.CreateContext();
        var session = ActiveSession.Create(userId, DeviceId, label, "phone", RefreshTokenId);
        if (revoked)
            session.Revoke();
        var entry = context.ActiveSessions.Add(session);
        entry.Property(s => s.Id).CurrentValue = sessionId;
        entry.Property(s => s.FirstSeenAt).CurrentValue = FirstSeenAt;
        entry.Property(s => s.LastActivityAt).CurrentValue = lastActivityAt;
        await context.SaveChangesAsync();
    }

    private async Task<ListSessionsResult> ListAsync(Guid userId)
    {
        await using var context = _db.CreateContext();
        return await new ListSessionsHandler(context).Handle(new ListSessionsQuery(userId), CancellationToken.None);
    }

    [Fact]
    public async Task Handle_WhenUserHasNoSessions_ReturnsEmptyList()
    {
        var result = await ListAsync(UserId);

        result.Sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenUserHasActiveSession_ReturnsMappedDto()
    {
        await SeedAsync(OlderSessionId, UserId, "Test Phone", OlderActivityAt);

        var result = await ListAsync(UserId);

        result.Sessions.Should().ContainSingle().Which.Should().Be(new SessionDto(
            OlderSessionId, DeviceId, "Test Phone", "phone", FirstSeenAt, OlderActivityAt));
    }

    [Fact]
    public async Task Handle_WithSeveralSessions_OrdersByLastActivityDescending()
    {
        await SeedAsync(OlderSessionId, UserId, "Older", OlderActivityAt);
        await SeedAsync(NewerSessionId, UserId, "Newer", NewerActivityAt);

        var result = await ListAsync(UserId);

        result.Sessions.Select(s => s.Id).Should().Equal(NewerSessionId, OlderSessionId);
    }

    [Fact]
    public async Task Handle_WhenSessionIsRevoked_ExcludesIt()
    {
        await SeedAsync(OlderSessionId, UserId, "Revoked", OlderActivityAt, revoked: true);
        await SeedAsync(NewerSessionId, UserId, "Active", NewerActivityAt);

        var result = await ListAsync(UserId);

        result.Sessions.Select(s => s.Id).Should().Equal(NewerSessionId);
    }

    [Fact]
    public async Task Handle_WhenOtherUserHasSessions_ExcludesThem()
    {
        await SeedAsync(OlderSessionId, OtherUserId, "Other", OlderActivityAt);

        var result = await ListAsync(UserId);

        result.Sessions.Should().BeEmpty();
    }
}
