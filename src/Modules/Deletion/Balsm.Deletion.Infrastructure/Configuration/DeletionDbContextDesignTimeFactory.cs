using Balsm.Deletion.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Balsm.Deletion.Infrastructure.Configuration;

internal sealed class DeletionDbContextDesignTimeFactory : IDesignTimeDbContextFactory<DeletionDbContext>
{
    public DeletionDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<DeletionDbContext>();
        builder.UseNpgsql("Host=localhost;Database=design;Username=design;Password=design");
        return new DeletionDbContext(builder.Options, new NullDomainEventDispatcher());
    }

    private sealed class NullDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
