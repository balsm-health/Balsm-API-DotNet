using Balsm.Supervisor.Configuration;
using Balsm.Supervisor.Controllers;
using Balsm.Supervisor.Models;
using Balsm.Supervisor.Services;
using Balsm.Supervisor.Tests.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using static Balsm.Supervisor.Tests.Support.ActionResultReader;

namespace Balsm.Supervisor.Tests.Controllers;

[Collection(ProcessStateCollection.Name)]
public sealed class AdminNetworkControllerTests : IDisposable
{
    private readonly BaseDirectoryFileScope _configFile = new("appsettings.Production.json");
    private readonly BaseDirectoryFileScope _certFile = new("supervisor.pfx");
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"balsm-network-{Guid.NewGuid():N}");
    private readonly NetworkDiscoveryService _networkDiscovery = new(NullLogger<NetworkDiscoveryService>.Instance);
    private readonly ServerStatusService _statusService =
        SupervisorFactory.CreateStatusService(new FakeHostApplicationLifetime());
    private readonly MdnsService _mdnsService;
    private readonly ConnectionInfoService _connectionInfo;

    public AdminNetworkControllerTests()
    {
        Directory.CreateDirectory(_tempDir);
        var options = Options.Create(new SupervisorOptions
        {
            EnableMdns = false,
            ConnectionInfoPath = Path.Combine(_tempDir, "connection-info.txt")
        });
        _mdnsService = new MdnsService(
            options,
            _networkDiscovery,
            SupervisorFactory.CreateAuthService(SupervisorFactory.CreateStore()),
            _statusService,
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MdnsService>.Instance);
        _connectionInfo = new ConnectionInfoService(options, _networkDiscovery, NullLogger<ConnectionInfoService>.Instance);
    }

    [Fact]
    public async Task Get_WhenMdnsNotRunning_ReportsStaticHostnameAndNoPublicIp()
    {
        var sut = CreateSut(port: 6060);

        var result = await sut.Get();

        ((ObjectResult)result).Value.Should().BeOfType<NetworkInfoResponse>()
            .Which.Should().Match<NetworkInfoResponse>(i =>
                !i.MdnsRegistered && i.MdnsApiUrl == "http://balsm.local:6060" && i.PublicIp == null);
    }

    [Fact]
    public async Task Get_WhenMdnsAdvertisesName_ReportsAdvertisedHostname()
    {
        typeof(MdnsService).GetProperty(nameof(MdnsService.MdnsName))!.SetValue(_mdnsService, "balsm-clinic");
        var sut = CreateSut(port: null);

        var result = await sut.Get();

        ((ObjectResult)result).Value.Should().BeOfType<NetworkInfoResponse>()
            .Which.Should().Match<NetworkInfoResponse>(i =>
                i.MdnsHostname == "balsm-clinic.local" && i.MdnsApiUrl == "http://balsm-clinic.local:5050");
    }

    [Fact]
    public async Task GetConnectionInfo_WhenNotGenerated_Returns404()
    {
        var sut = CreateSut();

        var result = await sut.GetConnectionInfo();

        StatusOf(result).Should().Be(404);
    }

    [Fact]
    public async Task GetConnectionInfo_WhenGenerated_ReturnsContent()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "connection-info.txt"), "Connect to balsm.local");
        var sut = CreateSut();

        var result = await sut.GetConnectionInfo();

        BodyOf(result).GetProperty("Content").GetString().Should().Be("Connect to balsm.local");
    }

    public void Dispose()
    {
        _mdnsService.Dispose();
        _configFile.Dispose();
        _certFile.Dispose();
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* Best effort cleanup */ }
    }

    private AdminNetworkController CreateSut(int? port = 5050)
        => new(_networkDiscovery, _mdnsService, _connectionInfo, _statusService)
        {
            ControllerContext = SupervisorFactory.ControllerContextFor("localhost", port)
        };
}
