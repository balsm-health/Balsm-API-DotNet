using Balsm.CareTeam.Application.Queries;
using Balsm.CareTeam.Infrastructure.Data;
using Balsm.Infrastructure.Encryption;
using Balsm.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.CareTeam.Infrastructure.Handlers;

public sealed class PullCareProvidersHandler(CareTeamDbContext db, CareTeamEncryptionService crypto)
    : IRequestHandler<PullCareProvidersQuery, Result<IReadOnlyList<CareProviderDto>>>
{
    public async Task<Result<IReadOnlyList<CareProviderDto>>> Handle(
        PullCareProvidersQuery query, CancellationToken ct)
    {
        // IgnoreQueryFilters so tombstones reach the client — a delete on one
        // device must propagate, not simply vanish from the result set.
        var rows = await db.CareProviders
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.UserId == query.UserId && p.HealthProfileId == query.HealthProfileId)
            .Where(p => query.Since == null || (p.UpdatedAt ?? p.CreatedAt) > query.Since)
            .OrderBy(p => p.UpdatedAt ?? p.CreatedAt)
            .ToListAsync(ct);

        var dtos = rows.Select(p => new CareProviderDto(
            p.Id,
            p.HealthProfileId,
            p.Type,
            crypto.Decrypt(p.Name),
            crypto.DecryptOptional(p.Specialty),
            crypto.DecryptOptional(p.Phone),
            crypto.DecryptOptional(p.Phone2),
            crypto.DecryptOptional(p.Email),
            crypto.DecryptOptional(p.Clinic),
            crypto.DecryptOptional(p.Address),
            crypto.DecryptOptional(p.MapUrl),
            crypto.DecryptOptional(p.Notes),
            p.CreatedAt,
            p.UpdatedAt ?? p.CreatedAt,
            p.IsDeleted)).ToList();

        return Result.Success<IReadOnlyList<CareProviderDto>>(dtos);
    }
}
