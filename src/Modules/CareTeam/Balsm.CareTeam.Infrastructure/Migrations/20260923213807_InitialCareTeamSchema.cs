using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balsm.CareTeam.Infrastructure.Migrations
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
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    health_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name_ct = table.Column<byte[]>(type: "bytea", nullable: false),
                    specialty_ct = table.Column<byte[]>(type: "bytea", nullable: true),
                    phone_ct = table.Column<byte[]>(type: "bytea", nullable: true),
                    phone2_ct = table.Column<byte[]>(type: "bytea", nullable: true),
                    email_ct = table.Column<byte[]>(type: "bytea", nullable: true),
                    clinic_ct = table.Column<byte[]>(type: "bytea", nullable: true),
                    address_ct = table.Column<byte[]>(type: "bytea", nullable: true),
                    map_url_ct = table.Column<byte[]>(type: "bytea", nullable: true),
                    notes_ct = table.Column<byte[]>(type: "bytea", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
