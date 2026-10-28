using Balsm.Supervisor.Configuration;
using Balsm.Supervisor.Models;
using Balsm.Supervisor.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Balsm.Supervisor.Tests.Services;

public class FederationServiceTests
{
    private readonly InMemoryFederationStore _store = new();
    private readonly FederationService _sut;

    public FederationServiceTests()
    {
        var options = Options.Create(new SupervisorOptions { ServerId = "test-server-id" });
        _sut = new FederationService(
            _store, options, NullLogger<FederationService>.Instance);
    }

    [Fact]
    public void GenerateCode_ReturnsValidCode()
    {
        var result = _sut.GenerateCode();

        result.Code.Should().NotBeNullOrEmpty();
        result.Code.Should().HaveLength(6);
        result.ExpiresInSeconds.Should().Be(600);
    }

    [Fact]
    public void GenerateCode_ProducesUniqueCodesOnMultipleCalls()
    {
        var code1 = _sut.GenerateCode();
        var code2 = _sut.GenerateCode();

        code1.Code.Should().NotBe(code2.Code);
    }

    [Fact]
    public async Task ValidateAndCompletePairing_WithValidCode_CreatesPairing()
    {
        var codeResponse = _sut.GenerateCode();
        var request = new PairRequest
        {
            Code = codeResponse.Code,
            ServerId = "remote-server",
            ServerName = "Remote",
            ServerUrl = "https://remote.example.com",
            ApiKey = Convert.ToBase64String(new byte[32])
        };

        var result = await _sut.ValidateAndCompletePairingAsync(request);

        result.Should().NotBeNull();
        result!.ServerId.Should().Be("test-server-id");
        result.ServerName.Should().NotBeNullOrEmpty();
        result.ApiKey.Should().NotBeNullOrEmpty();

        var data = await _store.LoadAsync();
        data.Pairings.Should().HaveCount(1);
        data.Pairings[0].ServerId.Should().Be("remote-server");
        data.Pairings[0].Status.Should().Be(PairingStatus.Active);
    }

