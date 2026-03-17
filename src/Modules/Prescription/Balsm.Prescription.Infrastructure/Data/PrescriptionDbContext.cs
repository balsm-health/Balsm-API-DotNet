using Balsm.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Prescription.Infrastructure.Data;

public sealed class PrescriptionDbContext(
    DbContextOptions<PrescriptionDbContext> options,
    IDomainEventDispatcher domainEventDispatcher) : BaseDbContext(options, domainEventDispatcher)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("prescription");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PrescriptionDbContext).Assembly);
    }
}
