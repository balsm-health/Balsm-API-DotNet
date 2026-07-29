using MediatR;

namespace Balsm.Account.Application.Queries;

public sealed record GetSelfQuery(Guid UserId) : IRequest<GetSelfResult>;

public sealed record GetSelfResult(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? Handle,
    string? DisplayName,
    string? Bio,
    string? Gender,
    string? Nationality,
    string? Phone,
    string CountryCode,
    string PreferredLanguage,
    string DeletionState,
    int? DobYear,
    DateOnly? DateOfBirth,
    string? NationalId);
