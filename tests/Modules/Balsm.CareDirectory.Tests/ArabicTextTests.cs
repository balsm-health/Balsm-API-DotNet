using Balsm.CareDirectory.Domain;
using FluentAssertions;
using Xunit;

namespace Balsm.CareDirectory.Tests;

/// <summary>
/// Egyptian listings spell the same word several ways — أشعة and اشعة both occur
/// freely — so raw string comparison silently drops a large share of Arabic
/// matches. Normalising both sides is what makes directory search work at all.
/// </summary>
public sealed class ArabicTextTests
{
    [Theory]
    [InlineData("أشعة", "اشعه")]        // hamza-above alif -> bare alif, ta marbuta -> ha
    [InlineData("اشعة", "اشعه")]        // already bare alif
    [InlineData("إشعاع", "اشعاع")]      // hamza-below alif
    [InlineData("آمنة", "امنه")]        // madda alif
    [InlineData("مستشفى", "مستشفي")]    // alif maqsura -> ya
    [InlineData("مـستـشفى", "مستشفي")]  // tatweel stripped
    public void Normalize_FoldsOrthographicVariants(string input, string expected)
        => ArabicText.Normalize(input).Should().Be(expected);

    [Fact]
    public void Normalize_MakesBothSpellingsOfRadiologyEqual()
        => ArabicText.Normalize("مركز الشروق للأشعة")
            .Should().Be(ArabicText.Normalize("مركز الشروق للاشعة"));

    [Fact]
    public void Normalize_StripsDiacritics()
        => ArabicText.Normalize("مُسْتَشْفَى").Should().Be(ArabicText.Normalize("مستشفى"));

    [Fact]
    public void Normalize_PassesLatinThroughLowercased()
        => ArabicText.Normalize("Cairo Scan").Should().Be("cairo scan");

    [Fact]
    public void Normalize_ReturnsNullForNull()
        => ArabicText.Normalize(null).Should().BeNull();

    [Fact]
    public void Normalize_IsIdempotent()
    {
        var once = ArabicText.Normalize("مستشفى قصر العيني");
        ArabicText.Normalize(once).Should().Be(once);
    }
}
