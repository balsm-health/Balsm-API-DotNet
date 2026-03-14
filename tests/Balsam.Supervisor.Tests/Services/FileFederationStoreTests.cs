using Balsam.Supervisor.Configuration;
using Balsam.Supervisor.Models;
using Balsam.Supervisor.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Balsam.Supervisor.Tests.Services;

public class FileFederationStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileFederationStore _sut;

    public FileFederationStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"balsam-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        var options = Options.Create(new SupervisorOptions
        {
            FederationDataPath = Path.Combine(_tempDir, "federation-pairings.json")
        });

        _sut = new FileFederationStore(options, NullLogger<FileFederationStore>.Instance);
    }

    [Fact]
    public async Task LoadAsync_WhenNoFile_ReturnsEmptyData()
    {
        var data = await _sut.LoadAsync();

        data.Should().NotBeNull();
        data.Pairings.Should().NotBeNull();
        data.Pairings.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var data = new FederationData();
        data.Pairings.Add(new ServerPairing
        {
            ServerId = "server-1",
            ServerName = "Test Server",
            ServerUrl = "https://test.example.com",
            OurApiKey = "our-key-base64",
            TheirApiKey = "their-key-base64",
            Status = PairingStatus.Active,
            PairedAt = DateTime.UtcNow
        });

        await _sut.SaveAsync(data);
        var loaded = await _sut.LoadAsync();

        loaded.Pairings.Should().HaveCount(1);
        loaded.Pairings[0].ServerId.Should().Be("server-1");
        loaded.Pairings[0].ServerName.Should().Be("Test Server");
        loaded.Pairings[0].ServerUrl.Should().Be("https://test.example.com");
        loaded.Pairings[0].OurApiKey.Should().Be("our-key-base64");
        loaded.Pairings[0].TheirApiKey.Should().Be("their-key-base64");
        loaded.Pairings[0].Status.Should().Be(PairingStatus.Active);
    }

    [Fact]
    public async Task SaveAsync_OverwritesExistingData()
    {
        var data1 = new FederationData();
        data1.Pairings.Add(new ServerPairing
        {
            ServerId = "server-1",
            ServerName = "First",
            ServerUrl = "https://first.example.com",
            OurApiKey = "key1",
            TheirApiKey = "key2"
        });
        await _sut.SaveAsync(data1);

        var data2 = new FederationData();
        data2.Pairings.Add(new ServerPairing
        {
            ServerId = "server-2",
            ServerName = "Second",
            ServerUrl = "https://second.example.com",
            OurApiKey = "key3",
            TheirApiKey = "key4"
        });
        await _sut.SaveAsync(data2);

        var loaded = await _sut.LoadAsync();
        loaded.Pairings.Should().HaveCount(1);
        loaded.Pairings[0].ServerId.Should().Be("server-2");
    }

    [Fact]
    public async Task ConcurrentAccess_DoesNotCorrupt()
    {
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            var data = new FederationData();
            data.Pairings.Add(new ServerPairing
            {
                ServerId = $"server-{i}",
                ServerName = $"Server {i}",
                ServerUrl = $"https://s{i}.example.com",
                OurApiKey = $"key-{i}",
                TheirApiKey = $"their-{i}"
            });
            await _sut.SaveAsync(data);
        });

        await Task.WhenAll(tasks);

        var loaded = await _sut.LoadAsync();
        loaded.Pairings.Should().HaveCount(1); // Last write wins
        loaded.Pairings[0].ServerId.Should().NotBeNullOrEmpty();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* Best effort cleanup */ }
    }
}
