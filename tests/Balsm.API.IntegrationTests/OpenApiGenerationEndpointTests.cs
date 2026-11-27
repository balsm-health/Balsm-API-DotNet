using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// The stripped host that scripts/generate-openapi.sh boots to emit the specs.
/// </summary>
[Collection(nameof(OpenApiGenerationWebAppCollection))]
public sealed class OpenApiGenerationEndpointTests(OpenApiGenerationWebAppFactory factory)
{
    [Fact]
    public async Task OpenApiDocument_ServesTheAggregateSpec()
    {
        var response = await factory.CreateClient().GetAsync("/openapi/v1/all.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void HostedServices_SharedInfrastructureJobsAreNotRegistered()
    {
        var hosted = factory.Services.GetServices<IHostedService>();

        hosted.Select(s => s.GetType().Namespace ?? string.Empty)
            .Should().NotContain(ns => ns.StartsWith("Balsm.Infrastructure."));
    }
}
