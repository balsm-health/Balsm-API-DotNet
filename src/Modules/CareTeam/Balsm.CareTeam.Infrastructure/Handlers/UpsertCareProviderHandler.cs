using Balsm.CareTeam.Application.Commands;
using Balsm.CareTeam.Domain;
using Balsm.CareTeam.Domain.Entities;
using Balsm.CareTeam.Infrastructure.Data;
using Balsm.Infrastructure.Encryption;
using Balsm.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.CareTeam.Infrastructure.Handlers;

public sealed class UpsertCareProviderHandler(CareTeamDbContext db, CareTeamEncryptionService crypto)
    : IRequestHandler<UpsertCareProviderCommand, Result>
{
    public async Task<Result> Handle(UpsertCareProviderCommand cmd, CancellationToken ct)
    {
        if (!CareProvider.AllowedTypes.Contains(cmd.Type))
            return Result.Failure(CareTeamErrors.InvalidType);

        var fields = new CareProviderFields(
            crypto.Encrypt(cmd.Name),
            crypto.EncryptOptional(cmd.Specialty),
            crypto.EncryptOptional(cmd.Phone),
            crypto.EncryptOptional(cmd.Phone2),
            crypto.EncryptOptional(cmd.Email),
            crypto.EncryptOptional(cmd.Clinic),
            crypto.EncryptOptional(cmd.Address),
            crypto.EncryptOptional(cmd.MapUrl),
            crypto.EncryptOptional(cmd.Notes));

        // IgnoreQueryFilters so a tombstoned id is found and refused rather than
        // silently re-created under the same primary key.
        var existing = await db.CareProviders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == cmd.Id, ct);

        if (existing is null)
        {
            db.CareProviders.Add(CareProvider.Create(
                cmd.Id, cmd.UserId, cmd.HealthProfileId, cmd.Type, fields, cmd.CreatedAt));
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }

        // Unknown and not-owned collapse into one answer so ownership cannot be probed.
        if (existing.UserId != cmd.UserId)
            return Result.Failure(CareTeamErrors.NotFound);

        if (existing.IsDeleted)
            return Result.Failure(CareTeamErrors.Tombstoned);

        existing.Overwrite(cmd.Type, fields);
        // BaseDbContext.SetAuditFields stamps UpdatedAt from the server clock —
        // the client's CreatedAt is never used for ordering.
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
