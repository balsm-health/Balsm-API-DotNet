using Balsm.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Identity.Infrastructure.Data;

public sealed class IdentityDbContext(
    DbContextOptions<IdentityDbContext> options,
    IDomainEventDispatcher domainEventDispatcher) : BaseDbContext(options, domainEventDispatcher)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("identity");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
