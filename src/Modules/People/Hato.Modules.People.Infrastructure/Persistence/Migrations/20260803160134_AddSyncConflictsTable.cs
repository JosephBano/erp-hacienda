using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.People.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncConflictsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sync_conflicts",
                schema: "people",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    server_value = table.Column<string>(type: "text", nullable: true),
                    attempted_value = table.Column<string>(type: "text", nullable: true),
                    resolution = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    client_operation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    device_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    detected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_sync_conflicts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_sync_conflicts_entity_type_entity_id",
                schema: "people",
                table: "sync_conflicts",
                columns: new[] { "entity_type", "entity_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sync_conflicts",
                schema: "people");
        }
    }
}
