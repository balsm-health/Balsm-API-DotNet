using System.Text.Json;
using Balsm.Supervisor.Controllers;
using Balsm.Supervisor.Models;
using Balsm.Supervisor.Tests.Support;
using FluentAssertions;
using Xunit;
using static Balsm.Supervisor.Tests.Support.ActionResultReader;

namespace Balsm.Supervisor.Tests.Controllers;

[Collection(ProcessStateCollection.Name)]
public sealed class AdminModeControllerTests : IDisposable
{
    private readonly BaseDirectoryFileScope _configFile = new("appsettings.Production.json");
    private readonly BaseDirectoryFileScope _certFile = new("supervisor.pfx");
    private readonly FakeHostApplicationLifetime _lifetime = new();

    [Fact]
    public void Get_WithoutProductionConfig_ReportsLocalModeAndConfiguredPorts()
    {
        var sut = CreateSut(new Dictionary<string, string?> { ["Server:Urls"] = "http://0.0.0.0:6060" });

        var body = BodyOf(sut.Get());

        body.GetProperty("mode").GetString().Should().Be("local");
        body.GetProperty("http_port").GetInt32().Should().Be(6060);
        body.GetProperty("https_port").GetInt32().Should().Be(6061);
    }

    [Theory]
    [InlineData("{\"Server\":{\"Mode\":\"public\"}}", "public")]
    [InlineData("{\"Server\":{\"Urls\":\"http://0.0.0.0:5050\"}}", "network")]
    [InlineData("{\"Server\":{\"Urls\":\"http://localhost:5050\"}}", "local")]
    [InlineData("not json", "local")]
    public void Get_WithProductionConfig_ReportsPersistedMode(string config, string expectedMode)
    {
        File.WriteAllText(_configFile.FilePath, config);
        var sut = CreateSut();

        var body = BodyOf(sut.Get());

        body.GetProperty("mode").GetString().Should().Be(expectedMode);
    }

    [Fact]
    public void Get_WhenHttpPortIs80_ReportsHttpsPort443()
    {
        var sut = CreateSut(new Dictionary<string, string?> { ["Server:Urls"] = "http://localhost:80" });

        var body = BodyOf(sut.Get());

        body.GetProperty("https_port").GetInt32().Should().Be(443);
    }

    [Fact]
    public async Task Put_WithUnknownMode_Returns400AndDoesNotRestart()
    {
        var sut = CreateSut();

        var result = await sut.Put(new ModeChangeRequest { Mode = "cloud", Port = 5050 });

        StatusOf(result).Should().Be(400);
        _lifetime.StopRequests.Should().Be(0);
        File.Exists(_configFile.FilePath).Should().BeFalse();
    }

    [Theory]
    [InlineData("network", "http://0.0.0.0:7070")]
    [InlineData("local", "http://localhost:7070")]
    public async Task Put_WithValidMode_PersistsModeAndRequestsRestart(string mode, string expectedUrls)
    {
        var sut = CreateSut();

        var result = await sut.Put(new ModeChangeRequest { Mode = mode, Port = 7070 });

        StatusOf(result).Should().Be(200);
        _lifetime.StopRequests.Should().Be(1);
        var server = JsonDocument.Parse(File.ReadAllText(_configFile.FilePath)).RootElement.GetProperty("Server");
        server.GetProperty("Urls").GetString().Should().Be(expectedUrls);
        server.GetProperty("Mode").GetString().Should().Be(mode);
    }

    public void Dispose()
    {
        _configFile.Dispose();
        _certFile.Dispose();
    }

    private AdminModeController CreateSut(IDictionary<string, string?>? configuration = null)
        => new(SupervisorFactory.CreateStatusService(_lifetime, configuration));
}
