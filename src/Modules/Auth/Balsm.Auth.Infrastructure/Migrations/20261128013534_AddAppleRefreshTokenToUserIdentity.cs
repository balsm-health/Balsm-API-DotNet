using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balsm.Auth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppleRefreshTokenToUserIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "apple_refresh_token_ciphertext",
                schema: "public",
                table: "user_identities",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "apple_refresh_token_ciphertext",
                schema: "public",
                table: "user_identities");
        }
    }
}
