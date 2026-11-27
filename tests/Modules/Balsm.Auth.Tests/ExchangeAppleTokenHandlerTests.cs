using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using Balsm.Auth.Application.Commands;
using Balsm.Auth.Infrastructure.Handlers;
using Balsm.Geofence.Domain;
using Balsm.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Balsm.Auth.Tests;

/// <summary>
/// Drives the real <see cref="AppleOidcValidator"/> with ID tokens signed by a
/// test RSA key. Its JWKS fetch goes to a fake <see cref="IHttpClientFactory"/>
/// that serves that key's public half — nothing leaves the process. Token
/// lifetimes are fixed dates (valid until 2099, or expired in 2020).
///
/// Only rejection paths are covered: the validator looks up the "sub" claim
/// after JwtSecurityTokenHandler has already renamed it to
/// ClaimTypes.NameIdentifier (default inbound claim mapping), so it returns
/// null even for a correctly signed token and no success path is reachable.
/// </summary>
public sealed class ExchangeAppleTokenHandlerTests : IDisposable
{
    private static readonly Guid ProvisionedUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid DeviceId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly DateTime IssuedAt = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FarFuture = new(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private const string AppleIssuer = "https://appleid.apple.com";
    private const string Subject = "test-apple-subject-0001";
    private const string Email = "apple-user@example.test";
    private const string KeyId = "test-key";

    private readonly AuthTestDatabase _database = new();
    private readonly FakeAccountProvisioner _provisioner = new(ProvisionedUserId);
    private readonly RSA _signingKey = RSA.Create(2048);
    private readonly JwksHandler _jwks;

    public ExchangeAppleTokenHandlerTests() => _jwks = new JwksHandler(Jwks(_signingKey));

    public void Dispose()
    {
        _database.Dispose();
        _signingKey.Dispose();
        _jwks.Dispose();
    }

    private ExchangeAppleTokenHandler Handler(IGeofenceService? geofence = null)
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        var cache = services.BuildServiceProvider().GetRequiredService<HybridCache>();
        var validator = new AppleOidcValidator(
            TestSettings.Build(), new FakeHttpClientFactory(_jwks), cache, NullLogger<AppleOidcValidator>.Instance);

        return new ExchangeAppleTokenHandler(
            _database.Db, _provisioner, validator, geofence ?? new FakeGeofenceService(),
            new JwtService(TestSettings.Build()));
    }

    private static ExchangeAppleTokenCommand Command(string idToken, string countryCode = "EG") =>
        new(idToken, DeviceId, "test-device", countryCode);

    private string IdToken(
        string? subject = Subject,
        string? email = Email,
        string? emailVerified = "true",
        string audience = TestSettings.AppleClientId,
        DateTime? expires = null,
        RSA? signingKey = null)
    {
        var claims = new List<Claim>();
        if (subject is not null) claims.Add(new Claim("sub", subject));
        if (email is not null) claims.Add(new Claim("email", email));
        if (emailVerified is not null) claims.Add(new Claim("email_verified", emailVerified));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = AppleIssuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = IssuedAt,
            NotBefore = IssuedAt,
            Expires = expires ?? FarFuture,
            SigningCredentials = new SigningCredentials(
                new RsaSecurityKey(signingKey ?? _signingKey) { KeyId = KeyId }, SecurityAlgorithms.RsaSha256),
        };
        return new JwtSecurityTokenHandler().CreateEncodedJwt(descriptor);
    }

    private static string Jwks(RSA key)
    {
        var p = key.ExportParameters(includePrivateParameters: false);
        return $$"""{"keys":[{"kty":"RSA","kid":"{{KeyId}}","use":"sig","alg":"RS256","n":"{{Base64UrlEncoder.Encode(p.Modulus)}}","e":"{{Base64UrlEncoder.Encode(p.Exponent)}}"}]}""";
    }

    [Fact]
    public async Task Handle_DeniedCountry_ThrowsGeofenceDenied()
    {
        var act = () => Handler(new FakeGeofenceService("IR")).Handle(Command(IdToken(), "IR"), CancellationToken.None);

        await act.Should().ThrowAsync<GeofenceDeniedException>();
    }

    [Fact]
    public async Task Handle_MalformedToken_ThrowsUnauthorized()
    {
        var act = () => Handler().Handle(Command("not-a-jwt"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("Apple ID token validation failed");
    }

    [Fact]
    public async Task Handle_ExpiredToken_ThrowsUnauthorized()
    {
        var expired = IdToken(expires: IssuedAt.AddHours(1));

        var act = () => Handler().Handle(Command(expired), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_TokenForOtherAudience_ThrowsUnauthorized()
    {
        var act = () => Handler().Handle(Command(IdToken(audience: "other.client.example.test")), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_TokenSignedByUnknownKey_ThrowsUnauthorized()
    {
        using var otherKey = RSA.Create(2048);

        var act = () => Handler().Handle(Command(IdToken(signingKey: otherKey)), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_TokenWithoutSubject_ThrowsUnauthorized()
    {
        var act = () => Handler().Handle(Command(IdToken(subject: null)), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private sealed class JwksHandler(string jwks) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jwks, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
