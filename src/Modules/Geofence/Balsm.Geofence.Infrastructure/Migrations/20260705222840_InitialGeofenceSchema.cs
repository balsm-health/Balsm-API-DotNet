using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Balsm.Geofence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialGeofenceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "denied_country_blocklist",
                schema: "public",
                columns: table => new
                {
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    added_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_denied_country_blocklist", x => x.country_code);
                    table.CheckConstraint("CK_denied_country_blocklist_source", "source IN ('ofac','apple_denied','google_denied','manual')");
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "denied_country_blocklist",
                columns: new[] { "country_code", "added_at", "source" },
                values: new object[,]
                {
                    { "CU", new DateTime(2026, 7, 5, 22, 28, 39, 875, DateTimeKind.Utc).AddTicks(4706), "ofac" },
                    { "IR", new DateTime(2026, 7, 5, 22, 28, 39, 875, DateTimeKind.Utc).AddTicks(5249), "ofac" },
                    { "KP", new DateTime(2026, 7, 5, 22, 28, 39, 875, DateTimeKind.Utc).AddTicks(5253), "ofac" },
                    { "SY", new DateTime(2026, 7, 5, 22, 28, 39, 875, DateTimeKind.Utc).AddTicks(5254), "ofac" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "denied_country_blocklist",
                schema: "public");
        }
    }
}
