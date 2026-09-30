using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balsm.CareTeam.Infrastructure.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialCareTeamSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "care_provider",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    health_profile_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    name_ct = table.Column<byte[]>(type: "bytea", nullable: false),
                    specialty_ct = table.Column<byte[]>(type: "bytea", nullable: true),
                    phone_ct = table.Column<byte[]>(type: "bytea", nullable: true),
                    phone2_ct = table.Column<byte[]>(type: "bytea", nullable: true),
                    email_ct = table.Column<byte[]>(type: "bytea", nullable: true),
                    clinic_ct = table.Column<byte[]>(type: "bytea", nullable: true),
                    address_ct = table.Column<byte[]>(type: "bytea", nullable: true),
                    map_url_ct = table.Column<byte[]>(type: "bytea", nullable: true),
                    notes_ct = table.Column<byte[]>(type: "bytea", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_care_provider", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_care_provider_user_id_health_profile_id_updated_at",
                schema: "public",
                table: "care_provider",
                columns: new[] { "user_id", "health_profile_id", "updated_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "care_provider",
                schema: "public");
        }
    }
}
