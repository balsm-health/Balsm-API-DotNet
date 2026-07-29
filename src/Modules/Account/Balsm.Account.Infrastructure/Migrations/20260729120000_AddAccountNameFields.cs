using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balsm.Account.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountNameFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "first_name",
                schema: "public",
                table: "user_account",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_name",
                schema: "public",
                table: "user_account",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "first_name",
                schema: "public",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "last_name",
                schema: "public",
                table: "user_account");
        }
    }
}
