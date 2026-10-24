using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Balsm.Entity.Infrastructure.Migrations.Npgsql.Migrations
{
    /// <inheritdoc />
    public partial class InitialEntitySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "entity");

            migrationBuilder.CreateTable(
                name: "Branches",
                schema: "entity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityRootId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    AddressLine = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    Governorate = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EntityRoots",
                schema: "entity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TypeCode = table.Column<string>(type: "text", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntityRoots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EntityTypes",
                schema: "entity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    LabelEn = table.Column<string>(type: "text", nullable: false),
                    LabelAr = table.Column<string>(type: "text", nullable: false),
                    IsSeeded = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntityTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workspaces",
                schema: "entity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LocaleDefault = table.Column<string>(type: "text", nullable: false),
                    SingletonKey = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Workspace"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspaces", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "entity",
                table: "EntityTypes",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "IsSeeded", "LabelAr", "LabelEn", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "pharmacy", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, false, true, "صيدلية", "Pharmacy", null, null },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "clinic", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, false, true, "عيادة", "Clinic", null, null },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "hospital", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, false, true, "مستشفى", "Hospital", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Branch_EntityRootId_IsDeleted",
                schema: "entity",
                table: "Branches",
                columns: new[] { "EntityRootId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_EntityRoot_WorkspaceId_IsDeleted",
                schema: "entity",
                table: "EntityRoots",
                columns: new[] { "WorkspaceId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_EntityType_Code",
                schema: "entity",
                table: "EntityTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Workspace_SingletonKey",
                schema: "entity",
                table: "Workspaces",
                column: "SingletonKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Branches",
                schema: "entity");

            migrationBuilder.DropTable(
                name: "EntityRoots",
                schema: "entity");

            migrationBuilder.DropTable(
                name: "EntityTypes",
                schema: "entity");

            migrationBuilder.DropTable(
                name: "Workspaces",
                schema: "entity");
        }
    }
}
