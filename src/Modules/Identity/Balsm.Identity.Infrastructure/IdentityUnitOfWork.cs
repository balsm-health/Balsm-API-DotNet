using Balsm.Identity.Domain;
using Balsm.Identity.Infrastructure.Data;

namespace Balsm.Identity.Infrastructure;

internal sealed class IdentityUnitOfWork(IdentityDbContext db) : IIdentityUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
