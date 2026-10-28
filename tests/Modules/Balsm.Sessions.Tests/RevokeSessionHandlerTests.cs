using Balsm.Sessions.Application.Commands;
using Balsm.Sessions.Domain.Entities;
using Balsm.Sessions.Infrastructure.Handlers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Balsm.Sessions.Tests;

public sealed class RevokeSessionHandlerTests : IDisposable
{
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid IntruderId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid SessionId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid MissingSessionId = Guid.Parse("a0000000-0000-0000-0000-0000000000ff");

    private readonly SessionsTestDb _db = new();

    public RevokeSessionHandlerTests()
    {
        using var context = _db.CreateContext();
        var entry = context.ActiveSessions.Add(ActiveSession.Create(
            OwnerId,
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "Test Phone",
            "phone",
            Guid.Parse("33333333-3333-3333-3333-333333333333")));
        entry.Property(s => s.Id).CurrentValue = SessionId;
        context.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private async Task RevokeAsync(Guid sessionId, Guid requestingUserId)
    {
        await using var context = _db.CreateContext();
        await new RevokeSessionHandler(context)
            .Handle(new RevokeSessionCommand(sessionId, requestingUserId), CancellationToken.None);
    }

    private async Task<ActiveSession> ReloadAsync()
    {
        await using var context = _db.CreateContext();
        return await context.ActiveSessions.AsNoTracking().SingleAsync(s => s.Id == SessionId);
    }

    [Fact]
    public async Task Handle_WhenOwnerRevokes_PersistsRevocation()
    {
        await RevokeAsync(SessionId, OwnerId);

        (await ReloadAsync()).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenSessionDoesNotExist_ThrowsInvalidOperationException()
    {
        var act = () => RevokeAsync(MissingSessionId, OwnerId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Session not found");
    }

    [Fact]
    public async Task Handle_WhenSessionBelongsToAnotherUser_ThrowsUnauthorizedAccessException()
    {
        var act = () => RevokeAsync(SessionId, IntruderId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Session belongs to different user");
    }

    [Fact]
    public async Task Handle_WhenSessionBelongsToAnotherUser_LeavesSessionActive()
    {
        var act = () => RevokeAsync(SessionId, IntruderId);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();

        var session = await ReloadAsync();

        session.IsActive.Should().BeTrue();
    }
}
