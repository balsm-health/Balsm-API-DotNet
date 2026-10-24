using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balsm.Sessions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSessionsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "active_session",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_label = table.Column<string>(type: "text", nullable: false),
                    device_type = table.Column<string>(type: "text", nullable: false),
                    first_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    last_activity_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    refresh_token_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_active_session", x => x.id);
                    table.CheckConstraint("CK_active_session_device_type", "device_type IN ('phone','tablet','desktop','web')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_active_session_user_id_revoked_at",
                schema: "public",
                table: "active_session",
                columns: new[] { "user_id", "revoked_at" },
                filter: "revoked_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "active_session",
                schema: "public");
        }
    }
}
