using Balsm.Disclosure.Domain.Entities;
using Balsm.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Disclosure.Infrastructure.Data;

public sealed class DisclosureDbContext(
    DbContextOptions<DisclosureDbContext> options,
    IDomainEventDispatcher domainEventDispatcher) : BaseDbContext(options, domainEventDispatcher)
{
    public DbSet<DisclosureAcceptance> DisclosureAcceptances { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DisclosureDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
