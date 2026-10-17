using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Deletion.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddDeletionApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly));
        services.AddValidatorsFromAssembly(AssemblyReference.Assembly);
        return services;
    }
}
