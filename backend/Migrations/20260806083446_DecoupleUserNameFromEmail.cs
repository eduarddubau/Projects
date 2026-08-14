using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <summary>
    /// Data-only: the schema is unchanged, so EF generated an empty migration and the body
    /// below is written by hand. UserName stops being a copy of Email so that changing an
    /// address is a single-column write; while the two were coupled, updating only Email
    /// would leave the old address reserved in normalized_user_name forever.
    /// </summary>
    public partial class DecoupleUserNameFromEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Derived from id rather than gen_random_uuid() for two reasons. It matches what
            // C# now does for new rows (Guid.ToString("N") == replace(id::text,'-','')), and
            // it avoids a Postgres trap: in "SET a = <random>, b = upper(a)" the second
            // expression reads the OLD a, which would leave the pair permanently desynced.
            migrationBuilder.Sql(@"
                UPDATE ""AspNetUsers""
                   SET user_name = replace(id::text, '-', ''),
                       normalized_user_name = upper(replace(id::text, '-', ''));
            ");

            // Fail loudly rather than leaving a half-migrated table behind.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE bad int;
                BEGIN
                    SELECT count(*) INTO bad
                      FROM ""AspNetUsers""
                     WHERE normalized_user_name IS DISTINCT FROM upper(replace(id::text, '-', ''));

                    IF bad > 0 THEN
                        RAISE EXCEPTION 'UserName decoupling incomplete: % row(s) do not match their id', bad;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort only, and deliberately not faithful. Re-coupling to email cannot
            // restore what anonymised rows used to hold — their email is a tombstone — and
            // it will fail outright if two live rows ever shared a normalized email, which
            // the partial unique index on normalized_user_name would then reject.
            migrationBuilder.Sql(@"
                UPDATE ""AspNetUsers""
                   SET user_name = email,
                       normalized_user_name = normalized_email
                 WHERE email IS NOT NULL;
            ");
        }
    }
}
