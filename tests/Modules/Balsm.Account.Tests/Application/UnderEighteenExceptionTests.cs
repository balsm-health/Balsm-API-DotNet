using Balsm.Account.Application.Commands;
using FluentAssertions;
using Xunit;

namespace Balsm.Account.Tests.Application;

public sealed class UnderEighteenExceptionTests
{
    [Fact]
    public void Constructor_Always_SetsUnderEighteenMessage()
    {
        var exception = new UnderEighteenException();

        exception.Message.Should().Be("User is under 18");
    }
}
