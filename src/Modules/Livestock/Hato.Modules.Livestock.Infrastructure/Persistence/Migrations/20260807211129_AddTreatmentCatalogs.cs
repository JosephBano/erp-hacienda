using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the two new configurable catalogs required by the treatment-detail
    /// feature (PLAN-FASE-3-5-PORCINO-3.5a.2-A):
    ///
    /// <list type="bullet">
    /// <item><c>livestock.administration_routes</c> — vías de administración
    /// (oral en agua, oral en alimento, IM, SC, …). Configurable in data, not
    /// in code (Art. 8).</item>
    /// <item><c>livestock.treatment_reasons</c> — por qué se aplicó el
    /// tratamiento (<c>scheduled</c> / <c>curative</c> / <c>preventive</c>).
    /// Distinguirlos es lo que separa "vacuna de calendario" de "vacuna
    /// porque se enfermó".</item>
    /// </list>
    ///
    /// <para>
    /// The migration also tightens <c>animals.birth_weight_kg</c> from
    /// <c>numeric</c> to <c>numeric(8,3)</c>. This is a pre-existing drift
    /// between the configuration and the model snapshot — the column was
    /// created in <c>20260807053208_AddAnimalBirthWeight</c> with the
    /// provider default, and <c>AnimalConfiguration</c> has carried
    /// <c>numeric(8,3)</c> since. Aligning them here keeps the snapshot
    /// honest and stops this drift from being inherited by every future
    /// migration. It is not a behavioural change to existing rows.
    /// </para>
    /// </summary>
    public partial class AddTreatmentCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Align birth_weight_kg precision with AnimalConfiguration (drift fix
            // that pre-dates this work — the configuration has carried
            // numeric(8,3) since 3.5a block B1, but the original migration
            // created the column with the provider-default precision).
            migrationBuilder.AlterColumn<decimal>(
                name: "birth_weight_kg",
                schema: "livestock",
                table: "animals",
                type: "numeric(8,3)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            // administration_routes
            migrationBuilder.CreateTable(
                name: "administration_routes",
                schema: "livestock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    label_es = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    default_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_administration_routes", x => x.id);
                });

            // treatment_reasons
            migrationBuilder.CreateTable(
                name: "treatment_reasons",
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
                    table.PrimaryKey("p_k_treatment_reasons", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_administration_routes_is_active",
                schema: "livestock",
                table: "administration_routes",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "i_x_administration_routes_key",
                schema: "livestock",
                table: "administration_routes",
                column: "key",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "i_x_treatment_reasons_is_active",
                schema: "livestock",
                table: "treatment_reasons",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "i_x_treatment_reasons_key",
                schema: "livestock",
                table: "treatment_reasons",
                column: "key",
                unique: true,
                filter: "deleted_at IS NULL");

            // Seed the catalogue (PLAN-FASE-3-5-PORCINO-3.5a.2-A sec."Tareas"
            // punto 1-2). Routes: oral en agua, oral en alimento, IM, SC,
            // tópica, intranasal, intrauterina. Reasons: scheduled, curative,
            // preventive. IDs are stable so re-seeding in dev environments is
            // idempotent and other tests can reference the well-known keys
            // instead of hard-coding GUIDs.
            var seedNow = DateTimeOffset.UtcNow;
            var idOralWater = new Guid("1a000000-0000-0000-0000-00000000a001");
            var idOralFeed = new Guid("1a000000-0000-0000-0000-00000000a002");
            var idIm = new Guid("1a000000-0000-0000-0000-00000000a003");
            var idSc = new Guid("1a000000-0000-0000-0000-00000000a004");
            var idTopica = new Guid("1a000000-0000-0000-0000-00000000a005");
            var idIntranasal = new Guid("1a000000-0000-0000-0000-00000000a006");
            var idIntrauterina = new Guid("1a000000-0000-0000-0000-00000000a007");

            var idReasonScheduled = new Guid("1b000000-0000-0000-0000-00000000b001");
            var idReasonCurative = new Guid("1b000000-0000-0000-0000-00000000b002");
            var idReasonPreventive = new Guid("1b000000-0000-0000-0000-00000000b003");

            migrationBuilder.InsertData(
                schema: "livestock",
                table: "administration_routes",
                columns: new[] { "id", "key", "label_es", "is_active", "created_at", "created_by", "updated_at", "updated_by", "deleted_at" },
                values: new object[,]
                {
                    { idOralWater, "oral_water", "Oral en agua", true, seedNow, null, null, null, null },
                    { idOralFeed, "oral_feed", "Oral en alimento", true, seedNow, null, null, null, null },
                    { idIm, "im", "Intramuscular (IM)", true, seedNow, null, null, null, null },
                    { idSc, "sc", "Subcutánea (SC)", true, seedNow, null, null, null, null },
                    { idTopica, "topica", "Tópica", true, seedNow, null, null, null, null },
                    { idIntranasal, "intranasal", "Intranasal", true, seedNow, null, null, null, null },
                    { idIntrauterina, "intrauterina", "Intrauterina", true, seedNow, null, null, null, null },
                });

            migrationBuilder.InsertData(
                schema: "livestock",
                table: "treatment_reasons",
                columns: new[] { "id", "key", "label_es", "is_active", "created_at", "created_by", "updated_at", "updated_by", "deleted_at" },
                values: new object[,]
                {
                    { idReasonScheduled, "scheduled", "Programada (cronograma)", true, seedNow, null, null, null, null },
                    { idReasonCurative, "curative", "Curativa", true, seedNow, null, null, null, null },
                    { idReasonPreventive, "preventive", "Preventiva", true, seedNow, null, null, null, null },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "treatment_reasons",
                schema: "livestock");

            migrationBuilder.DropTable(
                name: "administration_routes",
                schema: "livestock");

            migrationBuilder.AlterColumn<decimal>(
                name: "birth_weight_kg",
                schema: "livestock",
                table: "animals",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,3)",
                oldNullable: true);
        }
    }
}