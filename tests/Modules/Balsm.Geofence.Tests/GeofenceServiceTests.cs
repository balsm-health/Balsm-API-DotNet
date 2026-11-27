using Balsm.Geofence.Domain.Entities;
using Balsm.Geofence.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace Balsm.Geofence.Tests;

public sealed class GeofenceServiceTests : IDisposable
{
    private readonly GeofenceTestDb _db = new();

    public void Dispose() => _db.Dispose();

    private async Task<bool> IsDeniedAsync(string countryCode)
    {
        await using var context = _db.CreateContext();
        return await new GeofenceService(context).IsDeniedAsync(countryCode, CancellationToken.None);
    }

    [Theory]
    [InlineData("CU")]
    [InlineData("IR")]
    [InlineData("KP")]
    [InlineData("SY")]
    public async Task IsDeniedAsync_WhenCountryInSeededBlocklist_ReturnsTrue(string countryCode)
    {
        var denied = await IsDeniedAsync(countryCode);

        denied.Should().BeTrue();
    }

    [Fact]
    public async Task IsDeniedAsync_WhenCodeIsLowercase_MatchesCaseInsensitively()
    {
        var denied = await IsDeniedAsync("ir");

        denied.Should().BeTrue();
    }

    [Theory]
    [InlineData("EG")]
    [InlineData("SA")]
    public async Task IsDeniedAsync_WhenCountryNotInBlocklist_ReturnsFalse(string countryCode)
    {
        var denied = await IsDeniedAsync(countryCode);

        denied.Should().BeFalse();
    }

    [Fact]
    public async Task IsDeniedAsync_WhenCountryAddedManually_ReturnsTrue()
    {
        await using (var context = _db.CreateContext())
        {
            context.DeniedCountries.Add(DeniedCountryBlocklist.Create("xx", "manual"));
            await context.SaveChangesAsync();
        }

        var denied = await IsDeniedAsync("XX");

        denied.Should().BeTrue();
    }
}
