using Balsm.Entity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Entity.Infrastructure.Data.Configurations;

internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.HasIndex(b => new { b.EntityRootId, b.IsDeleted })
            .HasDatabaseName("IX_Branch_EntityRootId_IsDeleted");
    }
}
