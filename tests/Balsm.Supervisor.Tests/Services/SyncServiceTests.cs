using System.Net;
using System.Reflection;
using Balsm.Supervisor.Models;
using Balsm.Supervisor.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Balsm.Supervisor.Tests.Services;

public sealed class SyncServiceTests
{
    private static readonly Guid PairingId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid RecordId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly DateTime FixedTime = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryFederationStore _store = new();
    private readonly ListLogger<SyncService> _logger = new();

    private SyncService CreateSut(StubHttpMessageHandler handler)
        => new(_store, new StubHttpClientFactory(handler), _logger);

    private static ServerPairing Pairing(PairingStatus status) => new()
    {
        Id = PairingId,
        ServerId = "remote-server",
        ServerName = "Remote",
        ServerUrl = "https://remote.example.test/",
        TheirApiKey = "their-key",
        Status = status,
        PairedAt = FixedTime,
    };

    // SendHeartbeatAsync is only reached after the 30 s start-up delay of the
    // heartbeat loop, so it is driven directly to keep the test fast and deterministic.
    private static Task SendHeartbeatAsync(SyncService sut, ServerPairing pairing)
    {
        var method = typeof(SyncService).GetMethod(
            "SendHeartbeatAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Task)method.Invoke(sut, [pairing, CancellationToken.None])!;
    }

    [Fact]
    public async Task SendHeartbeatAsync_WhenRemoteResponds_CallsHeartbeatEndpointWithApiKey()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Text("{}"));
        var sut = CreateSut(handler);

        await SendHeartbeatAsync(sut, Pairing(PairingStatus.Active));

        var request = handler.Requests.Single();
        request.RequestUri!.ToString().Should().Be("https://remote.example.test/api/v1/federation/heartbeat");
        request.Headers.GetValues("X-Balsm-ApiKey").Should().Equal("their-key");
    }

    [Fact]
    public async Task SendHeartbeatAsync_WhenDisconnectedPairingResponds_ReactivatesAndPersists()
    {
        var stored = Pairing(PairingStatus.Disconnected);
        (await _store.LoadAsync()).Pairings.Add(stored);
        var pairing = Pairing(PairingStatus.Disconnected);
        var sut = CreateSut(new StubHttpMessageHandler(_ => StubHttpMessageHandler.Text("{}")));

        await SendHeartbeatAsync(sut, pairing);

        stored.Status.Should().Be(PairingStatus.Active);
        stored.LastHeartbeatAt.Should().NotBeNull().And.Be(pairing.LastHeartbeatAt);
    }

    [Fact]
    public async Task SendHeartbeatAsync_WhenPairingNoLongerStored_UpdatesOnlyInMemoryPairing()
    {
        var pairing = Pairing(PairingStatus.Active);
        var sut = CreateSut(new StubHttpMessageHandler(_ => StubHttpMessageHandler.Text("{}")));

        await SendHeartbeatAsync(sut, pairing);

        pairing.LastHeartbeatAt.Should().NotBeNull();
        (await _store.LoadAsync()).Pairings.Should().BeEmpty();
    }

    [Fact]
    public async Task SendHeartbeatAsync_WhenRemoteReturnsError_LogsWarningAndLeavesPairingUntouched()
    {
        var pairing = Pairing(PairingStatus.Active);
        var sut = CreateSut(new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Text("down", HttpStatusCode.ServiceUnavailable)));

        await SendHeartbeatAsync(sut, pairing);

        pairing.LastHeartbeatAt.Should().BeNull();
        _logger.Contains(LogLevel.Warning, "Heartbeat failed for remote-server: ServiceUnavailable").Should().BeTrue();
    }

    [Fact]
    public async Task SendHeartbeatAsync_WhenRequestThrows_LogsWarningWithoutThrowing()
    {
        var pairing = Pairing(PairingStatus.Active);
        var sut = CreateSut(new StubHttpMessageHandler(_ => throw new HttpRequestException("unreachable")));

        await SendHeartbeatAsync(sut, pairing);

        _logger.Entries.Should().Contain(e =>
            e.Level == LogLevel.Warning && e.Exception is HttpRequestException);
    }

    [Fact]
    public async Task ExecuteAsync_WithQueuedBatch_ProcessesEveryRecordUntilStopped()
    {
        var sut = CreateSut(new StubHttpMessageHandler(_ => StubHttpMessageHandler.Text("{}")));
        await sut.EnqueueInboundAsync(new SyncBatch
        {
            SourceServerId = "remote-server",
            SequenceNumber = 1,
            SentAt = FixedTime,
            Records = [new SyncRecord { EntityType = "Workspace", EntityId = RecordId, Operation = "Upsert" }],
        });

        await sut.StartAsync(CancellationToken.None);
        var processed = await Poll.UntilAsync(() => _logger.Contains(LogLevel.Debug, $"Workspace/{RecordId}"));
        await sut.StopAsync(CancellationToken.None);

        processed.Should().BeTrue();
        _logger.Contains(LogLevel.Information, "remote-server: 1 records").Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithMalformedBatch_LogsErrorAndKeepsProcessing()
    {
        var sut = CreateSut(new StubHttpMessageHandler(_ => StubHttpMessageHandler.Text("{}")));
        await sut.EnqueueInboundAsync(new SyncBatch { SourceServerId = "broken-server", Records = null! });
        await sut.EnqueueInboundAsync(new SyncBatch { SourceServerId = "healthy-server" });

        await sut.StartAsync(CancellationToken.None);
        var processed = await Poll.UntilAsync(() => _logger.Contains(LogLevel.Information, "healthy-server"));
        await sut.StopAsync(CancellationToken.None);

        processed.Should().BeTrue();
        _logger.Contains(LogLevel.Error, "Error processing sync batch from broken-server").Should().BeTrue();
    }
}
