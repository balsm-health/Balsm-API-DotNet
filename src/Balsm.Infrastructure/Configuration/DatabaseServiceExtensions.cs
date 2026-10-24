using Balsm.Infrastructure.Audit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Infrastructure.Configuration;

public static class DatabaseServiceExtensions
{
    public static DbContextOptionsBuilder ConfigureDatabase(
        this DbContextOptionsBuilder options,
        DatabaseOptions databaseOptions,
        AuditSaveChangesInterceptor? auditInterceptor = null,
        string? sqliteMigrationsAssembly = null,
        string? npgsqlMigrationsAssembly = null)
    {
        // Migrations are provider-specific: a context that ships both SQLite and Npgsql
        // migration sets keeps each set in its own assembly and names the non-native one
        // here so EF resolves the correct set for the active provider. A null arg falls back
        // to the context's own assembly (correct for single-provider / no-migration contexts).
        DbContextOptionsBuilder configured = databaseOptions.Provider.ToLowerInvariant() switch
        {
            "sqlite" => options.UseSqlite(databaseOptions.ConnectionString, o =>
            {
                o.CommandTimeout(30);
                if (sqliteMigrationsAssembly is not null)
                    o.MigrationsAssembly(sqliteMigrationsAssembly);
            }),
            "postgresql" => options.UseNpgsql(databaseOptions.ConnectionString, o =>
            {
                if (npgsqlMigrationsAssembly is not null)
                    o.MigrationsAssembly(npgsqlMigrationsAssembly);
            }),
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
