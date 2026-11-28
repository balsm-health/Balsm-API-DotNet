using Balsm.Deletion.Infrastructure.Data;
using Balsm.Deletion.Infrastructure.Jobs;
using Balsm.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Deletion.Infrastructure.Services;

/// <summary>
/// Deletion-side implementation of <see cref="IDeletedAccountRegistry"/>: an
/// account was deleted when a deletion log carries its user-id hash. The log is
/// kept two years after the purge.
/// </summary>
public sealed class DeletedAccountRegistry(DeletionDbContext db) : IDeletedAccountRegistry
{
    public async Task<bool> WasDeletedAsync(Guid userId, CancellationToken ct = default)
    {
        var hash = DeletionPurgeJob.ComputeHash(userId);
        return await db.DeletionLogs.AnyAsync(l => l.UserIdHash == hash, ct).ConfigureAwait(false);
    }
}
