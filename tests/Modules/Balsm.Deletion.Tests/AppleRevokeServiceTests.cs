using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Cryptography;
using Balsm.Deletion.Infrastructure.Data;
using Balsm.Deletion.Infrastructure.Services;
using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Balsm.Deletion.Tests;

/// <summary>
/// FR-031 Apple revoke call. No network: the named "apple" client is backed by
/// a recording handler, and the signing key is generated per test.
/// </summary>
public sealed class AppleRevokeServiceTests : IDisposable
{
    private const string AppleSubject = "000000.synthetic-apple-subject.0000";
    private readonly SqliteConnection _connection;
    private readonly DeletionDbContext _db;
    private readonly RecordingHandler _handler = new();

    public AppleRevokeServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new DeletionDbContext(
            new DbContextOptionsBuilder<DeletionDbContext>().UseSqlite(_connection).Options,
            Substitute.For<IDomainEventDispatcher>());
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task RevokeAsync_NoAppleIdentity_MakesNoCallToApple()
    {
        var service = Service(ConfiguredSettings());

        await service.RevokeAsync(Guid.NewGuid(), appleProviderSubject: null, _db, CancellationToken.None);

        _handler.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Apple:TeamId")]
    [InlineData("Apple:ClientId")]
    [InlineData("Apple:KeyId")]
    [InlineData("Apple:PrivateKey")]
    public async Task RevokeAsync_ConfigValueMissing_MakesNoCallToApple(string missingKey)
    {
        var settings = ConfiguredSettings();
        settings.Remove(missingKey);
        var service = Service(settings);

        await service.RevokeAsync(Guid.NewGuid(), AppleSubject, _db, CancellationToken.None);

        _handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task RevokeAsync_Configured_PostsAnEs256SignedClientSecretToAppleRevoke()
    {
        var service = Service(ConfiguredSettings());

        await service.RevokeAsync(Guid.NewGuid(), AppleSubject, _db, CancellationToken.None);

        var (uri, form) = _handler.Requests.Single();
        uri.Should().Be("https://appleid.apple.com/auth/revoke");
        form["client_id"].Should().Be("health.example.app");
        form["token"].Should().Be(AppleSubject);
        form["token_type_hint"].Should().Be("access_token");
        var secret = new JwtSecurityTokenHandler().ReadJwtToken(form["client_secret"]);
        secret.Header.Alg.Should().Be("ES256");
        secret.Header.Kid.Should().Be("KEY0000001");
        secret.Issuer.Should().Be("TEAM000001");
        secret.Subject.Should().Be("health.example.app");
    }

    [Fact]
    public async Task RevokeAsync_AppleAnswersAnError_DoesNotThrow()
    {
        _handler.Status = HttpStatusCode.BadRequest;
        var service = Service(ConfiguredSettings());

        var act = () => service.RevokeAsync(Guid.NewGuid(), AppleSubject, _db, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RevokeAsync_UnreadablePrivateKey_DoesNotThrowAndMakesNoCall()
    {
        var settings = ConfiguredSettings();
        settings["Apple:PrivateKey"] = "not-a-key";
        var service = Service(settings);

        var act = () => service.RevokeAsync(Guid.NewGuid(), AppleSubject, _db, CancellationToken.None);

        await act.Should().NotThrowAsync();
        _handler.Requests.Should().BeEmpty();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        _handler.Dispose();
    }

    private AppleRevokeService Service(Dictionary<string, string?> settings)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("apple").Returns(_ => new HttpClient(_handler, disposeHandler: false));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new AppleRevokeService(factory, configuration, NullLogger<AppleRevokeService>.Instance);
    }

    private static Dictionary<string, string?> ConfiguredSettings()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return new Dictionary<string, string?>
        {
            ["Apple:TeamId"] = "TEAM000001",
            ["Apple:ClientId"] = "health.example.app",
            ["Apple:KeyId"] = "KEY0000001",
            ["Apple:PrivateKey"] = key.ExportPkcs8PrivateKeyPem(),
        };
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        public List<(string Uri, Dictionary<string, string> Form)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = await request.Content!.ReadAsStringAsync(ct);
            var form = body.Split('&')
                .Select(pair => pair.Split('=', 2))
                .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1].Replace('+', ' ')));
            Requests.Add((request.RequestUri!.ToString(), form));
            return new HttpResponseMessage(Status);
        }
    }
}
