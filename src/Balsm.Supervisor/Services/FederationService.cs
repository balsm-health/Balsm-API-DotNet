using System.Collections.Concurrent;
using System.Security.Cryptography;
using Balsm.Supervisor.Configuration;
using Balsm.Supervisor.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balsm.Supervisor.Services;

public sealed class FederationService
{
    private readonly IFederationStore _store;
    private readonly SupervisorOptions _options;
    private readonly ILogger<FederationService> _logger;
    private readonly ConcurrentDictionary<string, PairingCode> _pendingCodes = new();

    public FederationService(
        IFederationStore store,
        IOptions<SupervisorOptions> options,
        ILogger<FederationService> logger)
    {
        _store = store;
        _options = options.Value;
        _logger = logger;
    }

    public GenerateCodeResponse GenerateCode()
    {
        CleanExpiredCodes();

        var code = GenerateAlphanumericCode(6);
        var apiKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var expiresAt = DateTime.UtcNow.AddMinutes(10);

        _pendingCodes[code] = new PairingCode
        {
            Code = code,
            ExpiresAt = expiresAt,
            OurApiKey = apiKey
        };

        _logger.LogInformation("Generated pairing code (expires in 10 min)");

        return new GenerateCodeResponse
        {
            Code = code,
            ExpiresInSeconds = 600
        };
    }

    public async Task<PairResponse?> ValidateAndCompletePairingAsync(
        PairRequest request, CancellationToken ct = default)
    {
        if (!_pendingCodes.TryRemove(request.Code, out var pending))
        {
            _logger.LogWarning("Invalid or expired pairing code");
            return null;
        }

        if (DateTime.UtcNow > pending.ExpiresAt)
        {
            _logger.LogWarning("Pairing code expired");
            return null;
        }

        var pairing = new ServerPairing
        {
            ServerId = request.ServerId,
            ServerName = request.ServerName,
            ServerUrl = request.ServerUrl,
            OurApiKey = pending.OurApiKey,
            TheirApiKey = request.ApiKey,
            Status = PairingStatus.Active,
            PairedAt = DateTime.UtcNow
        };

        var data = await _store.LoadAsync(ct);
        data.Pairings.Add(pairing);
        await _store.SaveAsync(data, ct);

        _logger.LogInformation(
            "Pairing completed with server {ServerId} ({Name})",
            request.ServerId, request.ServerName);

        return new PairResponse
        {
            ServerId = _options.ServerId ?? "unknown",
            ServerName = Environment.MachineName,
            ApiKey = pending.OurApiKey
        };
    }

    public async Task<ServerPairing?> InitiatePairingAsync(
        string remoteServerId, string remoteServerName, string remoteServerUrl,
        string theirApiKey, CancellationToken ct = default)
    {
        var ourApiKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var pairing = new ServerPairing
        {
            ServerId = remoteServerId,
            ServerName = remoteServerName,
            ServerUrl = remoteServerUrl,
            OurApiKey = ourApiKey,
            TheirApiKey = theirApiKey,
            Status = PairingStatus.Active,
            PairedAt = DateTime.UtcNow
        };

        var data = await _store.LoadAsync(ct);
        data.Pairings.Add(pairing);
        await _store.SaveAsync(data, ct);

        _logger.LogInformation(
            "Initiated pairing with server {ServerId} ({Name})",
            remoteServerId, remoteServerName);

        return pairing;
    }

    public string GetLocalApiKeyForPairing(ServerPairing pairing)
    {
        return pairing.OurApiKey;
    }

    public async Task<List<PairingSummary>> GetPairingsAsync(CancellationToken ct = default)
    {
        var data = await _store.LoadAsync(ct);
        return data.Pairings.Select(p => new PairingSummary
        {
            Id = p.Id,
            ServerId = p.ServerId,
            ServerName = p.ServerName,
            ServerUrl = p.ServerUrl,
            Direction = p.Direction.ToString(),
            SyncEntities = p.SyncEntities,
            Status = p.Status.ToString(),
            PairedAt = p.PairedAt,
            LastSyncAt = p.LastSyncAt,
            LastHeartbeatAt = p.LastHeartbeatAt
        }).ToList();
    }

    public async Task<bool> RemovePairingAsync(Guid id, CancellationToken ct = default)
    {
        var data = await _store.LoadAsync(ct);
        var removed = data.Pairings.RemoveAll(p => p.Id == id);
        if (removed > 0)
        {
            await _store.SaveAsync(data, ct);
            _logger.LogInformation("Removed pairing {Id}", id);
        }
        return removed > 0;
    }

    public async Task<bool> PausePairingAsync(Guid id, CancellationToken ct = default)
    {
        var data = await _store.LoadAsync(ct);
        var pairing = data.Pairings.FirstOrDefault(p => p.Id == id);
        if (pairing is null) return false;

        pairing.Status = PairingStatus.Paused;
        await _store.SaveAsync(data, ct);
        _logger.LogInformation("Paused pairing {Id}", id);
        return true;
    }

    public async Task<bool> ResumePairingAsync(Guid id, CancellationToken ct = default)
    {
        var data = await _store.LoadAsync(ct);
        var pairing = data.Pairings.FirstOrDefault(p => p.Id == id);
        if (pairing is null) return false;

        pairing.Status = PairingStatus.Active;
        await _store.SaveAsync(data, ct);
        _logger.LogInformation("Resumed pairing {Id}", id);
        return true;
    }

    public async Task<ServerPairing?> ValidateApiKeyAsync(
        string apiKey, CancellationToken ct = default)
    {
        var data = await _store.LoadAsync(ct);
        var apiKeyBytes = Convert.FromBase64String(apiKey);

        foreach (var pairing in data.Pairings)
        {
            if (pairing.Status is PairingStatus.Active or PairingStatus.Paused)
            {
                try
                {
                    var storedKeyBytes = Convert.FromBase64String(pairing.OurApiKey);
                    if (CryptographicOperations.FixedTimeEquals(apiKeyBytes, storedKeyBytes))
                    {
                        return pairing;
                    }
                }
                catch (FormatException)
                {
                    // Skip invalid base64 keys
                }
            }
        }

        return null;
    }

    public async Task UpdateHeartbeatAsync(
        Guid pairingId, CancellationToken ct = default)
    {
        var data = await _store.LoadAsync(ct);
        var pairing = data.Pairings.FirstOrDefault(p => p.Id == pairingId);
        if (pairing is not null)
        {
            pairing.LastHeartbeatAt = DateTime.UtcNow;
            await _store.SaveAsync(data, ct);
        }
    }

    public async Task UpdateSyncTimestampAsync(
        Guid pairingId, CancellationToken ct = default)
    {
        var data = await _store.LoadAsync(ct);
        var pairing = data.Pairings.FirstOrDefault(p => p.Id == pairingId);
        if (pairing is not null)
        {
            pairing.LastSyncAt = DateTime.UtcNow;
            await _store.SaveAsync(data, ct);
        }
    }

    private void CleanExpiredCodes()
    {
        var now = DateTime.UtcNow;
        foreach (var (key, code) in _pendingCodes)
        {
            if (now > code.ExpiresAt)
                _pendingCodes.TryRemove(key, out _);
        }
    }

    private static string GenerateAlphanumericCode(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(length);
        var result = new char[length];
        for (var i = 0; i < length; i++)
        {
            result[i] = chars[bytes[i] % chars.Length];
        }
        return new string(result);
    }
}
