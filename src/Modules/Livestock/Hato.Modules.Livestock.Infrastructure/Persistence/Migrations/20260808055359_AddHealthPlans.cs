using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "health_plans",
                schema: "livestock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    species_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_health_plans", x => x.id);
                    table.ForeignKey(
                        name: "f_k_health_plans_species_species_id",
                        column: x => x.species_id,
                        principalSchema: "livestock",
                        principalTable: "species",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "health_plan_assignments",
                schema: "livestock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    health_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    animal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_health_plan_assignments", x => x.id);
                    table.CheckConstraint("CK_HealthPlanAssignment_AnimalXorGroup", "(animal_id IS NOT NULL) <> (group_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "f_k_health_plan_assignments_animal_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "livestock",
                        principalTable: "animal_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_health_plan_assignments_animals_animal_id",
                        column: x => x.animal_id,
                        principalSchema: "livestock",
                        principalTable: "animals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_health_plan_assignments_health_plans_health_plan_id",
                        column: x => x.health_plan_id,
                        principalSchema: "livestock",
                        principalTable: "health_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "health_plan_items",
                schema: "livestock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    health_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    event_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    anchor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    anchor_offset_days = table.Column<int>(type: "integer", nullable: false),
                    compliance_window_days = table.Column<int>(type: "integer", nullable: false),
                    inventory_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    route_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dose_quantity = table.Column<decimal>(type: "numeric(12,3)", nullable: true),
                    dose_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    repetitions = table.Column<int>(type: "integer", nullable: true),
                    applies_to_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    applies_to_sex = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_health_plan_items", x => x.id);
                    table.CheckConstraint("CK_HealthPlanItem_OffsetOrRepetition", "anchor_offset_days <> 0 OR repetitions IS NOT NULL");
                    table.ForeignKey(
                        name: "f_k_health_plan_items_administration_routes_route_id",
                        column: x => x.route_id,
                        principalSchema: "livestock",
                        principalTable: "administration_routes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_health_plan_items_animal_categories_applies_to_category_id",
                        column: x => x.applies_to_category_id,
                        principalSchema: "livestock",
                        principalTable: "animal_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_health_plan_items_health_plans_health_plan_id",
                        column: x => x.health_plan_id,
                        principalSchema: "livestock",
                        principalTable: "health_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_animal_events_health_plan_item_id",
                schema: "livestock",
                table: "animal_events",
                column: "health_plan_item_id");

            migrationBuilder.CreateIndex(
                name: "i_x_health_plan_assignments_animal_id",
                schema: "livestock",
                table: "health_plan_assignments",
                column: "animal_id");

            migrationBuilder.CreateIndex(
                name: "i_x_health_plan_assignments_group_id",
                schema: "livestock",
                table: "health_plan_assignments",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "i_x_health_plan_assignments_health_plan_id",
                schema: "livestock",
                table: "health_plan_assignments",
                column: "health_plan_id");

            migrationBuilder.CreateIndex(
                name: "i_x_health_plan_assignments_is_active",
                schema: "livestock",
                table: "health_plan_assignments",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "i_x_health_plan_items_applies_to_category_id",
                schema: "livestock",
                table: "health_plan_items",
                column: "applies_to_category_id");

            migrationBuilder.CreateIndex(
                name: "i_x_health_plan_items_event_type",
                schema: "livestock",
                table: "health_plan_items",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "i_x_health_plan_items_health_plan_id",
                schema: "livestock",
                table: "health_plan_items",
                column: "health_plan_id");

            migrationBuilder.CreateIndex(
                name: "i_x_health_plan_items_is_active",
                schema: "livestock",
                table: "health_plan_items",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "i_x_health_plan_items_route_id",
                schema: "livestock",
                table: "health_plan_items",
                column: "route_id");

            migrationBuilder.CreateIndex(
                name: "i_x_health_plans_name_species_id",
                schema: "livestock",
                table: "health_plans",
                columns: new[] { "name", "species_id" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "i_x_health_plans_species_id",
                schema: "livestock",
                table: "health_plans",
                column: "species_id");

            migrationBuilder.AddForeignKey(
                name: "f_k_animal_events_health_plan_items_health_plan_item_id",
                schema: "livestock",
                table: "animal_events",
                column: "health_plan_item_id",
                principalSchema: "livestock",
                principalTable: "health_plan_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_animal_events_health_plan_items_health_plan_item_id",
                schema: "livestock",
                table: "animal_events");

            migrationBuilder.DropTable(
                name: "health_plan_assignments",
                schema: "livestock");

            migrationBuilder.DropTable(
                name: "health_plan_items",
                schema: "livestock");

            migrationBuilder.DropTable(
                name: "health_plans",
                schema: "livestock");

            migrationBuilder.DropIndex(
                name: "i_x_animal_events_health_plan_item_id",
                schema: "livestock",
                table: "animal_events");
        }
    }
}
