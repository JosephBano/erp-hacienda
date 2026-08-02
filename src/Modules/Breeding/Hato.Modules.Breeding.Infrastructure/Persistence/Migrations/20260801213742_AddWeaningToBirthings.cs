using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Breeding.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWeaningToBirthings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "weaned_at",
                schema: "breeding",
                table: "birthings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "weaned_count",
                schema: "breeding",
                table: "birthings",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "weaned_at",
                schema: "breeding",
                table: "birthings");

            migrationBuilder.DropColumn(
                name: "weaned_count",
                schema: "breeding",
                table: "birthings");
        }
    }
}
