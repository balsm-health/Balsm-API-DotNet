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
        // Provider chosen by Database__Provider so `dotnet ef migrations add` can target each
        // provider's migration assembly. Connection strings here are design-time only (no connect).
        var provider = Environment.GetEnvironmentVariable("Database__Provider") ?? "sqlite";
        var optionsBuilder = new DbContextOptionsBuilder<PlatformDbContext>();
        if (provider.Equals("postgresql", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseNpgsql(
                "Host=localhost;Database=design;Username=design;Password=design",
                o => o.MigrationsAssembly("Balsm.Infrastructure.Migrations.Npgsql"));
        }
        else
        {
            optionsBuilder.UseSqlite("Data Source=design-time.db");
        }

        return new PlatformDbContext(optionsBuilder.Options, NullDomainEventDispatcher.Instance);
    }
}

internal sealed class NullDomainEventDispatcher : IDomainEventDispatcher
{
    public static readonly NullDomainEventDispatcher Instance = new();

    public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
