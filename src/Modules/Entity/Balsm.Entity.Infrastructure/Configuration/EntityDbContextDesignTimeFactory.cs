using Balsm.Entity.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Balsm.Entity.Infrastructure.Configuration;

internal sealed class EntityDbContextDesignTimeFactory : IDesignTimeDbContextFactory<EntityDbContext>
{
    public EntityDbContext CreateDbContext(string[] args)
    {
        // Provider chosen by Database__Provider so `dotnet ef migrations add` can target each
        // provider's migration assembly. Connection strings here are design-time only (no connect).
        var provider = Environment.GetEnvironmentVariable("Database__Provider") ?? "sqlite";
        var builder = new DbContextOptionsBuilder<EntityDbContext>();
        if (provider.Equals("postgresql", StringComparison.OrdinalIgnoreCase))
        {
            builder.UseNpgsql(
                "Host=localhost;Database=design;Username=design;Password=design",
                o => o.MigrationsAssembly("Balsm.Entity.Infrastructure.Migrations.Npgsql"));
        }
        else
        {
            builder.UseSqlite("Data Source=design-time.db");
        }

        return new EntityDbContext(builder.Options, new NullDomainEventDispatcher());
    }

    private sealed class NullDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
