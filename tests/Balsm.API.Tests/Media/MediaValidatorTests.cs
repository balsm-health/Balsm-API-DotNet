using Balsm.Infrastructure.Media;
using Balsm.SharedKernel.Media;
using FluentAssertions;
using Xunit;

namespace Balsm.API.Tests.Media;

public class MediaValidatorTests
{
    private static readonly string[] Locales = ["en", "ar"];

    private static readonly MediaAxisDto Theme = new("theme", "app");

    private static readonly MediaAxisDto Gender = new(
        "gender", "user", "Baby", [new MediaOptionDto("girl", "Girl"), new MediaOptionDto("boy", "Boy")], "girl");

    private static MediaRenditionDto Rendition(string url = "https://cdn.example.test/a.jpg", MediaFileDto? thumbnail = null, params (string Axis, string Value)[] when) =>
        new(when.ToDictionary(w => w.Axis, w => w.Value), url, "image/jpeg", 679, 480, 30000, thumbnail);

    private static MediaDto Media(IReadOnlyList<MediaAxisDto> axes, params MediaRenditionDto[] renditions) =>
        new("image", "Week 12", "art", "https://example.test/art", axes, renditions);

    private static MediaDto LightAndDark() => Media(
        [Theme],
        Rendition("https://cdn.example.test/w12.jpg", null, ("theme", "light")),
        Rendition("https://cdn.example.test/w12-dark.jpg", null, ("theme", "dark")));

    [Fact]
    public void Validate_LightAndDarkRenditions_HasNoProblems()
    {
        var problems = MediaValidator.Validate(LightAndDark(), Locales);

        problems.Should().BeEmpty();
    }

    [Fact]
    public void Validate_UserAxisWithDefaultRenditionAndHttpsThumbnail_HasNoProblems()
    {
        var thumbnail = new MediaFileDto("https://cdn.example.test/girl-thumb.jpg", "image/jpeg", 160, 113, 4100);
        var media = Media(
            [Theme, Gender],
            Rendition("https://cdn.example.test/girl.jpg", thumbnail, ("gender", "girl")),
            Rendition("https://cdn.example.test/boy-dark.jpg", null, ("gender", "boy"), ("theme", "dark")));

        var problems = MediaValidator.Validate(media, Locales);

        problems.Should().BeEmpty();
    }

    [Fact]
    public void Validate_KindNotImage_ReportsKind()
    {
        var media = LightAndDark() with { Kind = "video" };

        var problems = MediaValidator.Validate(media, Locales);

        problems.Should().ContainSingle().Which.Should().Contain("video");
    }

    [Theory]
    [InlineData("http://cdn.example.test/w12.jpg")]
    [InlineData("w12.jpg")]
    [InlineData("ftp://cdn.example.test/w12.jpg")]
    public void Validate_RenditionUrlNotAbsoluteHttps_ReportsUrl(string url)
    {
        var media = Media([], Rendition(url));

        var problems = MediaValidator.Validate(media, Locales);

        problems.Should().ContainSingle().Which.Should().Contain(url).And.Contain("https");
    }

    [Fact]
    public void Validate_ThumbnailUrlNotHttps_ReportsThumbnailUrl()
    {
        var thumbnail = new MediaFileDto("http://cdn.example.test/thumb.jpg", "image/jpeg", 160, 113, 4100);
        var media = Media([], Rendition(thumbnail: thumbnail));

        var problems = MediaValidator.Validate(media, Locales);

        problems.Should().ContainSingle().Which.Should().Contain("thumb.jpg");
    }

    [Fact]
    public void Validate_WhenNamesUndeclaredAxis_ReportsAxis()
    {
        var media = Media([], Rendition(when: [("theme", "light")]));

        var problems = MediaValidator.Validate(media, Locales);

        problems.Should().Contain(p => p.Contains("undeclared axis 'theme'"));
    }

