using System.Text.Json;
using System.Text.Json.Serialization;
using Balsm.Infrastructure.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Hosting.Internal;
using Xunit;

namespace Balsm.API.Tests.Configuration;

public class DeploymentEnvironmentTests
{
    private static DeploymentEnvironment From(string name) =>
        new HostingEnvironment { EnvironmentName = name }.GetDeploymentEnvironment();

    [Theory]
    [InlineData("Production", DeploymentEnvironment.Production)]
    [InlineData("Staging", DeploymentEnvironment.Staging)]
    [InlineData("Development", DeploymentEnvironment.Development)]
    [InlineData("staging", DeploymentEnvironment.Staging)]
    public void GetDeploymentEnvironment_KnownName_MapsToItsValue(string name, DeploymentEnvironment expected)
    {
        From(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Stg")]
    [InlineData("Testing")]
    public void GetDeploymentEnvironment_UnknownName_ReadsAsProduction(string name)
    {
        From(name).Should().Be(DeploymentEnvironment.Production);
    }

    [Theory]
    [InlineData(DeploymentEnvironment.Production, "production")]
    [InlineData(DeploymentEnvironment.Staging, "staging")]
    [InlineData(DeploymentEnvironment.Development, "development")]
    public void ToTelemetryName_IsLowercase(DeploymentEnvironment environment, string expected)
    {
        environment.ToTelemetryName().Should().Be(expected);
    }

    [Fact]
    public void Serialize_WithTheApiEnumConverter_IsLowercase()
    {
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };

        JsonSerializer.Serialize(DeploymentEnvironment.Staging, options).Should().Be("\"staging\"");
    }
}
