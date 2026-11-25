using Balsm.Identity.Domain;
using Balsm.Identity.Domain.Repositories;
using Balsm.Identity.Infrastructure.Data;
using Balsm.Identity.Infrastructure.FamilyLinks;
using Balsm.Identity.Infrastructure.Repositories;
using Balsm.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Balsm.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbOptions = configuration
            .GetSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>() ?? new DatabaseOptions();

        services.AddDbContext<IdentityDbContext>(options =>
            options.ConfigureDatabase(dbOptions,
                npgsqlMigrationsAssembly: "Balsm.Identity.Infrastructure.Migrations.Npgsql"));

        // Expose as DbContext so MigrationRunner discovers and migrates it on boot.
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<IdentityDbContext>());

        services.AddScoped<IAdminUserMirrorRepository, AdminUserMirrorRepository>();
        services.AddScoped<IIdentityUnitOfWork, IdentityUnitOfWork>();

        // Family linking: handlers touch IdentityDbContext, so they live here
        // (repo convention). IProfileQrOwnerResolver and IAccountDisplayNameReader
        // come from the EmergencyQr and Account modules via Program.cs.
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<FamilyLinkStore>();
        services.AddScoped<FamilyLinkDtoFactory>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        return services;
    }
}
