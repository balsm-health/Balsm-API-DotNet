using Balsm.Auth.Infrastructure.Data;
using Balsm.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Auth.Infrastructure.Services;

/// <summary>Auth-side implementation of the account-erasure hook.</summary>
public sealed class AccountAuthEraser(AuthDbContext db) : IAccountAuthEraser
{
    public async Task<int> EraseForAccountAsync(Guid userId, CancellationToken ct = default)
    {
        var identities = await db.UserIdentities
            .Where(i => i.UserId == userId)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);
        var tokens = await db.UserRefreshTokens
            .Where(t => t.UserId == userId)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);
        return identities + tokens;
    }
}
