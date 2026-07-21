using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balsm.Account.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "gender",
                schema: "public",
                table: "user_account",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "national_id_ciphertext",
                schema: "public",
                table: "user_account",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nationality",
                schema: "public",
                table: "user_account",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                schema: "public",
                table: "user_account",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "gender",
                schema: "public",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "national_id_ciphertext",
                schema: "public",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "nationality",
                schema: "public",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "phone",
                schema: "public",
                table: "user_account");
        }
    }
}
