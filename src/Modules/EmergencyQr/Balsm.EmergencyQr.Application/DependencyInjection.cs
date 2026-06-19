using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.EmergencyQr.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddEmergencyQrApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly));
        services.AddValidatorsFromAssembly(AssemblyReference.Assembly);
        return services;
    }
}
