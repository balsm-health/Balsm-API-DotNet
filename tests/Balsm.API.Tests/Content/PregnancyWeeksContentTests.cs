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
        $$"""{"week":{{w}},"title":{{Text("t")}},"summary":{{Text("b")}},"symptoms":["sym"],"feelings":{{Text("f")}},"tips":[{{Text("tip")}}],"tests_due":["bp"],"danger_signs":["bleeding"],"topics":["visits"],"sources":["src"],"baby":{"fact":{{Text("fact")}},"fetal_size":{"length_mm":null,"weight_g":{{w * 10}},"comparison":null},"illustration":{"key":"pregnancy/week-{{w:00}}","credit":"art","source_url":"https://example.test/art","image_url":"https://cdn.example.test/w{{w}}.jpg","image_url_dark":"https://cdn.example.test/w{{w}}-dark.jpg"},"sources":["src"]} }""";

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
    [InlineData("\"credit\":\"art\"", "\"credit\":\"nope\"")]
    [InlineData("\"topics\":[\"visits\"]", "\"topics\":[\"nope\"]")]
    [InlineData("\"symptoms\":[\"sym\"]", "\"symptoms\":[\"nope\"]")]
    public void Load_WeekCitesUndefinedKey_Throws(string cited, string undefined)
    {
        var json = Document(Enumerable.Range(4, 37)).Replace(cited, undefined);

        var act = () => Load(json);

        act.Should().Throw<InvalidDataException>().WithMessage("*nope*");
    }

    [Fact]
    public void Load_WeekWithoutArtwork_KeepsNullIllustration()
    {
        var json = Document(Enumerable.Range(4, 37)).Replace(
            "\"illustration\":{\"key\":\"pregnancy/week-04\",\"credit\":\"art\",\"source_url\":\"https://example.test/art\",\"image_url\":\"https://cdn.example.test/w4.jpg\",\"image_url_dark\":\"https://cdn.example.test/w4-dark.jpg\"}",
            "\"illustration\":null");

        var content = Load(json);

        (content.File.Weeks[0].Baby.Illustration, content.File.Weeks[1].Baby.Illustration!.Credit).Should().Be((null, "art"));
    }

    [Theory]
    [InlineData("\"image_url\":\"https://cdn.example.test/w4.jpg\"", "\"image_url\":\"http://cdn.example.test/w4.jpg\"")]
    [InlineData("\"image_url_dark\":\"https://cdn.example.test/w4-dark.jpg\"", "\"image_url_dark\":\"week-04-dark.jpg\"")]
    public void Load_IllustrationImageNotHttps_Throws(string valid, string invalid)
    {
        var json = Document(Enumerable.Range(4, 37)).Replace(valid, invalid);

        var act = () => Load(json);

        act.Should().Throw<InvalidDataException>().WithMessage("*https*");
    }

    [Fact]
    public void Load_IllustrationImages_ReachTheWire()
    {
        var content = Load(Document(Enumerable.Range(4, 37))).For("en");

        using var body = JsonDocument.Parse(content.Body);
        var art = body.RootElement.GetProperty("data").GetProperty("weeks")[0].GetProperty("baby").GetProperty("illustration");
        (art.GetProperty("image_url").GetString(), art.GetProperty("image_url_dark").GetString())
            .Should().Be(("https://cdn.example.test/w4.jpg", "https://cdn.example.test/w4-dark.jpg"));
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
