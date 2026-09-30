using Balsm.SharedKernel.Domain;

namespace Balsm.CareTeam.Domain.Entities;

/// <summary>
/// The encrypted free-text columns of one care-team row. Every member is
/// already AES-256-GCM ciphertext produced by CareTeamEncryptionService —
/// the domain never sees plaintext PHI (FR-502).
/// </summary>
public sealed record CareProviderFields(
    byte[] Name,
    byte[]? Specialty,
    byte[]? Phone,
    byte[]? Phone2,
    byte[]? Email,
    byte[]? Clinic,
    byte[]? Address,
    byte[]? MapUrl,
    byte[]? Notes);

/// <summary>
/// Cloud mirror of one on-device care_provider row. The id is minted by the
/// device (UUIDv7) and is authoritative on both sides — the server never
/// assigns one (FR-505).
/// </summary>
public sealed class CareProvider : AggregateRoot
{
    /// <summary>Mirrors CareProviderType.id in the Flutter domain.</summary>
    public static readonly string[] AllowedTypes =
        ["doctor", "nurse", "carer", "pharmacy", "lab", "physio", "clinic", "other"];

    public Guid UserId { get; private set; }
    public Guid HealthProfileId { get; private set; }
    public string Type { get; private set; } = string.Empty;

    public byte[] Name { get; private set; } = [];
    public byte[]? Specialty { get; private set; }
    public byte[]? Phone { get; private set; }
    public byte[]? Phone2 { get; private set; }
    public byte[]? Email { get; private set; }
    public byte[]? Clinic { get; private set; }
    public byte[]? Address { get; private set; }
    public byte[]? MapUrl { get; private set; }
    public byte[]? Notes { get; private set; }

    private CareProvider() { }

    public static CareProvider Create(
        Guid id,
        Guid userId,
        Guid healthProfileId,
        string type,
        CareProviderFields fields,
        DateTime createdAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("id must not be the empty GUID", nameof(id));
        if (!AllowedTypes.Contains(type))
            throw new ArgumentException($"Invalid care provider type: {type}", nameof(type));

        var provider = new CareProvider
        {
            Id = id,
            UserId = userId,
            HealthProfileId = healthProfileId,
            Type = type,
            CreatedAt = createdAt
        };
        provider.Apply(fields);
        return provider;
    }

    /// <summary>
    /// Whole-row overwrite — the sync contract is last-writer-wins on the row,
    /// not a per-field merge (see spec "Row-level last-writer-wins").
    /// </summary>
    public void Overwrite(string type, CareProviderFields fields)
    {
        if (IsDeleted)
            throw new InvalidOperationException("Cannot overwrite a tombstoned care provider");
        if (!AllowedTypes.Contains(type))
            throw new ArgumentException($"Invalid care provider type: {type}", nameof(type));
        Type = type;
        Apply(fields);
    }

    /// <summary>Soft-delete so the removal can propagate to other devices (FR-507).</summary>
    public void Tombstone()
    {
        if (IsDeleted) return; // idempotent
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }

    private void Apply(CareProviderFields f)
    {
        Name = f.Name;
        Specialty = f.Specialty;
        Phone = f.Phone;
        Phone2 = f.Phone2;
        Email = f.Email;
        Clinic = f.Clinic;
        Address = f.Address;
        MapUrl = f.MapUrl;
        Notes = f.Notes;
    }
}
