using Microsoft.EntityFrameworkCore;

namespace Balsam.Infrastructure.Configuration;

public static class DatabaseServiceExtensions
{
    public static DbContextOptionsBuilder ConfigureDatabase(
        this DbContextOptionsBuilder options,
        DatabaseOptions databaseOptions)
    {
        return databaseOptions.Provider.ToLowerInvariant() switch
        {
            "sqlite" => options.UseSqlite(databaseOptions.ConnectionString),
            "postgresql" => options.UseNpgsql(databaseOptions.ConnectionString),
            _ => throw new InvalidOperationException(
                $"Unsupported database provider: {databaseOptions.Provider}. Supported: Sqlite, PostgreSql")
        };
    }
}
