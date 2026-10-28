using Balsm.Account.Application.Commands;
using FluentAssertions;
using Xunit;

namespace Balsm.Account.Tests.Application;

public sealed class HandleConflictExceptionTests
{
    [Fact]
    public void Constructor_WithSuggestions_ExposesThem()
    {
        var exception = new HandleConflictException(["jane_doe_1", "jane_doe_2"]);

        exception.Suggestions.Should().Equal("jane_doe_1", "jane_doe_2");
    }

    [Fact]
    public void Constructor_Always_SetsTakenMessage()
    {
        var exception = new HandleConflictException([]);

        exception.Message.Should().Be("Handle is taken");
    }
}
