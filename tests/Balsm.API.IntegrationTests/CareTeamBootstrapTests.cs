using Balsm.CareTeam.Infrastructure.Data;
using Balsm.Infrastructure.Encryption;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// Regressions for the two review findings that made the module non-functional in
/// every deployed environment while all 44 unit tests stayed green: the unit tests
/// build their own in-memory context and their own key, so neither the migration
/// wiring nor the configuration was ever exercised.
/// </summary>
[Collection(nameof(LiteWebAppCollection))]
public sealed class CareTeamBootstrapTests(LiteWebAppFactory factory)
{
    [Fact]
    public async Task CareTeamTablesAreMigratedOnBoot()
    {
        // C1: MigrationRunner discovers contexts via GetServices<DbContext>(). Without
        // the AddScoped<DbContext> bridge the context is resolvable but never migrated,
        // so the first real write 500s with "relation care_provider does not exist".
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareTeamDbContext>();

        await db.CareProviders.AnyAsync();
        await db.CareTeamAuditLogs.AnyAsync();
    }

    [Fact]
    public void CareTeamDbContextIsRegisteredForMigrationDiscovery()
    {
        using var scope = factory.Services.CreateScope();

        var contexts = scope.ServiceProvider.GetServices<DbContext>();

        contexts.Should().Contain(c => c is CareTeamDbContext,
            "MigrationRunner only migrates contexts resolvable as DbContext");
    }

    [Fact]
    public void EncryptionServiceResolvesWithAConfiguredKey()
    {
        // C2: the service throws in its constructor when CareTeamEncryption:Key is
        // absent. AddScoped defers that to the first care-team request, so a missing
        // key is a runtime 500 rather than a boot failure.
        using var scope = factory.Services.CreateScope();

        var act = () => scope.ServiceProvider.GetRequiredService<CareTeamEncryptionService>();

        act.Should().NotThrow();
    }
}
