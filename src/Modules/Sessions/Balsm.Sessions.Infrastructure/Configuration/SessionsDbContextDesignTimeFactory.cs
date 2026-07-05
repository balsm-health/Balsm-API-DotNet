using Balsm.SharedKernel.Events;
using Balsm.Sessions.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Balsm.Sessions.Infrastructure.Configuration;

internal sealed class SessionsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SessionsDbContext>
{
    public SessionsDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<SessionsDbContext>();
        builder.UseNpgsql("Host=localhost;Database=design;Username=design;Password=design");
        return new SessionsDbContext(builder.Options, new NullDomainEventDispatcher());
    }

    private sealed class NullDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
