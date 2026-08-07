using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Breeding.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNursingCohortAndLitterCohortId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "nursing_cohort_id",
                schema: "breeding",
                table: "birthings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "nursing_cohorts",
                schema: "breeding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    species_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateOnly>(type: "date", nullable: false),
                    closed_at = table.Column<DateOnly>(type: "date", nullable: true),
                    weaned_at = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_nursing_cohorts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_birthings_nursing_cohort_id",
                schema: "breeding",
                table: "birthings",
                column: "nursing_cohort_id");

            migrationBuilder.CreateIndex(
                name: "i_x_nursing_cohorts_species_id_closed_at",
                schema: "breeding",
                table: "nursing_cohorts",
                columns: new[] { "species_id", "closed_at" });

            migrationBuilder.CreateIndex(
                name: "i_x_nursing_cohorts_started_at",
                schema: "breeding",
                table: "nursing_cohorts",
                column: "started_at");

            migrationBuilder.AddForeignKey(
                name: "f_k_birthings_nursing_cohorts_nursing_cohort_id",
                schema: "breeding",
                table: "birthings",
                column: "nursing_cohort_id",
                principalSchema: "breeding",
                principalTable: "nursing_cohorts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_birthings_nursing_cohorts_nursing_cohort_id",
                schema: "breeding",
                table: "birthings");

            migrationBuilder.DropTable(
                name: "nursing_cohorts",
                schema: "breeding");

            migrationBuilder.DropIndex(
                name: "i_x_birthings_nursing_cohort_id",
                schema: "breeding",
                table: "birthings");

            migrationBuilder.DropColumn(
                name: "nursing_cohort_id",
                schema: "breeding",
                table: "birthings");
        }
    }
}
