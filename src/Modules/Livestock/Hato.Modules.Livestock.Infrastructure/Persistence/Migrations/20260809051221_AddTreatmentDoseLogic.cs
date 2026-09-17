using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTreatmentDoseLogic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "event_id",
                schema: "livestock",
                table: "withdrawal_periods",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "treatment_course_id",
                schema: "livestock",
                table: "withdrawal_periods",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "migrated_to_course_id",
                schema: "livestock",
                table: "animal_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "dose_kinds",
                schema: "livestock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    label_es = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_dose_kinds", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "treatment_courses",
                schema: "livestock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    animal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dose_kind_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dose_factor_amount = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    dose_factor_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    is_synthetic = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_treatment_courses", x => x.id);
                    table.CheckConstraint("CK_TreatmentCourse_AnimalXorGroup", "(animal_id IS NOT NULL) <> (group_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "f_k_treatment_courses_administration_routes_route_id",
                        column: x => x.route_id,
                        principalSchema: "livestock",
                        principalTable: "administration_routes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_treatment_courses_animal_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "livestock",
                        principalTable: "animal_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_treatment_courses_animals_animal_id",
                        column: x => x.animal_id,
                        principalSchema: "livestock",
                        principalTable: "animals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_treatment_courses_dose_kinds_dose_kind_id",
                        column: x => x.dose_kind_id,
                        principalSchema: "livestock",
                        principalTable: "dose_kinds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "treatment_course_applications",
                schema: "livestock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    treatment_course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_no = table.Column<int>(type: "integer", nullable: false),
                    applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    calculated_dose_amount = table.Column<decimal>(type: "numeric(12,3)", nullable: true),
                    calculated_dose_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_estimated = table.Column<bool>(type: "boolean", nullable: false),
                    administered_dose_amount = table.Column<decimal>(type: "numeric(12,3)", nullable: true),
                    administered_dose_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_treatment_course_applications", x => x.id);
                    table.CheckConstraint("CK_TreatmentCourseApplication_ApplicationNoPositive", "application_no > 0");
                    table.ForeignKey(
                        name: "f_k_treatment_course_applications_treatment_courses_treatment_c~",
                        column: x => x.treatment_course_id,
                        principalSchema: "livestock",
                        principalTable: "treatment_courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_withdrawal_periods_treatment_course_id",
                schema: "livestock",
                table: "withdrawal_periods",
                column: "treatment_course_id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WithdrawalPeriod_EventXorCourse",
                schema: "livestock",
                table: "withdrawal_periods",
                sql: "(event_id IS NOT NULL) <> (treatment_course_id IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "i_x_animal_events_migrated_to_course_id",
                schema: "livestock",
                table: "animal_events",
                column: "migrated_to_course_id");

            migrationBuilder.CreateIndex(
                name: "i_x_dose_kinds_is_active",
                schema: "livestock",
                table: "dose_kinds",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "i_x_dose_kinds_key",
                schema: "livestock",
                table: "dose_kinds",
                column: "key",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "i_x_treatment_course_applications_applied_at",
                schema: "livestock",
                table: "treatment_course_applications",
                column: "applied_at");

            migrationBuilder.CreateIndex(
                name: "i_x_treatment_course_applications_treatment_course_id_applicati~",
                schema: "livestock",
                table: "treatment_course_applications",
                columns: new[] { "treatment_course_id", "application_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_treatment_courses_animal_id_starts_at",
                schema: "livestock",
                table: "treatment_courses",
                columns: new[] { "animal_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "i_x_treatment_courses_dose_kind_id",
                schema: "livestock",
                table: "treatment_courses",
                column: "dose_kind_id");

            migrationBuilder.CreateIndex(
                name: "i_x_treatment_courses_group_id_starts_at",
                schema: "livestock",
                table: "treatment_courses",
                columns: new[] { "group_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "i_x_treatment_courses_route_id",
                schema: "livestock",
                table: "treatment_courses",
                column: "route_id");

            migrationBuilder.AddForeignKey(
                name: "f_k_animal_events_treatment_courses_migrated_to_course_id",
                schema: "livestock",
                table: "animal_events",
                column: "migrated_to_course_id",
                principalSchema: "livestock",
                principalTable: "treatment_courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "f_k_withdrawal_periods_treatment_courses_treatment_course_id",
                schema: "livestock",
                table: "withdrawal_periods",
                column: "treatment_course_id",
                principalSchema: "livestock",
                principalTable: "treatment_courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_animal_events_treatment_courses_migrated_to_course_id",
                schema: "livestock",
                table: "animal_events");

            migrationBuilder.DropForeignKey(
                name: "f_k_withdrawal_periods_treatment_courses_treatment_course_id",
                schema: "livestock",
                table: "withdrawal_periods");

            migrationBuilder.DropTable(
                name: "treatment_course_applications",
                schema: "livestock");

            migrationBuilder.DropTable(
                name: "treatment_courses",
                schema: "livestock");

            migrationBuilder.DropTable(
                name: "dose_kinds",
                schema: "livestock");

            migrationBuilder.DropIndex(
                name: "i_x_withdrawal_periods_treatment_course_id",
                schema: "livestock",
                table: "withdrawal_periods");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WithdrawalPeriod_EventXorCourse",
                schema: "livestock",
                table: "withdrawal_periods");

            migrationBuilder.DropIndex(
                name: "i_x_animal_events_migrated_to_course_id",
                schema: "livestock",
                table: "animal_events");

            migrationBuilder.DropColumn(
                name: "treatment_course_id",
                schema: "livestock",
                table: "withdrawal_periods");

            migrationBuilder.DropColumn(
                name: "migrated_to_course_id",
                schema: "livestock",
                table: "animal_events");

            migrationBuilder.AlterColumn<Guid>(
                name: "event_id",
                schema: "livestock",
                table: "withdrawal_periods",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
