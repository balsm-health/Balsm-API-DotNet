using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Sessions.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSessionsApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly));
        services.AddValidatorsFromAssembly(AssemblyReference.Assembly);
        return services;
    }
}
