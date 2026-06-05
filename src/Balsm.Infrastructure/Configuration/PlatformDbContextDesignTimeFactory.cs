using Balsm.Infrastructure.Platform;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Balsm.Infrastructure.Configuration;

/// <summary>Used only by dotnet-ef tooling at design time.</summary>
internal sealed class PlatformDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PlatformDbContext>();
        optionsBuilder.UseSqlite("Data Source=design-time.db");
        return new PlatformDbContext(optionsBuilder.Options, NullDomainEventDispatcher.Instance);
    }
}

internal sealed class NullDomainEventDispatcher : IDomainEventDispatcher
{
    public static readonly NullDomainEventDispatcher Instance = new();

    public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
