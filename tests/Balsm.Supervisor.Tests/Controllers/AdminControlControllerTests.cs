using Balsm.Supervisor.Controllers;
using Balsm.Supervisor.Tests.Support;
using FluentAssertions;
using Xunit;
using static Balsm.Supervisor.Tests.Support.ActionResultReader;

namespace Balsm.Supervisor.Tests.Controllers;

public class AdminControlControllerTests
{
    [Fact]
    public void Restart_SignalsHostToStop()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var sut = new AdminControlController(SupervisorFactory.CreateStatusService(lifetime));

        var result = sut.Restart();

        StatusOf(result).Should().Be(200);
        lifetime.StopRequests.Should().Be(1);
    }
}
