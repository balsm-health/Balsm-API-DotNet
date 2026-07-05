using Balsm.Disclosure.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Balsm.Disclosure.Infrastructure.Configuration;

internal sealed class DisclosureDbContextDesignTimeFactory : IDesignTimeDbContextFactory<DisclosureDbContext>
{
    public DisclosureDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<DisclosureDbContext>();
        builder.UseNpgsql("Host=localhost;Database=design;Username=design;Password=design");
        return new DisclosureDbContext(builder.Options, new NullDomainEventDispatcher());
    }

    private sealed class NullDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
