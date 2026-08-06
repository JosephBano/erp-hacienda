using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnimalGroupTrackingModeAndGroupEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "disposed_at",
                schema: "livestock",
                table: "animals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tracking_mode",
                schema: "livestock",
                table: "animal_groups",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Individual");

            migrationBuilder.AlterColumn<Guid>(
                name: "animal_id",
                schema: "livestock",
                table: "animal_events",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "affected_count",
                schema: "livestock",
                table: "animal_events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "group_id",
                schema: "livestock",
                table: "animal_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "i_x_animal_events_group_id_occurred_at",
                schema: "livestock",
                table: "animal_events",
                columns: new[] { "group_id", "occurred_at" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AnimalEvent_AffectedCountPositive",
                schema: "livestock",
                table: "animal_events",
                sql: "affected_count IS NULL OR affected_count > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AnimalEvent_AnimalXorGroup",
                schema: "livestock",
                table: "animal_events",
                sql: "(animal_id IS NOT NULL) <> (group_id IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "f_k_animal_events_animal_groups_group_id",
                schema: "livestock",
                table: "animal_events",
                column: "group_id",
                principalSchema: "livestock",
                principalTable: "animal_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_animal_events_animal_groups_group_id",
                schema: "livestock",
                table: "animal_events");

            migrationBuilder.DropIndex(
                name: "i_x_animal_events_group_id_occurred_at",
                schema: "livestock",
                table: "animal_events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AnimalEvent_AffectedCountPositive",
                schema: "livestock",
                table: "animal_events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AnimalEvent_AnimalXorGroup",
                schema: "livestock",
                table: "animal_events");

            migrationBuilder.DropColumn(
                name: "disposed_at",
                schema: "livestock",
                table: "animals");

            migrationBuilder.DropColumn(
                name: "tracking_mode",
                schema: "livestock",
                table: "animal_groups");

            migrationBuilder.DropColumn(
                name: "affected_count",
                schema: "livestock",
                table: "animal_events");

            migrationBuilder.DropColumn(
                name: "group_id",
                schema: "livestock",
                table: "animal_events");

            migrationBuilder.AlterColumn<Guid>(
                name: "animal_id",
                schema: "livestock",
                table: "animal_events",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
