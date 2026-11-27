using Balsm.Geofence.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Balsm.Geofence.Tests;

public sealed class DeniedCountryBlocklistTests
{
    [Fact]
    public void Create_WithLowercaseCode_StoresUppercaseCode()
    {
        var entry = DeniedCountryBlocklist.Create("xx", "manual");

        entry.CountryCode.Should().Be("XX");
    }

    [Fact]
    public void Create_WithSource_StoresSource()
    {
        var entry = DeniedCountryBlocklist.Create("XX", "apple_denied");

        entry.Source.Should().Be("apple_denied");
    }

    [Fact]
    public void Create_Always_StampsAddedAtWithCurrentTime()
    {
        var before = DateTime.UtcNow;

        var entry = DeniedCountryBlocklist.Create("XX", "manual");

        entry.AddedAt.Should().BeOnOrAfter(before);
    }
}
