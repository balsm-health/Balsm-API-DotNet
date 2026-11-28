using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Cryptography;
using Balsm.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Balsm.Auth.Tests;

public sealed class AppleTokenClientTests
{
    private const string TokenUrl = "https://appleid.apple.com/auth/token";
    private const string RevokeUrl = "https://appleid.apple.com/auth/revoke";

    [Fact]
    public async Task ExchangeCode_CalledTwice_BothPost()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"refresh_token":"r-1"}"""));
        var client = Client(handler);

        var first = await client.ExchangeCodeAsync("code-1", CancellationToken.None);
        var second = await client.ExchangeCodeAsync("code-2", CancellationToken.None);

        first.Should().Be("r-1");
        second.Should().Be("r-1");
        handler.Requests.Select(r => r.Url).Should().Equal(TokenUrl, TokenUrl);
    }

    [Fact]
    public async Task ExchangeCode_PostsFormWithSignedClientSecret()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"refresh_token":"r-1"}"""));

        await Client(handler).ExchangeCodeAsync("code-1", CancellationToken.None);

        var form = handler.Requests.Single().Form;
        form["client_id"].Should().Be("health.balsm.app");
        form["code"].Should().Be("code-1");
        form["grant_type"].Should().Be("authorization_code");
        AssertClientSecret(form["client_secret"]);
    }

    [Fact]
    public async Task ExchangeCode_Non2xx_ReturnsNull()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}"""));

        var result = await Client(handler).ExchangeCodeAsync("code-1", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ExchangeCode_Timeout_ReturnsNull()
    {
        var handler = new StubHandler(_ => throw new TaskCanceledException("timeout"));

        var result = await Client(handler).ExchangeCodeAsync("code-1", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Revoke_PostsRefreshTokenHint_ReturnsRevoked()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var outcome = await Client(handler).RevokeAsync("r-1", CancellationToken.None);

        outcome.Should().Be(AppleRevokeOutcome.Revoked);
        var request = handler.Requests.Single();
        request.Url.Should().Be(RevokeUrl);
        request.Form["client_id"].Should().Be("health.balsm.app");
        request.Form["token"].Should().Be("r-1");
        request.Form["token_type_hint"].Should().Be("refresh_token");
        AssertClientSecret(request.Form["client_secret"]);
    }

    [Fact]
    public async Task Revoke_Non2xx_ReturnsFailed()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));

        var outcome = await Client(handler).RevokeAsync("r-1", CancellationToken.None);

        outcome.Should().Be(AppleRevokeOutcome.Failed);
    }

    private static void AssertClientSecret(string secret)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(secret);
        jwt.Header.Alg.Should().Be("ES256");
        jwt.Header.Kid.Should().Be("KEY123");
        jwt.Issuer.Should().Be("TEAM123");
        jwt.Subject.Should().Be("health.balsm.app");
        jwt.Audiences.Should().Equal("https://appleid.apple.com");
        var iat = long.Parse(jwt.Claims.Single(c => c.Type == "iat").Value);
        var exp = long.Parse(jwt.Claims.Single(c => c.Type == "exp").Value);
        (exp - iat).Should().BeInRange(1, 300);
    }

    private static AppleTokenClient Client(StubHandler handler)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Apple:TeamId"] = "TEAM123",
            ["Apple:ClientId"] = "health.balsm.app",
            ["Apple:KeyId"] = "KEY123",
            ["Apple:PrivateKey"] = key.ExportPkcs8PrivateKeyPem(),
        }).Build();
        return new AppleTokenClient(new StubFactory(handler), config, NullLogger<AppleTokenClient>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(string Url, Dictionary<string, string> Form)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            var form = body.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split('=', 2))
                .ToDictionary(p => WebUtility.UrlDecode(p[0]), p => WebUtility.UrlDecode(p[1]));
            Requests.Add((request.RequestUri!.ToString(), form));
            return respond(request);
        }
    }
}
