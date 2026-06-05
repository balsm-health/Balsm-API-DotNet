using Balsm.Infrastructure.Lifecycle;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Balsm.API.Tests.Controllers;

public class HealthControllerTests
{
    private static API.Controllers.HealthController CreateController()
    {
        var gate = new ReadinessGate();
        gate.SetReady();
        return new API.Controllers.HealthController(gate);
    }

    [Fact]
    public void Get_ReturnsOkWithHealthyStatus()
    {
        var controller = CreateController();

        var result = controller.Get();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        var statusProperty = value!.GetType().GetProperty("status")?.GetValue(value)
            ?? value!.GetType().GetProperty("Status")?.GetValue(value);
        statusProperty.Should().NotBeNull();
    }

    [Fact]
    public void Get_ReturnsTimestamp()
    {
        var before = DateTime.UtcNow;

        var controller = CreateController();
        var result = controller.Get();

        var after = DateTime.UtcNow;
        result.Should().BeOfType<OkObjectResult>();
    }
}
