using Balsm.Auth.Infrastructure.Data;
using Balsm.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Auth.Infrastructure.Services;

/// <summary>Auth-side implementation of the account-erasure hook.</summary>
public sealed class AccountAuthEraser(AuthDbContext db) : IAccountAuthEraser
{
    public async Task<int> EraseForAccountAsync(Guid userId, CancellationToken ct = default)
    {
        // One transaction so a failure leaves neither half applied. The
        // execution strategy owns it when the provider retries on failure.
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            var identities = await db.UserIdentities
                .Where(i => i.UserId == userId)
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);
            var tokens = await db.UserRefreshTokens
                .Where(t => t.UserId == userId)
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
            return identities + tokens;
        }).ConfigureAwait(false);
    }
}
