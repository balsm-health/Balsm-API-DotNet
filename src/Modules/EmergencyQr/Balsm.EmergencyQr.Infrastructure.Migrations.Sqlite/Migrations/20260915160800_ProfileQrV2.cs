using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balsm.EmergencyQr.Infrastructure.Migrations.Sqlite.Migrations
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
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "profile");

            migrationBuilder.CreateTable(
                name: "qr_scan_record",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    token_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    client_class = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    country = table.Column<string>(type: "TEXT", maxLength: 2, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
