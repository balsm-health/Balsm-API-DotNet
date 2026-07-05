using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balsm.EmergencyQr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialEmergencyQrSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "emergency_qr_token",
                schema: "public",
                columns: table => new
                {
                    jti = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ciphertext = table.Column<byte[]>(type: "bytea", nullable: false),
                    profile_etag = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    PreferredLanguage = table.Column<string>(type: "text", nullable: false),
                    ttl_seconds = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_emergency_qr_token", x => x.jti);
                    table.CheckConstraint("CK_emergency_qr_token_ttl", "ttl_seconds IN (3600, 21600, 86400, 604800)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_emergency_qr_token_user_id",
                schema: "public",
                table: "emergency_qr_token",
                column: "user_id",
                filter: "revoked_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "emergency_qr_token",
                schema: "public");
        }
    }
}
