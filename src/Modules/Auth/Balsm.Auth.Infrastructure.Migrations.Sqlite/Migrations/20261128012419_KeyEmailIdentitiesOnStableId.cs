using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balsm.Auth.Infrastructure.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class KeyEmailIdentitiesOnStableId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email_canonical",
                schema: "public",
                table: "user_identities",
                type: "text",
                nullable: true);

            // Same rules as EmailCanonical.From: lowercase; for gmail.com /
            // googlemail.com drop dots and any "+suffix" in the local part and
            // use gmail.com. Split at the first '@', as From does.
            migrationBuilder.Sql("""
                UPDATE user_identities
                SET email_canonical = (
                    SELECT CASE
                        WHEN at = 0 THEN e
                        WHEN substr(e, at + 1) IN ('gmail.com', 'googlemail.com') THEN
                            replace(
                                CASE WHEN instr(substr(e, 1, at - 1), '+') > 0
                                     THEN substr(e, 1, instr(substr(e, 1, at - 1), '+') - 1)
                                     ELSE substr(e, 1, at - 1) END,
                                '.', '') || '@gmail.com'
                        ELSE e
                    END
                    FROM (SELECT lower(trim(email_normalized)) AS e,
                                 instr(lower(trim(email_normalized)), '@') AS at)
                )
                WHERE email_normalized IS NOT NULL;
                """);

            // Two email identities on one mailbox cannot be keyed apart, and
            // merging accounts is not a migration's call: stop and name them.
            // SQLite raises only from a trigger, so a temp trigger carries the
            // message; it aborts the migration's transaction.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE key_email_identities_collision (message TEXT NOT NULL);
                CREATE TEMP TRIGGER key_email_identities_collision_abort
                BEFORE INSERT ON key_email_identities_collision
                BEGIN
                    SELECT RAISE(ABORT, NEW.message);
                END;
                INSERT INTO key_email_identities_collision (message)
                SELECT message FROM (
                    SELECT 'KeyEmailIdentitiesOnStableId aborted: email identities share one canonical address; resolve by hand, then rerun. Colliding identity ids: '
                           || group_concat(ids, '; ') AS message
                    FROM (
                        SELECT group_concat(id, ', ') AS ids
                        FROM user_identities
                        WHERE provider = 'email' AND email_canonical IS NOT NULL
                        GROUP BY email_canonical
                        HAVING count(*) > 1
                    )
                )
                WHERE message IS NOT NULL;
                DROP TRIGGER key_email_identities_collision_abort;
                DROP TABLE key_email_identities_collision;
                """);

            // EF stores Guids as upper-case text in SQLite; the subject is
            // Guid.ToString(), which is lower-case.
            migrationBuilder.Sql("""
                UPDATE user_identities SET provider_subject = lower(id) WHERE provider = 'email';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_user_identities_email_canonical",
                schema: "public",
                table: "user_identities",
                column: "email_canonical",
                unique: true,
                filter: "provider = 'email'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE user_identities SET provider_subject = email_normalized
                WHERE provider = 'email' AND email_normalized IS NOT NULL;
                """);

            // Before this migration there was no unique index on the address
            // (only a comment claiming one), so none is restored here.
            migrationBuilder.DropIndex(
                name: "IX_user_identities_email_canonical",
                schema: "public",
                table: "user_identities");

            migrationBuilder.DropColumn(
                name: "email_canonical",
                schema: "public",
                table: "user_identities");
        }
    }
}
