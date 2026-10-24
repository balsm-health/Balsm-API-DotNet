using Balsm.Entity.Domain;
using Balsm.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Entity.Infrastructure.Data;

public sealed class EntityDbContext(
    DbContextOptions<EntityDbContext> options,
    IDomainEventDispatcher domainEventDispatcher) : BaseDbContext(options, domainEventDispatcher)
{
    public DbSet<Workspace> Workspaces { get; set; } = null!;
    public DbSet<EntityRoot> Entities { get; set; } = null!;
    public DbSet<Branch> Branches { get; set; } = null!;
    public DbSet<EntityType> EntityTypes { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("entity");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EntityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
