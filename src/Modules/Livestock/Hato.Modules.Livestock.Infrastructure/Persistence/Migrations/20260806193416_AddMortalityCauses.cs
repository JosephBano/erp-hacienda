using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMortalityCauses : Migration
    {
        // docs/spec/plan-0002-fase-3-5/spec-3.5a.md sec.3.5a.3: the standard list, ampliable from the panel.
        // The client's own list may differ (sec.7-B) — this is a starting point, not a
        // closed catalog, and the seed exists so the branch is testable from day one
        // instead of shipping an empty, silently-useless table.
        private static readonly Guid CauseCrushing = new("d1a1c1a1-0001-4a1a-8a1a-000000000001");
        private static readonly Guid CauseStarvation = new("d1a1c1a1-0001-4a1a-8a1a-000000000002");
        private static readonly Guid CauseWeakAtBirth = new("d1a1c1a1-0001-4a1a-8a1a-000000000003");
        private static readonly Guid CauseDiarrhea = new("d1a1c1a1-0001-4a1a-8a1a-000000000004");
        private static readonly Guid CauseHernia = new("d1a1c1a1-0001-4a1a-8a1a-000000000005");
        private static readonly Guid CauseUnknown = new("d1a1c1a1-0001-4a1a-8a1a-000000000006");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "cause_id",
                schema: "livestock",
                table: "animal_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "mortality_causes",
                schema: "livestock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_mortality_causes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_animal_events_cause_id",
                schema: "livestock",
                table: "animal_events",
                column: "cause_id");

            migrationBuilder.CreateIndex(
                name: "i_x_mortality_causes_name",
                schema: "livestock",
                table: "mortality_causes",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "f_k_animal_events_mortality_causes_cause_id",
                schema: "livestock",
                table: "animal_events",
                column: "cause_id",
                principalSchema: "livestock",
                principalTable: "mortality_causes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            var now = DateTimeOffset.UtcNow;
            migrationBuilder.InsertData("mortality_causes", new[] { "id", "name", "is_active", "created_at" }, new object[,]
            {
                { CauseCrushing, "Aplastamiento", true, now },
                { CauseStarvation, "Inanición", true, now },
                { CauseWeakAtBirth, "Débil al nacer", true, now },
                { CauseDiarrhea, "Diarrea", true, now },
                { CauseHernia, "Hernia", true, now },
                { CauseUnknown, "Desconocida", true, now },
            }, schema: "livestock");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_animal_events_mortality_causes_cause_id",
                schema: "livestock",
                table: "animal_events");

            migrationBuilder.DropTable(
                name: "mortality_causes",
                schema: "livestock");

            migrationBuilder.DropIndex(
                name: "i_x_animal_events_cause_id",
                schema: "livestock",
                table: "animal_events");

            migrationBuilder.DropColumn(
                name: "cause_id",
                schema: "livestock",
                table: "animal_events");
        }
    }
}
