using Balsm.Identity.Domain;
using Balsm.Identity.Domain.Repositories;
using Balsm.Identity.Infrastructure.Data;
using Balsm.Identity.Infrastructure.Repositories;
using Balsm.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            options.ConfigureDatabase(dbOptions));

        // Expose as DbContext so MigrationRunner discovers and migrates it on boot.
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<IdentityDbContext>());

        services.AddScoped<IAdminUserMirrorRepository, AdminUserMirrorRepository>();
        services.AddScoped<IIdentityUnitOfWork, IdentityUnitOfWork>();

        return services;
    }
}
