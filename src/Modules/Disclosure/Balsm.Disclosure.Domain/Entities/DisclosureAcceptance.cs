namespace Balsm.Disclosure.Domain.Entities;

public sealed class DisclosureAcceptance
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string DisclosureId { get; private set; } = string.Empty;
    public string Version { get; private set; } = string.Empty;
    public string CountryCodeAtAccept { get; private set; } = string.Empty;
    public string SupervisoryAuthorityNameAtAccept { get; private set; } = string.Empty;
    public string PreferredLanguageAtAccept { get; private set; } = string.Empty;
    public DateTime AcceptedAt { get; private set; } = DateTime.UtcNow;

    private DisclosureAcceptance() { }

    public static DisclosureAcceptance Create(
        Guid userId, string disclosureId, string version,
        string countryCode, string supervisoryAuthority, string language) =>
        new()
        {
            UserId = userId,
            DisclosureId = disclosureId,
            Version = version,
            CountryCodeAtAccept = countryCode,
            SupervisoryAuthorityNameAtAccept = supervisoryAuthority,
            PreferredLanguageAtAccept = language
        };
}
