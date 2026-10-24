using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balsm.Auth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordToUserIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "password_hash",
                schema: "public",
                table: "user_identities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "password_set_at",
                schema: "public",
                table: "user_identities",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "password_hash",
                schema: "public",
                table: "user_identities");

            migrationBuilder.DropColumn(
                name: "password_set_at",
                schema: "public",
                table: "user_identities");
        }
    }
}
