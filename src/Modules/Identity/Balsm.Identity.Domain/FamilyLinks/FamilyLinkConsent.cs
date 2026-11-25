namespace Balsm.Identity.Domain.FamilyLinks;

/// <summary>
/// One party's consent on one link. Never deleted: withdrawal stamps
/// <see cref="WithdrawnAt"/> so the consent history stays reconstructable.
/// </summary>
public sealed class FamilyLinkConsent
{
    public Guid Id { get; private set; }
    public Guid FamilyLinkId { get; private set; }
    public Guid UserId { get; private set; }
    public FamilyLinkConsentPurpose Purpose { get; private set; }
    public DateTime GrantedAt { get; private set; }
    public DateTime? WithdrawnAt { get; private set; }

    public bool IsActive => WithdrawnAt is null;

    private FamilyLinkConsent() { }

    internal static FamilyLinkConsent Grant(Guid linkId, Guid userId, FamilyLinkConsentPurpose purpose, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        FamilyLinkId = linkId,
        UserId = userId,
        Purpose = purpose,
        GrantedAt = now,
    };

    internal void Withdraw(DateTime now) => WithdrawnAt ??= now;
}
