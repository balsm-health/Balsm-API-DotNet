using Xunit;
using Balsm.CareDirectory.Infrastructure.MapPacks;

namespace Balsm.CareDirectory.Tests;

public sealed class GovernorateRegistryTests
{
    private const string Json = """
        [
          {
            "id": "cairo",
            "name_en": "Cairo",
            "name_ar": "القاهرة",
            "west": 31.21,
            "south": 29.75,
            "east": 31.91,
            "north": 30.32
          }
        ]
        """;

    [Fact]
    public void ParsesSnakeCaseFieldsIntoARef()
    {
        var registry = GovernorateRegistry.Parse(Json);

        var cairo = Assert.Single(registry);
        Assert.Equal("cairo", cairo.Id);
        Assert.Equal("Cairo", cairo.NameEn);
        Assert.Equal("القاهرة", cairo.NameAr);
        Assert.Equal(31.21, cairo.West);
        Assert.Equal(30.32, cairo.North);
    }

    [Fact]
    public void EmptyArrayParsesToAnEmptyList()
    {
        Assert.Empty(GovernorateRegistry.Parse("[]"));
    }

    [Theory]
    [InlineData(30.0, 31.5, true)]   // inside
    [InlineData(29.75, 31.21, true)] // on the boundary — inclusive
    [InlineData(28.0, 31.5, false)]  // south of the box
    [InlineData(30.0, 32.5, false)]  // east of the box
    public void ContainsIsBoundingBoxMembership(double lat, double lng, bool expected)
    {
        var cairo = GovernorateRegistry.Parse(Json)[0];

        Assert.Equal(expected, cairo.Contains(lat, lng));
    }
}
