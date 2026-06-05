using Balsm.Infrastructure.Audit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Infrastructure.Configuration;

public static class DatabaseServiceExtensions
{
    public static DbContextOptionsBuilder ConfigureDatabase(
        this DbContextOptionsBuilder options,
        DatabaseOptions databaseOptions,
        AuditSaveChangesInterceptor? auditInterceptor = null)
    {
        DbContextOptionsBuilder configured = databaseOptions.Provider.ToLowerInvariant() switch
        {
            "sqlite" => options.UseSqlite(databaseOptions.ConnectionString,
                o => o.CommandTimeout(30)),
            "postgresql" => options.UseNpgsql(databaseOptions.ConnectionString),
            _ => throw new InvalidOperationException(
                $"Unsupported database provider: {databaseOptions.Provider}. Supported: Sqlite, PostgreSql")
        };

        if (auditInterceptor is not null)
            configured.AddInterceptors(auditInterceptor);

        if (databaseOptions.Provider.ToLowerInvariant() == "sqlite")
        {
            configured.AddInterceptors(new SqlitePragmaInterceptor());
        }

        return configured;
    }
}
