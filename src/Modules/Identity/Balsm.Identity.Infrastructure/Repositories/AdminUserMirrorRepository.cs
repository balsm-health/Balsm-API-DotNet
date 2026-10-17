using Balsm.Identity.Domain;
using Balsm.Identity.Domain.Repositories;
using Balsm.Identity.Infrastructure.Data;
using Balsm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Identity.Infrastructure.Repositories;

internal sealed class AdminUserMirrorRepository(IdentityDbContext db)
    : BaseRepository<AdminUserMirror>(db), IAdminUserMirrorRepository
{
    public async Task<AdminUserMirror?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await db.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), ct)
            .ConfigureAwait(false);
}
