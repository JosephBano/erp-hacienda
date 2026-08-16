using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Seeds the closed set of dose forms (docs/planes/fase-3-5/spec-3.5a.md sec.3.5a.2-B task 1):
    /// <c>absolute</c>, <c>per_weight</c>, <c>per_head</c>. A table, not an enum
    /// (Art. 8) — the seed is what makes the catalogue usable from day one, the
    /// same posture ADR-0022 took for plausibility ranges.
    /// </summary>
    public partial class SeedDoseKinds : Migration
    {
        // Stable GUIDs: DoseResolver and the legacy-dose migration in
        // RecordAnimalEventCommand look these rows up by Key, not by Id, so the
        // exact value matters less than it staying constant across environments.
        private static readonly Guid DoseKindAbsolute = new("e3e3e3e3-0001-4a3a-8a3a-000000000001");
        private static readonly Guid DoseKindPerWeight = new("e3e3e3e3-0001-4a3a-8a3a-000000000002");
        private static readonly Guid DoseKindPerHead = new("e3e3e3e3-0001-4a3a-8a3a-000000000003");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var now = DateTimeOffset.UtcNow;

            migrationBuilder.InsertData(
                schema: "livestock",
                table: "dose_kinds",
                columns: new[] { "id", "key", "label_es", "is_active", "created_at", "deleted_at" },
                values: new object[,]
                {
                    { DoseKindAbsolute, "absolute", "Absoluta (cantidad fija)", true, now, (DateTimeOffset?)null },
                    { DoseKindPerWeight, "per_weight", "Por peso (tasa por kg)", true, now, (DateTimeOffset?)null },
                    { DoseKindPerHead, "per_head", "Por cabeza (dosis fija por animal)", true, now, (DateTimeOffset?)null },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "livestock",
                table: "dose_kinds",
                keyColumn: "id",
                keyValues: new object[] { DoseKindAbsolute, DoseKindPerWeight, DoseKindPerHead });
        }
    }
}
