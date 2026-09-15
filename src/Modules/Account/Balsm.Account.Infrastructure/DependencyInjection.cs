using Balsm.Account.Infrastructure.Data;
using Balsm.Account.Infrastructure.Services;
using Balsm.SharedKernel.Contracts;
using Balsm.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Account.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAccountInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbOptions = configuration
            .GetSection("CloudDatabase")
            .Get<DatabaseOptions>() ?? new DatabaseOptions { Provider = "postgresql", ConnectionString = string.Empty };

        services.AddDbContext<AccountDbContext>(options =>
            options.ConfigureDatabase(dbOptions, sqliteMigrationsAssembly: "Balsm.Account.Infrastructure.Migrations.Sqlite"));

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<AccountDbContext>());
        // Published interface consumed by Auth (module-boundary rule: no
        // cross-module project references).
        services.AddScoped<IUserAccountProvisioner, UserAccountProvisioner>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        return services;
    }
}
