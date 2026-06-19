using Balsm.Disclosure.Infrastructure.Data;
using Balsm.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Disclosure.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDisclosureInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbOptions = configuration
            .GetSection("CloudDatabase")
            .Get<DatabaseOptions>() ?? new DatabaseOptions { Provider = "postgresql", ConnectionString = string.Empty };

        services.AddDbContext<DisclosureDbContext>(options =>
            options.ConfigureDatabase(dbOptions));

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<DisclosureDbContext>());
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        return services;
    }
}
