using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.CareDirectory.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCareDirectoryApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly));
        services.AddValidatorsFromAssembly(AssemblyReference.Assembly);
        return services;
    }
}
