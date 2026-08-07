using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpeciesLactationParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "cohort_window_days",
                schema: "livestock",
                table: "species",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "days_of_lactation",
                schema: "livestock",
                table: "species",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cohort_window_days",
                schema: "livestock",
                table: "species");

            migrationBuilder.DropColumn(
                name: "days_of_lactation",
                schema: "livestock",
                table: "species");
        }
    }
}
