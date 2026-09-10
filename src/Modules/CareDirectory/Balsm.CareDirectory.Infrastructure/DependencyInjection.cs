using Balsm.CareDirectory.Infrastructure.Data;
using Balsm.CareDirectory.Infrastructure.Import;
using Balsm.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.CareDirectory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCareDirectoryInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // NON-PHI public directory data lives in the LOCAL SQLite balsm.db, so bind
        // the "Database" section (DatabaseOptions.SectionName) — NOT "CloudDatabase".
        var dbOptions = configuration
            .GetSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>() ?? new DatabaseOptions();

        services.AddDbContext<CareDirectoryDbContext>(options =>
            options.ConfigureDatabase(dbOptions, sqliteMigrationsAssembly: "Balsm.CareDirectory.Infrastructure.Migrations.Sqlite"));

        // Register as DbContext too so the host MigrationRunner auto-migrates it.
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<CareDirectoryDbContext>());
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        // Directory rows come from the import artifact, never from a HasData seed.
        // Registered after the host's MigrationRunner so the schema exists first —
        // hosted services start in registration order.
        services.Configure<CareDirectoryOptions>(configuration.GetSection(CareDirectoryOptions.SectionName));
        services.AddHostedService<CareDirectoryImportService>();

        return services;
    }
}
