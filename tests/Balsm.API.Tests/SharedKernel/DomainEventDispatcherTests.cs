using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.API.Tests.SharedKernel;

public sealed class DomainEventDispatcherTests
{
    private sealed record SampleEvent(Guid EventId, DateTime OccurredAt) : IDomainEvent;

    private sealed record UnhandledEvent(Guid EventId, DateTime OccurredAt) : IDomainEvent;

    private sealed class RecordingHandler(List<string> log, string name) : IDomainEventHandler<SampleEvent>
    {
        public Task HandleAsync(SampleEvent domainEvent, CancellationToken cancellationToken = default)
        {
            log.Add($"{name}:{domainEvent.EventId}");
            return Task.CompletedTask;
        }
    }

    private static readonly DateTime At = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DispatchEventsAsync_EveryRegisteredHandler_ReceivesEachEvent()
    {
        var log = new List<string>();
        var provider = new ServiceCollection()
            .AddSingleton<IDomainEventHandler<SampleEvent>>(new RecordingHandler(log, "first"))
            .AddSingleton<IDomainEventHandler<SampleEvent>>(new RecordingHandler(log, "second"))
            .BuildServiceProvider();
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");

        await new DomainEventDispatcher(provider).DispatchEventsAsync([new SampleEvent(id, At)]);

        log.Should().Equal($"first:{id}", $"second:{id}");
    }

    [Fact]
    public async Task DispatchEventsAsync_EventWithoutHandlers_DoesNothing()
    {
        var provider = new ServiceCollection().BuildServiceProvider();

        var act = () => new DomainEventDispatcher(provider)
            .DispatchEventsAsync([new UnhandledEvent(Guid.Empty, At)]);

        await act.Should().NotThrowAsync();
    }
}
