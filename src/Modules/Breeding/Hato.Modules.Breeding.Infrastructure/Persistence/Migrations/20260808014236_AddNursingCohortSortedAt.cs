using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Breeding.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNursingCohortSortedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "sorted_at",
                schema: "breeding",
                table: "nursing_cohorts",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sorted_at",
                schema: "breeding",
                table: "nursing_cohorts");
        }
    }
}
