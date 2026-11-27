using Balsm.SharedKernel.Results;

namespace Balsm.Identity.Domain.FamilyLinks;

/// <summary>Expected-failure catalog for family linking (Result pattern).</summary>
public static class FamilyLinkErrors
{
    /// <summary>Unknown link, a link the caller is not a party to, or an
    /// unknown / revoked / expired profile-QR jti — one error so none of these
    /// can be told apart (contract: 404, threat E10).</summary>
    public static readonly Error NotFound = new("FamilyLink.NotFound", "Family link not found.");

    /// <summary>The scanned QR belongs to the caller (contract: 409).</summary>
    public static readonly Error SelfLink = new("FamilyLink.SelfLink", "You cannot link to your own account.");

    /// <summary>A pending request or an active link already exists for the
    /// pair, in either direction (contract: 409).</summary>
    public static readonly Error AlreadyExists = new("FamilyLink.AlreadyExists", "A request or link already exists for this person.");

    /// <summary>Approve/decline on a link that is no longer pending (contract: 409).</summary>
    public static readonly Error NotPending = new("FamilyLink.NotPending", "The request is no longer pending.");

    /// <summary>Card share on a link that is not linked (contract: 409).</summary>
    public static readonly Error NotLinked = new("FamilyLink.NotLinked", "The link is not active.");

    /// <summary>The recipient tried to cancel a pending request; they decline it instead (contract: 409).</summary>
    public static readonly Error RecipientMustDecline = new("FamilyLink.RecipientMustDecline", "Decline the request instead of cancelling it.");

    /// <summary>Key upload on a declined, cancelled or unlinked link (contract: 409).</summary>
    public static readonly Error Ended = new("FamilyLink.Ended", "The link has ended.");

    /// <summary>A concurrent change to the same link won the race (contract: 409, retry).</summary>
    public static readonly Error ConcurrentUpdate = new("FamilyLink.ConcurrentUpdate", "The link changed meanwhile; retry.");

    /// <summary>A card version not newer than the stored one (contract: 409).</summary>
    public static readonly Error StaleCardVersion = new("FamilyLink.StaleCardVersion", "A newer emergency card version is already stored.");

    /// <summary>Sealed card ciphertext over <see cref="FamilyLinkCardShare.MaxCiphertextBytes"/> (contract: 413).</summary>
    public static readonly Error CardTooLarge = new("FamilyLink.CardTooLarge", "The sealed emergency card is too large.");

    /// <summary>Relation outside <see cref="FamilyRelation.Allowed"/> (contract: 422).</summary>
    public static readonly Error InvalidRelation = new("FamilyLink.InvalidRelation", "Invalid relation.");

    /// <summary>The empty GUID as a link id (contract: 422).</summary>
    public static readonly Error InvalidId = new("FamilyLink.InvalidId", "Invalid family link id.");

    /// <summary>A device public key that is not a 32-byte X25519 key (contract: 422).</summary>
    public static readonly Error InvalidPublicKey = new("FamilyLink.InvalidPublicKey", "Invalid device public key.");

    /// <summary>A sealed card with a bad version, ephemeral key, nonce or empty ciphertext (contract: 422).</summary>
    public static readonly Error InvalidCard = new("FamilyLink.InvalidCard", "Invalid sealed emergency card.");
}
