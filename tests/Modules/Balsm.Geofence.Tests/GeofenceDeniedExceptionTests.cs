using Balsm.Geofence.Domain;
using FluentAssertions;
using Xunit;

namespace Balsm.Geofence.Tests;

public sealed class GeofenceDeniedExceptionTests
{
    [Fact]
    public void Constructor_WithCountryCode_NamesCountryInMessage()
    {
        var exception = new GeofenceDeniedException("IR");

        exception.Message.Should().Be("Country 'IR' is blocked by OFAC sanctions list");
    }
}
