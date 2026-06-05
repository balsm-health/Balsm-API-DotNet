using Balsm.SharedKernel.Domain;
using Balsm.SharedKernel.Repositories;

namespace Balsm.Identity.Domain.Repositories;

public interface IAdminUserMirrorRepository : IRepository<AdminUserMirror>
{
    Task<AdminUserMirror?> GetByEmailAsync(string email, CancellationToken ct = default);
}
