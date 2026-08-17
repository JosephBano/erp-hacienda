using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Inventory.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Schema half of docs/spec/plan-0002-fase-3-5/spec-3.5a.md sec.3.5a.5 task 2: a feed consumption keeps
    /// both quantities, the one the operator typed ("3 sacos") and the one the cost engine
    /// reads (120 kg), plus the factor that links them.
    ///
    /// The branch that introduced those properties on <c>GroupFeedConsumption</c> shipped
    /// without this migration, so the runtime asked Postgres for four columns that did not
    /// exist and every consumption ended in a 500 (AGENTS.md rule 7). This is that missing
    /// migration.
    ///
    /// <c>quantity</c> is <b>renamed</b>, never dropped and re-added: the scaffolded version
    /// of this migration proposed a DROP, which would have deleted every consumption already
    /// recorded (Art. 1). Rows written before conversions existed were, by definition,
    /// expressed in the item's own unit, so the backfill sets factor 1 and copies the item's
    /// unit — the truthful reading of that history, not a zero.
    /// </summary>
    public partial class AddFeedConsumptionRecordedQuantities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "quantity",
                schema: "inventory",
                table: "group_feed_consumptions",
                newName: "quantity_recorded");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity_recorded",
                schema: "inventory",
                table: "group_feed_consumptions",
                type: "numeric(18,3)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<decimal>(
                name: "applied_factor",
                schema: "inventory",
                table: "group_feed_consumptions",
                type: "numeric(18,6)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "quantity_in_base_unit",
                schema: "inventory",
                table: "group_feed_consumptions",
                type: "numeric(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "unit_recorded",
                schema: "inventory",
                table: "group_feed_consumptions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Backfill: every historical row was recorded in the item's base unit.
            migrationBuilder.Sql(
                """
                UPDATE inventory.group_feed_consumptions AS c
                SET quantity_in_base_unit = c.quantity_recorded,
                    applied_factor = 1,
                    unit_recorded = i.unit
                FROM inventory.inventory_items AS i
                WHERE i.id = c.inventory_item_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "applied_factor",
                schema: "inventory",
                table: "group_feed_consumptions");

            migrationBuilder.DropColumn(
                name: "quantity_in_base_unit",
                schema: "inventory",
                table: "group_feed_consumptions");

            migrationBuilder.DropColumn(
                name: "unit_recorded",
                schema: "inventory",
                table: "group_feed_consumptions");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity_recorded",
                schema: "inventory",
                table: "group_feed_consumptions",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,3)");

            migrationBuilder.RenameColumn(
                name: "quantity_recorded",
                schema: "inventory",
                table: "group_feed_consumptions",
                newName: "quantity");
        }
    }
}
