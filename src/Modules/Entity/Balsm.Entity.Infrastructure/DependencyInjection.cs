using Balsm.Infrastructure.Configuration;
using Balsm.Entity.Domain;
using Balsm.Entity.Domain.Repositories;
using Balsm.Entity.Infrastructure.Data;
using Balsm.Entity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Entity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEntityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbOptions = configuration
            .GetSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>() ?? new DatabaseOptions();

        services.AddDbContext<EntityDbContext>(options =>
            options.ConfigureDatabase(dbOptions,
                npgsqlMigrationsAssembly: "Balsm.Entity.Infrastructure.Migrations.Npgsql"));

        // register EntityDbContext as DbContext so MigrationRunner can discover it
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<EntityDbContext>());

        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IEntityRepository, EntityRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IEntityTypeRepository, EntityTypeRepository>();
        services.AddScoped<IEntityUnitOfWork, EntityUnitOfWork>();

        return services;
    }
}
