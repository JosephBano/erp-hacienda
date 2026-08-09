using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlausibilityRanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "plausibility_ranges",
                schema: "livestock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    species_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    magnitude = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    plausible_min = table.Column<decimal>(type: "numeric(12,3)", nullable: true),
                    plausible_max = table.Column<decimal>(type: "numeric(12,3)", nullable: true),
                    absolute_min = table.Column<decimal>(type: "numeric(12,3)", nullable: true),
                    absolute_max = table.Column<decimal>(type: "numeric(12,3)", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_plausibility_ranges", x => x.id);
                    table.ForeignKey(
                        name: "f_k_plausibility_ranges_animal_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "livestock",
                        principalTable: "animal_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_plausibility_ranges_species_species_id",
                        column: x => x.species_id,
                        principalSchema: "livestock",
                        principalTable: "species",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_plausibility_ranges_category_id",
                schema: "livestock",
                table: "plausibility_ranges",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "i_x_plausibility_ranges_is_active",
                schema: "livestock",
                table: "plausibility_ranges",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "i_x_plausibility_ranges_species_id_category_id_magnitude",
                schema: "livestock",
                table: "plausibility_ranges",
                columns: new[] { "species_id", "category_id", "magnitude" },
                unique: true,
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plausibility_ranges",
                schema: "livestock");
        }
    }
}
