using Balsm.Infrastructure.Audit;
using Balsm.Infrastructure.Backup;
using Balsm.Infrastructure.Configuration;
using Balsm.Infrastructure.Lifecycle;
using Balsm.Infrastructure.Platform;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Balsm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSharedInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        // Readiness gate
        services.AddSingleton<ReadinessGate>();

        // Cold-start timing observer (logs startup duration, warns past budget)
        services.Configure<StartupOptions>(configuration.GetSection(StartupOptions.SectionName));
        services.AddHostedService<StartupTimingObserver>();

        // Audit pipeline
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<AuditSaveChangesInterceptor>();

        // Platform DbContext
        services.AddDbContext<PlatformDbContext>((sp, opts) =>
        {
            var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var interceptor = sp.GetService<AuditSaveChangesInterceptor>();
            opts.ConfigureDatabase(dbOptions, interceptor);
        });

        // Also register PlatformDbContext as DbContext for MigrationRunner discovery
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<PlatformDbContext>());

        // Migration services
        services.AddScoped<MigrationRecoveryService>();
        services.AddHostedService<MigrationRunner>();

        // Backup configuration and services
        services.Configure<BackupOptions>(configuration.GetSection(BackupOptions.SectionName));
        services.AddScoped<IBackupService, SqliteOnlineBackupService>();
        services.AddScoped<RestoreOrchestrator>();
        services.AddScoped<AuditExportSink>();
        services.AddHostedService<BackupScheduler>();
        services.AddHostedService<AuditRetentionJob>();

        return services;
    }
}
