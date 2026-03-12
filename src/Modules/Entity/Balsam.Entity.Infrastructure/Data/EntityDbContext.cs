using Balsam.Infrastructure.Data;
using Balsam.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;

namespace Balsam.Entity.Infrastructure.Data;

public sealed class EntityDbContext(
    DbContextOptions<EntityDbContext> options,
    IDomainEventDispatcher domainEventDispatcher) : BaseDbContext(options, domainEventDispatcher)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("entity");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EntityDbContext).Assembly);
    }
}
