using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Disclosure.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddDisclosureApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly));
        services.AddValidatorsFromAssembly(AssemblyReference.Assembly);
        return services;
    }
}
