using Balsm.Entity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Entity.Infrastructure.Data.Configurations;

internal sealed class EntityTypeConfiguration : IEntityTypeConfiguration<EntityType>
{
    private static readonly Guid PharmacyId = new("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ClinicId = new("10000000-0000-0000-0000-000000000002");
    private static readonly Guid HospitalId = new("10000000-0000-0000-0000-000000000003");

    private static readonly DateTime SeedTime = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<EntityType> builder)
    {
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UX_EntityType_Code");
        builder.HasQueryFilter(e => !e.IsDeleted);

        builder.HasData(
            new EntityType(PharmacyId) { Code = "pharmacy", LabelEn = "Pharmacy", LabelAr = "صيدلية", IsSeeded = true, CreatedAt = SeedTime },
            new EntityType(ClinicId) { Code = "clinic", LabelEn = "Clinic", LabelAr = "عيادة", IsSeeded = true, CreatedAt = SeedTime },
            new EntityType(HospitalId) { Code = "hospital", LabelEn = "Hospital", LabelAr = "مستشفى", IsSeeded = true, CreatedAt = SeedTime }
        );
    }
}
