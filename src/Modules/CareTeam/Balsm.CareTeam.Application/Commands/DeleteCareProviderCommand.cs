using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.CareTeam.Application.Commands;

/// <summary>Tombstones one row. Idempotent — a repeated delete succeeds.</summary>
public sealed record DeleteCareProviderCommand(Guid Id, Guid UserId) : IRequest<Result>;
