using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balsm.CareDirectory.Infrastructure.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddMapPackArtifact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "map_pack_artifact",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    governorate_id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    name_en = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    name_ar = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    kind = table.Column<int>(type: "INTEGER", nullable: false),
                    version = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    size_bytes = table.Column<long>(type: "INTEGER", nullable: false),
                    sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    url = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    place_count = table.Column<int>(type: "INTEGER", nullable: true),
                    west = table.Column<double>(type: "REAL", nullable: false),
                    south = table.Column<double>(type: "REAL", nullable: false),
                    east = table.Column<double>(type: "REAL", nullable: false),
                    north = table.Column<double>(type: "REAL", nullable: false),
                    published_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_map_pack_artifact", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_map_pack_artifact_governorate_id_kind",
                schema: "public",
                table: "map_pack_artifact",
                columns: new[] { "governorate_id", "kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "map_pack_artifact",
                schema: "public");
        }
    }
}
