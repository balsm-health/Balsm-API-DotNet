using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balsm.Auth.Infrastructure.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuthSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "account_lockout",
                schema: "public",
                columns: table => new
                {
                    identifier = table.Column<string>(type: "TEXT", nullable: false),
                    identifier_type = table.Column<string>(type: "TEXT", nullable: false),
                    failed_attempts = table.Column<short>(type: "INTEGER", nullable: false, defaultValue: (short)0),
                    rolling_window_started_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    locked_until = table.Column<DateTime>(type: "TEXT", nullable: true)
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
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    provider = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    provider_subject = table.Column<string>(type: "TEXT", nullable: false),
                    email_normalized = table.Column<string>(type: "citext", nullable: true),
                    email_confirmed_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
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
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    token_hash = table.Column<string>(type: "TEXT", nullable: false),
                    device_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    expires_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "TEXT", nullable: true)
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
