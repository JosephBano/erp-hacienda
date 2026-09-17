using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Production.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMilkingSessionPlausibilityConfirmed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_plausibility_confirmed",
                schema: "production",
                table: "milking_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_plausibility_confirmed",
                schema: "production",
                table: "milking_sessions");
        }
    }
}
