using System.Net;
using System.Text.Json;
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

public class AdminFederationControllerTests
{
    private static readonly Guid UnknownId = new("00000000-0000-0000-0000-0000000000ff");

    private readonly MiddlewareTestFederationStore _store = new();
    private readonly FederationService _federationService;

    public AdminFederationControllerTests()
    {
        _federationService = new FederationService(
            _store,
            Options.Create(new SupervisorOptions { ServerId = "local-server" }),
            NullLogger<FederationService>.Instance);
    }

    [Fact]
    public async Task GetPairings_ReturnsStoredPairings()
    {
        var pairing = await AddPairingAsync();
        var sut = CreateSut(RespondWith(HttpStatusCode.OK, "{}"));

        var result = await sut.GetPairings();

        ((ObjectResult)result).Value.Should().BeOfType<PairingListResponse>()
            .Which.Pairings.Should().ContainSingle(p => p.Id == pairing.Id);
    }

    [Fact]
    public void GenerateCode_ReturnsSixCharacterCode()
    {
        var sut = CreateSut(RespondWith(HttpStatusCode.OK, "{}"));

        var result = sut.GenerateCode();

        ((ObjectResult)result).Value.Should().BeOfType<GenerateCodeResponse>()
            .Which.Code.Should().HaveLength(6);
    }

    [Theory]
    [InlineData("", "ABC234")]
    [InlineData("https://remote.example.test", " ")]
    public async Task InitiatePairing_WithMissingFields_Returns400(string serverUrl, string code)
    {
        var handler = RespondWith(HttpStatusCode.OK, "{}");
        var sut = CreateSut(handler);

        var result = await sut.InitiatePairing(new InitiatePairingRequest { ServerUrl = serverUrl, Code = code });

        StatusOf(result).Should().Be(400);
        handler.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("http://remote.example.test")]
    [InlineData("ftp://remote.example.test")]
    public async Task InitiatePairing_WithNonHttpsRemote_Returns400WithoutCallingIt(string serverUrl)
    {
        var handler = RespondWith(HttpStatusCode.OK, "{}");
        var sut = CreateSut(handler);

        var result = await sut.InitiatePairing(new InitiatePairingRequest { ServerUrl = serverUrl, Code = "ABC234" });

        StatusOf(result).Should().Be(400);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task InitiatePairing_WhenRemoteUnreachable_Returns502()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var sut = CreateSut(handler);

        var result = await sut.InitiatePairing(new InitiatePairingRequest
        {
            ServerUrl = "https://remote.example.test",
            Code = "ABC234"
        });

        StatusOf(result).Should().Be(502);
    }

    [Fact]
    public async Task InitiatePairing_WhenRemoteRejects_PropagatesStatus()
    {
        var sut = CreateSut(RespondWith(HttpStatusCode.BadRequest, "{\"message\":\"Invalid code\"}"));

        var result = await sut.InitiatePairing(new InitiatePairingRequest
        {
            ServerUrl = "https://remote.example.test",
            Code = "ABC234"
        });

        StatusOf(result).Should().Be(400);
        BodyOf(result).GetProperty("message").GetString().Should().Contain("Invalid code");
        (await _store.LoadAsync()).Pairings.Should().BeEmpty();
    }

    [Fact]
    public async Task InitiatePairing_WhenRemoteReturnsNull_Returns502()
    {
        var sut = CreateSut(RespondWith(HttpStatusCode.OK, "null"));

        var result = await sut.InitiatePairing(new InitiatePairingRequest
        {
            ServerUrl = "https://remote.example.test",
            Code = "ABC234"
        });

        StatusOf(result).Should().Be(502);
    }

    [Fact]
    public async Task InitiatePairing_WhenRemoteAccepts_StoresActivePairing()
    {
        var remoteKey = Convert.ToBase64String(new byte[32]);
        var handler = RespondWith(HttpStatusCode.OK, JsonSerializer.Serialize(new PairResponse
        {
            ServerId = "remote-server",
            ServerName = "Remote",
            ApiKey = remoteKey
        }));
        var sut = CreateSut(handler);

        var result = await sut.InitiatePairing(new InitiatePairingRequest
        {
            ServerUrl = "https://remote.example.test/",
            Code = "ABC234"
        });

        StatusOf(result).Should().Be(200);
        handler.Requests.Single().RequestUri!.ToString()
            .Should().Be("https://remote.example.test/api/v1/federation/pair");
        var stored = (await _store.LoadAsync()).Pairings.Single();
        stored.Should().Match<ServerPairing>(p =>
            p.ServerId == "remote-server" && p.TheirApiKey == remoteKey && p.Status == PairingStatus.Active);
    }

