using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balsm.EmergencyQr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PermanentEmergencyQrToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_emergency_qr_token_ttl",
                schema: "public",
                table: "emergency_qr_token");

            migrationBuilder.AlterColumn<DateTime>(
                name: "expires_at",
                schema: "public",
                table: "emergency_qr_token",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddCheckConstraint(
                name: "CK_emergency_qr_token_ttl",
                schema: "public",
                table: "emergency_qr_token",
                sql: "ttl_seconds IN (0, 3600, 21600, 86400, 604800)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_emergency_qr_token_ttl",
                schema: "public",
                table: "emergency_qr_token");

            migrationBuilder.AlterColumn<DateTime>(
                name: "expires_at",
                schema: "public",
                table: "emergency_qr_token",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_emergency_qr_token_ttl",
                schema: "public",
                table: "emergency_qr_token",
                sql: "ttl_seconds IN (3600, 21600, 86400, 604800)");
        }
    }
}
