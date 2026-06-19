using Balsm.Disclosure.Application.Commands;
using Balsm.Disclosure.Domain.Entities;
using Balsm.Disclosure.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Disclosure.Infrastructure.Handlers;

public sealed class AcceptDisclosureHandler(DisclosureDbContext db)
    : IRequestHandler<AcceptDisclosureCommand, AcceptDisclosureResult>
{
    public async Task<AcceptDisclosureResult> Handle(AcceptDisclosureCommand cmd, CancellationToken ct)
    {
        var existing = await db.DisclosureAcceptances
            .AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.UserId == cmd.UserId && d.DisclosureId == cmd.DisclosureId && d.Version == cmd.Version, ct);

        if (existing is not null)
            return new AcceptDisclosureResult(existing.Id, true);

        var acceptance = DisclosureAcceptance.Create(
            cmd.UserId, cmd.DisclosureId, cmd.Version,
            cmd.CountryCode, cmd.SupervisoryAuthority, cmd.Language);

        db.DisclosureAcceptances.Add(acceptance);
        await db.SaveChangesAsync(ct);
        return new AcceptDisclosureResult(acceptance.Id, false);
    }
}
