using Balsm.Entity.Domain;
using Balsm.Entity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Entity.Infrastructure;

public sealed class EntityUnitOfWork(EntityDbContext db) : IEntityUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);

    public Task<EntityRoot?> GetEntityIncludingDeletedAsync(Guid id, CancellationToken ct = default)
        => db.Entities.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<Branch?> GetBranchIncludingDeletedAsync(Guid id, CancellationToken ct = default)
        => db.Branches.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Id == id, ct);
}
