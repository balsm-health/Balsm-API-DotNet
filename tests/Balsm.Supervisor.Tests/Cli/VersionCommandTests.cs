using System.Text.Json;
using Balsm.Supervisor.Cli;
using Balsm.Supervisor.Tests.Support;
using FluentAssertions;
using Xunit;

namespace Balsm.Supervisor.Tests.Cli;

[Collection(ProcessStateCollection.Name)]
public class VersionCommandTests
{
    [Fact]
    public async Task RunAsync_Plain_PrintsVersionLine()
    {
        await using var cli = new CliHarness();

        var exitCode = await VersionCommand.RunAsync(json: false);

        exitCode.Should().Be(0);
        cli.Out.Should().StartWith("balsm ");
    }

    [Fact]
    public async Task RunAsync_Json_PrintsEnvelope()
    {
        await using var cli = new CliHarness();

        await VersionCommand.RunAsync(json: true);

        var root = JsonDocument.Parse(cli.Out).RootElement;
        root.GetProperty("status").GetString().Should().Be("ok");
        root.GetProperty("result").GetProperty("version").GetString().Should().NotBeNullOrEmpty();
    }
}
