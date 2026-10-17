using Balsm.SharedKernel.Domain;

namespace Balsm.Entity.Domain;

public sealed class EntityType : BaseEntity
{
    public EntityType() { }

    // Used for deterministic seeding in EF configuration
    internal EntityType(Guid id) { Id = id; }

    public string Code { get; set; } = "";
    public string LabelEn { get; set; } = "";
    public string LabelAr { get; set; } = "";
    public bool IsSeeded { get; set; }
}
