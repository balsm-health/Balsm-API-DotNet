using Balsm.API.Controllers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Balsm.API.Tests.Controllers;

public sealed class VersionControllerTests
{
    [Fact]
    public void Get_ModeConfigured_ReportsIt()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DeploymentMode"] = "Cloud" })
            .Build();

        var body = ActionResultReader.Ok(new VersionController(config).Get());

        body.GetProperty("DeploymentMode").GetString().Should().Be("Cloud");
    }

    [Fact]
    public void Get_ModeNotConfigured_ReportsUnknown()
    {
        var body = ActionResultReader.Ok(new VersionController(new ConfigurationBuilder().Build()).Get());

        body.GetProperty("DeploymentMode").GetString().Should().Be("Unknown");
    }

    [Fact]
    public void Get_ReportsTheAssemblyVersion()
    {
        var body = ActionResultReader.Ok(new VersionController(new ConfigurationBuilder().Build()).Get());

        body.GetProperty("Version").GetString()
            .Should().Be(typeof(VersionController).Assembly.GetName().Version!.ToString());
    }
}