    [Fact]
    public async Task ValidateAndCompletePairing_WithInvalidCode_ReturnsNull()
    {
        var request = new PairRequest
        {
            Code = "XXXXXX",
            ServerId = "remote",
            ServerName = "Remote",
            ServerUrl = "https://remote.example.com",
            ApiKey = Convert.ToBase64String(new byte[32])
        };

        var result = await _sut.ValidateAndCompletePairingAsync(request);
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAndCompletePairing_CodeCannotBeReused()
    {
        var codeResponse = _sut.GenerateCode();
        var request = new PairRequest
        {
            Code = codeResponse.Code,
            ServerId = "remote",
            ServerName = "Remote",
            ServerUrl = "https://remote.example.com",
            ApiKey = Convert.ToBase64String(new byte[32])
        };

        var first = await _sut.ValidateAndCompletePairingAsync(request);
        var second = await _sut.ValidateAndCompletePairingAsync(request);

        first.Should().NotBeNull();
        second.Should().BeNull();
    }

    [Fact]
    public async Task ValidateApiKey_MatchesPairingApiKey()
    {
        var codeResponse = _sut.GenerateCode();
        var request = new PairRequest
        {
            Code = codeResponse.Code,
            ServerId = "remote",
            ServerName = "Remote",
            ServerUrl = "https://remote.example.com",
            ApiKey = Convert.ToBase64String(new byte[32])
        };

        var pairResult = await _sut.ValidateAndCompletePairingAsync(request);
        var ourApiKey = pairResult!.ApiKey;

        var pairing = await _sut.ValidateApiKeyAsync(ourApiKey);
        pairing.Should().NotBeNull();
        pairing!.ServerId.Should().Be("remote");
    }

    [Fact]
    public async Task ValidateApiKey_InvalidKey_ReturnsNull()
    {
        var invalidKey = Convert.ToBase64String(new byte[32]);
        var result = await _sut.ValidateApiKeyAsync(invalidKey);
        result.Should().BeNull();
    }

    [Fact]
    public async Task PausePairing_SetsPausedStatus()
    {
        var codeResponse = _sut.GenerateCode();
        await _sut.ValidateAndCompletePairingAsync(new PairRequest
        {
            Code = codeResponse.Code,
            ServerId = "remote",
            ServerName = "Remote",
            ServerUrl = "https://remote.example.com",
            ApiKey = Convert.ToBase64String(new byte[32])
        });

        var data = await _store.LoadAsync();
        var id = data.Pairings[0].Id;

        var result = await _sut.PausePairingAsync(id);
        result.Should().BeTrue();

        data = await _store.LoadAsync();
        data.Pairings[0].Status.Should().Be(PairingStatus.Paused);
    }

    [Fact]
    public async Task ResumePairing_SetsActiveStatus()
    {
        var codeResponse = _sut.GenerateCode();
        await _sut.ValidateAndCompletePairingAsync(new PairRequest
        {
            Code = codeResponse.Code,
            ServerId = "remote",
            ServerName = "Remote",
            ServerUrl = "https://remote.example.com",
            ApiKey = Convert.ToBase64String(new byte[32])
        });

        var data = await _store.LoadAsync();
        var id = data.Pairings[0].Id;

        await _sut.PausePairingAsync(id);
        var result = await _sut.ResumePairingAsync(id);
        result.Should().BeTrue();

        data = await _store.LoadAsync();
        data.Pairings[0].Status.Should().Be(PairingStatus.Active);
    }

    [Fact]
    public async Task RemovePairing_DeletesPairing()
    {
        var codeResponse = _sut.GenerateCode();
        await _sut.ValidateAndCompletePairingAsync(new PairRequest
        {
            Code = codeResponse.Code,
            ServerId = "remote",
            ServerName = "Remote",
            ServerUrl = "https://remote.example.com",
            ApiKey = Convert.ToBase64String(new byte[32])
        });

        var data = await _store.LoadAsync();
        var id = data.Pairings[0].Id;

        var result = await _sut.RemovePairingAsync(id);
        result.Should().BeTrue();

        data = await _store.LoadAsync();
        data.Pairings.Should().BeEmpty();
    }

    [Fact]
    public async Task RemovePairing_NonExistentId_ReturnsFalse()
    {
        var result = await _sut.RemovePairingAsync(Guid.NewGuid());
        result.Should().BeFalse();
    }

    [Fact]
    public async Task PausePairing_NonExistentId_ReturnsFalse()
    {
        var result = await _sut.PausePairingAsync(Guid.NewGuid());
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetPairings_ReturnsSummaries()
    {
        var codeResponse = _sut.GenerateCode();
        await _sut.ValidateAndCompletePairingAsync(new PairRequest
        {
            Code = codeResponse.Code,
            ServerId = "remote",
            ServerName = "Remote",
            ServerUrl = "https://remote.example.com",
            ApiKey = Convert.ToBase64String(new byte[32])
        });

        var pairings = await _sut.GetPairingsAsync();
        pairings.Should().HaveCount(1);
        pairings[0].ServerId.Should().Be("remote");
        pairings[0].ServerName.Should().Be("Remote");
        pairings[0].Status.Should().Be("Active");
    }

    private static readonly Guid KnownPairingId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly DateTime FixedTime = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private async Task<ServerPairing> SeedPairingAsync(PairingStatus status, string ourApiKey)
    {
        var pairing = new ServerPairing
        {
            Id = KnownPairingId,
            ServerId = "seeded",
            ServerName = "Seeded",
            ServerUrl = "https://seeded.example.test",
            OurApiKey = ourApiKey,
            Status = status,
            PairedAt = FixedTime,
        };
        (await _store.LoadAsync()).Pairings.Add(pairing);
        return pairing;
    }

    // Pending codes are private; an already-expired code is planted directly so the
    // expiry branches run without waiting ten minutes of wall-clock time.
    private System.Collections.Concurrent.ConcurrentDictionary<string, PairingCode> PlantPendingCode(
        string code, DateTime expiresAt)
    {
        var field = typeof(FederationService).GetField(
            "_pendingCodes", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var codes = (System.Collections.Concurrent.ConcurrentDictionary<string, PairingCode>)field.GetValue(_sut)!;
        codes[code] = new PairingCode { Code = code, ExpiresAt = expiresAt, OurApiKey = "planted-key" };
        return codes;
    }

    [Fact]
    public async Task ValidateAndCompletePairingAsync_WhenCodeExpired_ReturnsNullAndStoresNothing()
    {
        PlantPendingCode("EXPIRD", FixedTime);

        var result = await _sut.ValidateAndCompletePairingAsync(new PairRequest { Code = "EXPIRD" });

        result.Should().BeNull();
        (await _store.LoadAsync()).Pairings.Should().BeEmpty();
    }

    [Fact]
    public void GenerateCode_WhenExpiredCodesPending_PurgesThem()
    {
        var codes = PlantPendingCode("STALE1", FixedTime);

        var fresh = _sut.GenerateCode();

        codes.Keys.Should().Equal(fresh.Code);
    }

    [Fact]
    public async Task InitiatePairingAsync_PersistsActivePairingWithFreshLocalKey()
    {
        var pairing = await _sut.InitiatePairingAsync(
            "remote-id", "Remote", "https://remote.example.test", "their-key");

        pairing.Should().NotBeNull();
        pairing!.Status.Should().Be(PairingStatus.Active);
        pairing.TheirApiKey.Should().Be("their-key");
        Convert.FromBase64String(pairing.OurApiKey).Should().HaveCount(32);
        (await _store.LoadAsync()).Pairings.Should().ContainSingle().Which.Should().BeSameAs(pairing);
    }

    [Fact]
    public void GetLocalApiKeyForPairing_ReturnsOurApiKey()
    {
        var pairing = new ServerPairing { OurApiKey = "our-key" };

        var key = _sut.GetLocalApiKeyForPairing(pairing);

        key.Should().Be("our-key");
    }

    [Fact]
    public async Task ResumePairingAsync_NonExistentId_ReturnsFalse()
    {
        var result = await _sut.ResumePairingAsync(KnownPairingId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WhenPairingIsDisconnected_ReturnsNull()
    {
        var key = Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray());
        await SeedPairingAsync(PairingStatus.Disconnected, key);

        var result = await _sut.ValidateApiKeyAsync(key);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WhenPairingIsPaused_ReturnsPairing()
    {
        var key = Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray());
        await SeedPairingAsync(PairingStatus.Paused, key);

        var result = await _sut.ValidateApiKeyAsync(key);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WhenStoredKeyIsNotBase64_SkipsIt()
    {
        await SeedPairingAsync(PairingStatus.Active, "not base64!");

        var result = await _sut.ValidateApiKeyAsync(Convert.ToBase64String(new byte[32]));

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateHeartbeatAsync_WhenPairingExists_StampsHeartbeat()
    {
        var pairing = await SeedPairingAsync(PairingStatus.Active, "key");

        await _sut.UpdateHeartbeatAsync(KnownPairingId);

        pairing.LastHeartbeatAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateHeartbeatAsync_WhenPairingMissing_DoesNothing()
    {
        var act = () => _sut.UpdateHeartbeatAsync(KnownPairingId);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdateSyncTimestampAsync_WhenPairingExists_StampsLastSync()
    {
        var pairing = await SeedPairingAsync(PairingStatus.Active, "key");

        await _sut.UpdateSyncTimestampAsync(KnownPairingId);

        pairing.LastSyncAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateSyncTimestampAsync_WhenPairingMissing_DoesNothing()
    {
        var act = () => _sut.UpdateSyncTimestampAsync(KnownPairingId);

        await act.Should().NotThrowAsync();
    }
}

public sealed class InMemoryFederationStore : IFederationStore
{
    private FederationData _data = new();

    public Task<FederationData> LoadAsync(CancellationToken ct = default)
        => Task.FromResult(_data);

    public Task SaveAsync(FederationData data, CancellationToken ct = default)
    {
        _data = data;
        return Task.CompletedTask;
    }
}
