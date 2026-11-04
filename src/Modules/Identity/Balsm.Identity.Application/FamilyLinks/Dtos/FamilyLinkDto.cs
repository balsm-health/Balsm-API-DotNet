namespace Balsm.Identity.Application.FamilyLinks.Dtos;

/// <summary>
/// One family link as the caller sees it. What is revealed depends on the
/// caller's side and the link's state: the requester sees nothing about the
/// recipient until the recipient approves; account ids appear only once the
/// two accounts have been linked; the other side's public key and card version
/// only while linked.
/// </summary>
/// <param name="Id">Client-minted link id.</param>
/// <param name="Direction"><c>outgoing</c> (the caller sent the request) or <c>incoming</c>.</param>
/// <param name="Status"><c>pending</c>, <c>linked</c>, <c>declined</c>, <c>cancelled</c> or <c>unlinked</c>.</param>
/// <param name="Relation">How the requester relates to the recipient (app <c>Relationship</c> wire code).</param>
/// <param name="Counterpart">The other party; null for an outgoing request that was never approved.</param>
/// <param name="CounterpartPublicKey">The other party's device X25519 public key (base64, 32 bytes) while linked.</param>
/// <param name="OwnPublicKey">The caller's own device public key as last uploaded (base64), if any.</param>
/// <param name="MyCardVersion">Version of the card the caller shares on this link; null when not sharing.</param>
/// <param name="TheirCardVersion">Version of the card the other party shares with the caller; null when none.</param>
public sealed record FamilyLinkDto(
    Guid Id,
    string Direction,
    string Status,
    string Relation,
    FamilyLinkCounterpartDto? Counterpart,
    byte[]? CounterpartPublicKey,
    byte[]? OwnPublicKey,
    int? MyCardVersion,
    int? TheirCardVersion,
    DateTime CreatedAt,
    DateTime? RespondedAt,
    DateTime? LinkedAt,
    DateTime? EndedAt);
