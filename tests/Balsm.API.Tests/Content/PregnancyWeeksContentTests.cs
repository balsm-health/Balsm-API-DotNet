using System.Text;
using System.Text.Json;
using Balsm.API.Content;
using FluentAssertions;
using Xunit;

namespace Balsm.API.Tests.Content;

public class PregnancyWeeksContentTests
{
    private static string Text(string en) => $$"""{"en":"{{en}}","ar":"{{en}}"}""";

    private static string Week(int w) => $$"""{"week":{{w}},"title":{{Text("t")}},"body":{{Text("b")}}}""";

    private static string Document(IEnumerable<int> weeks, string version = "v-test") =>
        $$"""{"version":"{{version}}","source":{{Text("s")}},"disclaimer":{{Text("d")}},"multiples_note":{{Text("m")}},"weeks":[{{string.Join(",", weeks.Select(Week))}}]}""";

    private static PregnancyWeeksContent Load(string json) => PregnancyWeeksContent.Load(new MemoryStream(Encoding.UTF8.GetBytes(json)));

    [Fact]
    public void Current_ShippedFile_ListsWeeks4To40()
    {
        var content = PregnancyWeeksContent.Current;

        content.Document.Weeks.Select(w => w.Week).Should().Equal(Enumerable.Range(4, 37));
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
