using Balsm.Supervisor.Controllers;
using Balsm.Supervisor.Tests.Support;
using FluentAssertions;
using Xunit;
using static Balsm.Supervisor.Tests.Support.ActionResultReader;

namespace Balsm.Supervisor.Tests.Controllers;

[Collection(ProcessStateCollection.Name)]
public sealed class ConnectControllerTests : IDisposable
{
    private readonly BaseDirectoryFileScope _configFile = new("appsettings.Production.json");
    private readonly BaseDirectoryFileScope _certFile = new("supervisor.pfx");

    [Fact]
    public void Get_ReturnsApiUrlForRequestHost()
    {
        var sut = new ConnectController(SupervisorFactory.CreateStatusService(new FakeHostApplicationLifetime()))
        {
            ControllerContext = SupervisorFactory.ControllerContextFor("balsm.local", 5050)
        };

        var body = BodyOf(sut.Get());

        body.GetProperty("apiUrl").GetString().Should().Be("http://balsm.local:5050/api/v1");
    }

    public void Dispose()
    {
        _configFile.Dispose();
        _certFile.Dispose();
    }
}
