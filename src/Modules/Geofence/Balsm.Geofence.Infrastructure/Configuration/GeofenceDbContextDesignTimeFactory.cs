using Balsm.Geofence.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Balsm.Geofence.Infrastructure.Configuration;

internal sealed class GeofenceDbContextDesignTimeFactory : IDesignTimeDbContextFactory<GeofenceDbContext>
{
    public GeofenceDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<GeofenceDbContext>();
        builder.UseNpgsql("Host=localhost;Database=design;Username=design;Password=design");
        return new GeofenceDbContext(builder.Options, new NullDomainEventDispatcher());
    }

    private sealed class NullDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
