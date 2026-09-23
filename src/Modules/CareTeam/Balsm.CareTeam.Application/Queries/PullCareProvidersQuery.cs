using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.CareTeam.Application.Queries;

/// <summary>One row as the client sees it — decrypted, tombstones included.</summary>
public sealed record CareProviderDto(
    Guid Id,
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
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsDeleted);

/// <param name="Since">Exclusive cursor on UpdatedAt; null pulls everything.</param>
public sealed record PullCareProvidersQuery(
    Guid UserId,
    Guid HealthProfileId,
    DateTime? Since) : IRequest<Result<IReadOnlyList<CareProviderDto>>>;
