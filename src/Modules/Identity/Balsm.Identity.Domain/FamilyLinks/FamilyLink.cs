using Balsm.Identity.Domain.FamilyLinks.Events;
using Balsm.SharedKernel.Domain;
using Balsm.SharedKernel.Results;

namespace Balsm.Identity.Domain.FamilyLinks;

/// <summary>
/// A link between two Balsm accounts that a requester asked for by scanning the
/// recipient's profile QR. The scanned jti is only a request handle: from
/// creation on the link binds to the two account ids, so the recipient rotating
/// or revoking their QR does not affect it.
///
/// Every operation is party-checked here: a caller who is neither the requester
/// nor the recipient gets <see cref="FamilyLinkErrors.NotFound"/>, never a hint
/// that the link exists (threat E10). Rows are never hard-deleted — except the
/// sealed card shares, which are purged when sharing stops or the link ends.
/// </summary>
public sealed class FamilyLink : AggregateRoot
{
    private readonly List<FamilyLinkConsent> _consents = [];
    private readonly List<FamilyLinkCardShare> _cardShares = [];

    public Guid RequesterUserId { get; private set; }
    public Guid RecipientUserId { get; private set; }

    /// <summary>Lower of the two account ids — with <see cref="PairHighUserId"/>
    /// an order-free pair key, so one open request or link per pair holds in
    /// both directions.</summary>
    public Guid PairLowUserId { get; private set; }
    public Guid PairHighUserId { get; private set; }

    /// <summary>How the requester relates to the recipient; one of <see cref="FamilyRelation.Allowed"/>.</summary>
    public string Relation { get; private set; } = string.Empty;
    public FamilyLinkStatus Status { get; private set; }

    /// <summary>Requester's device X25519 public key (32 bytes) for sealed card shares.</summary>
    public byte[]? RequesterPublicKey { get; private set; }

    /// <summary>Recipient's device X25519 public key (32 bytes) for sealed card shares.</summary>
    public byte[]? RecipientPublicKey { get; private set; }

    /// <summary>When the recipient approved or declined.</summary>
    public DateTime? RespondedAt { get; private set; }

    /// <summary>When the link became <see cref="FamilyLinkStatus.Linked"/>; stays set after unlinking.</summary>
    public DateTime? LinkedAt { get; private set; }

    /// <summary>When the request was cancelled or the link unlinked.</summary>
    public DateTime? EndedAt { get; private set; }
    public Guid? EndedByUserId { get; private set; }

    /// <summary>Optimistic-concurrency token, renewed by every change, so two
    /// concurrent edits of one link cannot silently overwrite each other.</summary>
    public Guid ConcurrencyStamp { get; private set; }

    public IReadOnlyCollection<FamilyLinkConsent> Consents => _consents.AsReadOnly();
    public IReadOnlyCollection<FamilyLinkCardShare> CardShares => _cardShares.AsReadOnly();

    /// <summary>Pending or linked — the states that block another request for the pair.</summary>
    public bool IsOpen => Status is FamilyLinkStatus.Pending or FamilyLinkStatus.Linked;

    private FamilyLink() { }

    /// <summary>Opens a pending request from <paramref name="requesterUserId"/> to
    /// <paramref name="recipientUserId"/> and records the requester's consent.</summary>
    /// <param name="id">Client-minted id; the create is idempotent on it.</param>
    public static Result<FamilyLink> Request(
        Guid id,
        Guid requesterUserId,
        Guid recipientUserId,
        string relation,
        byte[]? requesterPublicKey,
        DateTime now)
    {
        if (id == Guid.Empty) return Result.Failure<FamilyLink>(FamilyLinkErrors.InvalidId);
        if (requesterUserId == recipientUserId) return Result.Failure<FamilyLink>(FamilyLinkErrors.SelfLink);
        if (!FamilyRelation.IsAllowed(relation)) return Result.Failure<FamilyLink>(FamilyLinkErrors.InvalidRelation);
        if (requesterPublicKey is not null && !IsValidPublicKey(requesterPublicKey))
            return Result.Failure<FamilyLink>(FamilyLinkErrors.InvalidPublicKey);

        var low = requesterUserId.CompareTo(recipientUserId) < 0;
        var link = new FamilyLink
        {
            Id = id,
            RequesterUserId = requesterUserId,
            RecipientUserId = recipientUserId,
            PairLowUserId = low ? requesterUserId : recipientUserId,
            PairHighUserId = low ? recipientUserId : requesterUserId,
            Relation = relation,
            Status = FamilyLinkStatus.Pending,
            RequesterPublicKey = requesterPublicKey,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid(),
        };
        link._consents.Add(FamilyLinkConsent.Grant(id, requesterUserId, FamilyLinkConsentPurpose.Link, now));
        return Result.Success(link);
    }

