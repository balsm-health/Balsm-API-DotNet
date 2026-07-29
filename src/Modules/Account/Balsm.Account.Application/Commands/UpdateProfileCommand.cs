using MediatR;

namespace Balsm.Account.Application.Commands;

/// <summary>
/// Partial profile update. A null field is left unchanged; an empty string
/// clears it. DateOfBirth and NationalId are field-level encrypted at rest.
/// </summary>
public sealed record UpdateProfileCommand(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? Bio,
    string? Gender,
    string? Nationality,
    string? Phone,
    DateOnly? DateOfBirth,
    string? NationalId) : IRequest;
