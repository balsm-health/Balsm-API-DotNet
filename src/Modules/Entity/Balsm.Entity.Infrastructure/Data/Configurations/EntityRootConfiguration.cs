using Balsm.Entity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Entity.Infrastructure.Data.Configurations;

internal sealed class EntityRootConfiguration : IEntityTypeConfiguration<EntityRoot>
{
    public void Configure(EntityTypeBuilder<EntityRoot> builder)
    {
        builder.ToTable("EntityRoots");
        builder.HasIndex(e => new { e.WorkspaceId, e.IsDeleted })
            .HasDatabaseName("IX_EntityRoot_WorkspaceId_IsDeleted");
    }
}
