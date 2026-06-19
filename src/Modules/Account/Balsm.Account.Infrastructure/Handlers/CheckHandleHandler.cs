using Balsm.Account.Application.Queries;
using Balsm.Account.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Balsm.Account.Infrastructure.Handlers;

public sealed class CheckHandleHandler(AccountDbContext db)
    : IRequestHandler<CheckHandleQuery, CheckHandleResult>
{
    private static readonly Regex HandlePattern = new(@"^[a-z0-9_]{3,30}$", RegexOptions.Compiled);

    public async Task<CheckHandleResult> Handle(CheckHandleQuery query, CancellationToken ct)
    {
        var handle = query.Handle.ToLowerInvariant();

        if (!HandlePattern.IsMatch(handle))
            return new CheckHandleResult(false, "InvalidFormat");

        var isReserved = await db.ReservedHandleBlocklist
            .AsNoTracking()
            .AnyAsync(r => r.HandleNormalized == handle, ct);
        if (isReserved)
            return new CheckHandleResult(false, "Reserved");

        var isTaken = await db.UsernameReservations
            .AsNoTracking()
            .AnyAsync(r => r.HandleNormalized == handle && r.ReleasedAt == null, ct);
        return isTaken
            ? new CheckHandleResult(false, "Taken")
            : new CheckHandleResult(true, null);
    }
}
