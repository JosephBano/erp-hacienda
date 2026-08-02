using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Breeding.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBreeding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "breeding");

            migrationBuilder.CreateTable(
                name: "birthings",
                schema: "breeding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pregnancy_id = table.Column<Guid>(type: "uuid", nullable: true),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: false),
                    difficulty = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    total_born = table.Column<int>(type: "integer", nullable: false),
                    born_alive = table.Column<int>(type: "integer", nullable: false),
                    born_dead = table.Column<int>(type: "integer", nullable: false),
                    mummified = table.Column<int>(type: "integer", nullable: false),
                    litter_weight = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_birthings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "breeding_services",
                schema: "breeding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    sire_animal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    straw_id = table.Column<Guid>(type: "uuid", nullable: true),
                    service_date = table.Column<DateOnly>(type: "date", nullable: false),
                    technician = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    body_condition_score = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_breeding_services", x => x.id);
                    table.CheckConstraint("CK_BreedingService_SireOrStraw", "(sire_animal_id IS NOT NULL AND straw_id IS NULL) OR (sire_animal_id IS NULL AND straw_id IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "pregnancies",
                schema: "breeding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_at = table.Column<DateOnly>(type: "date", nullable: false),
                    expected_birth_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_pregnancies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pregnancy_checks",
                schema: "breeding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    check_date = table.Column<DateOnly>(type: "date", nullable: false),
                    method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    result = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    checked_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_pregnancy_checks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "semen_straws",
                schema: "breeding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    bull_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    bull_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    breed_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    initial_quantity = table.Column<int>(type: "integer", nullable: false),
                    current_quantity = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_semen_straws", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_birthings_birth_date",
                schema: "breeding",
                table: "birthings",
                column: "birth_date");

            migrationBuilder.CreateIndex(
                name: "i_x_birthings_dam_id",
                schema: "breeding",
                table: "birthings",
                column: "dam_id");

            migrationBuilder.CreateIndex(
                name: "i_x_breeding_services_dam_id",
                schema: "breeding",
                table: "breeding_services",
                column: "dam_id");

            migrationBuilder.CreateIndex(
                name: "i_x_breeding_services_service_date",
                schema: "breeding",
                table: "breeding_services",
                column: "service_date");

            migrationBuilder.CreateIndex(
                name: "i_x_pregnancies_dam_id",
                schema: "breeding",
                table: "pregnancies",
                column: "dam_id");

            migrationBuilder.CreateIndex(
                name: "i_x_pregnancies_expected_birth_date",
                schema: "breeding",
                table: "pregnancies",
                column: "expected_birth_date");

            migrationBuilder.CreateIndex(
                name: "i_x_pregnancy_checks_dam_id",
                schema: "breeding",
                table: "pregnancy_checks",
                column: "dam_id");

            migrationBuilder.CreateIndex(
                name: "i_x_pregnancy_checks_service_id",
                schema: "breeding",
                table: "pregnancy_checks",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "i_x_semen_straws_code",
                schema: "breeding",
                table: "semen_straws",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "birthings",
                schema: "breeding");

            migrationBuilder.DropTable(
                name: "breeding_services",
                schema: "breeding");

            migrationBuilder.DropTable(
                name: "pregnancies",
                schema: "breeding");

            migrationBuilder.DropTable(
                name: "pregnancy_checks",
                schema: "breeding");

            migrationBuilder.DropTable(
                name: "semen_straws",
                schema: "breeding");
        }
    }
}
