namespace Balsm.Entity.Domain;

/// <summary>Unit-of-work abstraction for the Entity module, allowing Application layer to
/// call SaveChanges without a direct EF dependency.</summary>
public interface IEntityUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<EntityRoot?> GetEntityIncludingDeletedAsync(Guid id, CancellationToken ct = default);
    Task<Branch?> GetBranchIncludingDeletedAsync(Guid id, CancellationToken ct = default);
}
