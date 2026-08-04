using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Makes the Livestock tables usable by the offline pull protocol (ADR-0008).
    ///
    /// Two problems are fixed here. First, rows written before the shared audit
    /// interceptor existed carry <c>created_at = 0001-01-01</c>, because nothing ever
    /// stamped them; a client whose cursor has moved past that date would never receive
    /// them again. Second, the pull orders every collection by
    /// <c>(COALESCE(updated_at, created_at), id)</c> and there was no index supporting
    /// that ordering, so each pull degraded into a full sort of the table.
    /// </summary>
    public partial class AddSyncCursorIndexesAndBackfill : Migration
    {
        private static readonly string[] SyncTables =
        [
            "species",
            "breeds",
            "animal_categories",
            "animals",
            "animal_identifiers",
            "animal_groups",
            "group_memberships",
            "animal_events",
            "withdrawal_periods",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in SyncTables)
            {
                // Backfill: anchor un-stamped rows to a real instant so they sit inside
                // the change stream instead of at the beginning of time.
                migrationBuilder.Sql(
                    $"UPDATE livestock.{table} SET created_at = now() " +
                    "WHERE created_at = '0001-01-01T00:00:00Z'::timestamptz;");

                migrationBuilder.Sql(
                    $"CREATE INDEX IF NOT EXISTS ix_{table}_sync_cursor " +
                    $"ON livestock.{table} (COALESCE(updated_at, created_at), id);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in SyncTables)
            {
                migrationBuilder.Sql($"DROP INDEX IF EXISTS livestock.ix_{table}_sync_cursor;");
            }
        }
    }
}
