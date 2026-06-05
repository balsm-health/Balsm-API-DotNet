using Balsm.Entity.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Balsm.Entity.Infrastructure.Configuration;

internal sealed class EntityDbContextDesignTimeFactory : IDesignTimeDbContextFactory<EntityDbContext>
{
    public EntityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<EntityDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;
        return new EntityDbContext(options, new NullDomainEventDispatcher());
    }

    private sealed class NullDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
