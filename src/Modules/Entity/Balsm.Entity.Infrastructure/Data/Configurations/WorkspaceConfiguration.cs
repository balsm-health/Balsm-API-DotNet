using Balsm.Entity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Entity.Infrastructure.Data.Configurations;

internal sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.Property<string>("SingletonKey")
            .HasMaxLength(16)
            .HasDefaultValue("Workspace")
            .IsRequired();

        builder.HasIndex("SingletonKey").IsUnique().HasDatabaseName("UX_Workspace_SingletonKey");
    }
}
