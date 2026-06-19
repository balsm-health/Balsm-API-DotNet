using MediatR;

namespace Balsm.Account.Application.Commands;

public sealed record SetDobCommand(Guid UserId, DateOnly DateOfBirth) : IRequest;
public sealed class UnderEighteenException() : Exception("User is under 18");
