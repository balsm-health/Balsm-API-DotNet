using Balsm.Supervisor.Configuration;
using Balsm.Supervisor.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Balsm.Supervisor.Tests.Services;

public class ConnectionInfoServiceTests
{
    private readonly string _tempDir;
    private readonly ConnectionInfoService _sut;

    public ConnectionInfoServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"balsm-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        var options = Options.Create(new SupervisorOptions
        {
            ConnectionInfoPath = Path.Combine(_tempDir, "connection-info.txt")
        });

        var networkDiscovery = new NetworkDiscoveryService(
            NullLogger<NetworkDiscoveryService>.Instance);

        _sut = new ConnectionInfoService(
            options,
            networkDiscovery,
            NullLogger<ConnectionInfoService>.Instance);
    }

    [Fact]
    public async Task WriteAndRead_RoundTrip()
    {
        await _sut.WriteConnectionInfoAsync();

        var content = await _sut.ReadConnectionInfoAsync();

        content.Should().NotBeNull();
        content.Should().Contain("Balsm");
        content.Should().Contain("localhost");
    }

    [Fact]
    public async Task ReadConnectionInfo_WhenFileDoesNotExist_ReturnsNull()
    {
        var content = await _sut.ReadConnectionInfoAsync();

        content.Should().BeNull();
    }

    [Fact]
    public async Task WriteConnectionInfo_CreatesFile()
    {
        var path = Path.Combine(_tempDir, "connection-info.txt");
        File.Exists(path).Should().BeFalse();

        await _sut.WriteConnectionInfoAsync();

        File.Exists(path).Should().BeTrue();
    }
}
