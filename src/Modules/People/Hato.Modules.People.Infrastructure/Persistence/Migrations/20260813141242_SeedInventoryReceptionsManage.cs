using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.People.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Seeds the <c>inventory.receptions.manage</c> permission and grants it to the
    /// admin role (ADR-0026 Decisión 5). Without this row, the new
    /// <c>POST /api/v1/inventory/items/{itemId}/receptions</c> endpoint rejects every
    /// non-admin caller — and even for the admin, the grant keeps the audit trail of
    /// who is allowed to register a reception honest (the Admin override is a runtime
    /// fallback, not a substitute for a real role-permission row).
    /// </summary>
    public partial class SeedInventoryReceptionsManage : Migration
    {
        private static readonly Guid PermInventoryReceptionsManage =
            Guid.Parse("08000000-0000-0000-0000-000000000002");

        private static readonly Guid RoleAdminId =
            Guid.Parse("11111111-1111-1111-1111-111111111111");

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var now = DateTimeOffset.UtcNow;
            migrationBuilder.InsertData(
                "permissions",
                new[] { "id", "code", "name", "module", "description", "created_at" },
                new object[,]
                {
                    { PermInventoryReceptionsManage, "inventory.receptions.manage", "Registrar recepciones de inventario", "Inventory", "Permite registrar entradas de inventario con trazabilidad de proveedor, factura y fecha declarada", now },
                },
                schema: "people");

            migrationBuilder.InsertData(
                "role_permissions",
                new[] { "role_id", "permission_id" },
                new object[,]
                {
                    { RoleAdminId, PermInventoryReceptionsManage },
                },
                schema: "people");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM people.role_permissions WHERE permission_id = '08000000-0000-0000-0000-000000000002'::uuid;");
            migrationBuilder.Sql("DELETE FROM people.permissions WHERE id = '08000000-0000-0000-0000-000000000002'::uuid;");
        }
    }
}
