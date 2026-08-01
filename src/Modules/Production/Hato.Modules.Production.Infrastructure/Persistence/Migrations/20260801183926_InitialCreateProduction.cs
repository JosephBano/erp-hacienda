using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Production.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateProduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "production");

            migrationBuilder.CreateTable(
                name: "lactations",
                schema: "production",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    animal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lactation_number = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_lactations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "milking_sessions",
                schema: "production",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    shift = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_liters = table.Column<decimal>(type: "numeric", nullable: false),
                    recorded_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_milking_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "milk_yields",
                schema: "production",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    milking_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    animal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    liters = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_milk_yields", x => x.id);
                    table.ForeignKey(
                        name: "f_k_milk_yields_milking_sessions_milking_session_id",
                        column: x => x.milking_session_id,
                        principalSchema: "production",
                        principalTable: "milking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_lactations_animal_id_lactation_number",
                schema: "production",
                table: "lactations",
                columns: new[] { "animal_id", "lactation_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_milk_yields_animal_id",
                schema: "production",
                table: "milk_yields",
                column: "animal_id");

            migrationBuilder.CreateIndex(
                name: "i_x_milk_yields_milking_session_id",
                schema: "production",
                table: "milk_yields",
                column: "milking_session_id");

            migrationBuilder.CreateIndex(
                name: "i_x_milking_sessions_date_shift",
                schema: "production",
                table: "milking_sessions",
                columns: new[] { "date", "shift" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lactations",
                schema: "production");

            migrationBuilder.DropTable(
                name: "milk_yields",
                schema: "production");

            migrationBuilder.DropTable(
                name: "milking_sessions",
                schema: "production");
        }
    }
}
