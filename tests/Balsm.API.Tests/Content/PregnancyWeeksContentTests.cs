using System.Text;
using System.Text.Json;
using Balsm.API.Content;
using FluentAssertions;
using Xunit;

namespace Balsm.API.Tests.Content;

public class PregnancyWeeksContentTests
{
    private static string Text(string en) => $$"""{"en":"{{en}}","ar":"{{en}}-ar"}""";

    private static string Week(int w) =>
        $$"""{"week":{{w}},"title":{{Text("t")}},"summary":{{Text("b")}},"symptoms":["sym"],"feelings":{{Text("f")}},"tips":[{{Text("tip")}}],"tests_due":["bp"],"danger_signs":["bleeding"],"topics":["visits"],"sources":["src"],"baby":{"fact":{{Text("fact")}},"fetal_size":{"length_mm":null,"weight_g":{{w * 10}},"comparison":null},"media":{{Media(w)}},"sources":["src"]} }""";

    private static string Rendition(string when, string url) =>
        $$"""{"when":{{{when}}},"url":"{{url}}","mime":"image/jpeg","width":679,"height":480,"bytes":30000}""";

    private static string Media(int w) =>
        $$"""{"kind":"image","alt":{{Text($"Week {w}")}},"credit":"art","source_url":"https://example.test/art","axes":[{"id":"theme","source":"app"}],"renditions":[{{Rendition("\"theme\":\"light\"", $"https://cdn.example.test/w{w}.jpg")}},{{Rendition("\"theme\":\"dark\"", $"https://cdn.example.test/w{w}-dark.jpg")}}]}""";

    private static string Document(IEnumerable<int> weeks, string version = "v-test") =>
        $$"""{"version":"{{version}}","source":{{Text("s")}},"disclaimer":{{Text("d")}},"multiples_note":{{Text("m")}},"labels":{{{string.Join(",", PregnancyWeeksContent.RequiredLabels.Select(k => $"\"{k}\":{Text(k)}"))}}},"sources":[{"id":"src","title":"t","publisher":"p","url":"https://example.test/","accessed":"2026-11-24"}],"symptom_guide":{"sym":{"label":{{Text("Nausea")}},"self_care":{{Text("Ginger")}},"sources":["src"]} },"danger_sign_labels":{"bleeding":{{Text("Bleeding")}}},"test_labels":{"bp":{{Text("Blood pressure")}}},"topic_labels":{"visits":{{Text("Visits")}}},"illustration_credits":{"art":{"author":"a","license":"CC BY-ND 4.0","license_url":"https://example.test/l","author_url":"https://example.test/a","text":{{Text("Illustration: a")}} } },"weeks":[{{string.Join(",", weeks.Select(Week))}}]}""";

    private static PregnancyWeeksContent Load(string json) => PregnancyWeeksContent.Load(new MemoryStream(Encoding.UTF8.GetBytes(json)));

    [Fact]
    public void Current_ShippedFile_ListsWeeks4To40()
    {
        var content = PregnancyWeeksContent.Current;

        content.File.Weeks.Select(w => w.Week).Should().Equal(Enumerable.Range(4, 37));
    }

    [Fact]
    public void Load_ValidDocument_KeepsSizesKeysAndSourcesOnTheWire()
    {
        var content = Load(Document(Enumerable.Range(4, 37))).For("en");

        using var body = JsonDocument.Parse(content.Body);
        var data = body.RootElement.GetProperty("data");
        var week = data.GetProperty("weeks")[0];
        (week.GetProperty("baby").GetProperty("fetal_size").GetProperty("weight_g").GetInt32(),
            week.GetProperty("baby").GetProperty("fetal_size").GetProperty("length_mm").ValueKind,
            week.GetProperty("danger_signs")[0].GetString(),
            data.GetProperty("danger_sign_labels").GetProperty("bleeding").GetString(),
            data.GetProperty("sources")[0].GetProperty("accessed").GetString())
            .Should().Be((40, JsonValueKind.Null, "bleeding", "Bleeding", "2026-11-24"));
    }

