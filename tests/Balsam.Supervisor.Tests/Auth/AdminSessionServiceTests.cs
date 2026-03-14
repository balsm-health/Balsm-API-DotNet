using Balsam.Supervisor.Auth;
using FluentAssertions;
using Xunit;

namespace Balsam.Supervisor.Tests.Auth;

public class AdminSessionServiceTests
{
    private readonly AdminSessionService _sut = new();

    [Fact]
    public void CreateSession_ReturnsNonEmptyToken()
    {
        var token = _sut.CreateSession("admin");
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CreateSession_ReturnsDifferentTokensEachTime()
    {
        var t1 = _sut.CreateSession("admin");
        var t2 = _sut.CreateSession("admin");
        t1.Should().NotBe(t2);
    }

    [Fact]
    public void ValidateSession_ReturnsTrueForValidToken()
    {
        var token = _sut.CreateSession("admin");
        _sut.ValidateSession(token).Should().BeTrue();
    }

    [Fact]
    public void ValidateSession_ReturnsFalseForUnknownToken()
    {
        _sut.ValidateSession("unknown-token").Should().BeFalse();
    }

    [Fact]
    public void InvalidateSession_MakesTokenInvalid()
    {
        var token = _sut.CreateSession("admin");
        _sut.InvalidateSession(token);
        _sut.ValidateSession(token).Should().BeFalse();
    }

    [Fact]
    public void InvalidateAllSessions_ClearsEverything()
    {
        var t1 = _sut.CreateSession("admin");
        var t2 = _sut.CreateSession("admin");

        _sut.InvalidateAllSessions();

        _sut.ValidateSession(t1).Should().BeFalse();
        _sut.ValidateSession(t2).Should().BeFalse();
    }
}
