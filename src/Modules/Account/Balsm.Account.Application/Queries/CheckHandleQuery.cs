using MediatR;

namespace Balsm.Account.Application.Queries;

public sealed record CheckHandleQuery(string Handle) : IRequest<CheckHandleResult>;
public sealed record CheckHandleResult(bool Available, string? Reason);
