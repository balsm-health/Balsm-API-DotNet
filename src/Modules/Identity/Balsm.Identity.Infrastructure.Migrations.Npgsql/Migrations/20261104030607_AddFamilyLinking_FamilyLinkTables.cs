using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balsm.Identity.Infrastructure.Migrations.Npgsql.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyLinking_FamilyLinkTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "family_link",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pair_low_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pair_high_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    requester_public_key = table.Column<byte[]>(type: "bytea", nullable: true),
                    recipient_public_key = table.Column<byte[]>(type: "bytea", nullable: true),
                    responded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    linked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ended_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_family_link", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "family_link_audit",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_link_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    from_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    to_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_family_link_audit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "family_link_card_share",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_link_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    ephemeral_public_key = table.Column<byte[]>(type: "bytea", nullable: false),
                    nonce = table.Column<byte[]>(type: "bytea", nullable: false),
                    ciphertext = table.Column<byte[]>(type: "bytea", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_family_link_card_share", x => x.id);
                    table.ForeignKey(
                        name: "FK_family_link_card_share_family_link_family_link_id",
                        column: x => x.family_link_id,
                        principalSchema: "identity",
                        principalTable: "family_link",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "family_link_consent",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_link_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    withdrawn_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_family_link_consent", x => x.id);
                    table.ForeignKey(
                        name: "FK_family_link_consent_family_link_family_link_id",
                        column: x => x.family_link_id,
                        principalSchema: "identity",
                        principalTable: "family_link",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_family_link_recipient_user_id_status_created_at",
                schema: "identity",
                table: "family_link",
                columns: new[] { "recipient_user_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_family_link_requester_user_id_status_created_at",
                schema: "identity",
                table: "family_link",
                columns: new[] { "requester_user_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_family_link_open_pair",
                schema: "identity",
                table: "family_link",
                columns: new[] { "pair_low_user_id", "pair_high_user_id" },
                unique: true,
                filter: "\"status\" IN ('pending', 'linked')");

            migrationBuilder.CreateIndex(
                name: "IX_family_link_audit_family_link_id_occurred_at",
                schema: "identity",
                table: "family_link_audit",
                columns: new[] { "family_link_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_family_link_card_share_family_link_id_owner_user_id",
                schema: "identity",
                table: "family_link_card_share",
                columns: new[] { "family_link_id", "owner_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_family_link_consent_family_link_id_user_id",
                schema: "identity",
                table: "family_link_consent",
                columns: new[] { "family_link_id", "user_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "family_link_audit",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "family_link_card_share",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "family_link_consent",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "family_link",
                schema: "identity");
        }
    }
}
