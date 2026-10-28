using Balsm.Auth.Application.Commands;
using Balsm.Infrastructure.Auth;
using Balsm.Infrastructure.RateLimit;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Balsm.Auth.Tests;

public sealed class SignUpGuardTests
{
    private readonly InMemoryRateLimitStore _store = new(new MemoryCache(new MemoryCacheOptions()));

    private SignUpGuard Guard(string? mode = null, string? threshold = null) => new(
        _store,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:SignUp:RevealExisting"] = mode,
            ["Auth:SignUp:StrictAfterEmailsPerHour"] = threshold,
        }).Build(),
        NullLogger<SignUpGuard>.Instance);

    [Fact]
    public async Task IsStrictAsync_ByDefault_IsFalse()
    {
        (await Guard().IsStrictAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task IsStrictAsync_WhenOn_IsTrue()
    {
        (await Guard("on").IsStrictAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task IsStrictAsync_AfterTrip_IsTrue()
    {
        var guard = Guard();

        await guard.TripAsync("test");

        (await guard.IsStrictAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task IsStrictAsync_WhenOff_IgnoresATrip()
    {
        await Guard().TripAsync("test");

        (await Guard("off").IsStrictAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task IsStrictAsync_AtTheHourlyEmailThreshold_IsTrue()
    {
        var guard = Guard(threshold: "2");

        await guard.RecordSentAsync();
        var below = await guard.IsStrictAsync();
        await guard.RecordSentAsync();

        below.Should().BeFalse();
        (await guard.IsStrictAsync()).Should().BeTrue();
    }

    [Theory]
    [InlineData("signup")]
    [InlineData("sign_up")]
    public void Parse_SignUpWireValue_IsSignUp(string wire)
    {
        OtpPurposeWire.Parse(wire).Should().Be(OtpPurpose.SignUp);
    }

    [Fact]
    public void Parse_Register_StaysContinue()
    {
        OtpPurposeWire.Parse("register").Should().Be(OtpPurpose.Continue);
    }
}
