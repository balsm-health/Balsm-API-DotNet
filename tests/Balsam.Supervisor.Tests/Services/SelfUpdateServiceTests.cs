using Balsam.Supervisor.Services;
using FluentAssertions;
using Xunit;

namespace Balsam.Supervisor.Tests.Services;

public class SelfUpdateServiceTests
{
    [Theory]
    [InlineData("1.1.0", "1.0.0", true)]
    [InlineData("2.0.0", "1.9.9", true)]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("1.0.0", "1.1.0", false)]
    [InlineData("0.9.0", "1.0.0", false)]
    public void IsNewer_ComparesVersionsCorrectly(string latest, string current, bool expected)
    {
        SelfUpdateService.IsNewer(latest, current).Should().Be(expected);
    }

    [Theory]
    [InlineData("invalid", "1.0.0", false)]
    [InlineData("1.0.0", "invalid", false)]
    [InlineData("", "", false)]
    public void IsNewer_HandlesInvalidVersions(string latest, string current, bool expected)
    {
        SelfUpdateService.IsNewer(latest, current).Should().Be(expected);
    }

    [Fact]
    public void GetCurrentRid_ReturnsValidRid()
    {
        var rid = SelfUpdateService.GetCurrentRid();

        rid.Should().BeOneOf("osx-arm64", "osx-x64", "linux-x64", "win-x64");
    }
}
