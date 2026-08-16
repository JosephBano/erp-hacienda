using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedStages : Migration
    {
        // docs/planes/fase-3-5/spec-3.5a.md sec.3.5a.5 task 3: the standard porcine feeding-stage
        // list, ampliable from the panel (Art. 8). Seeded from day one so the catalog is
        // testable and usable immediately instead of shipping an empty, silently-useless
        // table (same rationale as AddMortalityCauses/AddAdministrationRoutes).
        private static readonly Guid StagePreStarter = new("f3ed57a9-0001-4a1a-8a1a-000000000001");
        private static readonly Guid StageStarter = new("f3ed57a9-0001-4a1a-8a1a-000000000002");
        private static readonly Guid StageGrower = new("f3ed57a9-0001-4a1a-8a1a-000000000003");
        private static readonly Guid StageFinisher = new("f3ed57a9-0001-4a1a-8a1a-000000000004");
        private static readonly Guid StageGestation = new("f3ed57a9-0001-4a1a-8a1a-000000000005");
        private static readonly Guid StageLactation = new("f3ed57a9-0001-4a1a-8a1a-000000000006");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "feed_stage_id",
                schema: "inventory",
                table: "inventory_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "feed_stages",
                schema: "inventory",
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
                    table.PrimaryKey("p_k_feed_stages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_inventory_items_feed_stage_id",
                schema: "inventory",
                table: "inventory_items",
                column: "feed_stage_id");

            migrationBuilder.CreateIndex(
                name: "i_x_feed_stages_is_active",
                schema: "inventory",
                table: "feed_stages",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "i_x_feed_stages_key",
                schema: "inventory",
                table: "feed_stages",
                column: "key",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.AddForeignKey(
                name: "f_k_inventory_items_feed_stages_feed_stage_id",
                schema: "inventory",
                table: "inventory_items",
                column: "feed_stage_id",
                principalSchema: "inventory",
                principalTable: "feed_stages",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            var now = DateTimeOffset.UtcNow;
            migrationBuilder.InsertData("feed_stages", new[] { "id", "key", "label_es", "is_active", "created_at" }, new object[,]
            {
                { StagePreStarter, "pre_starter", "Preiniciador", true, now },
                { StageStarter, "starter", "Iniciador", true, now },
                { StageGrower, "grower", "Crecimiento", true, now },
                { StageFinisher, "finisher", "Engorde", true, now },
                { StageGestation, "gestation", "Gestación", true, now },
                { StageLactation, "lactation", "Lactancia", true, now },
            }, schema: "inventory");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_inventory_items_feed_stages_feed_stage_id",
                schema: "inventory",
                table: "inventory_items");

            migrationBuilder.DropTable(
                name: "feed_stages",
                schema: "inventory");

            migrationBuilder.DropIndex(
                name: "i_x_inventory_items_feed_stage_id",
                schema: "inventory",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "feed_stage_id",
                schema: "inventory",
                table: "inventory_items");
        }
    }
}
