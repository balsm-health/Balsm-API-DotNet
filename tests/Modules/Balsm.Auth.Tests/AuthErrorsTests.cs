using Balsm.Auth.Domain;
using FluentAssertions;
using Xunit;

namespace Balsm.Auth.Tests;

public sealed class AuthErrorsTests
{
    [Fact]
    public void LockedMessage_Seconds_StatesRetryDelay()
    {
        var message = AuthErrors.LockedMessage(900);

        message.Should().Be("Too many attempts. Try again in 900 seconds.");
    }

    [Fact]
    public void InvalidCredentials_Message_DoesNotRevealWhichPartWasWrong()
    {
        var error = AuthErrors.InvalidCredentials;

        error.Message.Should().Be("Invalid email or password.");
    }
}
