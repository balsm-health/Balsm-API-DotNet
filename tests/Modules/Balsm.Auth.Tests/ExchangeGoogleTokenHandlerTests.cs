using Balsm.Auth.Application.Commands;
using Balsm.Auth.Infrastructure.Handlers;
using Balsm.Geofence.Domain;
using Balsm.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Balsm.Auth.Tests;

/// <summary>
/// Only the paths that fail before Google's signing certificates are needed.
/// <see cref="GoogleOidcValidator"/> is sealed and calls
/// GoogleJsonWebSignature.ValidateAsync with no injectable certificate source,
/// so a token that passes structural checks would fetch Google's certs over
/// the network; the success paths are not reachable from a unit test.
/// </summary>
public sealed class ExchangeGoogleTokenHandlerTests : IDisposable
{
    private static readonly Guid ProvisionedUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid DeviceId = Guid.Parse("00000000-0000-0000-0000-000000000010");

    private readonly AuthTestDatabase _database = new();
    private readonly FakeAccountProvisioner _provisioner = new(ProvisionedUserId);

    public void Dispose() => _database.Dispose();

    private ExchangeGoogleTokenHandler Handler(IGeofenceService? geofence = null) =>
        new(_database.Db, _provisioner,
            new GoogleOidcValidator(TestSettings.Build(), NullLogger<GoogleOidcValidator>.Instance),
            geofence ?? new FakeGeofenceService(), new JwtService(TestSettings.Build()));

    private static ExchangeGoogleTokenCommand Command(string idToken, string countryCode = "EG") =>
        new(idToken, DeviceId, "test-device", countryCode);

    [Fact]
    public async Task Handle_DeniedCountry_ThrowsGeofenceDenied()
    {
        var act = () => Handler(new FakeGeofenceService("IR")).Handle(Command("not-a-jwt", "IR"), CancellationToken.None);

        await act.Should().ThrowAsync<GeofenceDeniedException>();
    }

    [Fact]
    public async Task Handle_MalformedToken_ThrowsUnauthorized()
    {
        var act = () => Handler().Handle(Command("not-a-jwt"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("Google ID token validation failed");
    }
}
