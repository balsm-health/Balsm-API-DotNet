using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Balsm.Infrastructure.Lifecycle;

public sealed class MigrationRunner(
    IServiceScopeFactory scopeFactory,
    ReadinessGate gate,
    ILogger<MigrationRunner> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        gate.SetNotReady("migration");
        string? endpoint = null;
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            // Captured before any I/O so an unreachable database can still say
            // WHICH database. Reads the connection string only — no connection.
            endpoint = DatabaseEndpoint.Describe(
                scope.ServiceProvider.GetRequiredService<Platform.PlatformDbContext>());

            var recoveryService = scope.ServiceProvider.GetRequiredService<MigrationRecoveryService>();
            await recoveryService.RunAsync(cancellationToken).ConfigureAwait(false);

            var contexts = scope.ServiceProvider.GetServices<DbContext>().ToList();

            foreach (var ctx in contexts)
            {
                var name = ctx.GetType().Name;
                logger.LogInformation("Applying migrations for {DbContext}", name);
                await ctx.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                logger.LogInformation("Migrations applied for {DbContext}", name);
            }

            gate.SetReady();
            logger.LogInformation("All migrations complete — server is ready");
        }
        catch (Exception ex) when (DatabaseConnectionFailedException.IsConnectionFailure(ex))
        {
            // Nothing is wrong with the schema — the database is not there. Own
            // reason and own exception so the operator is told to start their
            // database instead of going to read migration code.
            gate.SetNotReady("database-unreachable");
            logger.LogCritical(
                ex, "Database unreachable at {Endpoint} — aborting startup", endpoint ?? "(unknown)");
            throw new DatabaseConnectionFailedException(endpoint, ex);
        }
        catch (Exception ex)
        {
            // Do not stay up in this state. Leaving the gate on "migration" made a
            // failed migration indistinguishable from one still running, so the
            // process kept answering 503 to every route indefinitely while still
            // reporting "Healthy" — a zombie nobody notices.
            //
            // Throw rather than StopApplication(): this runs as an IHostedService
            // registered ahead of the web host, so cancelling the host here would
            // cancel Kestrel mid-BindAsync and surface a TaskCanceledException
            // that hides the real cause. Throwing aborts startup before Kestrel
            // binds, and the cause travels with the exception.
            gate.SetNotReady("migration-failed");
            logger.LogCritical(ex, "Migration failure — aborting startup");
            throw new MigrationFailedException(ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
