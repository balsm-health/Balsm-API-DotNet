using Balsm.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Inventory.Infrastructure.Data;

public sealed class InventoryDbContext(
    DbContextOptions<InventoryDbContext> options,
    IDomainEventDispatcher domainEventDispatcher) : BaseDbContext(options, domainEventDispatcher)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("inventory");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);
    }
}
