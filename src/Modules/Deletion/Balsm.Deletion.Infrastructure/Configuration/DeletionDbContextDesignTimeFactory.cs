using Balsm.Deletion.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Balsm.Deletion.Infrastructure.Configuration;

internal sealed class DeletionDbContextDesignTimeFactory : IDesignTimeDbContextFactory<DeletionDbContext>
{
    public DeletionDbContext CreateDbContext(string[] args)
    {
        // Provider chosen by Database__Provider so `dotnet ef migrations add` can target each
        // provider's migration assembly. Connection strings here are design-time only (no connect).
        var provider = Environment.GetEnvironmentVariable("Database__Provider") ?? "postgresql";
        var builder = new DbContextOptionsBuilder<DeletionDbContext>();
        if (provider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
        {
            builder.UseSqlite(
                "Data Source=design-time.db",
                o => o.MigrationsAssembly("Balsm.Deletion.Infrastructure.Migrations.Sqlite"));
        }
        else
        {
            builder.UseNpgsql("Host=localhost;Database=design;Username=design;Password=design");
        }

        return new DeletionDbContext(builder.Options, new NullDomainEventDispatcher());
    }

    private sealed class NullDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
