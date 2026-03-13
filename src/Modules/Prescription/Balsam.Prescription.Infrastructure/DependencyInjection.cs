using Balsam.Infrastructure.Configuration;
using Balsam.Prescription.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Balsam.Prescription.Infrastructure;

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
