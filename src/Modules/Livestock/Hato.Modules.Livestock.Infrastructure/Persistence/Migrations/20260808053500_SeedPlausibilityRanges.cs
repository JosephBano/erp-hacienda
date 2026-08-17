using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Seed the foundational species and a baseline of plausibility ranges
    /// (ADR-0022 sec.4: "the seed is criterion of exit, not nice-to-have").
    ///
    /// Without a seed, the plausibility table is empty and the evaluator falls
    /// back to <c>pass</c> for everything — which is the correct fail-open
    /// behavior but defeats the purpose of the branch. The seed here is the
    /// minimum reasonable starting point; the client refines it from the panel
    /// (docs/spec/plan-0002-fase-3-5/spec.md sec.7-B: "the client's own list may differ").
    /// </summary>
    public partial class SeedPlausibilityRanges : Migration
    {
        // Stable GUIDs for the foundational species. The seed uses these IDs
        // directly so the plausibility ranges can reference them; the panel
        // can rename species but should not change these IDs.
        private static readonly Guid SpeciesBovino = new("e1e1e1e1-0001-4a1a-8a1a-000000000001");
        private static readonly Guid SpeciesPorcino = new("e1e1e1e1-0001-4a1a-8a1a-000000000002");
        private static readonly Guid SpeciesCaprino = new("e1e1e1e1-0001-4a1a-8a1a-000000000003");

        // Stable GUIDs for the seeded plausibility ranges. The pair
        // (species_id, category_id, magnitude) is the unique key.
        private static readonly Guid RangePorcinoWeight = new("e2e2e2e2-0001-4a2a-8a2a-000000000001");
        private static readonly Guid RangeBovinoWeight = new("e2e2e2e2-0001-4a2a-8a2a-000000000002");
        private static readonly Guid RangeBovinoMilk = new("e2e2e2e2-0001-4a2a-8a2a-000000000003");
        private static readonly Guid RangeCaprinoWeight = new("e2e2e2e2-0001-4a2a-8a2a-000000000004");
        private static readonly Guid RangeCaprinoMilk = new("e2e2e2e2-0001-4a2a-8a2a-000000000005");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var now = DateTimeOffset.UtcNow;

            // --- Species (foundational) ----------------------------------------
            // Porcino: 114 días de gestación (ref. Levine, E.; O'Brien, S. — datos
            // estándar en porcinos; el sistema NO usa valores hardcodeados porque
            // gestation_days es columna de la fila, ver SpeciesConfiguration).
            // Bovino: 283 días (id.).
            // Caprino: 150 días (id.).
            migrationBuilder.InsertData(
                schema: "livestock",
                table: "species",
                columns: new[] { "id", "name", "is_milkable", "gestation_days", "created_at", "deleted_at" },
                values: new object[,]
                {
                    { SpeciesBovino, "Bovino", true, 283, now, (DateTimeOffset?)null },
                    { SpeciesPorcino, "Porcino", false, 114, now, (DateTimeOffset?)null },
                    { SpeciesCaprino, "Caprino", true, 150, now, (DateTimeOffset?)null },
                });

            // --- Plausibility ranges (ADR-0022 sec.4) --------------------------
            // category_id is NULL: each range applies to all categories of the
            // species. The user's finer-grained ranges (per category) win when
            // added separately by the panel.
            //
            // The bounds are deliberately conservative: the absolute extremes
            // admit the physiological limits (lechón al nacer ~0.5 kg, toro
            // adulto 800 kg, etc.) and the plausible bounds cover the routine
            // range. The client may widen or narrow these from the panel.
            migrationBuilder.InsertData(
                schema: "livestock",
                table: "plausibility_ranges",
                columns: new[] { "id", "species_id", "category_id", "magnitude", "plausible_min", "plausible_max", "absolute_min", "absolute_max", "is_active", "created_at", "deleted_at" },
                values: new object[,]
                {
                    // Porcino — peso en kg. Lechón al nacer ~0.5 kg, cerdo de
                    // engorde 100–130 kg, cerda gestante 200–300 kg, verraco
                    // adulto 300 kg. Absolutos hasta 500 kg admiten outliers
                    // extremos sin bloquear el registro.
                    { RangePorcinoWeight, SpeciesPorcino, (Guid?)null, "weight_kg", (decimal?)0.5m, (decimal?)300m, (decimal?)0.1m, (decimal?)500m, true, now, (DateTimeOffset?)null },

                    // Bovino — peso en kg. Ternero al nacer 30–40 kg, novillo
                    // de engorde 400–500 kg, toro adulto 700–800 kg, vaca
                    // lechera 500–600 kg. Absolutos hasta 1200 kg.
                    { RangeBovinoWeight, SpeciesBovino, (Guid?)null, "weight_kg", (decimal?)30m, (decimal?)700m, (decimal?)1m, (decimal?)1200m, true, now, (DateTimeOffset?)null },

                    // Bovino — leche en litros por sesión. Vaca en producción
                    // 10–40 L/día, pico 50 L. Absolutos hasta 100 L (un valor
                    // mayor que 100 L en una sola sesión es indicador de error
                    // de captura, no de producción real).
                    { RangeBovinoMilk, SpeciesBovino, (Guid?)null, "milk_liters", (decimal?)5m, (decimal?)50m, (decimal?)0.5m, (decimal?)100m, true, now, (DateTimeOffset?)null },

                    // Caprino — peso en kg. Cabrito al nacer 2–4 kg, cabra
                    // adulta 40–70 kg, macho cabrío 80–100 kg.
                    { RangeCaprinoWeight, SpeciesCaprino, (Guid?)null, "weight_kg", (decimal?)2m, (decimal?)80m, (decimal?)0.5m, (decimal?)150m, true, now, (DateTimeOffset?)null },

                    // Caprino — leche en litros por sesión. Cabra lechera
                    // 2–6 L/día, excepcional 8 L. Absolutos hasta 15 L.
                    { RangeCaprinoMilk, SpeciesCaprino, (Guid?)null, "milk_liters", (decimal?)1m, (decimal?)8m, (decimal?)0.1m, (decimal?)15m, true, now, (DateTimeOffset?)null },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "livestock",
                table: "plausibility_ranges",
                keyColumn: "id",
                keyValues: new object[] { RangePorcinoWeight, RangeBovinoWeight, RangeBovinoMilk, RangeCaprinoWeight, RangeCaprinoMilk });

            migrationBuilder.DeleteData(
                schema: "livestock",
                table: "species",
                keyColumn: "id",
                keyValues: new object[] { SpeciesBovino, SpeciesPorcino, SpeciesCaprino });
        }
    }
}
