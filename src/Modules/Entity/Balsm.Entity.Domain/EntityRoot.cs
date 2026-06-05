using Balsm.SharedKernel.Domain;

namespace Balsm.Entity.Domain;

public sealed class EntityRoot : AggregateRoot
{
    private EntityRoot() { }

    public Guid WorkspaceId { get; private set; }
    public string Name { get; private set; } = "";
    public string TypeCode { get; private set; } = "";
    public string? RegistrationNumber { get; private set; }

    public static EntityRoot Create(Guid workspaceId, string name, string typeCode, string? registrationNumber = null)
    {
        return new EntityRoot
        {
            WorkspaceId = workspaceId,
            Name = name.Trim(),
            TypeCode = typeCode,
            RegistrationNumber = registrationNumber,
        };
    }

    public void Update(string name, string? registrationNumber)
    {
        Name = name.Trim();
        RegistrationNumber = registrationNumber;
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