    [Theory]
    [InlineData("\"sources\":[\"src\"]", "\"sources\":[\"nope\"]")]
    [InlineData("\"danger_signs\":[\"bleeding\"]", "\"danger_signs\":[\"nope\"]")]
    [InlineData("\"tests_due\":[\"bp\"]", "\"tests_due\":[\"nope\"]")]
    [InlineData("\"credit\":\"art\",\"source_url\"", "\"credit\":\"nope\",\"source_url\"")]
    [InlineData("\"topics\":[\"visits\"]", "\"topics\":[\"nope\"]")]
    [InlineData("\"symptoms\":[\"sym\"]", "\"symptoms\":[\"nope\"]")]
    public void Load_WeekCitesUndefinedKey_Throws(string cited, string undefined)
    {
        var json = Document(Enumerable.Range(4, 37)).Replace(cited, undefined);

        var act = () => Load(json);

        act.Should().Throw<InvalidDataException>().WithMessage("*nope*");
    }

    [Fact]
    public void Load_WeekWithoutArtwork_KeepsNullMedia()
    {
        var json = Document(Enumerable.Range(4, 37)).Replace(Media(4), "null");

        var content = Load(json);

        (content.File.Weeks[0].Baby.Media, content.File.Weeks[1].Baby.Media!.Credit).Should().Be((null, "art"));
    }

    [Theory]
    [InlineData("\"url\":\"https://cdn.example.test/w4.jpg\"", "\"url\":\"http://cdn.example.test/w4.jpg\"", "*https*")]
    [InlineData("\"theme\":\"dark\"}", "\"theme\":\"dim\"}", "*dim*")]
    [InlineData("\"kind\":\"image\"", "\"kind\":\"video\"", "*video*")]
    public void Load_InvalidMedia_ThrowsNamingTheWeek(string valid, string invalid, string message)
    {
        var json = Document(Enumerable.Range(4, 37)).Replace(valid, invalid);

        var act = () => Load(json);

        act.Should().Throw<InvalidDataException>().WithMessage("*week 4*").WithMessage(message);
    }

    [Fact]
    public void Load_MediaRenditions_ReachTheWireWithThemeAndSize()
    {
        var content = Load(Document(Enumerable.Range(4, 37))).For("en");

        using var body = JsonDocument.Parse(content.Body);
        var media = body.RootElement.GetProperty("data").GetProperty("weeks")[0].GetProperty("baby").GetProperty("media");
        var dark = media.GetProperty("renditions")[1];
        (media.GetProperty("kind").GetString(),
            media.GetProperty("alt").GetString(),
            media.GetProperty("credit").GetString(),
            media.GetProperty("axes")[0].GetProperty("source").GetString(),
            dark.GetProperty("when").GetProperty("theme").GetString(),
            dark.GetProperty("url").GetString(),
            dark.GetProperty("width").GetInt32(),
            dark.GetProperty("thumbnail").ValueKind)
            .Should().Be(("image", "Week 4", "art", "app", "dark", "https://cdn.example.test/w4-dark.jpg", 679, JsonValueKind.Null));
    }

    [Theory]
    [InlineData("en", "https://cdn.example.test/w4-en.jpg")]
    [InlineData("ar", "https://cdn.example.test/w4-ar.jpg")]
    public void For_LocaleRenditions_SendsOnlyTheRequestLanguageAndUnlocalizedOnes(string language, string expected)
    {
        var localized = $$"""{"kind":"image","alt":{{Text("Week 4")}},"axes":[{"id":"locale","source":"app"}],"renditions":[{{Rendition("\"locale\":\"en\"", "https://cdn.example.test/w4-en.jpg")}},{{Rendition("\"locale\":\"ar\"", "https://cdn.example.test/w4-ar.jpg")}},{{Rendition("", "https://cdn.example.test/w4.jpg")}}]}""";
        var json = Document(Enumerable.Range(4, 37)).Replace(Media(4), localized);

        var media = Load(json).For(language).Document.Weeks[0].Baby.Media!;

        media.Renditions.Select(r => r.Url).Should().Equal(expected, "https://cdn.example.test/w4.jpg");
    }

    [Fact]
    public void For_UserAxis_SendsLabelsInTheRequestLanguage()
    {
        var withUserAxis = Media(4).Replace(
            "\"axes\":[{\"id\":\"theme\",\"source\":\"app\"}]",
            "\"axes\":[{\"id\":\"theme\",\"source\":\"app\"}," +
            $$"""{"id":"gender","source":"user","label":{{Text("Baby")}},"options":[{"value":"girl","label":{{Text("Girl")}}},{"value":"boy","label":{{Text("Boy")}}}],"default":"girl"}]""");
        var json = Document(Enumerable.Range(4, 37)).Replace(Media(4), withUserAxis);

        var axis = Load(json).For("ar").Document.Weeks[0].Baby.Media!.Axes[1];

        (axis.Label, axis.Options![1].Label, axis.Default).Should().Be(("Baby-ar", "Boy-ar", "girl"));
    }

