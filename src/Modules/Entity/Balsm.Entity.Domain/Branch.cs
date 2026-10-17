using Balsm.SharedKernel.Domain;

namespace Balsm.Entity.Domain;

public sealed class Branch : AggregateRoot
{
    private Branch() { }

    public Guid EntityRootId { get; private set; }
    public string Name { get; private set; } = "";
    public string? AddressLine { get; private set; }
    public string? City { get; private set; }
    public string? Governorate { get; private set; }
    public string? Phone { get; private set; }

    public static Branch Create(
        Guid entityRootId, string name, string? addressLine = null,
        string? city = null, string? governorate = null, string? phone = null)
    {
        return new Branch
        {
            EntityRootId = entityRootId,
            Name = name.Trim(),
            AddressLine = addressLine,
            City = city,
            Governorate = governorate,
            Phone = phone,
        };
    }

    public void Update(string name, string? addressLine, string? city, string? governorate, string? phone)
    {
        Name = name.Trim();
        AddressLine = addressLine;
        City = city;
        Governorate = governorate;
        Phone = phone;
    }

    public void Deactivate()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }
}
