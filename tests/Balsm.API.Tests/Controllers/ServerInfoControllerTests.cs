using Balsm.API.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Balsm.API.Tests.Controllers;

public class ServerInfoControllerTests
{
    private static object GetPayload()
    {
        var result = new ServerInfoController().Get();
        return result.Should().BeOfType<OkObjectResult>().Subject.Value!;
    }

    private static object? Prop(object value, string name)
        => value.GetType().GetProperty(name)?.GetValue(value);

    [Fact]
    public void Get_ReturnsVersionModeAndUptime()
    {
        var payload = GetPayload();

        Prop(payload, "version").Should().NotBeNull().And.BeOfType<string>();
        Prop(payload, "mode").Should().NotBeNull();
        Prop(payload, "uptime_seconds").Should().BeOfType<long>();
    }

    [Fact]
    public void Get_DefaultsToStandaloneMode()
    {
        Environment.SetEnvironmentVariable("DeploymentMode", null);

        var payload = GetPayload();

        Prop(payload, "mode").Should().Be("Standalone");
    }

    [Fact]
    public void Get_UptimeIsNonNegative()
    {
        var payload = GetPayload();

        ((long)Prop(payload, "uptime_seconds")!).Should().BeGreaterThanOrEqualTo(0);
    }
}
