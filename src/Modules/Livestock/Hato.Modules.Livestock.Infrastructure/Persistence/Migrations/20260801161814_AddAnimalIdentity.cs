using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnimalIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "livestock");

            migrationBuilder.CreateTable(
                name: "species",
                schema: "livestock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    gestation_days = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_species", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "animal_categories",
                schema: "livestock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    species_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_animal_categories", x => x.id);
                    table.ForeignKey(
                        name: "f_k_animal_categories_species_species_id",
                        column: x => x.species_id,
                        principalSchema: "livestock",
                        principalTable: "species",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "breeds",
                schema: "livestock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    species_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_breeds", x => x.id);
                    table.ForeignKey(
                        name: "f_k_breeds_species_species_id",
                        column: x => x.species_id,
                        principalSchema: "livestock",
                        principalTable: "species",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "animals",
                schema: "livestock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    species_id = table.Column<Guid>(type: "uuid", nullable: false),
                    breed_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sex = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_animals", x => x.id);
                    table.ForeignKey(
                        name: "f_k_animals_animal_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "livestock",
                        principalTable: "animal_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_animals_breeds_breed_id",
                        column: x => x.breed_id,
                        principalSchema: "livestock",
                        principalTable: "breeds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_animals_species_species_id",
                        column: x => x.species_id,
                        principalSchema: "livestock",
                        principalTable: "species",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "animal_identifiers",
                schema: "livestock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    animal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_animal_identifiers", x => x.id);
                    table.ForeignKey(
                        name: "f_k_animal_identifiers_animals_animal_id",
                        column: x => x.animal_id,
                        principalSchema: "livestock",
                        principalTable: "animals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_animal_categories_species_id",
                schema: "livestock",
                table: "animal_categories",
                column: "species_id");

            migrationBuilder.CreateIndex(
                name: "i_x_animal_identifiers_animal_id_type",
                schema: "livestock",
                table: "animal_identifiers",
                columns: new[] { "animal_id", "type" },
                unique: true,
                filter: "valid_to IS NULL");

            migrationBuilder.CreateIndex(
                name: "i_x_animals_breed_id",
                schema: "livestock",
                table: "animals",
                column: "breed_id");

            migrationBuilder.CreateIndex(
                name: "i_x_animals_category_id",
                schema: "livestock",
                table: "animals",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "i_x_animals_species_id",
                schema: "livestock",
                table: "animals",
                column: "species_id");

            migrationBuilder.CreateIndex(
                name: "i_x_breeds_species_id",
                schema: "livestock",
                table: "breeds",
                column: "species_id");

            migrationBuilder.CreateIndex(
                name: "i_x_species_name",
                schema: "livestock",
                table: "species",
                column: "name",
                unique: true,
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "animal_identifiers",
                schema: "livestock");

            migrationBuilder.DropTable(
                name: "animals",
                schema: "livestock");

            migrationBuilder.DropTable(
                name: "animal_categories",
                schema: "livestock");

            migrationBuilder.DropTable(
                name: "breeds",
                schema: "livestock");

            migrationBuilder.DropTable(
                name: "species",
                schema: "livestock");
        }
    }
}
