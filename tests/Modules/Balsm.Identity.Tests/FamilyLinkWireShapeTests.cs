using System.Text.Json;
using System.Text.Json.Nodes;
using Balsm.Identity.Api.FamilyLinks;
using Balsm.Identity.Application.FamilyLinks.Dtos;
using FluentAssertions;
using Xunit;
using static Balsm.Identity.Tests.FamilyLinkFixtures;

namespace Balsm.Identity.Tests;

/// <summary>
/// Pins the JSON the app is built against (snake_case, byte arrays as base64),
/// using the host's serializer settings. A rename here is a breaking contract change.
/// </summary>
public sealed class FamilyLinkWireShapeTests
{
    private static readonly JsonSerializerOptions Wire = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private static string[] Keys(JsonNode? node) => node!.AsObject().Select(p => p.Key).ToArray();

    [Fact]
    public void LinkEnvelope_HasTheContractFields()
    {
        var dto = new FamilyLinkDto(
            LinkId, "incoming", "linked", "child", new FamilyLinkCounterpartDto(Requester, "handle1", "Member 1"),
            Key(1), Key(2), 3, 4, Now, Now, Now, null);

        var json = JsonNode.Parse(JsonSerializer.Serialize(new FamilyLinkResponse(dto), Wire))!;

        Keys(json).Should().Equal("data");
        Keys(json["data"]).Should().Equal(
            "id", "direction", "status", "relation", "counterpart", "counterpart_public_key", "own_public_key",
            "my_card_version", "their_card_version", "created_at", "responded_at", "linked_at", "ended_at");
        Keys(json["data"]!["counterpart"]).Should().Equal("user_id", "handle", "display_name");
        json["data"]!["counterpart_public_key"]!.GetValue<string>().Should().Be(Convert.ToBase64String(Key(1)));
        (dto.Relation, dto.MyCardVersion, dto.TheirCardVersion, dto.RespondedAt, dto.LinkedAt, dto.EndedAt)
            .Should().Be(("child", 3, 4, Now, Now, (DateTime?)null));
    }

    [Fact]
    public void ListEnvelope_HasPagingFields()
    {
        var json = JsonNode.Parse(JsonSerializer.Serialize(
            new FamilyLinkListResponse(new FamilyLinkPageDto([], 1, 20, 0, false)), Wire))!;

        Keys(json["data"]).Should().Equal("items", "page", "page_size", "total_count", "has_next_page");
    }

    [Fact]
    public void CardEnvelope_HasTheSealedEnvelopeFields()
    {
        var card = new FamilyLinkCardDto(LinkId, Recipient, 2, Key(), Nonce(), Ciphertext(), Now);

        var json = JsonNode.Parse(JsonSerializer.Serialize(new FamilyLinkCardResponse(card), Wire))!;

        Keys(json["data"]).Should().Equal(
            "family_link_id", "owner_user_id", "version", "ephemeral_public_key", "nonce", "ciphertext", "updated_at");
        (card.Nonce, card.Ciphertext, card.UpdatedAt).Should().Be((card.Nonce, card.Ciphertext, Now));
    }

    [Fact]
    public void ErrorEnvelope_HasCodeMessageCorrelationAndFields()
    {
        var json = JsonNode.Parse(JsonSerializer.Serialize(new FamilyLinkErrorResponse(new FamilyLinkError(
            "ValidationFailed", "m", "corr", [new FamilyLinkFieldError("relation", "FamilyLink.InvalidRelation", "m")])), Wire))!;

        Keys(json["error"]).Should().Equal("code", "message", "correlation_id", "fields");
        Keys(json["error"]!["fields"]![0]).Should().Equal("field", "code", "message");
    }
}
