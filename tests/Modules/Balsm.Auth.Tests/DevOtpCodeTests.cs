using Balsm.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Balsm.Auth.Tests;

/// <summary>
/// The fixed code that makes staging testable without a mailbox.
///
/// It is an authentication bypass, so it is fenced on three sides: never in
/// production, never for an address outside the configured test allowlist, and
/// never without a code having actually been requested first. Any one of them
/// missing and the code is refused.
/// </summary>
public sealed class DevOtpCodeTests
{
    private static DevOtpCodePolicy Policy(
        string? code = "123456",
        string? allowlist = "@balsm.test",
        string environment = "Production",
        string? deployment = "staging") =>
        new(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Otp:DevCode"] = code,
                ["Otp:DevCodeEmails"] = allowlist,
                ["Deployment:Environment"] = deployment,
            }).Build(),
            new FakeEnvironment(environment),
            NullLogger<DevOtpCodePolicy>.Instance);

    [Fact]
    public void Accepts_TheFixedCode_ForATestAddressInStaging()
    {
        // The staging box runs ASPNETCORE_ENVIRONMENT=Production — docker-compose
        // sets it for every deployment — so which deployment this is has to come
        // from Deployment:Environment, not from the host environment name.
        Policy().Accepts("qa@balsm.test", "123456").Should().BeTrue();
    }

    [Fact]
    public void Rejects_WhenTheDeploymentDoesNotSayStaging()
    {
        // Fail closed: unset, misspelt, or production all mean no.
        Policy(deployment: null).Accepts("qa@balsm.test", "123456").Should().BeFalse();
        Policy(deployment: "production").Accepts("qa@balsm.test", "123456").Should().BeFalse();
        Policy(deployment: "stg").Accepts("qa@balsm.test", "123456").Should().BeFalse();
    }

    [Fact]
    public void Rejects_AnAddressOutsideTheAllowlist()
    {
        // The property the config manifest objects to: a fixed code that works
        // for any address signs in as, or resets the password of, a real person
        // who has nothing to do with testing.
        Policy().Accepts("someone@gmail.com", "123456").Should().BeFalse();
    }

    [Fact]
    public void Rejects_EverythingInProduction()
    {
        Policy(deployment: "production").Accepts("qa@balsm.test", "123456").Should().BeFalse();
    }

    [Fact]
    public void Rejects_WhenNoAllowlistIsConfigured_InADeployedEnvironment()
    {
        // Fails closed: a DevCode with no allowlist is the unfenced version, so
        // in anything deployed it is worth nothing rather than worth everything.
        Policy(allowlist: null).Accepts("qa@balsm.test", "123456").Should().BeFalse();
        Policy(allowlist: "").Accepts("qa@balsm.test", "123456").Should().BeFalse();
    }

    [Fact]
    public void Allows_NoAllowlist_OnADeveloperMachine()
    {
        // Development is a laptop with no users on it, and requiring an
        // allowlist there would mean listing every address anyone types while
        // testing. Staging is reachable and gets the stricter rule.
        Policy(allowlist: null, environment: "Development", deployment: null)
            .Accepts("whatever@example.com", "123456").Should().BeTrue();
    }

    [Fact]
    public void Rejects_WhenNoDevCodeIsConfigured()
    {
        Policy(code: null).Accepts("qa@balsm.test", "123456").Should().BeFalse();
    }

    [Fact]
    public void Rejects_TheWrongCode()
    {
        Policy().Accepts("qa@balsm.test", "654321").Should().BeFalse();
    }

    [Fact]
    public void Accepts_AnyOfSeveralAllowlistedSuffixes()
    {
        var policy = Policy(allowlist: "@balsm.test, @e2e.balsm.health");

        policy.Accepts("qa@e2e.balsm.health", "123456").Should().BeTrue();
        policy.Accepts("qa@balsm.test", "123456").Should().BeTrue();
    }

    [Fact]
    public void MatchesTheAddressWhateverItsCasing()
    {
        Policy().Accepts("QA@Balsm.Test", "123456").Should().BeTrue();
    }

    private sealed class FakeEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Balsm.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
