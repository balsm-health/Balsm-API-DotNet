using Balsm.Prescription.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Prescription.Api;

public static class ModuleRegistration
{
    public static IServiceCollection AddPrescriptionModule(this IServiceCollection services)
    {
        services.AddPrescriptionApplication();
        return services;
    }
}
