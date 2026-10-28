using Balsm.Infrastructure.Diagnostics;
using FluentAssertions;
using Xunit;

namespace Balsm.API.Tests.Diagnostics;

public sealed class SensitiveDataScrubberTests
{
    [Fact]
    public void ScrubJson_SensitiveKeysAtAnyDepth_AreRedacted()
    {
        var body = """{"email":"jane@example.test","profile":{"Password":"pw","name":"Jane Doe"},"items":[{"token":"t"},1,true,null]}""";

        var scrubbed = SensitiveDataScrubber.ScrubJson(body);

        scrubbed.Should().Be(
            """{"email":"***REDACTED***","profile":{"Password":"***REDACTED***","name":"Jane Doe"},"items":[{"token":"***REDACTED***"},1,true,null]}""");
    }

    [Fact]
    public void ScrubJson_TopLevelArray_IsScrubbedElementWise()
    {
        var scrubbed = SensitiveDataScrubber.ScrubJson("""[{"dob":"1990-01-01"},"plain"]""");

        scrubbed.Should().Be("""[{"dob":"***REDACTED***"},"plain"]""");
    }

    [Fact]
    public void ScrubJson_NotJson_IsReplacedWholesale()
    {
        var scrubbed = SensitiveDataScrubber.ScrubJson("email=jane@example.test");

        scrubbed.Should().Be("[non-json body redacted, 23 bytes]");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ScrubJson_Blank_IsReturnedAsIs(string body)
    {
        var scrubbed = SensitiveDataScrubber.ScrubJson(body);

        scrubbed.Should().Be(body);
    }

    [Fact]
    public void ScrubQuery_SensitiveValues_AreRedacted()
    {
        var scrubbed = SensitiveDataScrubber.ScrubQuery("?code=123456&lang=en&flag&%65mail=x");

        scrubbed.Should().Be("?code=***REDACTED***&lang=en&flag&%65mail=***REDACTED***");
    }

    [Fact]
    public void ScrubQuery_WithoutLeadingQuestionMark_GainsOne()
    {
        var scrubbed = SensitiveDataScrubber.ScrubQuery("lang=ar");

        scrubbed.Should().Be("?lang=ar");
    }

    [Fact]
    public void ScrubQuery_Empty_IsReturnedAsIs()
    {
        var scrubbed = SensitiveDataScrubber.ScrubQuery(string.Empty);

        scrubbed.Should().BeEmpty();
    }
}
