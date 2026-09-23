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
/// <param name="HealthProfileId">Context for the audit row only — it does NOT filter.
/// The partition key is <paramref name="UserId"/>: a health profile id is minted on the
/// device, so filtering on it would return nothing to a patient signing in on a new
/// phone, which is the entire point of the feature. The client maps pulled rows onto
/// its own local profile on merge.</param>
public sealed record PullCareProvidersQuery(
    Guid UserId,
    Guid HealthProfileId,
    DateTime? Since,
    string? Actor = null,
    string? SourceIp = null,
    string? CorrelationId = null) : IRequest<Result<IReadOnlyList<CareProviderDto>>>;
