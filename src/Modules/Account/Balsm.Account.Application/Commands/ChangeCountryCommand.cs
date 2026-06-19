using MediatR;

namespace Balsm.Account.Application.Commands;

public sealed record ChangeCountryCommand(Guid UserId, string CountryCode) : IRequest;
