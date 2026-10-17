using Balsm.SharedKernel.Repositories;

namespace Balsm.Entity.Domain.Repositories;

public interface IWorkspaceRepository : IRepository<Workspace>
{
    Task<Workspace?> GetActiveAsync(CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
}

public interface IEntityTypeRepository
{
    Task<EntityType?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<EntityType>> ListAllAsync(CancellationToken ct = default);
    Task AddAsync(EntityType entityType, CancellationToken ct = default);
}

public interface IEntityRepository : IRepository<EntityRoot>
{
    Task<IReadOnlyList<EntityRoot>> ListAsync(Guid workspaceId, bool includeInactive, CancellationToken ct = default);
}

public interface IBranchRepository : IRepository<Branch>
{
    Task<IReadOnlyList<Branch>> ListByEntityAsync(Guid entityRootId, bool includeInactive, CancellationToken ct = default);
}
