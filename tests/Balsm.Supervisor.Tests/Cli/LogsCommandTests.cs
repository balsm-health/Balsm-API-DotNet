using System.Text.Json;
using Balsm.Supervisor.Cli;
using Balsm.Supervisor.Tests.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Balsm.Supervisor.Tests.Cli;

[Collection(ProcessStateCollection.Name)]
public class LogsCommandTests
{
    private const string FilesJson = """
        {
          "directory": "/var/log/balsm",
          "items": [
            { "name": "balsm-20250101.log", "sizeBytes": 512, "lastModified": "2025-01-01T00:00:00Z" },
            { "name": "balsm-20250102.log", "sizeBytes": 3145728, "lastModified": "2025-01-02T08:30:00Z" }
          ]
        }
        """;

    [Fact]
    public async Task RunAsync_WithoutTokenFile_Returns4()
    {
        await using var cli = new CliHarness();

        var exitCode = await LogsCommand.RunAsync([], json: false);

        exitCode.Should().Be(4);
    }

    [Fact]
    public async Task RunAsync_List_PrintsFilesWithHumanSizes()
    {
        await using var cli = new CliHarness();
        cli.WriteToken();
        await cli.StartServerAsync(ctx => ctx.Response.WriteAsync(FilesJson));

        var exitCode = await LogsCommand.RunAsync(["--list"], json: false);

        exitCode.Should().Be(0);
        cli.RequestedPathsAndQueries.Should().Equal("/api/v1/admin/logs/files");
        cli.Out.Should().Contain("directory : /var/log/balsm")
            .And.Contain("512 B  balsm-20250101.log")
            .And.Contain("3 MB  balsm-20250102.log");
    }

    [Fact]
    public async Task RunAsync_ListWithoutDirectory_PrintsPlaceholder()
    {
        await using var cli = new CliHarness();
        cli.WriteToken();
        await cli.StartServerAsync(ctx => ctx.Response.WriteAsync("{\"items\":[]}"));

        await LogsCommand.RunAsync(["--list"], json: false);

        cli.Out.Should().Contain("directory : ?");
    }

    [Fact]
    public async Task RunAsync_ListJson_PrintsServerBodyVerbatim()
    {
        await using var cli = new CliHarness();
        cli.WriteToken();
        await cli.StartServerAsync(ctx => ctx.Response.WriteAsync(FilesJson));

        await LogsCommand.RunAsync(["--list", "--json"], json: true);

        cli.Out.Trim().Should().Be(FilesJson.Trim());
    }

    [Fact]
    public async Task RunAsync_Tail_DefaultsTo200Lines()
    {
        await using var cli = new CliHarness();
        cli.WriteToken();
        await cli.StartServerAsync(ctx => ctx.Response.WriteAsync("first line\nsecond line"));

        var exitCode = await LogsCommand.RunAsync([], json: false);

        exitCode.Should().Be(0);
        cli.RequestedPathsAndQueries.Should().Equal("/api/v1/admin/logs/tail?lines=200");
        cli.Out.Should().Contain("first line\nsecond line");
    }

    [Fact]
    public async Task RunAsync_TailWithFlags_PassesLinesAndEscapedFile()
    {
        await using var cli = new CliHarness();
        cli.WriteToken();
        await cli.StartServerAsync(ctx => ctx.Response.WriteAsync("ok"));

        await LogsCommand.RunAsync(["--lines", "50", "--file", "balsm 1.log"], json: false);

        cli.RequestedPathsAndQueries.Should().Equal("/api/v1/admin/logs/tail?lines=50&file=balsm%201.log");
    }

    [Theory]
    [InlineData("--lines", "many")]
    [InlineData("--lines")]
    public async Task RunAsync_TailWithInvalidLines_FallsBackToDefault(params string[] args)
    {
        await using var cli = new CliHarness();
        cli.WriteToken();
        await cli.StartServerAsync(ctx => ctx.Response.WriteAsync("ok"));

        await LogsCommand.RunAsync(args, json: false);

        cli.RequestedPathsAndQueries.Should().Equal("/api/v1/admin/logs/tail?lines=200");
    }

    [Fact]
    public async Task RunAsync_TailJson_SplitsLinesIntoEnvelope()
    {
        await using var cli = new CliHarness();
        cli.WriteToken();
        await cli.StartServerAsync(ctx => ctx.Response.WriteAsync("first line\nsecond line"));

        await LogsCommand.RunAsync(["--json"], json: true);

        var lines = JsonDocument.Parse(cli.Out).RootElement.GetProperty("lines");
        lines.EnumerateArray().Select(l => l.GetString()).Should().Equal("first line", "second line");
    }

    [Fact]
    public async Task RunAsync_TailWhenServerRejects_Returns5()
    {
        await using var cli = new CliHarness();
        cli.WriteToken();
        await cli.StartServerAsync(ctx =>
        {
            ctx.Response.StatusCode = 404;
            return Task.CompletedTask;
        });

        var exitCode = await LogsCommand.RunAsync([], json: false);

        exitCode.Should().Be(5);
    }

    [Fact]
    public async Task RunAsync_WhenServerUnreachable_Returns4()
    {
        await using var cli = new CliHarness();
        cli.WriteToken();
        cli.UseClosedPort();

        var exitCode = await LogsCommand.RunAsync(["--list"], json: false);

        exitCode.Should().Be(4);
        cli.Error.Should().Contain("Cannot reach the server");
    }
}
