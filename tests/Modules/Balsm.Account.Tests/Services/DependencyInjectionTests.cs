using Balsm.Account.Infrastructure;
using Balsm.Account.Infrastructure.Data;
using Balsm.SharedKernel.Contracts;
using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.Account.Tests.Services;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddAccountInfrastructure_SqliteConfig_ResolvesProvisioner()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CloudDatabase:Provider"] = "sqlite",
            ["CloudDatabase:ConnectionString"] = "Data Source=:memory:",
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventDispatcher, NullDomainEventDispatcher>();

        services.AddAccountInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IUserAccountProvisioner>().Should().NotBeNull();
    }

    [Fact]
    public void AddAccountInfrastructure_NoDatabaseSection_RegistersAccountDbContext()
    {
        var services = new ServiceCollection();

        services.AddAccountInfrastructure(new ConfigurationBuilder().Build());

        services.Should().Contain(d => d.ServiceType == typeof(AccountDbContext));
    }

    private sealed class NullDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
