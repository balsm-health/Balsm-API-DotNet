using MediatR;

namespace Balsm.Sessions.Application.Commands;

public sealed record RevokeSessionCommand(Guid SessionId, Guid RequestingUserId) : IRequest;
public sealed record RevokeAllSessionsCommand(Guid UserId) : IRequest;
