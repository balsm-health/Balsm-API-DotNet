using Balsm.Supervisor.Auth;
using Balsm.Supervisor.Configuration;
using Balsm.Supervisor.Services;
using Balsm.Supervisor.Tests.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Balsm.Supervisor.Tests;

public sealed class SupervisorRegistrationTests : IDisposable
{
    private readonly ServiceProvider _provider;

    public SupervisorRegistrationTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Supervisor:ServerId"] = "srv-1",
                ["Supervisor:EnableMdns"] = "false"
            })
            .Build();
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton<IHostApplicationLifetime, FakeHostApplicationLifetime>()
            .AddSingleton<IServer, FakeServer>();

        services.AddSupervisorModule(configuration);

        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public void AddSupervisorModule_BindsSupervisorSection()
    {
        var options = _provider.GetRequiredService<IOptions<SupervisorOptions>>().Value;

        options.Should().Match<SupervisorOptions>(o => o.ServerId == "srv-1" && !o.EnableMdns);
    }

    [Fact]
    public void AddSupervisorModule_PrefersArgon2idForNewHashes()
    {
        var hashers = _provider.GetServices<IPasswordHasher>().Select(h => h.AlgorithmId);

        hashers.Should().BeEquivalentTo("pbkdf2", "argon2id");
    }

    [Fact]
    public void AddSupervisorModule_RegistersHostedServicesAsTheSameSingletons()
    {
        var hosted = _provider.GetServices<IHostedService>().ToList();

        hosted.Should().Contain(_provider.GetRequiredService<MdnsService>())
            .And.Contain(_provider.GetRequiredService<CloudflareTunnelService>())
            .And.Contain(_provider.GetRequiredService<FirstRunService>())
            .And.Contain(_provider.GetRequiredService<SyncService>())
            .And.ContainSingle(s => s is LocalCliTokenService);
    }

    [Fact]
    public void AddSupervisorModule_ResolvesAdminServices()
    {
        var act = () => new object[]
        {
            _provider.GetRequiredService<AdminAuthService>(),
            _provider.GetRequiredService<RecoveryCodeService>(),
            _provider.GetRequiredService<AdminSessionService>(),
            _provider.GetRequiredService<FederationService>(),
            _provider.GetRequiredService<SelfUpdateService>(),
            _provider.GetRequiredService<ConnectionInfoService>()
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void AddSupervisorModule_ConfiguresGitHubClient()
    {
        using var client = _provider.GetRequiredService<IHttpClientFactory>().CreateClient("GitHub");

        client.BaseAddress.Should().Be(new Uri("https://api.github.com"));
        client.DefaultRequestHeaders.UserAgent.ToString().Should().Be("Balsm-Updater");
    }

    [Fact]
    public void AddSupervisorModule_ConfiguresRegistryClient()
    {
        using var client = _provider.GetRequiredService<IHttpClientFactory>().CreateClient("BalsmRegistry");

        client.DefaultRequestHeaders.UserAgent.ToString().Should().Be("Balsm-Supervisor");
    }

    [Fact]
    public void AddSupervisorModule_ConfiguresFederationClientWithTimeout()
    {
        using var client = _provider.GetRequiredService<IHttpClientFactory>().CreateClient("Federation");

        client.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        client.DefaultRequestHeaders.UserAgent.ToString().Should().Be("Balsm-Federation");
    }

    public void Dispose() => _provider.Dispose();
}
