using Balsm.CareDirectory.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Balsm.CareDirectory.Infrastructure.Configuration;

internal sealed class CareDirectoryDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CareDirectoryDbContext>
{
    public CareDirectoryDbContext CreateDbContext(string[] args)
    {
        // Care directory is NON-PHI reference data that lives only in the LOCAL
        // SQLite balsm.db — there is no Npgsql/cloud branch. The connection string
        // here is design-time only (no connect) so `dotnet ef migrations add`
        // resolves the SQLite migration assembly.
        var builder = new DbContextOptionsBuilder<CareDirectoryDbContext>();
        builder.UseSqlite(
            "Data Source=design-time.db",
            o => o.MigrationsAssembly("Balsm.CareDirectory.Infrastructure.Migrations.Sqlite"));

        return new CareDirectoryDbContext(builder.Options, new NullDomainEventDispatcher());
    }

    private sealed class NullDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
