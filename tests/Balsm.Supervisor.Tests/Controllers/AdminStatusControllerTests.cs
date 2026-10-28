using Balsm.Supervisor.Controllers;
using Balsm.Supervisor.Models;
using Balsm.Supervisor.Tests.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Balsm.Supervisor.Tests.Controllers;

[Collection(ProcessStateCollection.Name)]
public sealed class AdminStatusControllerTests : IDisposable
{
    private readonly BaseDirectoryFileScope _configFile = new("appsettings.Production.json");
    private readonly BaseDirectoryFileScope _certFile = new("supervisor.pfx");

    [Fact]
    public void Get_ReturnsRunningStatusForThisProcess()
    {
        var sut = new AdminStatusController(SupervisorFactory.CreateStatusService(new FakeHostApplicationLifetime()));

        var result = sut.Get();

        ((ObjectResult)result).Value.Should().BeOfType<ApiStatusResponse>()
            .Which.Should().Match<ApiStatusResponse>(s =>
                s.IsRunning && s.Pid == Environment.ProcessId && s.HttpPort == 5050 && s.HttpsPort == 5051);
    }

    public void Dispose()
    {
        _configFile.Dispose();
        _certFile.Dispose();
    }
}
