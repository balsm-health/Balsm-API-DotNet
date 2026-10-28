using Balsm.Identity.Application;
using Balsm.Identity.Domain;
using Balsm.Identity.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Identity.Tests;

/// <summary>
/// The Identity handlers are internal; they are resolved through MediatR as registered by
/// <c>AddIdentityApplication</c>, with <see cref="FakeIdentityStore"/> behind the repository
/// and unit-of-work interfaces.
/// </summary>
internal static class HandlerHost
{
    public static ServiceProvider Build(FakeIdentityStore store)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityApplication();
        services.AddSingleton<IAdminUserMirrorRepository>(store);
        services.AddSingleton<IIdentityUnitOfWork>(store);
        return services.BuildServiceProvider();
    }

    public static ISender Sender(this ServiceProvider provider) => provider.GetRequiredService<ISender>();
}
