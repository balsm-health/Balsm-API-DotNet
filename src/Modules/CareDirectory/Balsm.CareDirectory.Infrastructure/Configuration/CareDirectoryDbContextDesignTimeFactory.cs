using Balsm.CareDirectory.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Balsm.CareDirectory.Infrastructure.Configuration;

internal sealed class CareDirectoryDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CareDirectoryDbContext>
{
    public CareDirectoryDbContext CreateDbContext(string[] args)
    {
        // The local "Database" section is Sqlite (embedded/self-hosted) or
        // PostgreSql (dev/cloud), so the module ships BOTH migration sets. Provider
        // is chosen by Database__Provider so `dotnet ef migrations add` targets each
        // provider's migration assembly. Connection strings are design-time only.
        var builder = new DbContextOptionsBuilder<CareDirectoryDbContext>();
        var provider = Environment.GetEnvironmentVariable("Database__Provider") ?? "postgresql";

        if (provider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
        {
            builder.UseSqlite(
                "Data Source=design-time.db",
                o => o.MigrationsAssembly("Balsm.CareDirectory.Infrastructure.Migrations.Sqlite"));
        }
        else
        {
            // Npgsql migrations live inline in this Infrastructure assembly (the
            // context's own assembly — the DI layer passes no npgsqlMigrationsAssembly).
            builder.UseNpgsql("Host=localhost;Database=design;Username=design;Password=design");
        }

        return new CareDirectoryDbContext(builder.Options, new NullDomainEventDispatcher());
    }

    private sealed class NullDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
