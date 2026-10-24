using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balsm.Deletion.Infrastructure.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialDeletionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "deletion_log",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id_hash = table.Column<string>(type: "TEXT", nullable: false),
                    country_code_at_deletion = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    reason_code = table.Column<string>(type: "TEXT", nullable: true),
                    apple_revoke_status = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    purge_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deletion_log", x => x.id);
                    table.CheckConstraint("CK_deletion_log_apple_revoke_status", "apple_revoke_status IS NULL OR apple_revoke_status IN ('not_applicable','succeeded','failed_final','failed_retrying')");
                    table.CheckConstraint("CK_deletion_log_reason_code", "reason_code IS NULL OR reason_code IN ('user_request','cancelled','support_request')");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deletion_log",
                schema: "public");
        }
    }
}
