using Balsm.Identity.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Balsm.Identity.Infrastructure.Configuration;

internal sealed class IdentityDbContextDesignTimeFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        // Provider chosen by Database__Provider so `dotnet ef migrations add` can target each
        // provider's migration assembly. Connection strings here are design-time only (no connect).
        var provider = Environment.GetEnvironmentVariable("Database__Provider") ?? "sqlite";
        var builder = new DbContextOptionsBuilder<IdentityDbContext>();
        if (provider.Equals("postgresql", StringComparison.OrdinalIgnoreCase))
        {
            builder.UseNpgsql(
                "Host=localhost;Database=design;Username=design;Password=design",
                o => o.MigrationsAssembly("Balsm.Identity.Infrastructure.Migrations.Npgsql"));
        }
        else
        {
            builder.UseSqlite("Data Source=design-time.db");
        }

        return new IdentityDbContext(builder.Options, new NullDomainEventDispatcher());
    }

    private sealed class NullDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
