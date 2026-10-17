using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balsm.Auth.Infrastructure.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkTokenToOtpChallenge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "link_token_hash",
                schema: "public",
                table: "otp_challenge",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "link_token_hash",
                schema: "public",
                table: "otp_challenge");
        }
    }
}
