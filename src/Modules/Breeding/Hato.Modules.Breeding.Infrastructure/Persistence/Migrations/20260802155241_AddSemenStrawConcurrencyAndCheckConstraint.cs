using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Breeding.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSemenStrawConcurrencyAndCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "xmin" is Postgres's own system column on every table — it is never created
            // or dropped by DDL (doing so is rejected by Postgres as a reserved name).
            // Mapping SemenStraw.xmin as a shadow concurrency-token property only changes
            // how EF Core reads/writes an existing column; no schema change is needed here.
            migrationBuilder.AddCheckConstraint(
                name: "CK_SemenStraw_CurrentQuantityNotNegative",
                schema: "breeding",
                table: "semen_straws",
                sql: "current_quantity >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SemenStraw_CurrentQuantityNotNegative",
                schema: "breeding",
                table: "semen_straws");
        }
    }
}
