using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Customer.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCustomerApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly));
        services.AddValidatorsFromAssembly(AssemblyReference.Assembly);
        return services;
    }
}