    /// <summary>X25519 public keys are exactly 32 bytes.</summary>
    public static bool IsValidPublicKey(byte[]? key) => key is { Length: FamilyLinkCardShare.PublicKeyBytes };

    public bool IsParty(Guid userId) => userId == RequesterUserId || userId == RecipientUserId;

    /// <summary>The other party. Only meaningful when <see cref="IsParty"/> holds.</summary>
    public Guid CounterpartOf(Guid userId) => userId == RequesterUserId ? RecipientUserId : RequesterUserId;

    public byte[]? PublicKeyOf(Guid userId) => userId == RequesterUserId ? RequesterPublicKey : RecipientPublicKey;

    public FamilyLinkCardShare? CardOwnedBy(Guid ownerUserId) =>
        _cardShares.FirstOrDefault(c => c.OwnerUserId == ownerUserId);

    /// <summary>Recipient approves: pending → linked, recipient consent recorded.</summary>
    public Result Approve(Guid actorUserId, byte[]? recipientPublicKey, DateTime now)
    {
        if (actorUserId != RecipientUserId) return Result.Failure(FamilyLinkErrors.NotFound);
        if (Status != FamilyLinkStatus.Pending) return Result.Failure(FamilyLinkErrors.NotPending);
        if (recipientPublicKey is not null && !IsValidPublicKey(recipientPublicKey))
            return Result.Failure(FamilyLinkErrors.InvalidPublicKey);

        if (recipientPublicKey is not null) RecipientPublicKey = recipientPublicKey;
        Status = FamilyLinkStatus.Linked;
        RespondedAt = now;
        LinkedAt = now;
        _consents.Add(FamilyLinkConsent.Grant(Id, RecipientUserId, FamilyLinkConsentPurpose.Link, now));
        Touch();
        AddDomainEvent(new FamilyLinkApproved(Id, RequesterUserId, RecipientUserId));
        return Result.Success();
    }

    /// <summary>Recipient declines: pending → declined.</summary>
    public Result Decline(Guid actorUserId, DateTime now)
    {
        if (actorUserId != RecipientUserId) return Result.Failure(FamilyLinkErrors.NotFound);
        if (Status != FamilyLinkStatus.Pending) return Result.Failure(FamilyLinkErrors.NotPending);

        Status = FamilyLinkStatus.Declined;
        RespondedAt = now;
        WithdrawConsents(now);
        Touch();
        return Result.Success();
    }

    /// <summary>
    /// The requester cancels a pending request (→ cancelled), or either party
    /// unlinks an active link (→ unlinked, every sealed card purged). Idempotent
    /// on an already-ended link: succeeds with <c>false</c> (nothing changed).
    /// </summary>
    public Result<bool> End(Guid actorUserId, DateTime now)
    {
        if (!IsParty(actorUserId)) return Result.Failure<bool>(FamilyLinkErrors.NotFound);

        switch (Status)
        {
            case FamilyLinkStatus.Pending when actorUserId != RequesterUserId:
                return Result.Failure<bool>(FamilyLinkErrors.RecipientMustDecline);
            case FamilyLinkStatus.Pending:
                Status = FamilyLinkStatus.Cancelled;
                break;
            case FamilyLinkStatus.Linked:
                Status = FamilyLinkStatus.Unlinked;
                _cardShares.Clear();
                break;
            default:
                return Result.Success(false);
        }

        EndedAt = now;
        EndedByUserId = actorUserId;
        WithdrawConsents(now);
        Touch();
        return Result.Success(true);
    }

