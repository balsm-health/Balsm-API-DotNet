using MediatR;

namespace Balsm.Account.Application.Commands;

public sealed record ClaimHandleCommand(Guid UserId, string Handle) : IRequest<ClaimHandleResult>;
public sealed record ClaimHandleResult(string Handle);
public sealed class HandleConflictException(string[] suggestions) : Exception("Handle is taken")
{
    public string[] Suggestions { get; } = suggestions;
}
