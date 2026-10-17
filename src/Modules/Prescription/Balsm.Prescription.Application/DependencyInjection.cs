using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Prescription.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPrescriptionApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly));
        services.AddValidatorsFromAssembly(AssemblyReference.Assembly);
        return services;
    }
}
