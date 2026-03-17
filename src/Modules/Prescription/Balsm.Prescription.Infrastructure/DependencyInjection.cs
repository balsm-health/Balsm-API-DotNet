using Balsm.Infrastructure.Configuration;
using Balsm.Prescription.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Prescription.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPrescriptionInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbOptions = configuration
            .GetSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>() ?? new DatabaseOptions();

        services.AddDbContext<PrescriptionDbContext>(options =>
            options.ConfigureDatabase(dbOptions));

        return services;
    }
}
