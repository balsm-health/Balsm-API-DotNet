using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Balsm.Identity.Domain.FamilyLinks;
using Balsm.Identity.Infrastructure.Data;
using Balsm.Infrastructure.Auth;
using Balsm.SharedKernel.Contracts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.E2E.Tests.FamilyLink;

/// <summary>
/// The whole family-link flow across Identity, EmergencyQr and Account, as two
/// patients' apps would drive it: A scans B's profile QR and asks; B pulls the
/// request and approves; B shares a sealed emergency card; A reads it; B stops
/// sharing (ciphertext purged); B unlinks. Ends by checking the audit trail.
/// </summary>
public sealed class FamilyLinkE2ETests : IClassFixture<E2EWebAppFactory>
{
    private const string Base = "/api/v1/family-links";
    private readonly E2EWebAppFactory _factory;

    public FamilyLinkE2ETests(E2EWebAppFactory factory) => _factory = factory;

    private async Task<(Guid Id, HttpClient Client)> SignInNewPatientAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var id = await scope.ServiceProvider.GetRequiredService<IUserAccountProvisioner>().ProvisionAsync("EG", "en");
        var token = scope.ServiceProvider.GetRequiredService<JwtService>().IssueAccessToken(id, $"{id:N}@example.test");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (id, client);
    }

    private static async Task<JsonNode> DataAsync(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!["data"]!;
    }

    private static string B64(byte fill, int length) => Convert.ToBase64String(Enumerable.Repeat(fill, length).ToArray());

    [Fact]
    public async Task RequestApproveShareReadRevokeUnlink_FlowsEndToEnd()
    {
        var (aId, a) = await SignInNewPatientAsync();
        var (bId, b) = await SignInNewPatientAsync();
        var jti = Guid.NewGuid();
        (await b.PostAsJsonAsync("/emergency-qr/mint", new
        {
            ciphertext = B64(1, 48), profile_etag = "etag-1", ttl_seconds = 0, token_id = jti,
        })).EnsureSuccessStatusCode();
        var linkId = Guid.NewGuid();

        var requested = await DataAsync(await a.PostAsJsonAsync(Base, new
        {
            id = linkId, jti, relation = "parent", public_key = B64(0xA1, 32),
        }));
        var incoming = await DataAsync(await b.GetAsync($"{Base}/incoming?status=pending"));
        var approved = await DataAsync(await b.PostAsJsonAsync($"{Base}/{linkId}/approve", new { public_key = B64(0xB1, 32) }));
        var bSeesA = await DataAsync(await b.GetAsync($"{Base}/{linkId}"));
        var sealedCard = new
        {
            version = 1, ephemeral_public_key = B64(0xE1, 32), nonce = B64(0x0C, 12), ciphertext = B64(0xCC, 512),
        };
        var share = await b.PutAsJsonAsync($"{Base}/{linkId}/card", sealedCard);
        var aSeesB = await DataAsync(await a.GetAsync($"{Base}/{linkId}"));
        var card = await DataAsync(await a.GetAsync($"{Base}/{linkId}/card"));
        var revoke = await b.DeleteAsync($"{Base}/{linkId}/card");
        var afterRevoke = await a.GetAsync($"{Base}/{linkId}/card");
        var unlink = await b.DeleteAsync($"{Base}/{linkId}");
        var outgoing = await DataAsync(await a.GetAsync($"{Base}/outgoing"));

        requested["status"]!.GetValue<string>().Should().Be("pending");
        incoming["items"]!.AsArray().Should().ContainSingle(i => i!["id"]!.GetValue<Guid>() == linkId);
        approved["status"]!.GetValue<string>().Should().Be("linked");
        bSeesA["counterpart_public_key"]!.GetValue<string>().Should().Be(B64(0xA1, 32));
        share.StatusCode.Should().Be(HttpStatusCode.NoContent);
        aSeesB["their_card_version"]!.GetValue<int>().Should().Be(1);
        aSeesB["counterpart"]!["user_id"]!.GetValue<Guid>().Should().Be(bId);
        (card["owner_user_id"]!.GetValue<Guid>(), card["version"]!.GetValue<int>()).Should().Be((bId, 1));
        (card["ephemeral_public_key"]!.GetValue<string>(), card["nonce"]!.GetValue<string>(), card["ciphertext"]!.GetValue<string>())
            .Should().Be((sealedCard.ephemeral_public_key, sealedCard.nonce, sealedCard.ciphertext));
        revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);
        afterRevoke.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unlink.StatusCode.Should().Be(HttpStatusCode.NoContent);
        outgoing["items"]![0]!["status"]!.GetValue<string>().Should().Be("unlinked");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var trail = await db.FamilyLinkAuditEntries.Where(e => e.FamilyLinkId == linkId)
            .OrderBy(e => e.OccurredAt).Select(e => e.Action).ToListAsync();
        trail.Should().Equal(
            FamilyLinkAuditAction.Requested, FamilyLinkAuditAction.Approved, FamilyLinkAuditAction.CardShared,
            FamilyLinkAuditAction.CardRead, FamilyLinkAuditAction.CardRevoked, FamilyLinkAuditAction.Unlinked);
        var consents = await db.FamilyLinkConsents.Where(c => c.FamilyLinkId == linkId).ToListAsync();
        consents.Select(c => (c.UserId, c.Purpose)).Should().BeEquivalentTo(new[]
        {
            (aId, FamilyLinkConsentPurpose.Link), (bId, FamilyLinkConsentPurpose.Link), (bId, FamilyLinkConsentPurpose.EmergencyCardShare),
        });
        consents.Should().OnlyContain(c => c.WithdrawnAt != null);
        (await db.FamilyLinkCardShares.AnyAsync(c => c.FamilyLinkId == linkId)).Should().BeFalse();
    }
}
