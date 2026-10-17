using Balsm.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;

namespace Balsm.POS.Infrastructure.Data;

public sealed class POSDbContext(
    DbContextOptions<POSDbContext> options,
    IDomainEventDispatcher domainEventDispatcher) : BaseDbContext(options, domainEventDispatcher)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("pos");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(POSDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
