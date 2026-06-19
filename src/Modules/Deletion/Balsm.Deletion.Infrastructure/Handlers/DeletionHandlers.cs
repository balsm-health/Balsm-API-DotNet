using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.Deletion.Application.Commands;
using Balsm.Deletion.Domain.Entities;
using Balsm.Deletion.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Balsm.Deletion.Infrastructure.Handlers;

public sealed class IntakeDeletionHandler(DeletionDbContext db, AccountDbContext accountDb)
    : IRequestHandler<IntakeDeletionCommand>
{
    public async Task Handle(IntakeDeletionCommand cmd, CancellationToken ct)
    {
        var account = await accountDb.UserAccounts.FindAsync([cmd.UserId], ct)
            ?? throw new InvalidOperationException("Account not found");

        if (account.DeletionState == DeletionState.DELETION_REQUESTED)
            return; // idempotent

        account.RequestDeletion(DateTime.UtcNow.AddDays(7));

        var userIdHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cmd.UserId.ToString())));
        var log = DeletionLog.Create(userIdHash, cmd.CountryCode, cmd.ReasonCode);
        db.DeletionLogs.Add(log);

        await accountDb.SaveChangesAsync(ct);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class CancelDeletionHandler(AccountDbContext accountDb)
    : IRequestHandler<CancelDeletionCommand>
{
    public async Task Handle(CancelDeletionCommand cmd, CancellationToken ct)
    {
        var account = await accountDb.UserAccounts.FindAsync([cmd.UserId], ct)
            ?? throw new InvalidOperationException("Account not found");

        if (account.DeletionState != DeletionState.DELETION_REQUESTED)
            throw new InvalidOperationException("Account is not pending deletion");

        if (account.DeletionGraceUntil < DateTime.UtcNow)
            throw new InvalidOperationException("Grace period has expired");

        account.CancelDeletion();
        await accountDb.SaveChangesAsync(ct);
    }
}
