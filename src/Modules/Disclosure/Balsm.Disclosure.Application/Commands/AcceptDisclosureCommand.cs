using MediatR;

namespace Balsm.Disclosure.Application.Commands;

public sealed record AcceptDisclosureCommand(
    Guid UserId,
    string DisclosureId,
    string Version,
    string CountryCode,
    string SupervisoryAuthority,
    string Language) : IRequest<AcceptDisclosureResult>;

public sealed record AcceptDisclosureResult(Guid AcceptanceId, bool WasAlreadyAccepted);
