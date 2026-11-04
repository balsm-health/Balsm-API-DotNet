namespace Balsm.Identity.Domain.FamilyLinks;

/// <summary>What a <see cref="FamilyLinkConsent"/> row consents to.</summary>
public enum FamilyLinkConsentPurpose
{
    /// <summary>Being linked with the other party (requester on create, recipient on approve).</summary>
    Link,

    /// <summary>Sharing the owner's sealed emergency card with the other party (opt-in, per link).</summary>
    EmergencyCardShare,
}
