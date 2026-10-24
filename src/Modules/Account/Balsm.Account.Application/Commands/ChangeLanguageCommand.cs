using MediatR;

namespace Balsm.Account.Application.Commands;

public sealed record ChangeLanguageCommand(Guid UserId, string Language) : IRequest;
