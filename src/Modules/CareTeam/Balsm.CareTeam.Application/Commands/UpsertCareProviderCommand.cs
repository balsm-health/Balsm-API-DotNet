using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.CareTeam.Application.Commands;

/// <summary>
/// Creates or overwrites one care-team row. Idempotent on <paramref name="Id"/>:
/// the client drains its outbox with at-least-once delivery, so a replayed push
/// must not duplicate.
/// </summary>
/// <param name="CreatedAt">Device clock, trusted only for first-write ordering.
/// UpdatedAt is always the server clock.</param>
public sealed record UpsertCareProviderCommand(
    Guid Id,
    Guid UserId,
    Guid HealthProfileId,
    string Type,
    string Name,
    string? Specialty,
    string? Phone,
    string? Phone2,
    string? Email,
    string? Clinic,
    string? Address,
    string? MapUrl,
    string? Notes,
    DateTime CreatedAt) : IRequest<Result>;
