using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balsm.Auth.Infrastructure.Migrations
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
                UPDATE public.user_identities
                SET email_canonical = CASE
                    WHEN position('@' IN lower(btrim(email_normalized))) = 0
                        THEN lower(btrim(email_normalized))
                    WHEN substr(lower(btrim(email_normalized)), position('@' IN lower(btrim(email_normalized))) + 1)
                         IN ('gmail.com', 'googlemail.com')
                        THEN replace(split_part(split_part(lower(btrim(email_normalized)), '@', 1), '+', 1), '.', '')
                             || '@gmail.com'
                    ELSE lower(btrim(email_normalized))
                END
                WHERE email_normalized IS NOT NULL;
                """);

            // Two email identities on one mailbox cannot be keyed apart, and
            // merging accounts is not a migration's call: stop and name them.
            migrationBuilder.Sql("""
                DO $$
                DECLARE collisions text;
                BEGIN
                    SELECT string_agg(ids, '; ') INTO collisions
                    FROM (
                        SELECT string_agg(id::text, ', ' ORDER BY id) AS ids
                        FROM public.user_identities
                        WHERE provider = 'email' AND email_canonical IS NOT NULL
                        GROUP BY email_canonical
                        HAVING count(*) > 1
                    ) d;
                    IF collisions IS NOT NULL THEN
                        RAISE EXCEPTION 'KeyEmailIdentitiesOnStableId aborted: email identities share one canonical address; resolve by hand, then rerun. Colliding identity ids: %', collisions;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                UPDATE public.user_identities SET provider_subject = id::text WHERE provider = 'email';
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
                UPDATE public.user_identities SET provider_subject = email_normalized
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
