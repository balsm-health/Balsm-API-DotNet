namespace Balsm.Identity.Domain;

public interface IIdentityUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
