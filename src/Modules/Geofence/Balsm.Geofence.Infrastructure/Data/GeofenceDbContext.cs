using Balsm.Geofence.Domain.Entities;
using Balsm.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Geofence.Infrastructure.Data;

public sealed class GeofenceDbContext(
    DbContextOptions<GeofenceDbContext> options,
    IDomainEventDispatcher domainEventDispatcher) : BaseDbContext(options, domainEventDispatcher)
{
    public DbSet<DeniedCountryBlocklist> DeniedCountries { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GeofenceDbContext).Assembly);
    }
}
