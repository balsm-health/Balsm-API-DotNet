using Balsm.API.Controllers;
using FluentAssertions;
using Xunit;

namespace Balsm.API.Tests.Controllers;

public sealed class DummyControllerTests
{
    [Fact]
    public void Get_ReportsTheEndpointIsWorking()
    {
        var body = ActionResultReader.Ok(new DummyController().Get());

        body.GetProperty("Message").GetString().Should().Be("Dummy endpoint is working");
    }

    [Fact]
    public void GetById_EchoesTheId()
    {
        var body = ActionResultReader.Ok(new DummyController().GetById(7));

        body.GetProperty("Name").GetString().Should().Be("Dummy Item 7");
    }
}
