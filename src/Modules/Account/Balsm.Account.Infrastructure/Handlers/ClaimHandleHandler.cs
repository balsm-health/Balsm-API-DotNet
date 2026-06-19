using Balsm.Account.Application.Commands;
using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Account.Infrastructure.Handlers;

public sealed class ClaimHandleHandler(AccountDbContext db)
    : IRequestHandler<ClaimHandleCommand, ClaimHandleResult>
{
    public async Task<ClaimHandleResult> Handle(ClaimHandleCommand cmd, CancellationToken ct)
    {
        var handle = cmd.Handle.ToLowerInvariant();

        var isTaken = await db.UsernameReservations
            .AsNoTracking()
            .AnyAsync(r => r.HandleNormalized == handle && r.ReleasedAt == null, ct);

        if (isTaken)
        {
            var suggestions = await SuggestAsync(handle, ct);
            throw new HandleConflictException(suggestions);
        }

        var reservation = UsernameReservation.Create(handle, cmd.UserId);
        db.UsernameReservations.Add(reservation);

        var account = await db.UserAccounts.FindAsync([cmd.UserId], ct);
        account?.ClaimHandle(handle);

        await db.SaveChangesAsync(ct);
        return new ClaimHandleResult(handle);
    }

    private async Task<string[]> SuggestAsync(string handle, CancellationToken ct)
    {
        var candidates = new[] { $"{handle}_1", $"{handle}_2", $"{handle}_3" };
        var taken = await db.UsernameReservations
            .AsNoTracking()
            .Where(r => candidates.Contains(r.HandleNormalized) && r.ReleasedAt == null)
            .Select(r => r.HandleNormalized)
            .ToListAsync(ct);
        return candidates.Except(taken).Take(3).ToArray();
    }
}
