using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Identity.Application.FamilyLinks.Commands;

/// <summary>
/// The requester cancels a pending request, or either party unlinks an active
/// link (sealed cards purged). Never a hard delete; idempotent once ended.
/// </summary>
public sealed record DeleteFamilyLinkCommand(
    Guid FamilyLinkId,
    Guid UserId,
    string? CorrelationId) : IRequest<Result>;
