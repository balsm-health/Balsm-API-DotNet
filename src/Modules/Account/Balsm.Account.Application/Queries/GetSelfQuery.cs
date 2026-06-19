using MediatR;

namespace Balsm.Account.Application.Queries;

public sealed record GetSelfQuery(Guid UserId) : IRequest<GetSelfResult>;

public sealed record GetSelfResult(
    Guid UserId,
    string? Handle,
    string? DisplayName,
    string? Bio,
    string CountryCode,
    string PreferredLanguage,
    string DeletionState,
    int? DobYear);