    [Theory]
    [InlineData("theme", "dim")]
    [InlineData("locale", "fr")]
    [InlineData("platform", "windows")]
    public void Validate_AppAxisValueNotKnown_ReportsValue(string axis, string value)
    {
        var media = Media([new MediaAxisDto(axis, "app")], Rendition(), Rendition("https://cdn.example.test/b.jpg", null, (axis, value)));

        var problems = MediaValidator.Validate(media, Locales);

        problems.Should().ContainSingle().Which.Should().Contain($"'{value}'");
    }

    [Fact]
    public void Validate_UserAxisValueNotAnOption_ReportsValue()
    {
        var media = Media([Gender], Rendition(), Rendition("https://cdn.example.test/b.jpg", null, ("gender", "twins")));

        var problems = MediaValidator.Validate(media, Locales);

        problems.Should().ContainSingle().Which.Should().Contain("'twins'");
    }

    [Fact]
    public void Validate_UnknownAppAxis_ReportsAxis()
    {
        var media = Media([new MediaAxisDto("season", "app")], Rendition());

        var problems = MediaValidator.Validate(media, Locales);

        problems.Should().ContainSingle().Which.Should().Contain("season");
    }

    [Fact]
    public void Validate_UnknownAxisSource_ReportsSource()
    {
        var media = Media([new MediaAxisDto("theme", "server")], Rendition());

        var problems = MediaValidator.Validate(media, Locales);

        problems.Should().ContainSingle().Which.Should().Contain("server");
    }

    [Fact]
    public void Validate_AxisDeclaredTwice_ReportsAxis()
    {
        var media = Media([Theme, Theme], Rendition());

        var problems = MediaValidator.Validate(media, Locales);

        problems.Should().ContainSingle().Which.Should().Contain("twice");
    }

    [Theory]
    [InlineData("boy-and-girl")]
    [InlineData(null)]
    public void Validate_UserAxisDefaultNotAnOption_ReportsDefault(string? fallback)
    {
        var media = Media([Gender with { Default = fallback }], Rendition());

        var problems = MediaValidator.Validate(media, Locales);

        problems.Should().ContainSingle().Which.Should().Contain("default");
    }

    [Fact]
    public void Validate_UserAxisWithoutOptions_ReportsOptions()
    {
        var media = Media([Gender with { Options = null, Default = null }], Rendition());

        var problems = MediaValidator.Validate(media, Locales);

        problems.Should().Contain(p => p.Contains("no options"));
    }

    [Fact]
    public void Validate_OnlyDarkRendition_ReportsNoDefaultMatch()
    {
        var media = Media([Theme], Rendition(when: [("theme", "dark")]));

        var problems = MediaValidator.Validate(media, Locales);

        problems.Should().HaveCount(2).And.AllSatisfy(p => p.Should().Contain("default context"));
    }

    [Fact]
    public void Validate_OnlyNonDefaultUserOption_ReportsNoDefaultMatch()
    {
        var media = Media([Gender], Rendition(when: [("gender", "boy")]));

        var problems = MediaValidator.Validate(media, Locales);

        problems.Should().HaveCount(2).And.AllSatisfy(p => p.Should().Contain("default context"));
    }

    [Fact]
    public void Validate_LocaleRenditionForOneLanguageOnly_ReportsTheOtherLocale()
    {
        var media = Media([new MediaAxisDto("locale", "app")], Rendition(when: [("locale", "en")]));

        var problems = MediaValidator.Validate(media, Locales);

        problems.Should().ContainSingle().Which.Should().Contain("locale 'ar'");
    }

    [Fact]
    public void Validate_PlatformRenditionsOnly_ReportsNoDefaultMatch()
    {
        var media = Media([new MediaAxisDto("platform", "app")], Rendition(when: [("platform", "ios")]));

        var problems = MediaValidator.Validate(media, ["en"]);

        problems.Should().ContainSingle().Which.Should().Contain("default context");
    }

    [Fact]
    public void Validate_NoRenditions_ReportsNoRenditions()
    {
        var media = Media([Theme]);

        var problems = MediaValidator.Validate(media, Locales);

        problems.Should().ContainSingle().Which.Should().Be("no renditions");
    }
}
