using Balsm.Entity.Domain;
using Balsm.Entity.Domain.Repositories;
using Balsm.Entity.Infrastructure.Data;
using Balsm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Entity.Infrastructure.Repositories;

public sealed class WorkspaceRepository(EntityDbContext context)
    : BaseRepository<Workspace>(context), IWorkspaceRepository
{
    private readonly EntityDbContext _db = context;

    public async Task<Workspace?> GetActiveAsync(CancellationToken ct = default)
        => await _db.Workspaces.FirstOrDefaultAsync(ct).ConfigureAwait(false);

    public async Task<bool> AnyAsync(CancellationToken ct = default)
        => await _db.Workspaces.AnyAsync(ct).ConfigureAwait(false);
}

public sealed class EntityRepository(EntityDbContext context)
    : BaseRepository<EntityRoot>(context), IEntityRepository
{
    private readonly EntityDbContext _db = context;

    public async Task<IReadOnlyList<EntityRoot>> ListAsync(Guid workspaceId, bool includeInactive, CancellationToken ct = default)
    {
        var query = includeInactive
            ? _db.Entities.IgnoreQueryFilters().Where(e => e.WorkspaceId == workspaceId)
            : _db.Entities.Where(e => e.WorkspaceId == workspaceId);
        return await query.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
    }
}

public sealed class BranchRepository(EntityDbContext context)
    : BaseRepository<Branch>(context), IBranchRepository
{
    private readonly EntityDbContext _db = context;

    public async Task<IReadOnlyList<Branch>> ListByEntityAsync(Guid entityRootId, bool includeInactive, CancellationToken ct = default)
    {
        var query = includeInactive
            ? _db.Branches.IgnoreQueryFilters().Where(b => b.EntityRootId == entityRootId)
            : _db.Branches.Where(b => b.EntityRootId == entityRootId);
        return await query.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
    }
}

public sealed class EntityTypeRepository(EntityDbContext context) : IEntityTypeRepository
{
    private readonly EntityDbContext _db = context;

    public async Task<EntityType?> GetByCodeAsync(string code, CancellationToken ct = default)
        => await _db.EntityTypes.FirstOrDefaultAsync(e => e.Code == code, ct).ConfigureAwait(false);

    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default)
        => await _db.EntityTypes.AnyAsync(e => e.Code == code, ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<EntityType>> ListAllAsync(CancellationToken ct = default)
        => await _db.EntityTypes.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);

    public async Task AddAsync(EntityType entityType, CancellationToken ct = default)
    {
        await _db.EntityTypes.AddAsync(entityType, ct).ConfigureAwait(false);
    }
}
