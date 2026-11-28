using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Balsm.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Balsm.Auth.Tests;

/// <summary>
/// <see cref="AppleOidcValidator.ValidateServerNotificationAsync"/>: signature,
/// issuer, audience and lifetime checks, and parsing of the <c>events</c> claim.
/// </summary>
public sealed class AppleServerNotificationValidationTests
{
    private const string ClientId = "health.balsm.app";
    private static readonly RSA AppleKey = RSA.Create(2048);
    private static readonly RSA OtherKey = RSA.Create(2048);
    private static readonly ECDsa EcKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    private readonly AppleOidcValidator _validator = new TestKeyValidator(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Apple:ClientId"] = ClientId,
        }).Build());

    [Fact]
    public async Task Validate_EmailDisabledEvent_ParsesAllFields()
    {
        var events = """{"type":"email-disabled","sub":"s-1","email":"x@privaterelay.appleid.com","is_private_email":"true","event_time":1735689600000}""";

        var evt = await _validator.ValidateServerNotificationAsync(Sign(events), CancellationToken.None);

        evt.Should().Be(new AppleServerEvent(
            "email-disabled", "s-1", new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            "x@privaterelay.appleid.com", true));
    }

    [Fact]
    public async Task Validate_EventTimeInSecondsAndBoolPrivateFlag_Parses()
    {
        var events = """{"type":"email-enabled","sub":"s-1","is_private_email":false,"event_time":1735689600}""";

        var evt = await _validator.ValidateServerNotificationAsync(Sign(events), CancellationToken.None);

        evt!.EventTime.Should().Be(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        evt.IsPrivateEmail.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_TokenWithoutExp_IsAccepted()
    {
        var evt = await _validator.ValidateServerNotificationAsync(
            Sign("""{"type":"consent-revoked","sub":"s-1"}""", withExpiry: false), CancellationToken.None);

        evt!.Type.Should().Be("consent-revoked");
    }

    [Fact]
    public async Task Validate_SignedByOtherKey_ReturnsNull()
    {
        var evt = await _validator.ValidateServerNotificationAsync(
            Sign("""{"type":"consent-revoked","sub":"s-1"}""", key: OtherKey), CancellationToken.None);

        evt.Should().BeNull();
    }

    [Fact]
    public async Task Validate_WrongIssuer_ReturnsNull()
    {
        var evt = await _validator.ValidateServerNotificationAsync(
            Sign("""{"type":"consent-revoked","sub":"s-1"}""", issuer: "https://evil.example"), CancellationToken.None);

        evt.Should().BeNull();
    }

    [Fact]
    public async Task Validate_SignedWithNonRs256Algorithm_ReturnsNull()
    {
        var evt = await _validator.ValidateServerNotificationAsync(
            SignEs256("""{"type":"consent-revoked","sub":"s-1"}"""), CancellationToken.None);

        evt.Should().BeNull();
    }

    [Fact]
    public async Task ValidateIdToken_Rs256Signed_ReturnsSubject()
    {
        var claims = new ClaimsIdentity([new Claim("sub", "s-1")]);
        var handler = new JwtSecurityTokenHandler();
        var token = handler.WriteToken(handler.CreateJwtSecurityToken(new SecurityTokenDescriptor
        {
            Issuer = "https://appleid.apple.com",
            Audience = ClientId,
            Subject = claims,
            SigningCredentials = new SigningCredentials(
                new RsaSecurityKey(AppleKey) { KeyId = "k" }, SecurityAlgorithms.RsaSha256),
        }));

        var payload = await _validator.ValidateAsync(token, CancellationToken.None);

        payload!.Subject.Should().Be("s-1");
    }

    [Fact]
    public async Task ValidateIdToken_SignedWithNonRs256Algorithm_ReturnsNull()
    {
        var payload = await _validator.ValidateAsync(SignEs256(events: null, sub: "s-1"), CancellationToken.None);

        payload.Should().BeNull();
    }

    [Fact]
    public async Task Validate_IatWithin7Days_IsAccepted()
    {
        var evt = await _validator.ValidateServerNotificationAsync(
            Sign("""{"type":"consent-revoked","sub":"s-1"}""", withExpiry: false, issuedAt: DateTime.UtcNow.AddDays(-6)),
            CancellationToken.None);

        evt.Should().NotBeNull();
    }

    [Fact]
    public async Task Validate_IatOlderThan7Days_ReturnsNull()
    {
        var evt = await _validator.ValidateServerNotificationAsync(
            Sign("""{"type":"consent-revoked","sub":"s-1"}""", withExpiry: false, issuedAt: DateTime.UtcNow.AddDays(-8)),
            CancellationToken.None);

        evt.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not json")]
    [InlineData("""{"type":"consent-revoked"}""")]
    [InlineData("""{"sub":"s-1"}""")]
    public async Task Validate_MissingOrMalformedEvents_ReturnsNull(string? events)
    {
        var evt = await _validator.ValidateServerNotificationAsync(Sign(events), CancellationToken.None);

        evt.Should().BeNull();
    }

    [Fact]
    public async Task Validate_NotAJwt_ReturnsNull()
    {
        var evt = await _validator.ValidateServerNotificationAsync("not-a-jwt", CancellationToken.None);

        evt.Should().BeNull();
    }

    private static string Sign(
        string? events,
        string issuer = "https://appleid.apple.com",
        RSA? key = null,
        bool withExpiry = true,
        DateTime? issuedAt = null)
    {
        var claims = new List<Claim> { new("jti", "jti-1") };
        if (events is not null) claims.Add(new Claim("events", events));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = ClientId,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = issuedAt ?? DateTime.UtcNow,
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            Expires = withExpiry ? DateTime.UtcNow.AddMinutes(5) : null,
            SigningCredentials = new SigningCredentials(
                new RsaSecurityKey(key ?? AppleKey) { KeyId = "k" }, SecurityAlgorithms.RsaSha256),
        };
        var handler = new JwtSecurityTokenHandler { SetDefaultTimesOnTokenCreation = false };
        return handler.WriteToken(handler.CreateJwtSecurityToken(descriptor));
    }

    private static string SignEs256(string? events, string? sub = null)
    {
        var claims = new List<Claim>();
        if (events is not null) claims.Add(new Claim("events", events));
        if (sub is not null) claims.Add(new Claim("sub", sub));
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateJwtSecurityToken(new SecurityTokenDescriptor
        {
            Issuer = "https://appleid.apple.com",
            Audience = ClientId,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(
                new ECDsaSecurityKey(EcKey) { KeyId = "ec" }, SecurityAlgorithms.EcdsaSha256),
        }));
    }

    private sealed class TestKeyValidator(IConfiguration config)
        : AppleOidcValidator(config, null!, null!, NullLogger<AppleOidcValidator>.Instance)
    {
        protected override Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(CancellationToken ct) =>
            Task.FromResult<IEnumerable<SecurityKey>>([new RsaSecurityKey(AppleKey) { KeyId = "k" }, new ECDsaSecurityKey(EcKey) { KeyId = "ec" }]);
    }
}
