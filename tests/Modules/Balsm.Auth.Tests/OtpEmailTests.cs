using Balsm.Auth.Application.Commands;
using Balsm.Infrastructure.Auth;
using FluentAssertions;
using Xunit;

namespace Balsm.Auth.Tests;

/// <summary>
/// The code email: one branded layout per language, one set of words per
/// purpose. These read the real layout files the API ships.
/// </summary>
public sealed class OtpEmailTests
{
    private const string Link = "https://api.example.test/auth/otp/link?t=abc_DEF-123&p=reset";

    private static string Layout(string lang) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Templates", "auth-otp", $"{lang}.html"));

    public static TheoryData<OtpEmailKind, string> Every()
    {
        var data = new TheoryData<OtpEmailKind, string>();
        foreach (var kind in Enum.GetValues<OtpEmailKind>())
            foreach (var lang in new[] { "en", "ar" })
                data.Add(kind, lang);
        return data;
    }

    [Theory]
    [MemberData(nameof(Every))]
    public void Every_purpose_renders_in_every_language(OtpEmailKind kind, string lang)
    {
        var message = OtpEmail.Render(Layout(lang), kind, lang, "482913", Link, 10, 2026);

        message.Subject.Should().NotBeNullOrWhiteSpace();
        message.Html.Should().NotContain("{{", "every placeholder must be filled");
        message.Html.Should().Contain(">482 913<", "the code is shown in two groups of three");
        // The link is written into an attribute, so its & must be escaped.
        message.Html.Should().Contain("href=\"https://api.example.test/auth/otp/link?t=abc_DEF-123&amp;p=reset\"");
        message.Html.Should().Contain($"cid:{OtpEmail.LogoContentId}");
        message.Html.Should().NotContain("data:image", "Gmail drops data: images");
        message.Html.Should().Contain(lang == "ar" ? "dir=\"rtl\"" : "dir=\"ltr\"");
    }

    [Fact]
    public void Each_purpose_says_what_it_is_for()
    {
        string Html(OtpEmailKind kind) => OtpEmail.Render(Layout("en"), kind, "en", "482913", Link, 10, 2026).Html;

        Html(OtpEmailKind.SignIn).Should().Contain("Your sign-in code").And.Contain("Sign in with one tap");
        Html(OtpEmailKind.ResetPassword).Should().Contain("Reset your password").And.NotContain("Sign in with one tap");
        Html(OtpEmailKind.DeleteAccount).Should().Contain("delete your Balsm.health account")
            .And.Contain("Nothing is deleted until you confirm");
    }

    [Fact]
    public void Subjects_differ_by_purpose_and_language()
    {
        var subjects = Every().Select(row =>
            OtpEmail.Render(Layout((string)row[1]), (OtpEmailKind)row[0], (string)row[1], "482913", Link, 10, 2026).Subject);

        subjects.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Deleting_an_account_is_coloured_as_a_warning()
    {
        var signIn = OtpEmail.Render(Layout("en"), OtpEmailKind.SignIn, "en", "482913", Link, 10, 2026).Html;
        var delete = OtpEmail.Render(Layout("en"), OtpEmailKind.DeleteAccount, "en", "482913", Link, 10, 2026).Html;

        // The button's colour, in the form Outlook reads; the brand stripe at
        // the top keeps its blue segment in both.
        signIn.Should().Contain("fillcolor=\"#1283FF\"");
        delete.Should().Contain("fillcolor=\"#D44A3C\"").And.NotContain("fillcolor=\"#1283FF\"");
    }

    [Fact]
    public void Arabic_text_uses_arabic_digits_and_the_code_stays_latin()
    {
        var html = OtpEmail.Render(Layout("ar"), OtpEmailKind.SignIn, "ar", "482913", Link, 10, 2026).Html;

        html.Should().Contain("١٠ دقائق").And.Contain("© ٢٠٢٦");
        html.Should().Contain(">482 913<");
    }

    [Theory]
    [InlineData("ar", "ar")]
    [InlineData("ar-EG", "ar")]
    [InlineData("AR_sa", "ar")]
    [InlineData("en", "en")]
    [InlineData("en-US,en;q=0.9", "en")]
    [InlineData("fr", "en")]
    [InlineData("", "en")]
    [InlineData(null, "en")]
    public void Language_tag_picks_the_layout(string? tag, string expected) =>
        OtpEmail.LanguageFor(tag).Should().Be(expected);

    [Fact]
    public void A_link_cannot_inject_markup()
    {
        var html = OtpEmail.Render(
            Layout("en"), OtpEmailKind.SignIn, "en", "482913", "https://x.test/?t=\"><script>alert(1)</script>", 10, 2026).Html;

        html.Should().NotContain("<script>alert(1)</script>");
    }

    [Fact]
    public void Without_a_layout_the_code_and_link_still_go_out()
    {
        var message = OtpEmail.RenderPlain(OtpEmailKind.ResetPassword, "en", "482913", Link, 10);

        message.Html.Should().Contain("482 913").And.Contain("&amp;p=reset");
    }

    [Theory]
    [InlineData("continue", OtpPurpose.Continue)]
    [InlineData("register", OtpPurpose.Continue)]
    [InlineData("Reset", OtpPurpose.Reset)]
    [InlineData("delete", OtpPurpose.DeleteAccount)]
    [InlineData("delete_account", OtpPurpose.DeleteAccount)]
    public void Wire_purposes_are_recognised(string wire, OtpPurpose expected) =>
        OtpPurposeWire.Parse(wire).Should().Be(expected);

    [Theory]
    [InlineData("login")]
    [InlineData("")]
    [InlineData(null)]
    public void Unknown_wire_purposes_are_refused(string? wire) =>
        OtpPurposeWire.Parse(wire).Should().BeNull();

    [Fact]
    public void The_link_names_its_purpose_only_when_it_is_not_a_plain_sign_in()
    {
        OtpPurposeWire.LinkCode(OtpPurpose.Continue).Should().BeNull();
        OtpPurposeWire.LinkCode(OtpPurpose.Reset).Should().Be("reset");
        OtpPurposeWire.LinkCode(OtpPurpose.DeleteAccount).Should().Be("delete");
        // Read back from an anonymous request, so only our own values pass.
        OtpPurposeWire.NormalizeLinkCode("DELETE").Should().Be("delete");
        OtpPurposeWire.NormalizeLinkCode("x&next=https://evil.test").Should().BeNull();
    }
}
