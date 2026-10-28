using System.Net;
using Balsm.Sessions.Infrastructure.Jobs;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// <c>GET /status</c> over real HTTP. The controller reports whatever the
/// background probe last cached, so each test writes that cache entry itself.
/// </summary>
[Collection(nameof(LiteWebAppCollection))]
public sealed class StatusEndpointTests(LiteWebAppFactory factory)
{
    private static readonly DateTime CheckedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Get_WhenProbeOperational_Returns200()
    {
        Cache.Set(StatusHealthJob.CacheKey, new HealthStatus("operational", CheckedAt));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_WhenProbeDegraded_Returns503()
    {
        Cache.Set(StatusHealthJob.CacheKey, new HealthStatus("degraded", CheckedAt));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/status");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Get_BeforeTheFirstProbe_ReportsStarting()
    {
        Cache.Remove(StatusHealthJob.CacheKey);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/status");

        (await TestAuth.ReadDataAsync(response)).GetProperty("status").GetString().Should().Be("starting");
    }

    private IMemoryCache Cache => factory.Services.GetRequiredService<IMemoryCache>();
}
