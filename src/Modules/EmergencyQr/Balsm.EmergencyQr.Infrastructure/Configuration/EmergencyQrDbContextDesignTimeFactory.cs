using Balsm.EmergencyQr.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Balsm.EmergencyQr.Infrastructure.Configuration;

internal sealed class EmergencyQrDbContextDesignTimeFactory : IDesignTimeDbContextFactory<EmergencyQrDbContext>
{
    public EmergencyQrDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<EmergencyQrDbContext>();
        builder.UseNpgsql("Host=localhost;Database=design;Username=design;Password=design");
        return new EmergencyQrDbContext(builder.Options, new NullDomainEventDispatcher());
    }

    private sealed class NullDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
