using Balsm.Infrastructure.Audit;
using Balsm.Infrastructure.Auth;
using Balsm.Infrastructure.Backup;
using Balsm.Infrastructure.Configuration;
using Balsm.Infrastructure.Encryption;
using Balsm.Infrastructure.Lifecycle;
using Balsm.Infrastructure.Platform;
using Balsm.Infrastructure.RateLimit;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Balsm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSharedInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var databaseOptions = configuration
            .GetSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>() ?? new DatabaseOptions();

        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        // Auth + encryption services (shared across modules)
        services.AddScoped<JwtService>();
        services.AddScoped<OtpService>();
        services.AddScoped<DobEncryptionService>();
        services.AddScoped<GoogleOidcValidator>();
        services.AddScoped<AppleOidcValidator>();
        services.AddHttpClient();

        // Rate-limit counters + HybridCache L2: Redis when configured (Cloud, shared across
        // replicas), in-process otherwise (Standalone). Same switch pattern as Database:Provider.
        var redisConnectionString = configuration["Redis:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
                // Reconnect in background instead of throwing at boot; RedisRateLimitStore
                // fails open while disconnected.
                redisOptions.AbortOnConnectFail = false;
                return ConnectionMultiplexer.Connect(redisOptions);
            });
            services.AddSingleton<IRateLimitStore, RedisRateLimitStore>();
            services.AddStackExchangeRedisCache(o => o.Configuration = redisConnectionString);
        }
        else
        {
            services.AddMemoryCache();
            services.AddSingleton<IRateLimitStore, InMemoryRateLimitStore>();
        }

        services.AddHybridCache();
        services.AddSingleton<OtpRateLimitPolicies>();

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
            // SQLite migrations live in this assembly (native); Npgsql set in the sibling assembly.
            opts.ConfigureDatabase(dbOptions, interceptor,
                npgsqlMigrationsAssembly: "Balsm.Infrastructure.Migrations.Npgsql");
        });

        // Also register PlatformDbContext as DbContext for MigrationRunner discovery
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<PlatformDbContext>());

        // Migration services
        services.AddScoped<MigrationRecoveryService>();
        services.AddHostedService<MigrationRunner>();

        // Backup configuration and services
        services.Configure<BackupOptions>(configuration.GetSection(BackupOptions.SectionName));
        if (databaseOptions.Provider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IBackupService, SqliteOnlineBackupService>();
            services.AddHostedService<BackupScheduler>();
        }
        else
        {
            services.AddScoped<IBackupService, UnsupportedBackupService>();
        }

        services.AddScoped<RestoreOrchestrator>();
        services.AddScoped<AuditExportSink>();
        services.AddHostedService<AuditRetentionJob>();

        return services;
    }
}
