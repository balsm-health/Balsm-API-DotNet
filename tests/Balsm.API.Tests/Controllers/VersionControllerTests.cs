using Balsm.API.Controllers;
using Balsm.Infrastructure.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.Internal;
using Xunit;

namespace Balsm.API.Tests.Controllers;

public sealed class VersionControllerTests
{
    private static VersionController Controller(IConfiguration? config = null, string environment = "Production") =>
        new(config ?? new ConfigurationBuilder().Build(), new HostingEnvironment { EnvironmentName = environment });

    [Fact]
    public void Get_ModeConfigured_ReportsIt()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DeploymentMode"] = "Cloud" })
            .Build();

        var body = ActionResultReader.Ok(Controller(config).Get());

        body.GetProperty("DeploymentMode").GetString().Should().Be("Cloud");
    }

    [Fact]
    public void Get_ModeNotConfigured_ReportsUnknown()
    {
        var body = ActionResultReader.Ok(Controller().Get());

        body.GetProperty("DeploymentMode").GetString().Should().Be("Unknown");
    }

    [Fact]
    public void Get_ReportsTheAssemblyVersion()
    {
        var body = ActionResultReader.Ok(Controller().Get());

        body.GetProperty("Version").GetString()
            .Should().Be(typeof(VersionController).Assembly.GetName().Version!.ToString());
    }

    [Theory]
    [InlineData("Staging", DeploymentEnvironment.Staging)]
    [InlineData("Development", DeploymentEnvironment.Development)]
    [InlineData("Production", DeploymentEnvironment.Production)]
    [InlineData("Testing", DeploymentEnvironment.Production)]
    public void Get_ReportsTheHostEnvironment(string environment, DeploymentEnvironment expected)
    {
        var body = ActionResultReader.Ok(Controller(environment: environment).Get());

        body.GetProperty("Environment").GetInt32().Should().Be((int)expected);
    }
}
