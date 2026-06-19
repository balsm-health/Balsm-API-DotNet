using MediatR;

namespace Balsm.Deletion.Application.Commands;

public sealed record IntakeDeletionCommand(Guid UserId, string CountryCode, string? ReasonCode) : IRequest;
public sealed record CancelDeletionCommand(Guid UserId) : IRequest;
