using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balsm.Disclosure.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialDisclosureSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "disclosure_acceptance",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    disclosure_id = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<string>(type: "text", nullable: false),
                    country_code_at_accept = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    supervisory_authority_name_at_accept = table.Column<string>(type: "text", nullable: false),
                    preferred_language_at_accept = table.Column<string>(type: "text", nullable: false),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disclosure_acceptance", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_disclosure_acceptance_user_id_disclosure_id_version",
                schema: "public",
                table: "disclosure_acceptance",
                columns: new[] { "user_id", "disclosure_id", "version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "disclosure_acceptance",
                schema: "public");
        }
    }
}
