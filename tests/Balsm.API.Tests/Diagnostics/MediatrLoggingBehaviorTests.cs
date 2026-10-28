using Balsm.Infrastructure.Diagnostics;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Balsm.API.Tests.Diagnostics;

public sealed class MediatrLoggingBehaviorTests
{
    public sealed record PingQuery : IRequest<string>;

    [Fact]
    public async Task Handle_DebugEnabled_LogsStartAndFinish()
    {
        var logger = new ListLogger<MediatrLoggingBehavior<PingQuery, string>>(LogLevel.Debug);

        await new MediatrLoggingBehavior<PingQuery, string>(logger)
            .Handle(new PingQuery(), _ => Task.FromResult("pong"), default);

        logger.Messages.Should().SatisfyRespectively(
            first => first.Should().Contain("PingQuery handling"),
            second => second.Should().Contain("PingQuery handled in"));
    }

    [Fact]
    public async Task Handle_DebugEnabled_ReturnsTheHandlerResponse()
    {
        var logger = new ListLogger<MediatrLoggingBehavior<PingQuery, string>>(LogLevel.Debug);

        var response = await new MediatrLoggingBehavior<PingQuery, string>(logger)
            .Handle(new PingQuery(), _ => Task.FromResult("pong"), default);

        response.Should().Be("pong");
    }

    [Fact]
    public async Task Handle_HandlerThrows_LogsAndRethrows()
    {
        var logger = new ListLogger<MediatrLoggingBehavior<PingQuery, string>>(LogLevel.Debug);
        var behavior = new MediatrLoggingBehavior<PingQuery, string>(logger);

        var act = () => behavior.Handle(new PingQuery(), _ => throw new InvalidOperationException("boom"), default);

        await act.Should().ThrowAsync<InvalidOperationException>();
        logger.Messages.Should().Contain(m => m.Contains("PingQuery threw after"));
    }

    [Fact]
    public async Task Handle_DebugDisabled_LogsNothing()
    {
        var logger = new ListLogger<MediatrLoggingBehavior<PingQuery, string>>(LogLevel.Information);

        await new MediatrLoggingBehavior<PingQuery, string>(logger)
            .Handle(new PingQuery(), _ => Task.FromResult("pong"), default);

        logger.Messages.Should().BeEmpty();
    }
}
