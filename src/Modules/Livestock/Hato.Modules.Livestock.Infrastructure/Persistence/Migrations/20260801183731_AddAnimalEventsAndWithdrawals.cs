using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnimalEventsAndWithdrawals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "animal_events",
                schema: "livestock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    animal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cost = table.Column<decimal>(type: "numeric", nullable: true),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    related_event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_animal_events", x => x.id);
                    table.ForeignKey(
                        name: "f_k_animal_events_animals_animal_id",
                        column: x => x.animal_id,
                        principalSchema: "livestock",
                        principalTable: "animals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "withdrawal_periods",
                schema: "livestock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    animal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    starts_at = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_at = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_withdrawal_periods", x => x.id);
                    table.ForeignKey(
                        name: "f_k_withdrawal_periods_animal_events_event_id",
                        column: x => x.event_id,
                        principalSchema: "livestock",
                        principalTable: "animal_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_withdrawal_periods_animals_animal_id",
                        column: x => x.animal_id,
                        principalSchema: "livestock",
                        principalTable: "animals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_animal_events_animal_id_occurred_at",
                schema: "livestock",
                table: "animal_events",
                columns: new[] { "animal_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "i_x_withdrawal_periods_animal_id_ends_at",
                schema: "livestock",
                table: "withdrawal_periods",
                columns: new[] { "animal_id", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "i_x_withdrawal_periods_event_id",
                schema: "livestock",
                table: "withdrawal_periods",
                column: "event_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "withdrawal_periods",
                schema: "livestock");

            migrationBuilder.DropTable(
                name: "animal_events",
                schema: "livestock");
        }
    }
}