    [Fact]
    public async Task InitiatePairing_SendsCodeAndOurIdentityToRemote()
    {
        var handler = RespondWith(HttpStatusCode.OK, JsonSerializer.Serialize(new PairResponse
        {
            ServerId = "remote-server",
            ServerName = "Remote",
            ApiKey = Convert.ToBase64String(new byte[32])
        }));
        var sut = CreateSut(handler, host: "balsm.local", port: 5050);

        await sut.InitiatePairing(new InitiatePairingRequest { ServerUrl = "http://localhost:6060", Code = "ABC234" });

        var sent = JsonSerializer.Deserialize<PairRequest>(
            handler.Bodies.Single()!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        sent.Should().Match<PairRequest>(r =>
            r.Code == "ABC234" && r.ServerId == "local-server" && r.ServerUrl == "http://balsm.local:5050");
    }

    [Fact]
    public async Task RemovePairing_WhenKnown_RemovesIt()
    {
        var pairing = await AddPairingAsync();
        var sut = CreateSut(RespondWith(HttpStatusCode.OK, "{}"));

        var result = await sut.RemovePairing(pairing.Id);

        StatusOf(result).Should().Be(200);
        (await _store.LoadAsync()).Pairings.Should().BeEmpty();
    }

    [Fact]
    public async Task RemovePairing_WhenUnknown_Returns404()
    {
        var sut = CreateSut(RespondWith(HttpStatusCode.OK, "{}"));

        var result = await sut.RemovePairing(UnknownId);

        StatusOf(result).Should().Be(404);
    }

    [Fact]
    public async Task PausePairing_WhenKnown_PausesIt()
    {
        var pairing = await AddPairingAsync();
        var sut = CreateSut(RespondWith(HttpStatusCode.OK, "{}"));

        var result = await sut.PausePairing(pairing.Id);

        StatusOf(result).Should().Be(200);
        (await _store.LoadAsync()).Pairings.Single().Status.Should().Be(PairingStatus.Paused);
    }

    [Fact]
    public async Task PausePairing_WhenUnknown_Returns404()
    {
        var sut = CreateSut(RespondWith(HttpStatusCode.OK, "{}"));

        var result = await sut.PausePairing(UnknownId);

        StatusOf(result).Should().Be(404);
    }

    [Fact]
    public async Task ResumePairing_WhenPaused_ReactivatesIt()
    {
        var pairing = await AddPairingAsync();
        await _federationService.PausePairingAsync(pairing.Id);
        var sut = CreateSut(RespondWith(HttpStatusCode.OK, "{}"));

        var result = await sut.ResumePairing(pairing.Id);

        StatusOf(result).Should().Be(200);
        (await _store.LoadAsync()).Pairings.Single().Status.Should().Be(PairingStatus.Active);
    }

    [Fact]
    public async Task ResumePairing_WhenUnknown_Returns404()
    {
        var sut = CreateSut(RespondWith(HttpStatusCode.OK, "{}"));

        var result = await sut.ResumePairing(UnknownId);

        StatusOf(result).Should().Be(404);
    }

    private async Task<ServerPairing> AddPairingAsync()
        => (await _federationService.InitiatePairingAsync(
            "remote-server", "Remote", "https://remote.example.test", Convert.ToBase64String(new byte[32])))!;

    private static StubHttpMessageHandler RespondWith(HttpStatusCode status, string json)
        => new(_ => StubHttpMessageHandler.Json(status, json));

    private AdminFederationController CreateSut(StubHttpMessageHandler handler, string host = "localhost", int? port = null)
        => new(
            _federationService,
            Options.Create(new SupervisorOptions { ServerId = "local-server" }),
            new StubHttpClientFactory(handler))
        {
            ControllerContext = SupervisorFactory.ControllerContextFor(host, port)
        };
}
