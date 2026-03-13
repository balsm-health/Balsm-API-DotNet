using Balsam.Infrastructure.Data;
using Balsam.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;

namespace Balsam.Customer.Infrastructure.Data;

public sealed class CustomerDbContext(
    DbContextOptions<CustomerDbContext> options,
    IDomainEventDispatcher domainEventDispatcher) : BaseDbContext(options, domainEventDispatcher)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("customer");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomerDbContext).Assembly);
    }
}
