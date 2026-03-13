using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Balsam.Entity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddEntityApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly));
        services.AddValidatorsFromAssembly(AssemblyReference.Assembly);
        return services;
    }
}
