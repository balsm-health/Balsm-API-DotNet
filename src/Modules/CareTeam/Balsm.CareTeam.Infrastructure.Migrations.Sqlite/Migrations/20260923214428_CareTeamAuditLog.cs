using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balsm.CareTeam.Infrastructure.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class CareTeamAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "care_team_audit_log",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    health_profile_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    actor = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    source_ip = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    correlation_id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    row_count = table.Column<int>(type: "INTEGER", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_care_team_audit_log", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_care_team_audit_log_user_id_occurred_at",
                schema: "public",
                table: "care_team_audit_log",
                columns: new[] { "user_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "care_team_audit_log",
                schema: "public");
        }
    }
}
