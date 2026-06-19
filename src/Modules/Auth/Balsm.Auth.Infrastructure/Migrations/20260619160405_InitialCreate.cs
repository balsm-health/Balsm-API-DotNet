using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balsm.Auth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.CreateTable(
                name: "account_lockout",
                schema: "public",
                columns: table => new
                {
                    identifier = table.Column<string>(type: "text", nullable: false),
                    identifier_type = table.Column<string>(type: "text", nullable: false),
                    failed_attempts = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    rolling_window_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    locked_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_lockout", x => x.identifier);
                    table.CheckConstraint("CK_account_lockout_identifier_type", "identifier_type IN ('email','apple_sub','google_sub','handle')");
                });

            migrationBuilder.CreateTable(
                name: "user_identities",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    provider_subject = table.Column<string>(type: "text", nullable: false),
                    email_normalized = table.Column<string>(type: "citext", nullable: true),
                    email_confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_identities", x => x.id);
                    table.CheckConstraint("CK_user_identities_provider", "provider IN ('email','google','apple')");
                });

            migrationBuilder.CreateTable(
                name: "user_refresh_token",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_refresh_token", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_identities_provider_provider_subject",
                schema: "public",
                table: "user_identities",
                columns: new[] { "provider", "provider_subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_refresh_token_token_hash",
                schema: "public",
                table: "user_refresh_token",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_refresh_token_user_id_revoked_at",
                schema: "public",
                table: "user_refresh_token",
                columns: new[] { "user_id", "revoked_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_lockout",
                schema: "public");

            migrationBuilder.DropTable(
                name: "user_identities",
                schema: "public");

            migrationBuilder.DropTable(
                name: "user_refresh_token",
                schema: "public");
        }
    }
}
