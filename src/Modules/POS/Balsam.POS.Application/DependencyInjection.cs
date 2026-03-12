using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Balsam.POS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPOSApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly));
        services.AddValidatorsFromAssembly(AssemblyReference.Assembly);
        return services;
    }
}
