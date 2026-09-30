using Balsm.CareTeam.Application.Commands;
using Balsm.CareTeam.Domain;
using Balsm.CareTeam.Infrastructure.Data;
using Balsm.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.CareTeam.Infrastructure.Handlers;

public sealed class DeleteCareProviderHandler(CareTeamDbContext db)
    : IRequestHandler<DeleteCareProviderCommand, Result>
{
    public async Task<Result> Handle(DeleteCareProviderCommand cmd, CancellationToken ct)
    {
        var provider = await db.CareProviders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == cmd.Id, ct);

        if (provider is null || provider.UserId != cmd.UserId)
            return Result.Failure(CareTeamErrors.NotFound);

        provider.Tombstone();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
