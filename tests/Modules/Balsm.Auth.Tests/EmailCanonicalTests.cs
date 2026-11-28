using Balsm.Auth.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Balsm.Auth.Tests;

/// Gmail ignores case, dots and a "+tag" in the local part, so those
/// spellings are one mailbox and must key one identity. Other providers make
/// no such promise: only case is folded there.
public sealed class EmailCanonicalTests
{
    [Fact]
    public void From_GmailDotsAndCase_Collapse()
    {
        var dotted = EmailCanonical.From("hossamEldin.M.I@gmail.com");
        var plain = EmailCanonical.From("hossameldinmi@gmail.com");

        dotted.Should().Be("hossameldinmi@gmail.com");
        plain.Should().Be("hossameldinmi@gmail.com");
    }

    [Fact]
    public void From_GmailPlusTag_Dropped()
    {
        var canonical = EmailCanonical.From("a.b+balsm@googlemail.com");

        canonical.Should().Be("ab@gmail.com");
    }

    [Fact]
    public void From_OtherDomain_KeepsDots()
    {
        var canonical = EmailCanonical.From("First.Last@Balsm.Health");

        canonical.Should().Be("first.last@balsm.health");
    }

    [Fact]
    public void From_OtherDomain_KeepsPlusTag()
    {
        var canonical = EmailCanonical.From("first+tag@example.com");

        canonical.Should().Be("first+tag@example.com");
    }

    [Fact]
    public void From_SurroundingWhitespace_Trimmed()
    {
        var canonical = EmailCanonical.From("  A.B@GMAIL.COM ");

        canonical.Should().Be("ab@gmail.com");
    }

    [Fact]
    public void From_GmailLookalikeDomain_LowercaseOnly()
    {
        var canonical = EmailCanonical.From("a.b+x@notgmail.com");

        canonical.Should().Be("a.b+x@notgmail.com");
    }

    [Fact]
    public void From_NoAtSign_LowercaseOnly()
    {
        var canonical = EmailCanonical.From("Not.An.Email");

        canonical.Should().Be("not.an.email");
    }

    [Fact]
    public void From_Blank_Throws()
    {
        var act = () => EmailCanonical.From("  ");

        act.Should().Throw<ArgumentException>();
    }
}
