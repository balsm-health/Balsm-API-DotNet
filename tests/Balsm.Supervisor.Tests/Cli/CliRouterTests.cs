using Balsm.Supervisor.Cli;
using Balsm.Supervisor.Tests.Support;
using FluentAssertions;
using Xunit;

namespace Balsm.Supervisor.Tests.Cli;

[Collection(ProcessStateCollection.Name)]
public class CliRouterTests
{
    [Theory]
    [InlineData("status")]
    [InlineData("VERSION")]
    [InlineData("logs")]
    [InlineData("backup")]
    public void IsCliInvocation_KnownCommand_ReturnsTrue(string command)
    {
        var result = CliRouter.IsCliInvocation([command]);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData]
    [InlineData("--urls")]
    [InlineData("serve")]
    public void IsCliInvocation_NoOrUnknownCommand_ReturnsFalse(params string[] args)
    {
        var result = CliRouter.IsCliInvocation(args);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_ReservedButUnimplementedCommand_ReturnsUsageError()
    {
        await using var cli = new CliHarness();

        var exitCode = await CliRouter.RunAsync(["backup"]);

        exitCode.Should().Be(2);
        cli.Error.Should().Contain("unknown command 'backup'");
    }

    [Fact]
    public async Task RunAsync_Version_DelegatesToVersionCommand()
    {
        await using var cli = new CliHarness();

        var exitCode = await CliRouter.RunAsync(["Version", "--json"]);

        exitCode.Should().Be(0);
        cli.Out.Should().Contain("\"command\": \"version\"");
    }

    [Fact]
    public async Task RunAsync_StatusWithoutToken_DelegatesToStatusCommand()
    {
        await using var cli = new CliHarness();

        var exitCode = await CliRouter.RunAsync(["status"]);

        exitCode.Should().Be(4);
        cli.Error.Should().Contain("Local CLI token not found");
    }

    [Fact]
    public async Task RunAsync_LogsWithoutToken_DelegatesToLogsCommand()
    {
        await using var cli = new CliHarness();

        var exitCode = await CliRouter.RunAsync(["logs", "--list"]);

        exitCode.Should().Be(4);
        cli.Error.Should().Contain("Local CLI token not found");
    }
}
