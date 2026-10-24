using Balsm.Infrastructure;
using Balsm.Infrastructure.RateLimit;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace Balsm.Auth.Tests;

/// <summary>
/// Composition switch: Redis:ConnectionString present ⇒ Redis-backed rate-limit store
/// (Cloud), absent ⇒ in-process store (Standalone). HybridCache registered in both modes.
/// Asserts service descriptors only — no provider build, no live Redis connection.
/// </summary>
public sealed class SharedInfrastructureCacheRegistrationTests
{
    private static ServiceCollection Register(Dictionary<string, string?>? settings = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? [])
            .Build();
        var services = new ServiceCollection();
        services.AddSharedInfrastructure(config);
        return services;
    }

    [Fact]
    public void AddSharedInfrastructure_WithoutRedis_RegistersInMemoryStore()
    {
        var services = Register();

        var store = services.Single(d => d.ServiceType == typeof(IRateLimitStore));
        store.ImplementationType.Should().Be(typeof(InMemoryRateLimitStore));
        services.Should().NotContain(d => d.ServiceType == typeof(IConnectionMultiplexer),
            "Standalone must not require a Redis connection");
    }

    [Fact]
    public void AddSharedInfrastructure_WithRedis_RegistersRedisStore()
    {
        var services = Register(new Dictionary<string, string?>
        {
            ["Redis:ConnectionString"] = "localhost:6379"
        });

        var store = services.Single(d => d.ServiceType == typeof(IRateLimitStore));
        store.ImplementationType.Should().Be(typeof(RedisRateLimitStore));
        services.Should().Contain(d => d.ServiceType == typeof(IConnectionMultiplexer));
    }

    [Fact]
    public void AddSharedInfrastructure_RegistersHybridCache_InBothModes()
    {
        Register().Should().Contain(d => d.ServiceType == typeof(HybridCache),
            "Standalone gets L1-only HybridCache");

        Register(new Dictionary<string, string?> { ["Redis:ConnectionString"] = "localhost:6379" })
            .Should().Contain(d => d.ServiceType == typeof(HybridCache),
                "Cloud gets HybridCache with Redis L2");
    }
}
