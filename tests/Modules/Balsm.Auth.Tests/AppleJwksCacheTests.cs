using System.Net;
using Balsm.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Balsm.Auth.Tests;

/// <summary>
/// Apple JWKS caching behavior: one fetch per 6h window (HybridCache),
/// and a bad JWKS response must not be cached for the window.
/// </summary>
public sealed class AppleJwksCacheTests
{
    private const string ValidJwks =
        """{"keys":[{"kty":"RSA","kid":"test-key","use":"sig","alg":"RS256","n":"sXchDaQebHnPiGvyDOAT4saGEUetSyo9MKLOoWFsueri23bOdgWp4Dy1WlUzewbgBHod5pcM9H95GQRV3JDXboIRROSBigeC5yjU1hGzHHyXss8UDprecbAYxknTcQkhslANGRUZmdTOQ5qTRsLAt6BTYuyvVRdhS8exSZEy_c4gs_7svlJJQ4H9_NxsiIoLwAEk7-Q3UXERGYw_75IDrGA84-lA_-Ct4eTlXHBIY2EaV7t7LjJaynVJCpkv4LKjTTAumiGUIuQhrNhZLuF_RJLqHpM2kgWFLU7-VTdL1VbC2tejvcI2BlMkEpk1BzBZI0KQB0GaDWFLN-aEAw3vRw","e":"AQAB"}]}""";

    private sealed class CountingHandler(Func<int, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref Calls);
            return Task.FromResult(responder(call));
        }
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static AppleOidcValidator CreateValidator(HttpMessageHandler handler)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Apple:ClientId"] = "health.balsm.app"
        }).Build();

        var services = new ServiceCollection();
        services.AddHybridCache();
        var cache = services.BuildServiceProvider().GetRequiredService<HybridCache>();

        return new AppleOidcValidator(
            config, new FakeHttpClientFactory(handler), cache, NullLogger<AppleOidcValidator>.Instance);
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public async Task ValidateAsync_TwoCalls_FetchesJwksOnce()
    {
        var handler = new CountingHandler(_ => Json(ValidJwks));
        var validator = CreateValidator(handler);

        // Garbage tokens: validation fails (returns null) but JWKS is still fetched + cached.
        await validator.ValidateAsync("not-a-jwt", CancellationToken.None);
        await validator.ValidateAsync("not-a-jwt", CancellationToken.None);

        handler.Calls.Should().Be(1, "second call must be served from HybridCache");
    }

    [Fact]
    public async Task ValidateAsync_EmptyJwksResponse_IsNotCached()
    {
        var handler = new CountingHandler(call => call == 1 ? Json("""{"keys":[]}""") : Json(ValidJwks));
        var validator = CreateValidator(handler);

        await validator.ValidateAsync("not-a-jwt", CancellationToken.None);
        await validator.ValidateAsync("not-a-jwt", CancellationToken.None);

        handler.Calls.Should().Be(2,
            "an empty/broken JWKS payload must not be cached for 6h — that would break Apple sign-in until expiry");
    }
}