    /// <summary>Stores the caller's current device public key on an open link.</summary>
    public Result SetPublicKey(Guid actorUserId, byte[] publicKey)
    {
        if (!IsParty(actorUserId)) return Result.Failure(FamilyLinkErrors.NotFound);
        if (!IsOpen) return Result.Failure(FamilyLinkErrors.Ended);
        if (!IsValidPublicKey(publicKey)) return Result.Failure(FamilyLinkErrors.InvalidPublicKey);

        if (actorUserId == RequesterUserId) RequesterPublicKey = publicKey;
        else RecipientPublicKey = publicKey;
        Touch();
        return Result.Success();
    }

    /// <summary>
    /// Stores the owner's sealed card for the other party. Only on a linked
    /// link, only newer versions; a byte-identical replay of the stored version
    /// succeeds with <c>false</c> (nothing changed). The first share records the
    /// owner's opt-in consent.
    /// </summary>
    public Result<bool> ShareCard(
        Guid ownerUserId,
        int version,
        byte[] ephemeralPublicKey,
        byte[] nonce,
        byte[] ciphertext,
        DateTime now)
    {
        if (!IsParty(ownerUserId)) return Result.Failure<bool>(FamilyLinkErrors.NotFound);
        if (Status != FamilyLinkStatus.Linked) return Result.Failure<bool>(FamilyLinkErrors.NotLinked);
        if (version < 1
            || ephemeralPublicKey.Length != FamilyLinkCardShare.PublicKeyBytes
            || nonce.Length != FamilyLinkCardShare.NonceBytes
            || ciphertext.Length == 0)
            return Result.Failure<bool>(FamilyLinkErrors.InvalidCard);
        if (ciphertext.Length > FamilyLinkCardShare.MaxCiphertextBytes)
            return Result.Failure<bool>(FamilyLinkErrors.CardTooLarge);

        var share = CardOwnedBy(ownerUserId);
        if (share is not null)
        {
            if (share.IsSameEnvelope(version, ephemeralPublicKey, nonce, ciphertext)) return Result.Success(false);
            if (version <= share.Version) return Result.Failure<bool>(FamilyLinkErrors.StaleCardVersion);
        }
        else
        {
            share = FamilyLinkCardShare.Create(Id, ownerUserId);
            _cardShares.Add(share);
        }

        if (!HasActiveConsent(ownerUserId, FamilyLinkConsentPurpose.EmergencyCardShare))
            _consents.Add(FamilyLinkConsent.Grant(Id, ownerUserId, FamilyLinkConsentPurpose.EmergencyCardShare, now));

        share.Replace(version, ephemeralPublicKey, nonce, ciphertext, now);
        Touch();
        return Result.Success(true);
    }

    /// <summary>
    /// The owner stops sharing: the sealed card is purged and the card-share
    /// consent withdrawn. Idempotent — succeeds with <c>false</c> when nothing
    /// was shared.
    /// </summary>
    public Result<bool> StopSharingCard(Guid ownerUserId, DateTime now)
    {
        if (!IsParty(ownerUserId)) return Result.Failure<bool>(FamilyLinkErrors.NotFound);

        var share = CardOwnedBy(ownerUserId);
        if (share is null) return Result.Success(false);

        _cardShares.Remove(share);
        foreach (var consent in _consents.Where(c =>
                     c.UserId == ownerUserId && c.Purpose == FamilyLinkConsentPurpose.EmergencyCardShare))
            consent.Withdraw(now);
        Touch();
        return Result.Success(true);
    }

    private void Touch() => ConcurrencyStamp = Guid.NewGuid();

    private bool HasActiveConsent(Guid userId, FamilyLinkConsentPurpose purpose) =>
        _consents.Any(c => c.UserId == userId && c.Purpose == purpose && c.IsActive);

    private void WithdrawConsents(DateTime now)
    {
        foreach (var consent in _consents) consent.Withdraw(now);
    }
}
