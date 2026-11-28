using Balsm.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.API.Tests;

/// <summary>
/// The OIDC validators are scoped, so a missing audience would only surface on
/// the first user's sign-in tap. With the feature on it must fail at boot.
/// </summary>
public sealed class SocialSignInBootValidationTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
            .Build();

    [Fact]
    public void AddSharedInfrastructure_WhenEnabledAndGoogleClientIdMissing_ThrowsNamingKey()
    {
        var config = Config(("SocialSignIn:Enabled", "true"), ("Apple:ClientId", "app.balsm.health"));

        var act = () => new ServiceCollection().AddSharedInfrastructure(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Google:ClientId*")
            .And.Message.Should().NotContain("Apple:ClientId");
    }

    [Fact]
    public void AddSharedInfrastructure_WhenEnabledAndBothMissing_NamesEveryKey()
    {
        var config = Config(("SocialSignIn:Enabled", "true"));

        var act = () => new ServiceCollection().AddSharedInfrastructure(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Google:ClientId*Apple:ClientId*");
    }

    [Fact]
    public void AddSharedInfrastructure_WhenDisabledAndIdsMissing_DoesNotThrow()
    {
        var config = Config(("SocialSignIn:Enabled", "false"));

        var act = () => new ServiceCollection().AddSharedInfrastructure(config);

        act.Should().NotThrow();
    }
}
