using Balsam.Supervisor.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Balsam.Supervisor.Tests.Services;

public class NetworkDiscoveryServiceTests
{
    private readonly NetworkDiscoveryService _sut;

    public NetworkDiscoveryServiceTests()
    {
        _sut = new NetworkDiscoveryService(NullLogger<NetworkDiscoveryService>.Instance);
    }

    [Fact]
    public void GetLanAddresses_ReturnsNonLoopbackAddresses()
    {
        var addresses = _sut.GetLanAddresses();

        // Every returned address should be non-loopback
        foreach (var (address, _, _) in addresses)
        {
            System.Net.IPAddress.IsLoopback(address).Should().BeFalse();
        }
    }

    [Fact]
    public void GetNetworkInfo_ReturnsHostname()
    {
        var info = _sut.GetNetworkInfo();

        info.Hostname.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetNetworkInfo_SetsMdnsFields()
    {
        var info = _sut.GetNetworkInfo(port: 5000);

        info.MdnsHostname.Should().Be("balsam.local");
        info.MdnsApiUrl.Should().Be("http://balsam.local:5000");
    }

    [Fact]
    public void GetNetworkInfo_LanAddresses_ContainCorrectUrls()
    {
        var info = _sut.GetNetworkInfo(port: 8080);

        foreach (var addr in info.LanAddresses)
        {
            addr.ApiUrl.Should().Contain(":8080");
            addr.AdminUrl.Should().Contain(":8080/admin");
            addr.IpAddress.Should().NotBeNullOrEmpty();
            addr.InterfaceName.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void FormatConsoleBanner_ContainsLocalhost()
    {
        var banner = _sut.FormatConsoleBanner(5000);

        banner.Should().Contain("localhost:5000");
        banner.Should().Contain("localhost:5000/admin");
        banner.Should().Contain("balsam.local");
        banner.Should().Contain("Balsam");
    }
}