    [Fact]
    public void Load_MissingLabel_Throws()
    {
        var json = Document(Enumerable.Range(4, 37)).Replace("\"sources_more\":", "\"sources_moar\":");

        var act = () => Load(json);

        act.Should().Throw<InvalidDataException>().WithMessage("*sources_more*");
    }

    [Fact]
    public void Load_MissingWeekField_Throws()
    {
        var json = Document(Enumerable.Range(4, 37)).Replace("\"topics\":[\"visits\"],", "");

        var act = () => Load(json);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Load_ValidDocument_WrapsItInADataEnvelope()
    {
        var content = Load(Document(Enumerable.Range(4, 37))).For("en");

        using var body = JsonDocument.Parse(content.Body);
        body.RootElement.GetProperty("data").GetProperty("multiples_note").GetString().Should().Be("m");
    }

    [Fact]
    public void For_Arabic_SendsEveryTextInArabicOnly()
    {
        var content = Load(Document(Enumerable.Range(4, 37))).For("ar");

        using var body = JsonDocument.Parse(content.Body);
        var data = body.RootElement.GetProperty("data");
        var week = data.GetProperty("weeks")[0];
        (data.GetProperty("language").GetString(),
            data.GetProperty("source").GetString(),
            data.GetProperty("labels").GetProperty("sources_more").GetString(),
            data.GetProperty("illustration_credits").GetProperty("art").GetProperty("text").GetString(),
            week.GetProperty("title").GetString(),
            week.GetProperty("tips")[0].GetString(),
            data.GetProperty("symptom_guide").GetProperty("sym").GetProperty("self_care").GetString())
            .Should().Be(("ar", "s-ar", "sources_more-ar", "Illustration: a-ar", "t-ar", "tip-ar", "Ginger-ar"));
    }

    [Fact]
    public void For_Arabic_WritesArabicAsUtf8NotEscapes()
    {
        var json = Document(Enumerable.Range(4, 37)).Replace("\"s-ar\"", "\"مصدر\"");

        var body = Encoding.UTF8.GetString(Load(json).For("ar").Body);

        body.Should().Contain("\"source\":\"مصدر\"").And.NotContain("\\u0645");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fr")]
    [InlineData("EN")]
    public void For_AnythingButArabic_IsEnglish(string? requested)
    {
        var content = Load(Document(Enumerable.Range(4, 37)));

        content.For(requested).Document.Language.Should().Be("en");
    }

    [Fact]
    public void For_EachLanguage_HasItsOwnETagAndAboutHalfTheBytesOfBoth()
    {
        var json = Document(Enumerable.Range(4, 37));
        var content = Load(json);

        var (en, ar) = (content.For("en"), content.For("AR"));

        en.ETag.Should().NotBe(ar.ETag);
        ar.Document.Language.Should().Be("ar");
        en.Body.Length.Should().BeLessThan(Encoding.UTF8.GetByteCount(json));
    }

    [Fact]
    public void Load_SameBytes_ProducesSameStrongETag()
    {
        var json = Document(Enumerable.Range(4, 37));

        var (a, b) = (Load(json).For("ar").ETag, Load(json).For("ar").ETag);

        a.Should().Be(b);
        a.IsWeak.Should().BeFalse();
    }

    [Fact]
    public void Load_DifferentBytes_ProducesDifferentETag()
    {
        var a = Load(Document(Enumerable.Range(4, 37), "v1")).For("en").ETag;
        var b = Load(Document(Enumerable.Range(4, 37), "v2")).For("en").ETag;

        a.Should().NotBe(b);
    }

    [Fact]
    public void Load_MissingWeek_Throws()
    {
        var act = () => Load(Document(Enumerable.Range(4, 36)));

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Load_BlankVersion_Throws()
    {
        var act = () => Load(Document(Enumerable.Range(4, 37), " "));

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Load_UnknownField_Throws()
    {
        var json = Document(Enumerable.Range(4, 37)).Replace("\"version\"", "\"extra\":1,\"version\"");

        var act = () => Load(json);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Load_MissingLanguage_Throws()
    {
        var json = Document(Enumerable.Range(4, 37)).Replace("""{"en":"s","ar":"s-ar"}""", """{"en":"s"}""");

        var act = () => Load(json);

        act.Should().Throw<JsonException>();
    }
}
