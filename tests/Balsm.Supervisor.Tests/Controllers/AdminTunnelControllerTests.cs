using System.Net;
using System.Text.Json;
using Balsm.Supervisor.Configuration;
using Balsm.Supervisor.Controllers;
using Balsm.Supervisor.Models;
using Balsm.Supervisor.Services;
using Balsm.Supervisor.Tests.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using static Balsm.Supervisor.Tests.Support.ActionResultReader;

namespace Balsm.Supervisor.Tests.Controllers;

[Collection(ProcessStateCollection.Name)]
public sealed class AdminTunnelControllerTests : IDisposable
{
    private const string RegistrySecret = "registry-secret-for-tests";
    private const string RegistryResponseJson =
        "{\"tunnelToken\":\"tunnel-token\",\"tunnelUrl\":\"https://srv-1.tunnel.example.test\",\"tunnelId\":\"tid\",\"serverId\":\"srv-1\"}";

    private readonly BaseDirectoryFileScope _configFile = new("appsettings.Production.json");
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"balsm-tunnel-{Guid.NewGuid():N}");

    public AdminTunnelControllerTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void GetStatus_ReportsTunnelState()
    {
        var sut = CreateSut(RespondWith(HttpStatusCode.OK, "{}"), CreateTunnel(), new SupervisorOptions { ServerId = "srv-1" });

        var result = sut.GetStatus();

        ((ObjectResult)result).Value.Should().BeOfType<TunnelStatusResponse>()
            .Which.Should().Match<TunnelStatusResponse>(s => !s.IsRunning && !s.CloudflaredInstalled && s.ServerId == "srv-1");
    }

    [Fact]
    public async Task Register_WhenCloudflaredMissing_Returns400WithoutCallingRegistry()
    {
        var handler = RespondWith(HttpStatusCode.OK, "{}");
        var sut = CreateSut(handler, CreateTunnel());

        var result = await sut.Register();

        StatusOf(result).Should().Be(400);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Register_WhenRegistryUnreachable_Returns502()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("no route"));
        var sut = CreateSut(handler, CreateTunnel(installed: true));

        var result = await sut.Register();

        StatusOf(result).Should().Be(502);
    }

    [Fact]
    public async Task Register_WhenRegistryErrors_PropagatesStatus()
    {
        var sut = CreateSut(RespondWith(HttpStatusCode.Conflict, "{\"error\":\"taken\"}"), CreateTunnel(installed: true));

        var result = await sut.Register();

        StatusOf(result).Should().Be(409);
        File.Exists(_configFile.FilePath).Should().BeFalse();
    }

    [Fact]
    public async Task Register_WhenRegistryReturnsNull_Returns502()
    {
        var sut = CreateSut(RespondWith(HttpStatusCode.OK, "null"), CreateTunnel(installed: true));

        var result = await sut.Register();

        StatusOf(result).Should().Be(502);
    }

    [Fact]
    public async Task Register_WithoutServerId_SendsGeneratedIdWithoutAuthorization()
    {
        var handler = RespondWith(HttpStatusCode.OK, RegistryResponseJson);
        var sut = CreateSut(handler, CreateTunnel(installed: true), new SupervisorOptions
        {
            RegistryUrl = "https://registry.example.test/",
            CloudflaredPath = MissingExecutable
        });

        await sut.Register();

        var request = handler.Requests.Single();
        request.RequestUri!.ToString().Should().Be("https://registry.example.test/api/tunnels/register");
        request.Headers.Authorization.Should().BeNull();
        JsonDocument.Parse(handler.Bodies.Single()!).RootElement.GetProperty("serverId").GetString()
            .Should().HaveLength(12);
    }

    [Fact]
    public async Task Register_WhenRegistryAccepts_PersistsTunnelConfigAndStartsTunnel()
    {
        await File.WriteAllTextAsync(_configFile.FilePath, "{\"Supervisor\":{\"EnableMdns\":false},\"Other\":1}");
        var handler = RespondWith(HttpStatusCode.OK, RegistryResponseJson);
        var tunnel = CreateTunnel(installed: true);
        var sut = CreateSut(handler, tunnel, RegisteredOptions());

        var result = await sut.Register();

        StatusOf(result).Should().Be(200);
        handler.Requests.Single().Headers.Authorization!.ToString().Should().Be($"Bearer {RegistrySecret}");
        var supervisor = ReadSupervisorSection();
        supervisor.GetProperty("EnableTunnel").GetBoolean().Should().BeTrue();
        supervisor.GetProperty("TunnelToken").GetString().Should().Be("tunnel-token");
        supervisor.GetProperty("ServerId").GetString().Should().Be("srv-1");
        supervisor.GetProperty("EnableMdns").GetBoolean().Should().BeFalse();
        tunnel.RegisteredUrl.Should().Be("https://srv-1.tunnel.example.test");
    }

    [Fact]
    public async Task Unregister_WithoutServerId_Returns400()
    {
        var sut = CreateSut(RespondWith(HttpStatusCode.OK, "{}"), CreateTunnel());

        var result = await sut.Unregister();

        StatusOf(result).Should().Be(400);
    }

    [Fact]
    public async Task Unregister_WhenRegistryUnreachable_StopsLocallyAndWarns()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("no route"));
        var tunnel = CreateTunnel();
        tunnel.RegisteredUrl = "https://srv-1.tunnel.example.test";
        var sut = CreateSut(handler, tunnel, RegisteredOptions());

        var result = await sut.Unregister();

        StatusOf(result).Should().Be(200);
        BodyOf(result).GetProperty("Message").GetString().Should().Contain("Registry cleanup warning");
        tunnel.RegisteredUrl.Should().BeNull();
    }

    [Fact]
    public async Task Unregister_WhenRegistryDeletes_ClearsTunnelConfigButKeepsServerId()
    {
        await File.WriteAllTextAsync(
            _configFile.FilePath,
            "{\"Supervisor\":{\"EnableTunnel\":true,\"TunnelToken\":\"t\",\"TunnelUrl\":\"u\",\"ServerId\":\"srv-1\"}}");
        var handler = RespondWith(HttpStatusCode.NoContent, "{}");
        var sut = CreateSut(handler, CreateTunnel(), RegisteredOptions());

        var result = await sut.Unregister();

        StatusOf(result).Should().Be(200);
        var request = handler.Requests.Single();
        request.Method.Should().Be(HttpMethod.Delete);
        request.RequestUri!.ToString().Should().Be("https://registry.example.test/api/tunnels/srv-1");
        var supervisor = ReadSupervisorSection();
        supervisor.GetProperty("EnableTunnel").GetBoolean().Should().BeFalse();
        supervisor.TryGetProperty("TunnelToken", out _).Should().BeFalse();
        supervisor.GetProperty("ServerId").GetString().Should().Be("srv-1");
    }

    [Fact]
    public async Task Unregister_WithoutSecretOrConfigFile_SendsAnonymousDeleteAndWritesConfig()
    {
        var handler = RespondWith(HttpStatusCode.NoContent, "{}");
        var sut = CreateSut(handler, CreateTunnel(), new SupervisorOptions
        {
            ServerId = "srv-1",
            RegistryUrl = "https://registry.example.test"
        });

        await sut.Unregister();

        handler.Requests.Single().Headers.Authorization.Should().BeNull();
        ReadSupervisorSection().GetProperty("EnableTunnel").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Stop_ReturnsOk()
    {
        var sut = CreateSut(RespondWith(HttpStatusCode.OK, "{}"), CreateTunnel());

        var result = await sut.Stop();

        StatusOf(result).Should().Be(200);
    }

    [Fact]
    public async Task Start_WithUnknownType_Returns400()
    {
        var sut = CreateSut(RespondWith(HttpStatusCode.OK, "{}"), CreateTunnel());

        var result = await sut.Start(new TunnelStartRequest { Type = "permanent" });

        StatusOf(result).Should().Be(400);
    }

    [Fact]
    public async Task Start_NamedWithoutToken_Returns400()
    {
        var sut = CreateSut(RespondWith(HttpStatusCode.OK, "{}"), CreateTunnel());

        var result = await sut.Start(new TunnelStartRequest { Type = "named", Token = " " });

        StatusOf(result).Should().Be(400);
    }

    [Fact]
    public async Task Start_WhenCloudflaredMissing_ReportsError()
    {
        var sut = CreateSut(RespondWith(HttpStatusCode.OK, "{}"), CreateTunnel());

        var result = await sut.Start(new TunnelStartRequest { Type = "quick" });

        ((ObjectResult)result).Value.Should().BeOfType<TunnelStatusResponse>()
            .Which.Error.Should().Be("cloudflared is not installed.");
    }

    public void Dispose()
    {
        _configFile.Dispose();
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* Best effort cleanup */ }
    }

    private string MissingExecutable => Path.Combine(_tempDir, "cloudflared-missing");

    private SupervisorOptions RegisteredOptions() => new()
    {
        ServerId = "srv-1",
        RegistryUrl = "https://registry.example.test",
        RegistrySecret = RegistrySecret,
        CloudflaredPath = MissingExecutable
    };

    private CloudflareTunnelService CreateTunnel(bool installed = false)
    {
        var tunnel = new CloudflareTunnelService(
            Options.Create(new SupervisorOptions { CloudflaredPath = MissingExecutable }),
            NullLogger<CloudflareTunnelService>.Instance);
        if (installed)
        {
            // IsCloudflaredInstalled is only set by the hosted-service probe, which would
            // spawn a real cloudflared; set it directly so the controller logic can run.
            typeof(CloudflareTunnelService).GetProperty(nameof(CloudflareTunnelService.IsCloudflaredInstalled))!
                .SetValue(tunnel, true);
        }

        return tunnel;
    }

    private JsonElement ReadSupervisorSection()
        => JsonDocument.Parse(File.ReadAllText(_configFile.FilePath)).RootElement.GetProperty("Supervisor");

    private static StubHttpMessageHandler RespondWith(HttpStatusCode status, string json)
        => new(_ => StubHttpMessageHandler.Json(status, json));

    private static AdminTunnelController CreateSut(
        StubHttpMessageHandler handler, CloudflareTunnelService tunnel, SupervisorOptions? options = null)
        => new(tunnel, Options.Create(options ?? new SupervisorOptions()), new StubHttpClientFactory(handler))
        {
            ControllerContext = SupervisorFactory.ControllerContextFor("localhost", 5050)
        };
}
