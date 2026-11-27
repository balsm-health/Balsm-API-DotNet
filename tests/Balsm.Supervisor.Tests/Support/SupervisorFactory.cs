using Balsm.Supervisor.Auth;
using Balsm.Supervisor.Configuration;
using Balsm.Supervisor.Services;
using Balsm.Supervisor.Tests.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Balsm.Supervisor.Tests.Support;

internal static class SupervisorFactory
{
    public static ServerStatusService CreateStatusService(
        FakeHostApplicationLifetime lifetime,
        IDictionary<string, string?>? configuration = null,
        SupervisorOptions? options = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configuration ?? new Dictionary<string, string?>())
            .Build();
        var scopeFactory = new ServiceCollection().BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();

        return new ServerStatusService(
            Options.Create(options ?? new SupervisorOptions()),
            config,
            lifetime,
            scopeFactory,
            NullLogger<ServerStatusService>.Instance);
    }

    public static AdminAuthService CreateAuthService(ICredentialStore store)
        => new(store, [new Pbkdf2Hasher()], NullLogger<AdminAuthService>.Instance);

    public static InMemoryCredentialStore CreateStore() => new();

    public static ControllerContext ControllerContextFor(string host = "localhost", int? port = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = port is null ? new HostString(host) : new HostString(host, port.Value);
        return new ControllerContext { HttpContext = context };
    }
}
