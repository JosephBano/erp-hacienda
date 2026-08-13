using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Inventory.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ADR-0026 Decisión 1 + 2: enriches <c>inventory_batches</c> with operator-declared
    /// reception metadata (supplier, invoice, notes, author, declared received-at).
    /// The single non-null column (<c>received_at</c>) is added first and backfilled with
    /// the row's <c>created_at</c> via raw SQL — that is the truthful reading of the
    /// pre-ADR history (no declared reception date ever existed for those rows). All five
    /// other columns are nullable.
    /// </summary>
    public partial class AddInventoryReceptionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add received_at as NOT NULL with no EF defaultValue (EF can't generate a
            //    "use created_at" default — we backfill in SQL right after).
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "received_at",
                schema: "inventory",
                table: "inventory_batches",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: null);

            // 2. Backfill: pre-existing rows inherit created_at as their best-known
            //    received_at. The badge in the UI admin-web will read
            //    "CreatedAt != ReceivedAt" to flag these rows as "no operator-declared
            //    date" — that is the honest reading, not a fake null.
            migrationBuilder.Sql("""
                UPDATE inventory.inventory_batches
                SET received_at = created_at
                WHERE received_at IS NULL;
            """);

            // 3. Optional columns. None of these carry a defaultValue because the ADR
            //    explicitly says "flujo pre-purchasing, texto libre": null is a valid
            //    answer (a batch can arrive without an invoice reference, for example).
            migrationBuilder.AddColumn<string>(
                name: "supplier_label",
                schema: "inventory",
                table: "inventory_batches",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_reference",
                schema: "inventory",
                table: "inventory_batches",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                schema: "inventory",
                table: "inventory_batches",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "recorded_by_id",
                schema: "inventory",
                table: "inventory_batches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recorded_by_label",
                schema: "inventory",
                table: "inventory_batches",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // 4. Index on received_at for "entradas del último mes" / per-week queries
            //    that the future Reporting module will want. Single-column B-tree.
            migrationBuilder.CreateIndex(
                name: "ix_inventory_batches_received_at",
                schema: "inventory",
                table: "inventory_batches",
                column: "received_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_inventory_batches_received_at",
                schema: "inventory",
                table: "inventory_batches");

            migrationBuilder.DropColumn(
                name: "recorded_by_label",
                schema: "inventory",
                table: "inventory_batches");

            migrationBuilder.DropColumn(
                name: "recorded_by_id",
                schema: "inventory",
                table: "inventory_batches");

            migrationBuilder.DropColumn(
                name: "notes",
                schema: "inventory",
                table: "inventory_batches");

            migrationBuilder.DropColumn(
                name: "invoice_reference",
                schema: "inventory",
                table: "inventory_batches");

            migrationBuilder.DropColumn(
                name: "supplier_label",
                schema: "inventory",
                table: "inventory_batches");

            migrationBuilder.DropColumn(
                name: "received_at",
                schema: "inventory",
                table: "inventory_batches");
        }
    }
}
