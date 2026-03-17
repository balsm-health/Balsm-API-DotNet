using System.Threading.Channels;
using Balsm.Supervisor.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Balsm.Supervisor.Services;

public sealed class SyncService : BackgroundService
{
    private readonly IFederationStore _store;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SyncService> _logger;
    private readonly Channel<SyncBatch> _inboundQueue;

    public SyncService(
        IFederationStore store,
        IHttpClientFactory httpClientFactory,
        ILogger<SyncService> logger)
    {
        _store = store;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _inboundQueue = Channel.CreateBounded<SyncBatch>(100);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SyncService started");

        await Task.WhenAll(
            HeartbeatLoopAsync(stoppingToken),
            InboundProcessingLoopAsync(stoppingToken));
    }

    public async Task EnqueueInboundAsync(SyncBatch batch)
    {
        await _inboundQueue.Writer.WriteAsync(batch);
    }

    private async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        // Wait 30s before first heartbeat to let the app fully start
        await Task.Delay(TimeSpan.FromSeconds(30), ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var data = await _store.LoadAsync(ct);
                var activePairings = data.Pairings
                    .Where(p => p.Status == PairingStatus.Active)
                    .ToList();

                foreach (var pairing in activePairings)
                {
                    await SendHeartbeatAsync(pairing, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Heartbeat loop error");
            }

            await Task.Delay(TimeSpan.FromSeconds(60), ct);
        }
    }

    private async Task SendHeartbeatAsync(ServerPairing pairing, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Federation");
            var url = $"{pairing.ServerUrl.TrimEnd('/')}/api/v1/federation/heartbeat";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Balsm-ApiKey", pairing.TheirApiKey);

            var response = await client.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                pairing.LastHeartbeatAt = DateTime.UtcNow;
                if (pairing.Status == PairingStatus.Disconnected)
                {
                    pairing.Status = PairingStatus.Active;
                    _logger.LogInformation(
                        "Server {ServerId} reconnected", pairing.ServerId);
                }

                var data = await _store.LoadAsync(ct);
                var stored = data.Pairings.FirstOrDefault(p => p.Id == pairing.Id);
                if (stored is not null)
                {
                    stored.LastHeartbeatAt = pairing.LastHeartbeatAt;
                    stored.Status = pairing.Status;
                    await _store.SaveAsync(data, ct);
                }
            }
            else
            {
                _logger.LogWarning(
                    "Heartbeat failed for {ServerId}: {Status}",
                    pairing.ServerId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Heartbeat failed for {ServerId}", pairing.ServerId);
        }
    }

    private async Task InboundProcessingLoopAsync(CancellationToken ct)
    {
        await foreach (var batch in _inboundQueue.Reader.ReadAllAsync(ct))
        {
            try
            {
                _logger.LogInformation(
                    "Processing inbound sync batch from {ServerId}: {Count} records",
                    batch.SourceServerId, batch.Records.Count);

                // MVP: Log receipt. Full entity sync will be implemented
                // when domain modules have actual data to sync.
                foreach (var record in batch.Records)
                {
                    _logger.LogDebug(
                        "Sync record: {EntityType}/{EntityId} ({Operation})",
                        record.EntityType, record.EntityId, record.Operation);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing sync batch from {ServerId}",
                    batch.SourceServerId);
            }
        }
    }
}
