using MediatR;

namespace Balsm.Sessions.Application.Queries;

public sealed record ListSessionsQuery(Guid UserId) : IRequest<ListSessionsResult>;
public sealed record SessionDto(Guid Id, Guid DeviceId, string DeviceLabel, string DeviceType, DateTime FirstSeenAt, DateTime LastActivityAt);
public sealed record ListSessionsResult(IReadOnlyList<SessionDto> Sessions);
