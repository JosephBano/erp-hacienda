using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnimalEventTreatmentPayload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "applied_by_user_id",
                schema: "livestock",
                table: "animal_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "batch_id",
                schema: "livestock",
                table: "animal_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "health_plan_item_id",
                schema: "livestock",
                table: "animal_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reason",
                schema: "livestock",
                table: "animal_events",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "route_id",
                schema: "livestock",
                table: "animal_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "i_x_animal_events_applied_by_user_id",
                schema: "livestock",
                table: "animal_events",
                column: "applied_by_user_id");

            migrationBuilder.CreateIndex(
                name: "i_x_animal_events_batch_id",
                schema: "livestock",
                table: "animal_events",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "i_x_animal_events_reason",
                schema: "livestock",
                table: "animal_events",
                column: "reason");

            migrationBuilder.CreateIndex(
                name: "i_x_animal_events_route_id",
                schema: "livestock",
                table: "animal_events",
                column: "route_id");

            migrationBuilder.AddForeignKey(
                name: "f_k_animal_events_administration_routes_route_id",
                schema: "livestock",
                table: "animal_events",
                column: "route_id",
                principalSchema: "livestock",
                principalTable: "administration_routes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_animal_events_administration_routes_route_id",
                schema: "livestock",
                table: "animal_events");

            migrationBuilder.DropIndex(
                name: "i_x_animal_events_applied_by_user_id",
                schema: "livestock",
                table: "animal_events");

            migrationBuilder.DropIndex(
                name: "i_x_animal_events_batch_id",
                schema: "livestock",
                table: "animal_events");

            migrationBuilder.DropIndex(
                name: "i_x_animal_events_reason",
                schema: "livestock",
                table: "animal_events");

            migrationBuilder.DropIndex(
                name: "i_x_animal_events_route_id",
                schema: "livestock",
                table: "animal_events");

            migrationBuilder.DropColumn(
                name: "applied_by_user_id",
                schema: "livestock",
                table: "animal_events");

            migrationBuilder.DropColumn(
                name: "batch_id",
                schema: "livestock",
                table: "animal_events");

            migrationBuilder.DropColumn(
                name: "health_plan_item_id",
                schema: "livestock",
                table: "animal_events");

            migrationBuilder.DropColumn(
                name: "reason",
                schema: "livestock",
                table: "animal_events");

            migrationBuilder.DropColumn(
                name: "route_id",
                schema: "livestock",
                table: "animal_events");
        }
    }
}
