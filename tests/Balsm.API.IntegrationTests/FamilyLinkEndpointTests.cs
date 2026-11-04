using System.Net;
using System.Net.Http.Json;
using Balsm.Identity.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Balsm.API.IntegrationTests.FamilyLinkApiDriver;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// <c>/api/v1/family-links</c> over real HTTP through the host: module
/// registration, auth, the jti → account resolution across modules, status
/// codes and the wire shape the app is built against.
/// </summary>
[Collection(nameof(LiteWebAppCollection))]
public sealed class FamilyLinkEndpointTests(LiteWebAppFactory factory)
{
    private readonly FamilyLinkApiDriver _api = new(factory);

    [Fact]
    public async Task Create_ThenReplay_Answers201Then200WithTheSameLink()
    {
        var a = await _api.NewPatientAsync();
        var b = await _api.NewPatientAsync();
        var jti = await MintProfileQrAsync(b);
        var id = Guid.NewGuid();

        var created = await CreateAsync(a, id, jti);
        var replay = await CreateAsync(a, id, jti);

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Headers.Location!.ToString().Should().EndWith($"{Base}/{id}");
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await BodyAsync(replay))["data"]!;
        (body["id"]!.GetValue<Guid>(), body["status"]!.GetValue<string>(), body["direction"]!.GetValue<string>())
            .Should().Be((id, "pending", "outgoing"));
        body["counterpart"].Should().BeNull();
    }

    [Fact]
    public async Task Create_OwnProfileQr_Answers409SelfLink()
    {
        var a = await _api.NewPatientAsync();

        var response = await CreateAsync(a, Guid.NewGuid(), await MintProfileQrAsync(a));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BodyAsync(response))["error"]!["code"]!.GetValue<string>().Should().Be("FamilyLink.SelfLink");
    }

    [Fact]
    public async Task Create_UnknownAndRevokedJti_AnswerTheSameUniform404()
    {
        var a = await _api.NewPatientAsync();
        var b = await _api.NewPatientAsync();
        var revoked = await MintProfileQrAsync(b);
        (await b.Client.PostAsync($"/emergency-qr/{revoked}/revoke", null)).EnsureSuccessStatusCode();

        var unknown = await CreateAsync(a, Guid.NewGuid(), Guid.NewGuid());
        var gone = await CreateAsync(a, Guid.NewGuid(), revoked);

        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        gone.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BodyAsync(gone))["error"]!["code"]!.ToString()
            .Should().Be((await BodyAsync(unknown))["error"]!["code"]!.ToString()).And.Be("FamilyLink.NotFound");
    }

    [Fact]
    public async Task Create_WhenThePairAlreadyHasAnOpenRequest_Answers409EitherDirection()
    {
        var a = await _api.NewPatientAsync();
        var b = await _api.NewPatientAsync();
        await _api.RequestAsync(a, b);

        var reverse = await CreateAsync(b, Guid.NewGuid(), await MintProfileQrAsync(a));

        reverse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BodyAsync(reverse))["error"]!["code"]!.GetValue<string>().Should().Be("FamilyLink.AlreadyExists");
    }

    [Fact]
    public async Task Create_InvalidRelation_Answers422WithFieldErrors()
    {
        var a = await _api.NewPatientAsync();

        var response = await CreateAsync(a, Guid.NewGuid(), Guid.NewGuid(), relation: "cousin");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var error = (await BodyAsync(response))["error"]!;
        error["code"]!.GetValue<string>().Should().Be("ValidationFailed");
        error["fields"]![0]!["field"]!.GetValue<string>().Should().Be("relation");
    }

    [Fact]
    public async Task Create_BeyondTheHourlyLimit_Answers429WithRetryAfter()
    {
        var a = await _api.NewPatientAsync();
        for (var i = 0; i < 20; i++)
            await CreateAsync(a, Guid.NewGuid(), Guid.NewGuid(), relation: "cousin");

        var response = await CreateAsync(a, Guid.NewGuid(), Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task Incoming_ShowsTheRequesterNames_OutgoingHidesTheRecipientUntilApproval()
    {
        var a = await _api.NewPatientAsync("Jane", "jane_a");
        var b = await _api.NewPatientAsync("John", "john_b");
        var id = await _api.RequestAsync(a, b);

        var incoming = (await BodyAsync(await b.Client.GetAsync($"{Base}/incoming?status=pending")))["data"]!;
        var outgoing = (await BodyAsync(await a.Client.GetAsync($"{Base}/outgoing")))["data"]!;

        incoming["total_count"]!.GetValue<int>().Should().Be(1);
        var item = incoming["items"]![0]!;
        (item["id"]!.GetValue<Guid>(), item["direction"]!.GetValue<string>()).Should().Be((id, "incoming"));
        (item["counterpart"]!["handle"]!.GetValue<string>(), item["counterpart"]!["display_name"]!.GetValue<string>())
            .Should().Be(("jane_a", "Jane Doe"));
        item["counterpart"]!["user_id"].Should().BeNull();
        outgoing["items"]![0]!["counterpart"].Should().BeNull();
    }

    [Fact]
    public async Task Approve_ByRecipient_LinksAndRevealsBothSides()
    {
        var a = await _api.NewPatientAsync("Jane", "jane_c");
        var b = await _api.NewPatientAsync("John", "john_d");
        var id = await _api.RequestAsync(a, b);

        var approve = await b.Client.PostAsync($"{Base}/{id}/approve", null);
        var again = await b.Client.PostAsync($"{Base}/{id}/approve", null);
        var asRequester = (await BodyAsync(await a.Client.GetAsync($"{Base}/{id}")))["data"]!;

        approve.StatusCode.Should().Be(HttpStatusCode.OK);
        (await BodyAsync(approve))["data"]!["status"]!.GetValue<string>().Should().Be("linked");
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        asRequester["counterpart"]!["user_id"]!.GetValue<Guid>().Should().Be(b.UserId);
        asRequester["counterpart"]!["handle"]!.GetValue<string>().Should().Be("john_d");
    }

    [Fact]
    public async Task Approve_ByTheRequester_Answers404()
    {
        var a = await _api.NewPatientAsync();
        var b = await _api.NewPatientAsync();
        var id = await _api.RequestAsync(a, b);

        var response = await a.Client.PostAsync($"{Base}/{id}/approve", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Decline_ThenCancel_AreTerminalAndAuditedWithoutHardDelete()
    {
        var a = await _api.NewPatientAsync();
        var b = await _api.NewPatientAsync();
        var declined = await _api.RequestAsync(a, b);
        (await b.Client.PostAsync($"{Base}/{declined}/decline", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelled = await _api.RequestAsync(a, b);

        var cancel = await a.Client.DeleteAsync($"{Base}/{cancelled}");
        var recipientCancel = await b.Client.DeleteAsync($"{Base}/{(await _api.RequestAsync(a, b))}");

        cancel.StatusCode.Should().Be(HttpStatusCode.NoContent);
        recipientCancel.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        (await db.FamilyLinks.CountAsync(l => l.Id == declined || l.Id == cancelled)).Should().Be(2);
        (await db.FamilyLinkAuditEntries.CountAsync(e => e.FamilyLinkId == declined || e.FamilyLinkId == cancelled)).Should().Be(4);
    }

    [Fact]
    public async Task Card_StaleVersionTooLargeAndPurgeOnUnlink()
    {
        var a = await _api.NewPatientAsync();
        var b = await _api.NewPatientAsync();
        var id = await _api.LinkAsync(a, b);
        (await PutCardAsync(b, id, 2)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var stale = await PutCardAsync(b, id, 1);
        var tooLarge = await PutCardAsync(b, id, 3, 16 * 1024 + 1);
        var read = await a.Client.GetAsync($"{Base}/{id}/card");
        var unlink = await a.Client.DeleteAsync($"{Base}/{id}");
        var afterUnlink = await a.Client.GetAsync($"{Base}/{id}/card");

        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BodyAsync(stale))["error"]!["code"]!.GetValue<string>().Should().Be("FamilyLink.StaleCardVersion");
        tooLarge.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        (await BodyAsync(read))["data"]!["version"]!.GetValue<int>().Should().Be(2);
        unlink.StatusCode.Should().Be(HttpStatusCode.NoContent);
        afterUnlink.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().FamilyLinkCardShares
            .AnyAsync(c => c.FamilyLinkId == id)).Should().BeFalse();
    }

    [Fact]
    public async Task Card_ShareWhilePending_Answers409NotLinked()
    {
        var a = await _api.NewPatientAsync();
        var b = await _api.NewPatientAsync();
        var id = await _api.RequestAsync(a, b);

        var response = await PutCardAsync(a, id, 1);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Keys_UploadAfterPhoneChange_IsVisibleToTheOtherSide()
    {
        var a = await _api.NewPatientAsync();
        var b = await _api.NewPatientAsync();
        var id = await _api.LinkAsync(a, b);

        var bad = await a.Client.PutAsJsonAsync($"{Base}/{id}/keys", new { public_key = Convert.ToBase64String(new byte[8]) });
        var put = await a.Client.PutAsJsonAsync($"{Base}/{id}/keys", new { public_key = Convert.ToBase64String(Key(9)) });
        var seenByB = (await BodyAsync(await b.Client.GetAsync($"{Base}/{id}")))["data"]!;

        bad.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);
        seenByB["counterpart_public_key"]!.GetValue<string>().Should().Be(Convert.ToBase64String(Key(9)));
    }

    [Fact]
    public async Task Link_SurvivesTheRecipientRotatingTheirProfileQr()
    {
        var a = await _api.NewPatientAsync();
        var b = await _api.NewPatientAsync();
        var id = await _api.LinkAsync(a, b);

        await MintProfileQrAsync(b);
        var link = (await BodyAsync(await a.Client.GetAsync($"{Base}/{id}")))["data"]!;

        link["status"]!.GetValue<string>().Should().Be("linked");
    }
}
