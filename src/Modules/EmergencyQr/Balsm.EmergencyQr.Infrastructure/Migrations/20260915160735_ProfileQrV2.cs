using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balsm.EmergencyQr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProfileQrV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                schema: "public",
                table: "emergency_qr_token");

            migrationBuilder.AddColumn<string>(
                name: "type",
                schema: "public",
                table: "emergency_qr_token",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "profile");

            migrationBuilder.CreateTable(
                name: "qr_scan_record",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    client_class = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qr_scan_record", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_qr_scan_record_owner_user_id_resolved_at",
                schema: "public",
                table: "qr_scan_record",
                columns: new[] { "owner_user_id", "resolved_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "qr_scan_record",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "type",
                schema: "public",
                table: "emergency_qr_token");

            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                schema: "public",
                table: "emergency_qr_token",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
