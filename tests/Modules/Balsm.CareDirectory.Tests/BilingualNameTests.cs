using Balsm.CareDirectory.Domain;
using FluentAssertions;
using Xunit;

namespace Balsm.CareDirectory.Tests;

/// <summary>
/// 14% of Egyptian listings write both names into Overture's single name field.
/// Recovering those pairs is the largest source of bilingual names available
/// without a second dataset, so the split has to handle the real separators and
/// both orderings — all cases below are taken from the actual extract.
/// </summary>
public sealed class BilingualNameTests
{
    [Theory]
    [InlineData("Arizona Hospital - مستشفي الأريزونا", "Arizona Hospital", "مستشفي الأريزونا")]
    [InlineData("Alborg lab - معامل البرج", "Alborg lab", "معامل البرج")]
    [InlineData("Dr. Amir Zedan د.أمير زيدان", "Dr. Amir Zedan", "د.أمير زيدان")]
    [InlineData("Abd Elrahman Ahmed pharmacy .صيدلية الدكتور عبدالرحمن احمد",
        "Abd Elrahman Ahmed pharmacy", "صيدلية الدكتور عبدالرحمن احمد")]
    public void TrySplit_SplitsLatinFirstNames(string input, string expectedEn, string expectedAr)
    {
        BilingualName.TrySplit(input, out var en, out var ar).Should().BeTrue();
        en.Should().Be(expectedEn);
        ar.Should().Be(expectedAr);
    }

    [Theory]
    [InlineData("صيدليات شفيق - Shafik pharmacies", "Shafik pharmacies", "صيدليات شفيق")]
    [InlineData("معمل الضحي للتحاليل - Al Doha Lab", "Al Doha Lab", "معمل الضحي للتحاليل")]
    public void TrySplit_SplitsArabicFirstNames(string input, string expectedEn, string expectedAr)
    {
        BilingualName.TrySplit(input, out var en, out var ar).Should().BeTrue();
        en.Should().Be(expectedEn);
        ar.Should().Be(expectedAr);
    }

    [Fact]
    public void TrySplit_KeepsLatinEmbeddedInTheArabicHalf()
    {
        // The Latin "AUH" sits inside the Arabic name; splitting at the FIRST
        // transition keeps it there rather than truncating the Arabic side.
        BilingualName.TrySplit("Alexandria AUH Hospital - مستشفي الأسكندرية AUH", out var en, out var ar)
            .Should().BeTrue();
        en.Should().Be("Alexandria AUH Hospital");
        ar.Should().Be("مستشفي الأسكندرية AUH");
    }

    [Theory]
    [InlineData("Fixture Pharmacy One")]
    [InlineData("معمل الاختبار للاشعة")]
    [InlineData("")]
    [InlineData(null)]
    public void TrySplit_ReturnsFalseForSingleScriptNames(string? input)
    {
        BilingualName.TrySplit(input, out var en, out var ar).Should().BeFalse();
        en.Should().BeNull();
        ar.Should().BeNull();
    }

    [Fact]
    public void TrySplit_RefusesWhenAHalfIsTooShort()
    {
        // "Dr" is not a name. Keeping the string whole beats storing a fragment.
        BilingualName.TrySplit("Dr مستشفى الاختبار", out _, out _).Should().BeFalse();
    }

    [Fact]
    public void IsArabic_DetectsScript()
    {
        BilingualName.IsArabic("معمل").Should().BeTrue();
        BilingualName.IsArabic("Lab").Should().BeFalse();
    }
}
