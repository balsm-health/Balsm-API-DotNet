using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balsm.Auth.Infrastructure.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddAppleConsentAtToUserIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "apple_consent_at",
                schema: "public",
                table: "user_identities",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "apple_consent_at",
                schema: "public",
                table: "user_identities");
        }
    }
}
