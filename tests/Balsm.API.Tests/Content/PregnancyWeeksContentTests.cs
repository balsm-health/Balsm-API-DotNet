using System.Text;
using System.Text.Json;
using Balsm.API.Content;
using FluentAssertions;
using Xunit;

namespace Balsm.API.Tests.Content;

public class PregnancyWeeksContentTests
{
    private static string Text(string en) => $$"""{"en":"{{en}}","ar":"{{en}}"}""";

    private static string Week(int w) =>
        $$"""{"week":{{w}},"title":{{Text("t")}},"body":{{Text("b")}},"fetal_size":{"length_mm":null,"weight_g":{{w * 10}},"comparison":null},"tests_due":["bp"],"danger_signs":["bleeding"],"topics":["visits"],"illustration":{"key":"pregnancy/week-{{w:00}}","credit":"art","source_url":"https://example.test/art"},"sources":["src"]}""";

    private static string Document(IEnumerable<int> weeks, string version = "v-test") =>
        $$"""{"version":"{{version}}","source":{{Text("s")}},"disclaimer":{{Text("d")}},"multiples_note":{{Text("m")}},"sources":[{"id":"src","title":"t","publisher":"p","url":"https://example.test/","accessed":"2026-11-24"}],"danger_sign_labels":{"bleeding":{{Text("Bleeding")}}},"test_labels":{"bp":{{Text("Blood pressure")}}},"illustration_credits":{"art":{"author":"a","license":"CC BY-ND 4.0","license_url":"https://example.test/l","author_url":"https://example.test/a","text":{{Text("Illustration: a")}} } },"weeks":[{{string.Join(",", weeks.Select(Week))}}]}""";

    private static PregnancyWeeksContent Load(string json) => PregnancyWeeksContent.Load(new MemoryStream(Encoding.UTF8.GetBytes(json)));

    [Fact]
    public void Current_ShippedFile_ListsWeeks4To40()
    {
        var content = PregnancyWeeksContent.Current;

        content.Document.Weeks.Select(w => w.Week).Should().Equal(Enumerable.Range(4, 37));
    }

    [Fact]
    public void Load_ValidDocument_KeepsSizesKeysAndSourcesOnTheWire()
    {
        var content = Load(Document(Enumerable.Range(4, 37)));

        using var body = JsonDocument.Parse(content.Body);
        var data = body.RootElement.GetProperty("data");
        var week = data.GetProperty("weeks")[0];
        (week.GetProperty("fetal_size").GetProperty("weight_g").GetInt32(),
            week.GetProperty("fetal_size").GetProperty("length_mm").ValueKind,
            week.GetProperty("danger_signs")[0].GetString(),
            data.GetProperty("danger_sign_labels").GetProperty("bleeding").GetProperty("en").GetString(),
            data.GetProperty("sources")[0].GetProperty("accessed").GetString())
            .Should().Be((40, JsonValueKind.Null, "bleeding", "Bleeding", "2026-11-24"));
    }

    [Theory]
    [InlineData("\"sources\":[\"src\"]", "\"sources\":[\"nope\"]")]
    [InlineData("\"danger_signs\":[\"bleeding\"]", "\"danger_signs\":[\"nope\"]")]
    [InlineData("\"tests_due\":[\"bp\"]", "\"tests_due\":[\"nope\"]")]
    [InlineData("\"credit\":\"art\"", "\"credit\":\"nope\"")]
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
            "\"illustration\":{\"key\":\"pregnancy/week-04\",\"credit\":\"art\",\"source_url\":\"https://example.test/art\"}",
            "\"illustration\":null");

        var content = Load(json);

        (content.Document.Weeks[0].Illustration, content.Document.Weeks[1].Illustration!.Credit).Should().Be((null, "art"));
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
        var content = Load(Document(Enumerable.Range(4, 37)));

        using var body = JsonDocument.Parse(content.Body);
        body.RootElement.GetProperty("data").GetProperty("multiples_note").GetProperty("en").GetString().Should().Be("m");
    }

    [Fact]
    public void Load_SameBytes_ProducesSameStrongETag()
    {
        var json = Document(Enumerable.Range(4, 37));

        var (a, b) = (Load(json).ETag, Load(json).ETag);

        a.Should().Be(b);
        a.IsWeak.Should().BeFalse();
    }

    [Fact]
    public void Load_DifferentBytes_ProducesDifferentETag()
    {
        var a = Load(Document(Enumerable.Range(4, 37), "v1")).ETag;
        var b = Load(Document(Enumerable.Range(4, 37), "v2")).ETag;

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
        var json = Document(Enumerable.Range(4, 37)).Replace("""{"en":"s","ar":"s"}""", """{"en":"s"}""");

        var act = () => Load(json);

        act.Should().Throw<JsonException>();
    }
}
