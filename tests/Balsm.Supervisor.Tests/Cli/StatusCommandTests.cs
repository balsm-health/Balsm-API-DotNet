using System.Text.Json;
using Balsm.Supervisor.Cli;
using Balsm.Supervisor.Tests.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Balsm.Supervisor.Tests.Cli;

[Collection(ProcessStateCollection.Name)]
public class StatusCommandTests
{
    private const string StatusJson =
        "{\"isRunning\":true,\"mode\":\"network\",\"version\":\"1.2.3\",\"pid\":4242,\"httpPort\":6060}";

    [Fact]
    public async Task RunAsync_WithoutTokenFile_Returns4()
    {
        await using var cli = new CliHarness();

        var exitCode = await StatusCommand.RunAsync(json: false);

        exitCode.Should().Be(4);
    }

    [Fact]
    public async Task RunAsync_WhenServerHealthy_PrintsSummaryUsingLoopbackToken()
    {
        await using var cli = new CliHarness();
        cli.WriteToken();
        await cli.StartServerAsync(ctx => ctx.Response.WriteAsync(StatusJson));

        var exitCode = await StatusCommand.RunAsync(json: false);

        exitCode.Should().Be(0);
        cli.RequestedPathsAndQueries.Should().Equal("/api/v1/admin/status");
        cli.Out.Should().Contain("status   : running")
            .And.Contain("version  : 1.2.3")
            .And.Contain("mode     : network")
            .And.Contain("pid      : 4242")
            .And.Contain("http://127.0.0.1:6060/admin");
    }

    [Fact]
    public async Task RunAsync_WhenFieldsMissing_PrintsPlaceholders()
    {
        await using var cli = new CliHarness();
        cli.WriteToken();
        await cli.StartServerAsync(ctx => ctx.Response.WriteAsync("{}"));

        await StatusCommand.RunAsync(json: false);

        cli.Out.Should().Contain("status   : stopped").And.Contain("pid      : ?");
    }

    [Fact]
    public async Task RunAsync_Json_WrapsServerResponseInEnvelope()
    {
        await using var cli = new CliHarness();
        cli.WriteToken();
        await cli.StartServerAsync(ctx => ctx.Response.WriteAsync(StatusJson));

        var exitCode = await StatusCommand.RunAsync(json: true);

        exitCode.Should().Be(0);
        var root = JsonDocument.Parse(cli.Out).RootElement;
        root.GetProperty("command").GetString().Should().Be("status");
        root.GetProperty("result").GetProperty("pid").GetInt32().Should().Be(4242);
    }

    [Fact]
    public async Task RunAsync_WhenServerRejects_Returns5()
    {
        await using var cli = new CliHarness();
        cli.WriteToken();
        await cli.StartServerAsync(ctx =>
        {
            ctx.Response.StatusCode = 500;
            return Task.CompletedTask;
        });

        var exitCode = await StatusCommand.RunAsync(json: false);

        exitCode.Should().Be(5);
        cli.Error.Should().Contain("Server responded 500");
    }

    [Fact]
    public async Task RunAsync_WhenServerUnreachable_Returns4()
    {
        await using var cli = new CliHarness();
        cli.WriteToken();
        cli.UseClosedPort();

        var exitCode = await StatusCommand.RunAsync(json: false);

        exitCode.Should().Be(4);
        cli.Error.Should().Contain("Cannot reach the server");
    }
}
