using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpeciesIsMilkable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_milkable",
                schema: "livestock",
                table: "species",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Art. 8 says species-level config lives in the database, not in code. The
            // default for the new column is false (fail-closed: a species that has not
            // been opted in cannot be milked). Pre-existing rows get their natural
            // answer from the species name; anything unknown stays false until an
            // operator flips it from the admin-web panel.
            migrationBuilder.Sql(@"
                UPDATE livestock.species SET is_milkable = TRUE
                WHERE LOWER(name) IN ('bovino', 'bovinos', 'caprino', 'caprinos');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_milkable",
                schema: "livestock",
                table: "species");
        }
    }
}
