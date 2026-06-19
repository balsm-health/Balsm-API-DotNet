using Balsm.Sessions.Application.Commands;
using Balsm.Sessions.Application.Queries;
using Balsm.Sessions.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Sessions.Infrastructure.Handlers;

public sealed class ListSessionsHandler(SessionsDbContext db)
    : IRequestHandler<ListSessionsQuery, ListSessionsResult>
{
    public async Task<ListSessionsResult> Handle(ListSessionsQuery query, CancellationToken ct)
    {
        var sessions = await db.ActiveSessions
            .AsNoTracking()
            .Where(s => s.UserId == query.UserId && s.RevokedAt == null)
            .OrderByDescending(s => s.LastActivityAt)
            .Select(s => new SessionDto(s.Id, s.DeviceId, s.DeviceLabel, s.DeviceType, s.FirstSeenAt, s.LastActivityAt))
            .ToListAsync(ct);
        return new ListSessionsResult(sessions);
    }
}

public sealed class RevokeSessionHandler(SessionsDbContext db)
    : IRequestHandler<RevokeSessionCommand>
{
    public async Task Handle(RevokeSessionCommand cmd, CancellationToken ct)
    {
        var session = await db.ActiveSessions.FindAsync([cmd.SessionId], ct)
            ?? throw new InvalidOperationException("Session not found");
        if (session.UserId != cmd.RequestingUserId)
            throw new UnauthorizedAccessException("Session belongs to different user");
        session.Revoke();
        await db.SaveChangesAsync(ct);
    }
}

public sealed class RevokeAllSessionsHandler(SessionsDbContext db)
    : IRequestHandler<RevokeAllSessionsCommand>
{
    public async Task Handle(RevokeAllSessionsCommand cmd, CancellationToken ct)
    {
        var sessions = await db.ActiveSessions
            .Where(s => s.UserId == cmd.UserId && s.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var s in sessions) s.Revoke();
        await db.SaveChangesAsync(ct);
    }
}
