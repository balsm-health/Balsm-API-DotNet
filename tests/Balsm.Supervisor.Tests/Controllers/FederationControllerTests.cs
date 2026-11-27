using Balsm.Supervisor.Configuration;
using Balsm.Supervisor.Controllers;
using Balsm.Supervisor.Models;
using Balsm.Supervisor.Services;
using Balsm.Supervisor.Tests.Middleware;
using Balsm.Supervisor.Tests.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using static Balsm.Supervisor.Tests.Support.ActionResultReader;

namespace Balsm.Supervisor.Tests.Controllers;

public class FederationControllerTests
{
    private static readonly string RemoteApiKey = Convert.ToBase64String(new byte[32]);

    private readonly MiddlewareTestFederationStore _store = new();
    private readonly FederationService _federationService;

    public FederationControllerTests()
    {
        _federationService = new FederationService(
            _store,
            Options.Create(new SupervisorOptions { ServerId = "local-server" }),
            NullLogger<FederationService>.Instance);
    }

    [Fact]
    public async Task AcceptPairing_WithoutCode_Returns400()
    {
        var sut = CreateSut();

        var result = await sut.AcceptPairing(ValidPairRequest(code: " "));

        StatusOf(result).Should().Be(400);
    }

    [Theory]
    [InlineData("", "https://remote.example.test", "key")]
    [InlineData("remote", "", "key")]
    [InlineData("remote", "https://remote.example.test", "")]
    public async Task AcceptPairing_WithMissingIdentity_Returns400(string serverId, string serverUrl, string apiKey)
    {
        var sut = CreateSut();

        var result = await sut.AcceptPairing(new PairRequest
        {
            Code = "ABC234",
            ServerId = serverId,
            ServerUrl = serverUrl,
            ApiKey = apiKey
        });

        StatusOf(result).Should().Be(400);
    }

    [Fact]
    public async Task AcceptPairing_WithUnknownCode_Returns400AndStoresNothing()
    {
        var sut = CreateSut();

        var result = await sut.AcceptPairing(ValidPairRequest(code: "ZZZZZZ"));

        StatusOf(result).Should().Be(400);
        (await _store.LoadAsync()).Pairings.Should().BeEmpty();
    }

    [Fact]
    public async Task AcceptPairing_WithIssuedCode_ReturnsOurIdentityAndStoresPairing()
    {
        var code = _federationService.GenerateCode().Code;
        var sut = CreateSut();

        var result = await sut.AcceptPairing(ValidPairRequest(code));

        StatusOf(result).Should().Be(200);
        ((ObjectResult)result).Value.Should().BeOfType<PairResponse>()
            .Which.ServerId.Should().Be("local-server");
        (await _store.LoadAsync()).Pairings.Should().ContainSingle(p => p.ServerId == "remote-server");
    }

    [Fact]
    public async Task Heartbeat_WithAuthenticatedPairing_RecordsHeartbeat()
    {
        var pairing = await AddPairingAsync();
        var sut = CreateSut();
        sut.HttpContext.Items["FederationPairing"] = pairing;

        var result = sut.Heartbeat();

        ((ObjectResult)result).Value.Should().BeOfType<HeartbeatResponse>()
            .Which.Should().Match<HeartbeatResponse>(h => h.ServerId == "local-server" && h.Status == "active");
        (await _store.LoadAsync()).Pairings.Single().LastHeartbeatAt.Should().NotBeNull();
    }

    [Fact]
    public void Heartbeat_WithoutServerId_ReportsUnknown()
    {
        var service = new FederationService(
            _store, Options.Create(new SupervisorOptions()), NullLogger<FederationService>.Instance);
        var sut = new FederationController(service, Options.Create(new SupervisorOptions()))
        {
            ControllerContext = SupervisorFactory.ControllerContextFor()
        };

        var result = sut.Heartbeat();

        ((ObjectResult)result).Value.Should().BeOfType<HeartbeatResponse>()
            .Which.ServerId.Should().Be("unknown");
    }

    [Fact]
    public async Task ReceiveSync_WithoutAuthenticatedPairing_Forbids()
    {
        var sut = CreateSut();

        var result = await sut.ReceiveSync(new SyncBatch { SourceServerId = "remote-server" });

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task ReceiveSync_FromDifferentServerThanApiKey_Returns400()
    {
        var pairing = await AddPairingAsync();
        var sut = CreateSut();
        sut.HttpContext.Items["FederationPairing"] = pairing;

        var result = await sut.ReceiveSync(new SyncBatch { SourceServerId = "impostor-server" });

        StatusOf(result).Should().Be(400);
        (await _store.LoadAsync()).Pairings.Single().LastSyncAt.Should().BeNull();
    }

    [Fact]
    public async Task ReceiveSync_FromPairedServer_AcceptsAndStampsSync()
    {
        var pairing = await AddPairingAsync();
        var sut = CreateSut();
        sut.HttpContext.Items["FederationPairing"] = pairing;
        var batch = new SyncBatch
        {
            SourceServerId = "remote-server",
            SequenceNumber = 7,
            SentAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Records =
            [
                new SyncRecord
                {
                    EntityType = "Workspace",
                    EntityId = new Guid("00000000-0000-0000-0000-000000000001"),
                    Operation = "Upsert",
                    JsonPayload = "{}",
                    UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            ]
        };

        var result = await sut.ReceiveSync(batch);

        StatusOf(result).Should().Be(202);
        BodyOf(result).GetProperty("recordCount").GetInt32().Should().Be(1);
        (await _store.LoadAsync()).Pairings.Single().LastSyncAt.Should().NotBeNull();
    }

    private async Task<ServerPairing> AddPairingAsync()
    {
        var code = _federationService.GenerateCode().Code;
        await _federationService.ValidateAndCompletePairingAsync(ValidPairRequest(code));
        return (await _store.LoadAsync()).Pairings.Single();
    }

    private static PairRequest ValidPairRequest(string code) => new()
    {
        Code = code,
        ServerId = "remote-server",
        ServerName = "Remote",
        ServerUrl = "https://remote.example.test",
        ApiKey = RemoteApiKey
    };

    private FederationController CreateSut()
        => new(_federationService, Options.Create(new SupervisorOptions { ServerId = "local-server" }))
        {
            ControllerContext = SupervisorFactory.ControllerContextFor()
        };
}
