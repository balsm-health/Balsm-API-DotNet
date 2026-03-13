using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Balsam.API.Tests.Controllers;

public class HealthControllerTests
{
    [Fact]
    public void Get_ReturnsOkWithHealthyStatus()
    {
        var controller = new API.Controllers.HealthController();

        var result = controller.Get();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        var statusProperty = value!.GetType().GetProperty("Status")!.GetValue(value);
        statusProperty.Should().Be("Healthy");
    }

    [Fact]
    public void Get_ReturnsTimestamp()
    {
        var before = DateTime.UtcNow;

        var controller = new API.Controllers.HealthController();
        var result = controller.Get();

        var after = DateTime.UtcNow;
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        var timestamp = (DateTime)value!.GetType().GetProperty("Timestamp")!.GetValue(value)!;
        timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}
