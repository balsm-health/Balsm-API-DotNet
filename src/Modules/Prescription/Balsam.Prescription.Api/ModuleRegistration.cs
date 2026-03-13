using Balsam.Prescription.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Balsam.Prescription.Api;

public static class ModuleRegistration
{
    public static IServiceCollection AddPrescriptionModule(this IServiceCollection services)
    {
        services.AddPrescriptionApplication();
        return services;
    }
}
