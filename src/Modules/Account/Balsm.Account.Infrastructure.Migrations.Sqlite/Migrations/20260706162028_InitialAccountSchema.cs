using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Balsm.Account.Infrastructure.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialAccountSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "reserved_handle_blocklist",
                schema: "public",
                columns: table => new
                {
                    handle_normalized = table.Column<string>(type: "citext", nullable: false),
                    added_by = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "system"),
                    added_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reserved_handle_blocklist", x => x.handle_normalized);
                });

            migrationBuilder.CreateTable(
                name: "user_account",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    handle = table.Column<string>(type: "citext", nullable: true),
                    display_name = table.Column<string>(type: "TEXT", nullable: true),
                    bio = table.Column<string>(type: "TEXT", nullable: true),
                    date_of_birth_ciphertext = table.Column<byte[]>(type: "bytea", nullable: true),
                    country_code = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    preferred_language = table.Column<string>(type: "TEXT", nullable: false),
                    deletion_state = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "ACTIVE"),
                    deletion_confirmed_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    deletion_grace_until = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_account", x => x.id);
                    table.CheckConstraint("CK_user_account_deletion_state", "deletion_state IN ('ACTIVE','DELETION_REQUESTED','DELETION_CANCELLED')");
                });

            migrationBuilder.CreateTable(
                name: "user_account_audit_log",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    target_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    read_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    source_ip = table.Column<string>(type: "inet", nullable: true),
                    correlation_id = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_account_audit_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "username_reservation",
                schema: "public",
                columns: table => new
                {
                    handle_normalized = table.Column<string>(type: "citext", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    claimed_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    released_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_username_reservation", x => x.handle_normalized);
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "reserved_handle_blocklist",
                columns: new[] { "handle_normalized", "added_at", "added_by" },
                values: new object[,]
                {
                    { "admin", new DateTime(2026, 7, 5, 22, 28, 28, 9, DateTimeKind.Utc).AddTicks(7629), "system" },
                    { "api", new DateTime(2026, 7, 5, 22, 28, 28, 9, DateTimeKind.Utc).AddTicks(8262), "system" },
                    { "balsm", new DateTime(2026, 7, 5, 22, 28, 28, 9, DateTimeKind.Utc).AddTicks(8257), "system" },
                    { "health", new DateTime(2026, 7, 5, 22, 28, 28, 9, DateTimeKind.Utc).AddTicks(8268), "system" },
                    { "help", new DateTime(2026, 7, 5, 22, 28, 28, 9, DateTimeKind.Utc).AddTicks(8263), "system" },
                    { "null", new DateTime(2026, 7, 5, 22, 28, 28, 9, DateTimeKind.Utc).AddTicks(8264), "system" },
                    { "support", new DateTime(2026, 7, 5, 22, 28, 28, 9, DateTimeKind.Utc).AddTicks(8261), "system" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_account_handle",
                schema: "public",
                table: "user_account",
                column: "handle",
                unique: true,
                filter: "handle IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_user_account_audit_log_target_user_id_read_at",
                schema: "public",
                table: "user_account_audit_log",
                columns: new[] { "target_user_id", "read_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reserved_handle_blocklist",
                schema: "public");

            migrationBuilder.DropTable(
                name: "user_account",
                schema: "public");

            migrationBuilder.DropTable(
                name: "user_account_audit_log",
                schema: "public");

            migrationBuilder.DropTable(
                name: "username_reservation",
                schema: "public");
        }
    }
}
