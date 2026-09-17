using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.People.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Seeds the <c>inventory.feed-stages.manage</c> permission and grants it to the
    /// admin role. Without this row, the three new endpoints introduced by
    /// <c>feature/admin-web-inventory-detail</c> (<c>POST /inventory/feed-stages</c>,
    /// <c>POST /inventory/feed-stages/{id}/activate</c>,
    /// <c>POST /inventory/feed-stages/{id}/deactivate</c>) reject every non-admin
    /// caller — and even for the admin, the grant keeps the audit trail of who is
    /// allowed to mutate the catalog honest (the Admin override is a runtime
    /// fallback, not a substitute for a real role-permission row).
    /// </summary>
    public partial class SeedInventoryFeedStagesManage : Migration
    {
        private static readonly Guid PermInventoryFeedStagesManage =
            Guid.Parse("08000000-0000-0000-0000-000000000001");

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
                    { PermInventoryFeedStagesManage, "inventory.feed-stages.manage", "Gestionar Etapas de Alimento", "Inventory", "Permite crear y activar o desactivar etapas de alimento", now },
                },
                schema: "people");

            migrationBuilder.InsertData(
                "role_permissions",
                new[] { "role_id", "permission_id" },
                new object[,]
                {
                    { RoleAdminId, PermInventoryFeedStagesManage },
                },
                schema: "people");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM people.role_permissions WHERE permission_id = '08000000-0000-0000-0000-000000000001'::uuid;");
            migrationBuilder.Sql("DELETE FROM people.permissions WHERE id = '08000000-0000-0000-0000-000000000001'::uuid;");
        }
    }
}
