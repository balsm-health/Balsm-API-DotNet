using Balsm.CareDirectory.Infrastructure.Data;
using Balsm.CareDirectory.Infrastructure.Import;
using Balsm.CareDirectory.Infrastructure.MapPacks;
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

        // Flat env-var names, not a nested section — see MapPackR2Options for
        // why they must line up with tools/map-packs/publish.py's own names.
        services.Configure<MapPackR2Options>(o =>
        {
            o.AccountId = configuration["R2_ACCOUNT_ID"] ?? string.Empty;
            o.AccessKeyId = configuration["R2_ACCESS_KEY_ID"] ?? string.Empty;
            o.SecretAccessKey = configuration["R2_SECRET_ACCESS_KEY"] ?? string.Empty;
            o.Bucket = configuration["R2_BUCKET"] ?? string.Empty;
            o.CdnBaseUrl = configuration["MAP_PACKS_CDN_BASE_URL"] ?? string.Empty;
            o.ExportCron = configuration["MAP_PACKS_EXPORT_CRON"] ?? o.ExportCron;
        });
        services.AddSingleton<IMapPackObjectStore, CloudflareR2ObjectStore>();
        services.AddHostedService<MapPackExportJob>();

        return services;
    }
}
