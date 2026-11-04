namespace Balsm.Identity.Application.FamilyLinks.Dtos;

/// <summary>
/// One family link as the caller sees it, flat, with every <c>other_*</c> field
/// describing the other party. What is revealed depends on the caller's side and
/// the link's state: the requester sees no name or handle for the recipient
/// until the recipient approves; <c>other_account_id</c> appears only once the
/// accounts have been linked (the app needs it for the sealed card's AAD); the
/// other side's public key only while linked.
/// </summary>
/// <param name="Id">Client-minted link id.</param>
/// <param name="Relation">How the requester relates to the recipient (son, daughter, mother, father, spouse, neighbour, friend, other).</param>
/// <param name="Status"><c>pending</c>, <c>linked</c>, <c>declined</c>, <c>cancelled</c> or <c>unlinked</c>.</param>
/// <param name="Direction"><c>outgoing</c> (the caller sent the request) or <c>incoming</c>.</param>
/// <param name="UpdatedAt">Last change to the link; null if never changed since creation.</param>
/// <param name="OtherAccountId">The other account's id; set once the link has been approved.</param>
/// <param name="OtherDisplayName">The other account's display name; withheld from the requester until linked.</param>
/// <param name="OtherHandle">The other account's @handle; withheld from the requester until linked.</param>
/// <param name="OtherPublicKey">The other party's device X25519 public key (base64, 32 bytes) while linked.</param>
/// <param name="CardSharedByMe">The caller currently shares a sealed card on this link.</param>
/// <param name="CardSharedWithMe">The other party currently shares a sealed card with the caller (linked only).</param>
/// <param name="OwnPublicKey">The caller's own device public key as last uploaded (base64), if any.</param>
/// <param name="MyCardVersion">Version of the card the caller shares; null when not sharing.</param>
/// <param name="TheirCardVersion">Version of the card shared with the caller; null when none.</param>
public sealed record FamilyLinkDto(
    Guid Id,
    string Relation,
    string Status,
    string Direction,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid? OtherAccountId,
    string? OtherDisplayName,
    string? OtherHandle,
    byte[]? OtherPublicKey,
    bool CardSharedByMe,
    bool CardSharedWithMe,
    byte[]? OwnPublicKey,
    long? MyCardVersion,
    long? TheirCardVersion,
    DateTime? RespondedAt,
    DateTime? LinkedAt,
    DateTime? EndedAt);
