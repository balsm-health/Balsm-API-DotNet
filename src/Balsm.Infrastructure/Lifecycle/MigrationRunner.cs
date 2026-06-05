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
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

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
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Migration failure — server remains NOT ready");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
